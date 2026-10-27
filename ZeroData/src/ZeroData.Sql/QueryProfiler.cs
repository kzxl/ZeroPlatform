using System;
using System.Diagnostics;

namespace ZeroData.Sql
{
    /// <summary>
    /// Tracks query execution statistics for monitoring and debugging.
    /// Attach to SqlContext to capture query timing, counts, and slow query detection.
    /// </summary>
    public class QueryProfiler
    {
        private int _totalQueries;
        private long _totalDurationTicks;
        private string _slowestQuery;
        private long _slowestTimeTicks;
        private readonly object _lock = new object();

        /// <summary>Enable or disable profiling. Default: false.</summary>
        public bool Enabled { get; set; }

        /// <summary>Threshold for slow query alerts. Default: 500ms.</summary>
        public TimeSpan SlowQueryThreshold { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>Fired when a query exceeds SlowQueryThreshold.</summary>
        public event Action<string, TimeSpan> OnSlowQuery;

        /// <summary>Total number of queries executed since profiler was enabled.</summary>
        public int TotalQueries => _totalQueries;

        /// <summary>Total duration of all queries.</summary>
        public TimeSpan TotalDuration => TimeSpan.FromTicks(_totalDurationTicks);

        /// <summary>The SQL of the slowest query.</summary>
        public string SlowestQuery => _slowestQuery;

        /// <summary>Duration of the slowest query.</summary>
        public TimeSpan SlowestTime => TimeSpan.FromTicks(_slowestTimeTicks);

        /// <summary>
        /// Starts timing a query. Call StopQuery with the returned Stopwatch when done.
        /// </summary>
        public Stopwatch StartQuery()
        {
            if (!Enabled) return null;
            return Stopwatch.StartNew();
        }

        /// <summary>
        /// Records a completed query's execution time.
        /// </summary>
        public void StopQuery(Stopwatch sw, string sql)
        {
            if (sw == null || !Enabled) return;
            sw.Stop();

            lock (_lock)
            {
                _totalQueries++;
                _totalDurationTicks += sw.ElapsedTicks;

                if (sw.ElapsedTicks > _slowestTimeTicks)
                {
                    _slowestTimeTicks = sw.ElapsedTicks;
                    _slowestQuery = sql;
                }
            }

            if (sw.Elapsed > SlowQueryThreshold)
                OnSlowQuery?.Invoke(sql, sw.Elapsed);
        }

        /// <summary>Resets all statistics.</summary>
        public void Reset()
        {
            lock (_lock)
            {
                _totalQueries = 0;
                _totalDurationTicks = 0;
                _slowestQuery = null;
                _slowestTimeTicks = 0;
            }
        }
    }
}
