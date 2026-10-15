using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace ZeroSecurity.Matching;

/// <summary>
/// High-performance, pure C# Cuckoo Filter for dynamic IOC set membership testing.
/// Unlike standard Bloom filters, Cuckoo filters support O(1) Insert, Contains, AND Delete operations,
/// with compact 16-bit fingerprints and zero external dependencies.
/// </summary>
public sealed class CuckooFilter
{
    private const int SlotsPerBucket = 4;
    private const int MaxKicks = 500;
    private const uint Magic = 0x4C464B43; // "CKFL" in LE

    private readonly ushort[] _buckets;
    private readonly int _bucketCount;
    private readonly int _bucketMask;
    private int _count;

    /// <summary>
    /// Gets the number of elements currently stored in the filter.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets the total capacity in available slots.
    /// </summary>
    public int Capacity => _bucketCount * SlotsPerBucket;

    /// <summary>
    /// Gets the load factor of the filter (Count / Capacity).
    /// </summary>
    public double LoadFactor => (double)_count / Capacity;

    /// <summary>
    /// Initializes a new CuckooFilter with the specified expected capacity.
    /// </summary>
    public CuckooFilter(int expectedElements = 65536)
    {
        if (expectedElements <= 0)
            throw new ArgumentOutOfRangeException(nameof(expectedElements), "Expected elements must be positive.");

        // Target ~80% load factor: capacity = expectedElements / 0.8
        int targetCapacity = Math.Max(16, (int)(expectedElements / 0.8));
        int targetBuckets = (targetCapacity + SlotsPerBucket - 1) / SlotsPerBucket;

        _bucketCount = NextPowerOfTwo(targetBuckets);
        _bucketMask = _bucketCount - 1;
        _buckets = new ushort[_bucketCount * SlotsPerBucket];
        _count = 0;
    }

    private CuckooFilter(int bucketCount, int count, ushort[] buckets)
    {
        _bucketCount = bucketCount;
        _bucketMask = bucketCount - 1;
        _buckets = buckets;
        _count = count;
    }

    /// <summary>
    /// Adds a string item (e.g. domain, threat hash, IP) to the filter.
    /// Returns true if inserted; false if the filter is full and cannot accommodate more items.
    /// </summary>
    public bool Add(string item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        int byteCount = Encoding.UTF8.GetByteCount(item);
        if (byteCount <= 256)
        {
            Span<byte> buffer = stackalloc byte[byteCount];
            unsafe
            {
                fixed (char* pChars = item)
                fixed (byte* pBytes = buffer)
                {
                    Encoding.UTF8.GetBytes(pChars, item.Length, pBytes, byteCount);
                }
            }
            return Add(buffer);
        }

        return Add(Encoding.UTF8.GetBytes(item));
    }

    /// <summary>
    /// Adds raw byte data to the filter.
    /// </summary>
    public bool Add(ReadOnlySpan<byte> item)
    {
        ulong hash = Hash64(item);
        ushort fp = GetFingerprint(hash);
        int i1 = (int)(hash & (ulong)_bucketMask);
        int i2 = AltIndex(i1, fp);

        if (PutInBucket(i1, fp) || PutInBucket(i2, fp))
        {
            _count++;
            return true;
        }

        // Cuckoo eviction using inline zero-allocation XorShift32 PRNG
        int curIndex = (hash & 1) == 0 ? i1 : i2;
        ushort curFp = fp;
        uint state = (uint)hash;
        if (state == 0) state = 0x9E3779B9u;

        for (int k = 0; k < MaxKicks; k++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            int slot = (int)(state & (SlotsPerBucket - 1));
            int idx = curIndex * SlotsPerBucket + slot;

            ushort oldFp = _buckets[idx];
            _buckets[idx] = curFp;
            curFp = oldFp;

            curIndex = AltIndex(curIndex, curFp);
            if (PutInBucket(curIndex, curFp))
            {
                _count++;
                return true;
            }
        }

        return false; // Filter is over-saturated
    }

    /// <summary>
    /// Checks if a string item is likely in the filter.
    /// </summary>
    public bool Contains(string item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        int byteCount = Encoding.UTF8.GetByteCount(item);
        if (byteCount <= 256)
        {
            Span<byte> buffer = stackalloc byte[byteCount];
            unsafe
            {
                fixed (char* pChars = item)
                fixed (byte* pBytes = buffer)
                {
                    Encoding.UTF8.GetBytes(pChars, item.Length, pBytes, byteCount);
                }
            }
            return Contains(buffer);
        }

        return Contains(Encoding.UTF8.GetBytes(item));
    }

    /// <summary>
    /// Checks if a byte span is likely in the filter.
    /// </summary>
    public bool Contains(ReadOnlySpan<byte> item)
    {
        ulong hash = Hash64(item);
        ushort fp = GetFingerprint(hash);
        int i1 = (int)(hash & (ulong)_bucketMask);
        int i2 = AltIndex(i1, fp);

        return BucketContains(i1, fp) || BucketContains(i2, fp);
    }

    /// <summary>
    /// Deletes a string item from the filter.
    /// Returns true if an instance was found and removed; false otherwise.
    /// </summary>
    public bool Delete(string item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        int byteCount = Encoding.UTF8.GetByteCount(item);
        if (byteCount <= 256)
        {
            Span<byte> buffer = stackalloc byte[byteCount];
            unsafe
            {
                fixed (char* pChars = item)
                fixed (byte* pBytes = buffer)
                {
                    Encoding.UTF8.GetBytes(pChars, item.Length, pBytes, byteCount);
                }
            }
            return Delete(buffer);
        }

        return Delete(Encoding.UTF8.GetBytes(item));
    }

    /// <summary>
    /// Deletes a byte span from the filter.
    /// </summary>
    public bool Delete(ReadOnlySpan<byte> item)
    {
        ulong hash = Hash64(item);
        ushort fp = GetFingerprint(hash);
        int i1 = (int)(hash & (ulong)_bucketMask);
        int i2 = AltIndex(i1, fp);

        if (DeleteFromBucket(i1, fp) || DeleteFromBucket(i2, fp))
        {
            _count--;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Serializes the CuckooFilter to a binary byte array for network distribution or disk persistence.
    /// </summary>
    public byte[] ToByteArray()
    {
        byte[] result = new byte[12 + _buckets.Length * sizeof(ushort)];
        Span<byte> span = result.AsSpan();

        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(0, 4), Magic);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(4, 4), _bucketCount);
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(8, 4), _count);

        Span<byte> payload = span.Slice(12);
        for (int i = 0; i < _buckets.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.Slice(i * 2, 2), _buckets[i]);
        }

        return result;
    }

    /// <summary>
    /// Deserializes a CuckooFilter from a byte span without intermediate stream or array allocations.
    /// </summary>
    public static CuckooFilter FromByteArray(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12)
            throw new ArgumentException("Buffer too small for CuckooFilter header.", nameof(bytes));

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(0, 4));
        if (magic != Magic)
            throw new InvalidDataException("Invalid CuckooFilter magic header.");

        int bucketCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4, 4));
        int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8, 4));
        if (bucketCount <= 0 || count < 0)
            throw new InvalidDataException("Invalid CuckooFilter header dimensions.");

        int totalSlots = bucketCount * SlotsPerBucket;
        int expectedLength = 12 + totalSlots * sizeof(ushort);
        if (bytes.Length < expectedLength)
            throw new InvalidDataException("Buffer truncated for CuckooFilter contents.");

        ushort[] buckets = new ushort[totalSlots];
        ReadOnlySpan<byte> payload = bytes.Slice(12, totalSlots * sizeof(ushort));
        for (int i = 0; i < buckets.Length; i++)
        {
            buckets[i] = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(i * 2, 2));
        }

        return new CuckooFilter(bucketCount, count, buckets);
    }

    #region Private Helpers

    private bool PutInBucket(int bucketIndex, ushort fp)
    {
        int baseIdx = bucketIndex * SlotsPerBucket;
        for (int i = 0; i < SlotsPerBucket; i++)
        {
            if (_buckets[baseIdx + i] == 0)
            {
                _buckets[baseIdx + i] = fp;
                return true;
            }
        }
        return false;
    }

    private bool BucketContains(int bucketIndex, ushort fp)
    {
        int baseIdx = bucketIndex * SlotsPerBucket;
        for (int i = 0; i < SlotsPerBucket; i++)
        {
            if (_buckets[baseIdx + i] == fp) return true;
        }
        return false;
    }

    private bool DeleteFromBucket(int bucketIndex, ushort fp)
    {
        int baseIdx = bucketIndex * SlotsPerBucket;
        for (int i = 0; i < SlotsPerBucket; i++)
        {
            if (_buckets[baseIdx + i] == fp)
            {
                _buckets[baseIdx + i] = 0;
                return true;
            }
        }
        return false;
    }

    private int AltIndex(int index, ushort fp)
    {
        ulong h = (ulong)fp * 0x517cc1b727220a95UL;
        h ^= h >> 32;
        return (int)((index ^ (int)(h & (ulong)_bucketMask)) & _bucketMask);
    }

    private static ushort GetFingerprint(ulong hash)
    {
        ushort fp = (ushort)(hash ^ (hash >> 16) ^ (hash >> 32) ^ (hash >> 48));
        return fp == 0 ? (ushort)1 : fp;
    }

    private static ulong Hash64(ReadOnlySpan<byte> data)
    {
        ulong hash = 14695981039346656037UL;
        for (int i = 0; i < data.Length; i++)
        {
            hash ^= data[i];
            hash *= 1099511628211UL;
        }
        hash ^= hash >> 33;
        hash *= 0xff51afd7ed558ccdUL;
        hash ^= hash >> 33;
        return hash;
    }

    private static int NextPowerOfTwo(int v)
    {
        v--;
        v |= v >> 1;
        v |= v >> 2;
        v |= v >> 4;
        v |= v >> 8;
        v |= v >> 16;
        return Math.Max(4, v + 1);
    }

    #endregion
}
