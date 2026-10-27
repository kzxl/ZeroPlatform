using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Ports;

namespace ZeroPipeline.Core.Nodes
{
    /// <summary>
    /// Abstract foundational base class for all industrial pipeline execution nodes.
    /// Provides port management, high-resolution execution timing, state management, and exception isolation.
    /// </summary>
    public abstract class PipelineNode : IPipelineNode
    {
        private readonly List<IPort> _inputPorts = new List<IPort>();
        private readonly List<IPort> _outputPorts = new List<IPort>();
        private readonly Stopwatch _stopwatch = new Stopwatch();

        public string Id { get; }
        public string Name { get; set; }
        public NodeState State { get; private set; }
        public Exception? LastException { get; private set; }

        public long ExecutionCount { get; private set; }
        public double LastExecutionDurationMs { get; private set; }
        public double TotalExecutionDurationMs { get; private set; }

        public IReadOnlyList<IPort> InputPorts => _inputPorts;
        public IReadOnlyList<IPort> OutputPorts => _outputPorts;

        protected PipelineNode(string? name = null, string? id = null)
        {
            Id = id ?? Guid.NewGuid().ToString("N");
            Name = name ?? GetType().Name;
            State = NodeState.Idle;
        }

        protected InputPort<T> AddInputPort<T>(
            string name,
            int capacity = 16,
            BackpressurePolicy policy = BackpressurePolicy.Block)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            var port = new InputPort<T>(this, name, capacity, policy);
            _inputPorts.Add(port);
            return port;
        }

        protected OutputPort<T> AddOutputPort<T>(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            var port = new OutputPort<T>(this, name);
            _outputPorts.Add(port);
            return port;
        }

        public async Task InitializeAsync(PipelineContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                State = NodeState.Configured;
                LastException = null;
                await OnInitializeAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                State = NodeState.Faulted;
                LastException = ex;
                throw;
            }
        }

        public async Task ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default)
        {
            if (State == NodeState.Faulted)
                throw new InvalidOperationException($"Cannot execute faulted node '{Name}' (Id: {Id}). Last error: {LastException?.Message}", LastException);

            State = NodeState.Running;
            _stopwatch.Restart();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await OnExecuteAsync(context, cancellationToken).ConfigureAwait(false);

                _stopwatch.Stop();
                double elapsed = _stopwatch.Elapsed.TotalMilliseconds;
                LastExecutionDurationMs = elapsed;
                TotalExecutionDurationMs += elapsed;
                ExecutionCount++;
                State = NodeState.Completed;
            }
            catch (Exception ex)
            {
                _stopwatch.Stop();
                State = NodeState.Faulted;
                LastException = ex;
                throw;
            }
        }

        public async Task ResetAsync()
        {
            State = NodeState.Idle;
            LastException = null;

            foreach (var port in _inputPorts)
            {
                // Clear any buffered packets in input ports
                var clearMethod = port.GetType().GetMethod("Clear");
                clearMethod?.Invoke(port, null);
            }

            await OnResetAsync().ConfigureAwait(false);
        }

        protected virtual Task OnInitializeAsync(PipelineContext context, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        protected abstract Task OnExecuteAsync(PipelineContext context, CancellationToken cancellationToken);

        protected virtual Task OnResetAsync() =>
            Task.CompletedTask;

        public override string ToString() =>
            $"{Name} [Id={Id.Substring(0, Math.Min(8, Id.Length))}, State={State}, In={_inputPorts.Count}, Out={_outputPorts.Count}]";
    }
}
