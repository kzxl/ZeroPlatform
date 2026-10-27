using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlatform.Concurrency.RateLimiting;

namespace ZeroSystem;

/// <summary>
/// Sovereign rate-limited hardware telemetry monitor and watcher.
/// Employs <see cref="TokenBucketRateLimiter"/> to prevent kernel query storming and CPU saturation.
/// </summary>
public sealed class HardwareTelemetryWatcher
{
    private readonly TokenBucketRateLimiter _rateLimiter;
    private readonly double _samplesPerSecond;
    private readonly double _burstCapacity;

    /// <summary>
    /// Gets the sustained sampling rate in snapshots per second.
    /// </summary>
    public double SamplesPerSecond => _samplesPerSecond;

    /// <summary>
    /// Gets the maximum burst capacity for telemetry captures.
    /// </summary>
    public double BurstCapacity => _burstCapacity;

    /// <summary>
    /// Initializes a new instance of <see cref="HardwareTelemetryWatcher"/>.
    /// </summary>
    /// <param name="samplesPerSecond">Sustained telemetry collection frequency (defaults to 5.0 snapshots/sec).</param>
    /// <param name="burstCapacity">Maximum allowed burst queries before throttling (defaults to 10.0).</param>
    public HardwareTelemetryWatcher(double samplesPerSecond = 5.0, double burstCapacity = 10.0)
    {
        if (samplesPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(samplesPerSecond), "Samples per second must be positive.");
        if (burstCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(burstCapacity), "Burst capacity must be positive.");

        _samplesPerSecond = samplesPerSecond;
        _burstCapacity = burstCapacity;
        _rateLimiter = new TokenBucketRateLimiter(burstCapacity, samplesPerSecond);
    }

    /// <summary>
    /// Attempts to capture a telemetry snapshot if permitted by the rate limiter without blocking.
    /// </summary>
    /// <param name="snapshot">The captured telemetry snapshot if successful; otherwise null.</param>
    /// <returns><c>true</c> if a snapshot was captured; <c>false</c> if throttled.</returns>
    public bool TryGetSnapshot([NotNullWhen(true)] out SystemDiagnosticsSnapshot? snapshot)
    {
        if (_rateLimiter.TryAcquire(1.0))
        {
            snapshot = HardwareTelemetry.GetSnapshot();
            return true;
        }

        snapshot = null;
        return false;
    }

    /// <summary>
    /// Asynchronously acquires rate limit capacity and captures a telemetry snapshot.
    /// </summary>
    public async ValueTask<SystemDiagnosticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await _rateLimiter.AcquireAsync(1.0, cancellationToken).ConfigureAwait(false);
        return HardwareTelemetry.GetSnapshot();
    }
}
