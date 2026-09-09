using System;

namespace ZeroPipeline.Core.Graph
{
    /// <summary>
    /// Base exception for pipeline graph and execution errors.
    /// </summary>
    public class PipelineException : Exception
    {
        public PipelineException(string message) : base(message) { }
        public PipelineException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when a circular feedback cycle is detected in the directed graph topology.
    /// </summary>
    public sealed class CycleDetectedException : PipelineException
    {
        public CycleDetectedException(string message) : base(message) { }
    }

    /// <summary>
    /// Exception thrown when attempting to connect ports with incompatible data types.
    /// </summary>
    public sealed class TypeMismatchException : PipelineException
    {
        public Type SourceType { get; }
        public Type TargetType { get; }

        public TypeMismatchException(Type sourceType, Type targetType, string message)
            : base(message)
        {
            SourceType = sourceType;
            TargetType = targetType;
        }
    }
}
