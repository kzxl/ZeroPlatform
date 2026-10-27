using System;

namespace ZeroSecurity.Anomaly;

/// <summary>
/// Online, single-pass Welford algorithm for streaming calculation of Mean, Sample Variance,
/// Standard Deviation, and Z-Score without storing historical data points.
/// Essential for real-time User & Entity Behavior Analytics (UEBA) and baseline anomaly detection.
/// </summary>
public sealed class WelfordStats
{
    private double _m2;

    /// <summary>
    /// Total number of observed samples.
    /// </summary>
    public long Count { get; private set; }

    /// <summary>
    /// Streaming population/sample mean.
    /// </summary>
    public double Mean { get; private set; }

    /// <summary>
    /// Unbiased sample variance (s^2).
    /// </summary>
    public double Variance => Count > 1 ? _m2 / (Count - 1) : 0.0;

    /// <summary>
    /// Sample standard deviation (s).
    /// </summary>
    public double StandardDeviation => Math.Sqrt(Variance);

    /// <summary>
    /// Ingests a new continuous observation into the running statistical model.
    /// </summary>
    public void Update(double x)
    {
        Count++;
        double delta = x - Mean;
        Mean += delta / Count;
        double delta2 = x - Mean;
        _m2 += delta * delta2;
    }

    /// <summary>
    /// Calculates the standard Z-Score: how many standard deviations the observation deviates from the baseline mean.
    /// </summary>
    public double CalculateZScore(double x)
    {
        double std = StandardDeviation;
        if (std < 1e-9) return 0.0;
        return (x - Mean) / std;
    }

    /// <summary>
    /// Evaluates if a new observation is statistically anomalous based on standard deviations.
    /// </summary>
    /// <param name="x">The observed metric.</param>
    /// <param name="zThreshold">Threshold in standard deviations (default is 3.0, representing 99.7% confidence).</param>
    /// <param name="minSamples">Minimum sample size before triggering alerts (default 10).</param>
    public bool IsAnomaly(double x, double zThreshold = 3.0, int minSamples = 10)
    {
        if (Count < minSamples) return false;
        return Math.Abs(CalculateZScore(x)) >= zThreshold;
    }

    /// <summary>
    /// Resets all statistics to initial baseline.
    /// </summary>
    public void Reset()
    {
        Count = 0;
        Mean = 0.0;
        _m2 = 0.0;
    }
}
