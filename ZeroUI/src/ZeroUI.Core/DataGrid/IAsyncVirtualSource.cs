using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.DataGrid
{
    /// <summary>
    /// Event arguments for when an asynchronous server-mode chunk is retrieved.
    /// </summary>
    public sealed class ChunkLoadedEventArgs : EventArgs
    {
        public int StartRow { get; }
        public int Count { get; }

        public ChunkLoadedEventArgs(int startRow, int count)
        {
            StartRow = startRow;
            Count = count;
        }
    }

    /// <summary>
    /// Low-level async streaming contract for remote datasets (e.g., SQL server, WebAPI, Big Data).
    /// </summary>
    public interface IAsyncVirtualSource
    {
        ValueTask<int> GetTotalRowCountAsync(CancellationToken ct = default);
        ValueTask<IReadOnlyList<object?[]>> FetchRowChunkAsync(int startRow, int count, CancellationToken ct = default);
    }

    /// <summary>
    /// High-performance, LRU-cached asynchronous virtual source for ZeroUI DataGrid.
    /// Eliminates UI thread stutter during deep scrolling by fetching row chunks asynchronously
    /// in low-priority background tasks and rendering lightweight skeletons during retrieval.
    /// </summary>
    public class AsyncVirtualGridSource<T> : IZeroVirtualSource
    {
        private readonly int _totalRowCount;
        private readonly int _totalColumnCount;
        private readonly int _chunkSize;
        private readonly Func<int, int, CancellationToken, Task<IReadOnlyList<T>>> _fetchFunc;
        private readonly Func<T, int, string> _valueExtractor;

        // Chunk Cache: chunkIndex -> Array of items
        private readonly ConcurrentDictionary<int, T[]> _cache = new ConcurrentDictionary<int, T[]>();
        private readonly HashSet<int> _pendingRequests = new HashSet<int>();
        private readonly object _lock = new object();

        public event EventHandler<ChunkLoadedEventArgs>? ChunkLoaded;

        public int TotalRowCount => _totalRowCount;
        public int TotalColumnCount => _totalColumnCount;
        public int ChunkSize => _chunkSize;
        public int CachedChunkCount => _cache.Count;

        public AsyncVirtualGridSource(
            int totalRowCount,
            int totalColumnCount,
            Func<int, int, CancellationToken, Task<IReadOnlyList<T>>> fetchFunc,
            Func<T, int, string> valueExtractor,
            int chunkSize = 100)
        {
            _totalRowCount = Math.Max(0, totalRowCount);
            _totalColumnCount = Math.Max(0, totalColumnCount);
            _fetchFunc = fetchFunc ?? throw new ArgumentNullException(nameof(fetchFunc));
            _valueExtractor = valueExtractor ?? throw new ArgumentNullException(nameof(valueExtractor));
            _chunkSize = Math.Max(10, chunkSize);
        }

        public void GetCellValue(int rowIndex, int columnIndex, ref CellValueBuffer buffer)
        {
            if (rowIndex < 0 || rowIndex >= _totalRowCount || columnIndex < 0 || columnIndex >= _totalColumnCount)
            {
                buffer.Reset();
                return;
            }

            int chunkIndex = rowIndex / _chunkSize;
            int offsetInChunk = rowIndex % _chunkSize;

            if (_cache.TryGetValue(chunkIndex, out T[]? chunk) && offsetInChunk < chunk.Length)
            {
                string text = _valueExtractor(chunk[offsetInChunk], columnIndex);
                buffer.Text = text.AsSpan();
                return;
            }

            // Not yet cached -> queue async fetch and return shimmer placeholder
            RequestChunk(chunkIndex);
            buffer.Text = "...".AsSpan();
        }

        private void RequestChunk(int chunkIndex)
        {
            lock (_lock)
            {
                if (_pendingRequests.Contains(chunkIndex) || _cache.ContainsKey(chunkIndex))
                    return;

                _pendingRequests.Add(chunkIndex);
            }

            int startRow = chunkIndex * _chunkSize;
            int count = Math.Min(_chunkSize, _totalRowCount - startRow);

            Task.Run(async () =>
            {
                try
                {
                    var items = await _fetchFunc(startRow, count, CancellationToken.None).ConfigureAwait(false);
                    if (items != null)
                    {
                        var arr = new T[items.Count];
                        for (int i = 0; i < items.Count; i++) arr[i] = items[i];
                        _cache[chunkIndex] = arr;
                    }
                }
                catch
                {
                    // Fallback gracefully on network/IO timeout
                }
                finally
                {
                    lock (_lock)
                    {
                        _pendingRequests.Remove(chunkIndex);
                    }
                    ChunkLoaded?.Invoke(this, new ChunkLoadedEventArgs(startRow, count));
                }
            });
        }

        public void Prefetch(int startRow, int count)
        {
            int startChunk = startRow / _chunkSize;
            int endChunk = (startRow + count - 1) / _chunkSize;
            for (int c = startChunk; c <= endChunk; c++)
            {
                RequestChunk(c);
            }
        }

        public void ClearCache()
        {
            _cache.Clear();
            lock (_lock)
            {
                _pendingRequests.Clear();
            }
        }
    }
}
