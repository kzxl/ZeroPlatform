using System;
using ZeroPipeline.Core.Ports;

namespace ZeroPipeline.Core.Graph
{
    /// <summary>
    /// Represents an active edge connecting an output port to an input port in the pipeline graph.
    /// </summary>
    public sealed class PipelineConnection
    {
        public IPort Source { get; }
        public IPort Target { get; }
        public Type DataType { get; }

        public PipelineConnection(IPort source, IPort target)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));

            if (source.Direction != PortDirection.Output)
                throw new ArgumentException($"Source port '{source.Name}' must be an Output port.", nameof(source));

            if (target.Direction != PortDirection.Input)
                throw new ArgumentException($"Target port '{target.Name}' must be an Input port.", nameof(target));

            if (!target.DataType.IsAssignableFrom(source.DataType))
            {
                throw new TypeMismatchException(
                    source.DataType,
                    target.DataType,
                    $"Cannot connect Output port '{source.Name}' ({source.DataType.Name}) to Input port '{target.Name}' ({target.DataType.Name}).");
            }

            DataType = source.DataType;
        }

        public override string ToString() =>
            $"{Source.OwnerNode.Name}.{Source.Name} -> {Target.OwnerNode.Name}.{Target.Name} ({DataType.Name})";
    }
}
