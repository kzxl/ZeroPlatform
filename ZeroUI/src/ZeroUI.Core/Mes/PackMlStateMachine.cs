using System;
using System.Collections.Generic;
using ZeroUI.Core.Runtime;

namespace ZeroUI.Core.Mes
{
    /// <summary>
    /// Standard 17 equipment states conforming to ISA-88 / PackML (TR88.00.02).
    /// </summary>
    public enum PackMlState
    {
        Clearing = 0,
        Stopped = 1,
        Starting = 2,
        Idle = 3,
        Suspended = 4,
        Execute = 5,
        Stopping = 6,
        Aborting = 7,
        Aborted = 8,
        Holding = 9,
        Held = 10,
        Unholding = 11,
        Suspending = 12,
        Unsuspending = 13,
        Resetting = 14,
        Completing = 15,
        Complete = 16
    }

    /// <summary>
    /// Standard operator and system transition commands in PackML.
    /// </summary>
    public enum PackMlCommand
    {
        Start,
        Stop,
        Hold,
        Unhold,
        Suspend,
        Unsuspend,
        Reset,
        Abort,
        Clear,
        Complete
    }

    /// <summary>
    /// Thread-safe ISA-88 / PackML finite state machine.
    /// Manages state transitions, validates allowed paths, measures state dwell duration,
    /// and synchronizes machine status into StateStore.
    /// </summary>
    public sealed class PackMlStateMachine
    {
        private readonly string _machineId;
        private readonly object _lock = new object();

        private PackMlState _currentState;
        private PackMlState _previousState;
        private DateTime _stateEnteredTime;

        public string MachineId => _machineId;

        public PackMlState CurrentState
        {
            get
            {
                lock (_lock) return _currentState;
            }
        }

        public PackMlState PreviousState
        {
            get
            {
                lock (_lock) return _previousState;
            }
        }

        public DateTime StateEnteredTime
        {
            get
            {
                lock (_lock) return _stateEnteredTime;
            }
        }

        public TimeSpan StateDuration
        {
            get
            {
                lock (_lock) return DateTime.UtcNow - _stateEnteredTime;
            }
        }

        /// <summary>
        /// Fired whenever the machine transitions to a new state.
        /// </summary>
        public event Action<PackMlStateMachine, PackMlState, PackMlState>? StateChanged;

        public PackMlStateMachine(string machineId, PackMlState initialState = PackMlState.Stopped)
        {
            _machineId = machineId ?? throw new ArgumentNullException(nameof(machineId));
            _currentState = initialState;
            _previousState = initialState;
            _stateEnteredTime = DateTime.UtcNow;

            StateStore.Default.SetState($"Machine.{_machineId}.State", _currentState.ToString());
        }

        /// <summary>
        /// Checks whether the specified command is allowed from the current state.
        /// </summary>
        public bool CanExecuteCommand(PackMlCommand command)
        {
            lock (_lock)
            {
                // Abort is universally allowed except when already Aborted or Aborting
                if (command == PackMlCommand.Abort)
                {
                    return _currentState != PackMlState.Aborted && _currentState != PackMlState.Aborting;
                }

                // Stop is allowed from almost all active states except Stopped, Stopping, Aborted, Aborting
                if (command == PackMlCommand.Stop)
                {
                    return _currentState != PackMlState.Stopped &&
                           _currentState != PackMlState.Stopping &&
                           _currentState != PackMlState.Aborted &&
                           _currentState != PackMlState.Aborting &&
                           _currentState != PackMlState.Clearing;
                }

                return (_currentState, command) switch
                {
                    (PackMlState.Stopped, PackMlCommand.Reset) => true,
                    (PackMlState.Resetting, PackMlCommand.Complete) => true,
                    (PackMlState.Idle, PackMlCommand.Start) => true,
                    (PackMlState.Starting, PackMlCommand.Complete) => true,
                    (PackMlState.Execute, PackMlCommand.Hold) => true,
                    (PackMlState.Execute, PackMlCommand.Suspend) => true,
                    (PackMlState.Execute, PackMlCommand.Complete) => true,
                    (PackMlState.Holding, PackMlCommand.Complete) => true,
                    (PackMlState.Held, PackMlCommand.Unhold) => true,
                    (PackMlState.Unholding, PackMlCommand.Complete) => true,
                    (PackMlState.Suspending, PackMlCommand.Complete) => true,
                    (PackMlState.Suspended, PackMlCommand.Unsuspend) => true,
                    (PackMlState.Unsuspending, PackMlCommand.Complete) => true,
                    (PackMlState.Completing, PackMlCommand.Complete) => true,
                    (PackMlState.Complete, PackMlCommand.Reset) => true,
                    (PackMlState.Stopping, PackMlCommand.Complete) => true,
                    (PackMlState.Aborted, PackMlCommand.Clear) => true,
                    (PackMlState.Clearing, PackMlCommand.Complete) => true,
                    (PackMlState.Aborting, PackMlCommand.Complete) => true,
                    _ => false
                };
            }
        }

        /// <summary>
        /// Executes an operator or supervisory command, triggering the corresponding state transition.
        /// </summary>
        public bool ExecuteCommand(PackMlCommand command)
        {
            lock (_lock)
            {
                if (!CanExecuteCommand(command))
                {
                    return false;
                }

                PackMlState targetState = command switch
                {
                    PackMlCommand.Abort => PackMlState.Aborting,
                    PackMlCommand.Stop => PackMlState.Stopping,
                    PackMlCommand.Reset when _currentState == PackMlState.Stopped || _currentState == PackMlState.Complete => PackMlState.Resetting,
                    PackMlCommand.Start when _currentState == PackMlState.Idle => PackMlState.Starting,
                    PackMlCommand.Hold when _currentState == PackMlState.Execute => PackMlState.Holding,
                    PackMlCommand.Unhold when _currentState == PackMlState.Held => PackMlState.Unholding,
                    PackMlCommand.Suspend when _currentState == PackMlState.Execute => PackMlState.Suspending,
                    PackMlCommand.Unsuspend when _currentState == PackMlState.Suspended => PackMlState.Unsuspending,
                    PackMlCommand.Clear when _currentState == PackMlState.Aborted => PackMlState.Clearing,
                    PackMlCommand.Complete => GetNextCompletedState(_currentState),
                    _ => _currentState
                };

                ApplyStateTransition(targetState);
                return true;
            }
        }

        /// <summary>
        /// Forcibly transitions the state (e.g. for PLC dual-synchronization).
        /// </summary>
        public void TransitionTo(PackMlState targetState)
        {
            lock (_lock)
            {
                if (_currentState == targetState) return;
                ApplyStateTransition(targetState);
            }
        }

        private static PackMlState GetNextCompletedState(PackMlState current) => current switch
        {
            PackMlState.Resetting => PackMlState.Idle,
            PackMlState.Starting => PackMlState.Execute,
            PackMlState.Holding => PackMlState.Held,
            PackMlState.Unholding => PackMlState.Execute,
            PackMlState.Suspending => PackMlState.Suspended,
            PackMlState.Unsuspending => PackMlState.Execute,
            PackMlState.Completing => PackMlState.Complete,
            PackMlState.Stopping => PackMlState.Stopped,
            PackMlState.Aborting => PackMlState.Aborted,
            PackMlState.Clearing => PackMlState.Stopped,
            PackMlState.Execute => PackMlState.Completing,
            _ => current
        };

        private void ApplyStateTransition(PackMlState newState)
        {
            var oldState = _currentState;
            _previousState = oldState;
            _currentState = newState;
            _stateEnteredTime = DateTime.UtcNow;

            StateStore.Default.SetState($"Machine.{_machineId}.State", newState.ToString());
            StateStore.Default.SetState($"Machine.{_machineId}.PreviousState", oldState.ToString());

            try
            {
                StateChanged?.Invoke(this, oldState, newState);
            }
            catch
            {
                // Guard callback
            }
        }
    }
}
