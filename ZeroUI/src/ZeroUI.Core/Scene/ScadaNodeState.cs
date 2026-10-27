using System;

namespace ZeroUI.Core.Scene
{
    /// <summary>
    /// Operational and equipment status states for industrial SCADA scene nodes.
    /// Aligned with PackML / ISA-88 state models.
    /// </summary>
    public enum ScadaNodeState : byte
    {
        /// <summary>
        /// Equipment is disconnected or offline.
        /// </summary>
        Offline = 0,

        /// <summary>
        /// Equipment is idle / stopped in a safe state.
        /// </summary>
        Stopped = 1,

        /// <summary>
        /// Equipment is in transition to running state.
        /// </summary>
        Starting = 2,

        /// <summary>
        /// Equipment is actively operating / running normal cycle.
        /// </summary>
        Running = 3,

        /// <summary>
        /// Equipment is transitioning to stopped state.
        /// </summary>
        Stopping = 4,

        /// <summary>
        /// Equipment is operating under a warning condition (pre-trip).
        /// </summary>
        Warning = 5,

        /// <summary>
        /// Equipment has an active unacknowledged or critical alarm.
        /// </summary>
        Alarm = 6,

        /// <summary>
        /// Equipment has tripped / faulted and requires manual reset.
        /// </summary>
        Fault = 7,

        /// <summary>
        /// Equipment is locked out for maintenance or calibration.
        /// </summary>
        Maintenance = 8
    }
}
