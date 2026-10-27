using System;
using ZeroRfid.Core.Codecs;
using ZeroRfid.Core.Models;

namespace ZeroRfid.Adapters.Abstractions;

/// <summary>
/// Utility helper for vendor SDK adapters to normalize disparate hardware units into standardized ZeroRfid domain models.
/// </summary>
public static class TagDataNormalizer
{
    /// <summary>
    /// Normalizes antenna IDs from zero-based to standard one-based indexing.
    /// </summary>
    public static int NormalizeAntenna(int rawAntenna, bool isZeroBased = true)
    {
        return isZeroBased ? (rawAntenna + 1) : Math.Max(1, rawAntenna);
    }

    /// <summary>
    /// Converts a vendor's raw ADC or proprietary signal strength integer (e.g. 0 to 255) into an approximate dBm value.
    /// </summary>
    public static double ConvertAdcToDbm(int rawAdc, double minDbm = -85.0, double maxDbm = -30.0)
    {
        double clamped = Math.Max(0, Math.Min(255, rawAdc));
        double ratio = clamped / 255.0;
        return minDbm + (ratio * (maxDbm - minDbm));
    }

    /// <summary>
    /// Cleans and formats raw EPC strings or byte arrays into uppercase canonical hexadecimal.
    /// </summary>
    public static string NormalizeEpc(string rawEpc)
    {
        return EpcHexCodec.Normalize(rawEpc);
    }

    /// <summary>
    /// Creates a standardized TagReadEvent with default sanitization.
    /// </summary>
    public static TagReadEvent CreateNormalizedEvent(
        string rawEpc,
        int rawAntenna,
        double rssiDbm,
        string readerId,
        bool isAntennaZeroBased = false,
        string? tid = null,
        DateTime? timestampUtc = null)
    {
        string normEpc = NormalizeEpc(rawEpc);
        int normAntenna = NormalizeAntenna(rawAntenna, isAntennaZeroBased);
        return new TagReadEvent(
            normEpc,
            normAntenna,
            rssiDbm,
            readerId,
            timestampUtc ?? DateTime.UtcNow,
            readCount: 1,
            tid: string.IsNullOrWhiteSpace(tid) ? null : EpcHexCodec.Normalize(tid!));
    }
}
