using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroPlatform.Concurrency.RateLimiting
{
    /// <summary>
    /// Ultra-fast, zero-allocation token bucket rate limiter with microsecond timestamp precision.
    /// Thread-safe and lock-free/micro-spin based to maximize throughput under heavy concurrency.
    /// </summary>
    public sealed class TokenBucketRateLimiter
    {
        private readonly double _capacity;
        private readonly double _tokensPerSecond;
        private readonly double _tokensPerTick;

        private SpinLock _spinLock = new SpinLock(enableThreadOwnerTracking: false);
        private long _lastTimestamp;
        private double _availableTokens;

        /// <summary>
        /// Gets the maximum capacity (burst size) of the token bucket.
        /// </summary>
        public double Capacity => _capacity;

        /// <summary>
        /// Gets the sustained refill rate in tokens per second.
        /// </summary>
        public double TokensPerSecond => _tokensPerSecond;

        /// <summary>
        /// Initializes a new instance of <see cref="TokenBucketRateLimiter"/>.
        /// </summary>
        /// <param name="capacity">Maximum burst capacity (must be greater than 0).</param>
        /// <param name="tokensPerSecond">Sustained rate of tokens replenished per second (must be greater than 0).</param>
        /// <param name="initialTokens">Initial tokens in the bucket. If negative, defaults to full capacity.</param>
        public TokenBucketRateLimiter(double capacity, double tokensPerSecond, double initialTokens = -1)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
            if (tokensPerSecond <= 0)
                throw new ArgumentOutOfRangeException(nameof(tokensPerSecond), "Tokens per second must be positive.");

            _capacity = capacity;
            _tokensPerSecond = tokensPerSecond;
            _tokensPerTick = tokensPerSecond / Stopwatch.Frequency;

            _availableTokens = initialTokens < 0 ? capacity : Math.Min(capacity, initialTokens);
            _lastTimestamp = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Gets the currently available tokens, accounting for elapsed time up to now.
        /// </summary>
        public double AvailableTokens
        {
            get
            {
                bool lockTaken = false;
                try
                {
                    _spinLock.Enter(ref lockTaken);
                    ReplenishTokens(Stopwatch.GetTimestamp());
                    return _availableTokens;
                }
                finally
                {
                    if (lockTaken) _spinLock.Exit(useMemoryBarrier: false);
                }
            }
        }

        /// <summary>
        /// Attempts to acquire the specified number of tokens immediately without blocking.
        /// Returns true if tokens were acquired; false if insufficient tokens are available.
        /// Zero managed allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAcquire(double tokens = 1.0)
        {
            if (tokens <= 0)
                throw new ArgumentOutOfRangeException(nameof(tokens), "Tokens must be positive.");

            long now = Stopwatch.GetTimestamp();
            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);

                ReplenishTokens(now);

                if (_availableTokens >= tokens)
                {
                    _availableTokens -= tokens;
                    return true;
                }

                return false;
            }
            finally
            {
                if (lockTaken) _spinLock.Exit(useMemoryBarrier: false);
            }
        }

        /// <summary>
        /// Asynchronously waits until the requested tokens are available and acquires them.
        /// If tokens are available immediately, completes synchronously with zero heap allocation.
        /// </summary>
        public async ValueTask AcquireAsync(double tokens = 1.0, CancellationToken cancellationToken = default)
        {
            if (tokens <= 0)
                throw new ArgumentOutOfRangeException(nameof(tokens), "Tokens must be positive.");
            if (tokens > _capacity)
                throw new ArgumentOutOfRangeException(nameof(tokens), "Requested tokens exceed total bucket capacity.");

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double delaySeconds = 0;
                long now = Stopwatch.GetTimestamp();
                bool lockTaken = false;

                try
                {
                    _spinLock.Enter(ref lockTaken);

                    ReplenishTokens(now);

                    if (_availableTokens >= tokens)
                    {
                        _availableTokens -= tokens;
                        return;
                    }

                    double deficit = tokens - _availableTokens;
                    delaySeconds = deficit / _tokensPerSecond;
                }
                finally
                {
                    if (lockTaken) _spinLock.Exit(useMemoryBarrier: false);
                }

                int delayMs = (int)Math.Ceiling(delaySeconds * 1000.0);
                if (delayMs < 1) delayMs = 1;

                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Resets the bucket to a specific token level or full capacity.
        /// </summary>
        public void Reset(double? newTokens = null)
        {
            bool lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);
                _lastTimestamp = Stopwatch.GetTimestamp();
                _availableTokens = newTokens.HasValue ? Math.Min(_capacity, Math.Max(0, newTokens.Value)) : _capacity;
            }
            finally
            {
                if (lockTaken) _spinLock.Exit(useMemoryBarrier: true);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReplenishTokens(long now)
        {
            long elapsedTicks = now - _lastTimestamp;
            if (elapsedTicks > 0)
            {
                double replenished = elapsedTicks * _tokensPerTick;
                _availableTokens = Math.Min(_capacity, _availableTokens + replenished);
                _lastTimestamp = now;
            }
        }
    }
}
