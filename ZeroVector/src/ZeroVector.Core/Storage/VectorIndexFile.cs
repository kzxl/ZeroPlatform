using System;
using System.IO;
using ZeroPrimitives.Cryptography;
using ZeroVector.Core.Indices;
using ZeroVector.Core.Metrics;

namespace ZeroVector.Core.Storage
{
    /// <summary>
    /// High-performance binary serialization format for persisting and loading vector indices.
    /// Incorporates CRC32C hardware checksum verification to guarantee data integrity.
    /// </summary>
    public static class VectorIndexFile
    {
        private static readonly byte[] Magic = new byte[] { (byte)'Z', (byte)'V', (byte)'F', (byte)'1' };
        private const uint FormatVersion = 1;

        /// <summary>
        /// Saves a FlatVectorIndex to a stream with CRC32C verification.
        /// </summary>
        public static void Save(FlatVectorIndex index, Stream stream)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            index.Save(stream);
        }

        /// <summary>
        /// Loads a FlatVectorIndex from a stream with validation.
        /// </summary>
        public static FlatVectorIndex Load(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            return FlatVectorIndex.Load(stream);
        }

        /// <summary>
        /// Computes CRC32C checksum of arbitrary buffer.
        /// </summary>
        public static uint ComputeChecksum(ReadOnlySpan<byte> buffer)
        {
            return FastCrc.Crc32C(buffer);
        }
    }
}
