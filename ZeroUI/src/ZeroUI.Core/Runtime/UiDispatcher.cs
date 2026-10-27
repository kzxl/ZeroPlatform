using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ZeroUI.Core.Runtime
{
    /// <summary>
    /// Thread-safe dispatcher for marshaling background operations onto the UI thread.
    /// Provides deadlock-safe synchronous dispatch, non-blocking asynchronous posting,
    /// and coalesced invalidation to decouple high-frequency telemetry from the UI frame rate.
    /// </summary>
    public static class UiDispatcher
    {
        private static SynchronizationContext? _syncContext;
        private static Action<Action>? _customInvoker;
        private static int _uiThreadId = -1;

        private static readonly ConcurrentDictionary<string, Action> _coalescedActions =
            new ConcurrentDictionary<string, Action>(StringComparer.Ordinal);

        /// <summary>
        /// Gets whether the dispatcher has been initialized with a synchronization context or invoker.
        /// </summary>
        public static bool IsInitialized => _syncContext != null || _customInvoker != null;

        /// <summary>
        /// Gets whether the current executing thread is the registered UI dispatcher thread.
        /// </summary>
        public static bool IsOnUiDispatcherThread =>
            _uiThreadId != -1 && Thread.CurrentThread.ManagedThreadId == _uiThreadId;

        /// <summary>
        /// Initializes the dispatcher with the current thread's SynchronizationContext.
        /// Call this once during application startup or main window creation on the UI thread.
        /// </summary>
        public static void Initialize(SynchronizationContext? syncContext = null)
        {
            _uiThreadId = Thread.CurrentThread.ManagedThreadId;
            _syncContext = syncContext ?? SynchronizationContext.Current;
        }

        /// <summary>
        /// Initializes the dispatcher with a custom invoker delegate (e.g. Control.BeginInvoke).
        /// </summary>
        public static void Initialize(Action<Action> customInvoker)
        {
            _uiThreadId = Thread.CurrentThread.ManagedThreadId;
            _customInvoker = customInvoker ?? throw new ArgumentNullException(nameof(customInvoker));
        }

        /// <summary>
        /// Posts an action asynchronously to the UI thread without waiting for completion.
        /// </summary>
        public static void Post(Action action)
        {
            if (action == null) return;

            if (_customInvoker != null)
            {
                _customInvoker(action);
                return;
            }

            if (_syncContext != null)
            {
                try
                {
                    _syncContext.Post(_ => action(), null);
                    return;
                }
                catch (InvalidOperationException)
                {
                    // Window handle might not be created yet, or form is disposing
                    return;
                }
            }

            // Auto-detect current thread's SynchronizationContext as last resort
            var currentCtx = SynchronizationContext.Current;
            if (currentCtx != null)
            {
                currentCtx.Post(_ => action(), null);
                return;
            }

            // Final fallback: log warning and execute on threadpool
            System.Diagnostics.Debug.WriteLine(
                "[UiDispatcher] WARNING: No SynchronizationContext available. " +
                "Call UiDispatcher.Initialize() on the UI thread during startup.");
            ThreadPool.QueueUserWorkItem(_ => action());
        }

        /// <summary>
        /// Executes an action synchronously on the UI thread, blocking until completion.
        /// If already executing on the UI thread, executes immediately to prevent deadlocks.
        /// </summary>
        public static void Send(Action action)
        {
            if (action == null) return;

            if (IsOnUiDispatcherThread)
            {
                action();
                return;
            }

            if (_syncContext != null)
            {
                try
                {
                    _syncContext.Send(_ => action(), null);
                    return;
                }
                catch (InvalidOperationException)
                {
                    return;
                }
            }

            if (_customInvoker != null)
            {
                using (var doneEvent = new ManualResetEventSlim(false))
                {
                    Exception? caughtEx = null;
                    _customInvoker(() =>
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception ex)
                        {
                            caughtEx = ex;
                        }
                        finally
                        {
                            doneEvent.Set();
                        }
                    });
                    doneEvent.Wait();
                    if (caughtEx != null) throw new InvalidOperationException("UI Dispatch Send failed.", caughtEx);
                }
                return;
            }

            // Fallback: synchronous direct invocation
            action();
        }

        /// <summary>
        /// Registers a coalesced action under a specific unique key (e.g. tag path or control ID).
        /// If multiple updates arrive within the same UI frame tick, only the latest registered action is retained.
        /// </summary>
        /// <param name="key">Unique key to group updates.</param>
        /// <param name="action">Action to execute on next flush.</param>
        public static void EnqueueDirty(string key, Action action)
        {
            if (string.IsNullOrEmpty(key) || action == null) return;
            _coalescedActions[key] = action;
        }

        /// <summary>
        /// Flushes all queued coalesced dirty actions to the UI thread in a single batch.
        /// Typically called periodically by ZeroAnimationClock (30 or 60 FPS).
        /// </summary>
        public static int FlushPending()
        {
            if (_coalescedActions.IsEmpty) return 0;

            var count = 0;
            // Drain entries
            foreach (var kvp in _coalescedActions)
            {
                if (_coalescedActions.TryRemove(kvp.Key, out var action))
                {
                    try
                    {
                        action();
                        count++;
                    }
                    catch
                    {
                        // Guard against individual UI rendering exceptions
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Clears all registered synchronization contexts and pending queues (useful for unit tests).
        /// </summary>
        public static void Reset()
        {
            _syncContext = null;
            _customInvoker = null;
            _uiThreadId = -1;
            _coalescedActions.Clear();
        }
    }
}
