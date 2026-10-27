using System;
using System.Text;

namespace ZeroSecurity.Matching;

/// <summary>
/// Cache-friendly, high-performance Bloom Filter for sub-millisecond IOC membership queries
/// (e.g. testing millions of malicious file hashes, domains, or IPs) with zero heap allocation per check.
/// Uses the Kirsch-Mitzenmacher double-hashing technique.
/// </summary>
public sealed class BloomFilter
{
    private readonly ulong[] _bits;
    private readonly int _bitCount;
    private readonly int _hashCount;
    private long _elementCount;

    /// <summary>
    /// Total number of bits allocated in the filter.
    /// </summary>
    public int BitCount => _bitCount;

    /// <summary>
    /// Number of independent hash functions (k).
    /// </summary>
    public int HashCount => _hashCount;

    /// <summary>
    /// Number of elements added to this filter.
    /// </summary>
    public long ElementCount => _elementCount;

    /// <summary>
    /// Constructs a Bloom filter with the specified capacity and desired false positive probability.
    /// </summary>
    /// <param name="expectedElements">Expected number of elements to store (n).</param>
    /// <param name="falsePositiveRate">Desired false positive probability (p), e.g. 0.01 for 1%.</param>
    public BloomFilter(int expectedElements, double falsePositiveRate = 0.01)
    {
        if (expectedElements <= 0) throw new ArgumentOutOfRangeException(nameof(expectedElements), "Expected elements must be > 0.");
        if (falsePositiveRate <= 0.0 || falsePositiveRate >= 1.0) throw new ArgumentOutOfRangeException(nameof(falsePositiveRate), "False positive rate must be between 0 and 1.");

        // m = - (n * ln(p)) / (ln(2)^2)
        double m = -(expectedElements * Math.Log(falsePositiveRate)) / 0.4804530139182014;
        _bitCount = Math.Max(64, (int)Math.Ceiling(m));
        
        // k = (m / n) * ln(2)
        double k = (_bitCount / (double)expectedElements) * 0.6931471805599453;
        _hashCount = Math.Max(1, (int)Math.Round(k));

        int ulongCount = (_bitCount + 63) / 64;
        _bits = new ulong[ulongCount];
    }

    private BloomFilter(ulong[] bits, int bitCount, int hashCount, long elementCount)
    {
        _bits = bits;
        _bitCount = bitCount;
        _hashCount = hashCount;
        _elementCount = elementCount;
    }

    /// <summary>
    /// Adds an element by byte span to the filter.
    /// </summary>
    public void Add(ReadOnlySpan<byte> item)
    {
        ComputeHashes(item, out ulong h1, out ulong h2);

        for (int i = 0; i < _hashCount; i++)
        {
            ulong combined = h1 + (ulong)i * h2;
            int bitIndex = (int)(combined % (ulong)_bitCount);
            _bits[bitIndex >> 6] |= (1UL << (bitIndex & 63));
        }

        _elementCount++;
    }

    /// <summary>
    /// Adds a string element (e.g. SHA-256 hash or domain name) to the filter.
    /// </summary>
    public void Add(string item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
#if NET8_0_OR_GREATER
        Span<byte> utf8 = stackalloc byte[Encoding.UTF8.GetByteCount(item)];
        Encoding.UTF8.GetBytes(item.AsSpan(), utf8);
        Add(utf8);
#else
        byte[] bytes = Encoding.UTF8.GetBytes(item);
        Add(bytes);
#endif
    }

    /// <summary>
    /// Checks if an element might be present in the set.
    /// Returns false if definitely not in set; returns true if possibly in set.
    /// </summary>
    public bool Contains(ReadOnlySpan<byte> item)
    {
        ComputeHashes(item, out ulong h1, out ulong h2);

        for (int i = 0; i < _hashCount; i++)
        {
            ulong combined = h1 + (ulong)i * h2;
            int bitIndex = (int)(combined % (ulong)_bitCount);
            if ((_bits[bitIndex >> 6] & (1UL << (bitIndex & 63))) == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if a string element might be present in the set.
    /// </summary>
    public bool Contains(string item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
#if NET8_0_OR_GREATER
        Span<byte> utf8 = stackalloc byte[Encoding.UTF8.GetByteCount(item)];
        Encoding.UTF8.GetBytes(item.AsSpan(), utf8);
        return Contains(utf8);
#else
        byte[] bytes = Encoding.UTF8.GetBytes(item);
        return Contains(bytes);
#endif
    }

    /// <summary>
    /// Calculates the current estimated false positive rate given the number of added items.
    /// </summary>
    public double EstimatedFalsePositiveRate
    {
        get
        {
            // (1 - e^(-k * n / m))^k
            double exponent = -((double)_hashCount * _elementCount) / _bitCount;
            return Math.Pow(1.0 - Math.Exp(exponent), _hashCount);
        }
    }

    /// <summary>
    /// Serializes the Bloom filter bitset into a compact byte array for offline storage or distribution.
    /// </summary>
    public byte[] ToByteArray()
    {
        byte[] buffer = new byte[24 + _bits.Length * 8];
        BitConverter.GetBytes(_bitCount).CopyTo(buffer, 0);
        BitConverter.GetBytes(_hashCount).CopyTo(buffer, 4);
        BitConverter.GetBytes(_elementCount).CopyTo(buffer, 8);
        BitConverter.GetBytes(_bits.Length).CopyTo(buffer, 16);

        Buffer.BlockCopy(_bits, 0, buffer, 24, _bits.Length * 8);
        return buffer;
    }

    /// <summary>
    /// Deserializes a Bloom filter from a serialized byte array.
    /// </summary>
    public static BloomFilter FromByteArray(byte[] buffer)
    {
        if (buffer == null || buffer.Length < 24)
            throw new ArgumentException("Invalid Bloom filter buffer.", nameof(buffer));

        int bitCount = BitConverter.ToInt32(buffer, 0);
        int hashCount = BitConverter.ToInt32(buffer, 4);
        long elementCount = BitConverter.ToInt64(buffer, 8);
        int ulongLen = BitConverter.ToInt32(buffer, 16);

        if (buffer.Length < 24 + ulongLen * 8)
            throw new ArgumentException("Buffer truncated.", nameof(buffer));

        ulong[] bits = new ulong[ulongLen];
        Buffer.BlockCopy(buffer, 24, bits, 0, ulongLen * 8);

        return new BloomFilter(bits, bitCount, hashCount, elementCount);
    }

    private static void ComputeHashes(ReadOnlySpan<byte> data, out ulong h1, out ulong h2)
    {
        // FNV-1a 64-bit
        ulong hash1 = 14695981039346656037UL;
        for (int i = 0; i < data.Length; i++)
        {
            hash1 ^= data[i];
            hash1 *= 1099511628211UL;
        }
        h1 = hash1;

        // Murmur-inspired 64-bit mixer
        ulong hash2 = (ulong)data.Length * 0xc6a4a7935bd1e995UL;
        for (int i = 0; i < data.Length; i++)
        {
            hash2 ^= (ulong)data[i] << ((i & 7) * 8);
            hash2 *= 0x5bd1e9955bd1e995UL;
        }
        hash2 ^= hash2 >> 47;
        h2 = hash2 != 0 ? hash2 : 1; // Prevent zero step
    }
}
