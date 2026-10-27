using System;
using System.Threading;

namespace ZeroPlatform.Concurrency
{
    /// <summary>
    /// Dedicated background worker operating on an isolated OS thread, optimized for hard real-time and streaming loops.
    /// Equipped with an adaptive backoff strategy (Spin -> Yield -> Sleep) to achieve sub-microsecond latency without saturating CPU when idle.
    /// </summary>
    public sealed class ZeroDedicatedWorker : IDisposable
    {
        private readonly Thread _thread;
        private readonly Func<bool> _workLoop;
        private volatile bool _isRunning;
        private volatile bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZeroDedicatedWorker"/> class.
        /// </summary>
        /// <param name="name">Descriptive thread identifier for profiling and diagnostics.</param>
        /// <param name="workLoop">Work loop function. Returns <c>true</c> when work was processed; <c>false</c> when idle (triggers backoff).</param>
        /// <param name="priority">OS thread priority (defaults to <see cref="ThreadPriority.AboveNormal"/> for real-time telemetry).</param>
        public ZeroDedicatedWorker(string name, Func<bool> workLoop, ThreadPriority priority = ThreadPriority.AboveNormal)
        {
            _workLoop = workLoop ?? throw new ArgumentNullException(nameof(workLoop));

            _thread = new Thread(RunLoop)
            {
                Name = name,
                IsBackground = true,
                Priority = priority
            };
        }

        /// <summary>
        /// Gets a value indicating whether the worker thread is actively running.
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Starts the dedicated worker thread.
        /// </summary>
        public void Start()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(ZeroDedicatedWorker));
            if (_isRunning) return;

            _isRunning = true;
            _thread.Start();
        }

        /// <summary>
        /// Requests worker termination and waits for the thread to exit cleanly.
        /// </summary>
        /// <param name="timeoutMs">Maximum milliseconds to wait for thread join.</param>
        /// <returns><c>true</c> if the thread exited cleanly; otherwise <c>false</c>.</returns>
        public bool Stop(int timeoutMs = 2000)
        {
            if (!_isRunning) return true;

            _isRunning = false;
            return _thread.Join(timeoutMs);
        }

        private void RunLoop()
        {
            try
            {
                SpinWait spinner = default;
                while (_isRunning)
                {
                    bool hasWork = _workLoop();
                    if (hasWork)
                    {
                        spinner.Reset();
                    }
                    else
                    {
                        // Adaptive backoff: lightweight spin -> yield -> sleep 1ms
                        spinner.SpinOnce();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ZeroDedicatedWorker Error in {_thread.Name}]: {ex}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// Releases all resources used by the worker.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
        }
    }
}
