using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroUI.Core.Runtime
{
    /// <summary>
    /// Pipeline stages for industrial command execution.
    /// </summary>
    public enum CommandPipelineStage
    {
        None,
        Permission,
        Validation,
        Interlock,
        Execution,
        Feedback
    }

    /// <summary>
    /// Represents the outcome of an industrial command execution.
    /// </summary>
    public sealed class CommandResult
    {
        public bool IsSuccess { get; }
        public string Message { get; }
        public CommandPipelineStage FailedStage { get; }
        public string? ErrorCode { get; }
        public TimeSpan Duration { get; }

        private CommandResult(bool success, string message, CommandPipelineStage stage, string? errorCode, TimeSpan duration)
        {
            IsSuccess = success;
            Message = message;
            FailedStage = stage;
            ErrorCode = errorCode;
            Duration = duration;
        }

        public static CommandResult Success(string message = "Command executed successfully.", TimeSpan duration = default)
            => new CommandResult(true, message, CommandPipelineStage.None, null, duration);

        public static CommandResult Failed(CommandPipelineStage stage, string message, string? errorCode = null, TimeSpan duration = default)
            => new CommandResult(false, message, stage, errorCode, duration);
    }

    /// <summary>
    /// Marker interface for industrial commands.
    /// </summary>
    public interface IIndustrialCommand
    {
        string CommandId { get; }
    }

    /// <summary>
    /// Interface for command validation.
    /// </summary>
    public interface ICommandValidator<in TCommand>
    {
        /// <summary>
        /// Validates command parameters. Returns error message if invalid, or null if valid.
        /// </summary>
        string? Validate(TCommand command);
    }

    /// <summary>
    /// Interface for industrial safety interlock checking.
    /// </summary>
    public interface IInterlockGuard<in TCommand>
    {
        /// <summary>
        /// Evaluates machine/system interlocks. Returns trip reason if interlocked, or null if clear.
        /// </summary>
        string? CheckInterlock(TCommand command);
    }

    /// <summary>
    /// Interface for command execution handlers.
    /// </summary>
    public interface ICommandHandler<in TCommand>
    {
        Task<CommandResult> ExecuteAsync(TCommand command, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Contract for the central industrial CommandBus.
    /// </summary>
    public interface ICommandBus
    {
        void RegisterHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : IIndustrialCommand;
        void RegisterHandler<TCommand>(Func<TCommand, CancellationToken, Task<CommandResult>> handler) where TCommand : IIndustrialCommand;
        void RegisterValidator<TCommand>(ICommandValidator<TCommand> validator) where TCommand : IIndustrialCommand;
        void RegisterValidator<TCommand>(Func<TCommand, string?> validator) where TCommand : IIndustrialCommand;
        void RegisterInterlock<TCommand>(IInterlockGuard<TCommand> guard) where TCommand : IIndustrialCommand;
        void RegisterInterlock<TCommand>(Func<TCommand, string?> guard) where TCommand : IIndustrialCommand;
        void SetPermissionProvider(Func<string, bool> permissionCheck);

        Task<CommandResult> ExecuteAsync<TCommand>(
            TCommand command,
            string? requiredRole = null,
            CancellationToken cancellationToken = default) where TCommand : IIndustrialCommand;
    }

    /// <summary>
    /// Industrial Command Pipeline Bus enforcing Permission -> Validation -> Interlock -> Execution -> Feedback.
    /// </summary>
    public sealed class CommandBus : ICommandBus
    {
        private static readonly Lazy<CommandBus> _defaultInstance = new Lazy<CommandBus>(() => new CommandBus());
        public static CommandBus Default => _defaultInstance.Value;

        private readonly ConcurrentDictionary<Type, object> _handlers = new ConcurrentDictionary<Type, object>();
        private readonly ConcurrentDictionary<Type, List<object>> _validators = new ConcurrentDictionary<Type, List<object>>();
        private readonly ConcurrentDictionary<Type, List<object>> _interlocks = new ConcurrentDictionary<Type, List<object>>();
        private Func<string, bool>? _permissionProvider;

        public void SetPermissionProvider(Func<string, bool> permissionCheck)
        {
            _permissionProvider = permissionCheck;
        }

        public void RegisterHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : IIndustrialCommand
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers[typeof(TCommand)] = handler;
        }

        public void RegisterHandler<TCommand>(Func<TCommand, CancellationToken, Task<CommandResult>> handler) where TCommand : IIndustrialCommand
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers[typeof(TCommand)] = new DelegateCommandHandler<TCommand>(handler);
        }

        public void RegisterValidator<TCommand>(ICommandValidator<TCommand> validator) where TCommand : IIndustrialCommand
        {
            if (validator == null) throw new ArgumentNullException(nameof(validator));
            var list = _validators.GetOrAdd(typeof(TCommand), _ => new List<object>());
            lock (list) list.Add(validator);
        }

        public void RegisterValidator<TCommand>(Func<TCommand, string?> validator) where TCommand : IIndustrialCommand
        {
            if (validator == null) throw new ArgumentNullException(nameof(validator));
            RegisterValidator(new DelegateCommandValidator<TCommand>(validator));
        }

        public void RegisterInterlock<TCommand>(IInterlockGuard<TCommand> guard) where TCommand : IIndustrialCommand
        {
            if (guard == null) throw new ArgumentNullException(nameof(guard));
            var list = _interlocks.GetOrAdd(typeof(TCommand), _ => new List<object>());
            lock (list) list.Add(guard);
        }

        public void RegisterInterlock<TCommand>(Func<TCommand, string?> guard) where TCommand : IIndustrialCommand
        {
            if (guard == null) throw new ArgumentNullException(nameof(guard));
            RegisterInterlock(new DelegateInterlockGuard<TCommand>(guard));
        }

        public async Task<CommandResult> ExecuteAsync<TCommand>(
            TCommand command,
            string? requiredRole = null,
            CancellationToken cancellationToken = default) where TCommand : IIndustrialCommand
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var sw = Stopwatch.StartNew();

            // Stage 1: Permission Check
            if (!string.IsNullOrEmpty(requiredRole) && _permissionProvider != null)
            {
                if (!_permissionProvider(requiredRole!))
                {
                    sw.Stop();
                    return CommandResult.Failed(
                        CommandPipelineStage.Permission,
                        $"Access denied. Required role: '{requiredRole}'.",
                        "AUTH_DENIED",
                        sw.Elapsed);
                }
            }

            // Stage 2: Validation
            if (_validators.TryGetValue(typeof(TCommand), out var valList))
            {
                List<object> validatorsCopy;
                lock (valList) validatorsCopy = new List<object>(valList);

                for (int i = 0; i < validatorsCopy.Count; i++)
                {
                    var validator = (ICommandValidator<TCommand>)validatorsCopy[i];
                    var error = validator.Validate(command);
                    if (!string.IsNullOrEmpty(error))
                    {
                        sw.Stop();
                        return CommandResult.Failed(
                            CommandPipelineStage.Validation,
                            $"Validation failed: {error}",
                            "VAL_ERROR",
                            sw.Elapsed);
                    }
                }
            }

            // Stage 3: Interlock Check
            if (_interlocks.TryGetValue(typeof(TCommand), out var intList))
            {
                List<object> interlocksCopy;
                lock (intList) interlocksCopy = new List<object>(intList);

                for (int i = 0; i < interlocksCopy.Count; i++)
                {
                    var guard = (IInterlockGuard<TCommand>)interlocksCopy[i];
                    var interlockReason = guard.CheckInterlock(command);
                    if (!string.IsNullOrEmpty(interlockReason))
                    {
                        sw.Stop();
                        return CommandResult.Failed(
                            CommandPipelineStage.Interlock,
                            $"Safety interlock active: {interlockReason}",
                            "INTERLOCK_ACTIVE",
                            sw.Elapsed);
                    }
                }
            }

            // Stage 4: Execution
            if (!_handlers.TryGetValue(typeof(TCommand), out var rawHandler))
            {
                sw.Stop();
                return CommandResult.Failed(
                    CommandPipelineStage.Execution,
                    $"No registered command handler for '{typeof(TCommand).Name}'.",
                    "NO_HANDLER",
                    sw.Elapsed);
            }

            var handler = (ICommandHandler<TCommand>)rawHandler;
            try
            {
                var result = await handler.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
                sw.Stop();
                if (result.IsSuccess)
                {
                    return CommandResult.Success(result.Message, sw.Elapsed);
                }
                return CommandResult.Failed(result.FailedStage, result.Message, result.ErrorCode, sw.Elapsed);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return CommandResult.Failed(
                    CommandPipelineStage.Execution,
                    $"Execution exception: {ex.Message}",
                    "EXEC_EXCEPTION",
                    sw.Elapsed);
            }
        }

        private sealed class DelegateCommandHandler<TCommand> : ICommandHandler<TCommand>
        {
            private readonly Func<TCommand, CancellationToken, Task<CommandResult>> _fn;
            public DelegateCommandHandler(Func<TCommand, CancellationToken, Task<CommandResult>> fn) => _fn = fn;
            public Task<CommandResult> ExecuteAsync(TCommand command, CancellationToken ct) => _fn(command, ct);
        }

        private sealed class DelegateCommandValidator<TCommand> : ICommandValidator<TCommand>
        {
            private readonly Func<TCommand, string?> _fn;
            public DelegateCommandValidator(Func<TCommand, string?> fn) => _fn = fn;
            public string? Validate(TCommand command) => _fn(command);
        }

        private sealed class DelegateInterlockGuard<TCommand> : IInterlockGuard<TCommand>
        {
            private readonly Func<TCommand, string?> _fn;
            public DelegateInterlockGuard(Func<TCommand, string?> fn) => _fn = fn;
            public string? CheckInterlock(TCommand command) => _fn(command);
        }
    }
}
