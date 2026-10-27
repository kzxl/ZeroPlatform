namespace ZeroPlatform.Concurrency
{
    /// <summary>
    /// Represents a unit of work that can be scheduled without incurring heap allocation for delegate closures.
    /// </summary>
    public interface IZeroWorkItem
    {
        /// <summary>
        /// Executes the work item.
        /// </summary>
        void Execute();
    }
}
