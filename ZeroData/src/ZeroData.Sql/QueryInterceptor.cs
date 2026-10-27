using System;

namespace ZeroData.Sql
{
    /// <summary>
    /// Provides hooks into the query execution pipeline.
    /// Register on SqlContext.Interceptor for global query monitoring.
    /// </summary>
    public class QueryInterceptor
    {
        /// <summary>
        /// Called before a SQL statement is executed.
        /// Return modified SQL or the original.
        /// </summary>
        public Func<string, object, string> OnBeforeExecute { get; set; }

        /// <summary>
        /// Called after a SQL statement is executed.
        /// Parameters: (sql, duration, rowCount)
        /// </summary>
        public Action<string, TimeSpan, int> OnAfterExecute { get; set; }

        /// <summary>
        /// Called when a SQL execution throws an exception.
        /// Parameters: (sql, exception)
        /// </summary>
        public Action<string, Exception> OnError { get; set; }
    }
}
