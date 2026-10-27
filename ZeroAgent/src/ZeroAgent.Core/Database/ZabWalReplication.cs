using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ZeroPrimitives.Cryptography;

namespace ZeroAgent.Core.Database
{
    /// <summary>
    /// Binary replication package encapsulating a sequence of atomic WAL mutations for network transport.
    /// Incorporates package-level hardware CRC32C to guard against network transmission bitflips.
    /// </summary>
    public sealed class ZabWalReplicationPackage
    {
        public const uint PackageMagic = 0x53574152; // 'R', 'A', 'W', 'S' (SWARM in little-endian)

        public long FromOffset { get; set; }
        public long ToOffset { get; set; }
        public List<byte[]> SerializedFrames { get; set; } = new List<byte[]>();

        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms, Encoding.UTF8))
            {
                writer.Write(PackageMagic);
                writer.Write(FromOffset);
                writer.Write(ToOffset);
                writer.Write(SerializedFrames.Count);

                for (int i = 0; i < SerializedFrames.Count; i++)
                {
                    writer.Write(SerializedFrames[i].Length);
                    writer.Write(SerializedFrames[i]);
                }

                // Checksum entire stream payload
                byte[] raw = ms.ToArray();
                uint crc = FastCrc.Crc32C(raw);

                using (var finalMs = new MemoryStream())
                using (var finalWriter = new BinaryWriter(finalMs, Encoding.UTF8))
                {
                    finalWriter.Write(raw);
                    finalWriter.Write(crc);
                    return finalMs.ToArray();
                }
            }
        }

        public static ZabWalReplicationPackage Deserialize(byte[] data)
        {
            if (data == null || data.Length < 24)
                throw new ArgumentException("Data too small to be a valid replication package.", nameof(data));

            // Verify checksum
            int payloadLen = data.Length - 4;
            uint expectedCrc = BitConverter.ToUInt32(data, payloadLen);
            byte[] payloadBytes = new byte[payloadLen];
            Buffer.BlockCopy(data, 0, payloadBytes, 0, payloadLen);
            uint computedCrc = FastCrc.Crc32C(payloadBytes);

            if (computedCrc != expectedCrc)
            {
                throw new InvalidDataException("Replication package CRC32C verification failed. Data corrupted in transit.");
            }

            using (var ms = new MemoryStream(payloadBytes))
            using (var reader = new BinaryReader(ms, Encoding.UTF8))
            {
                uint magic = reader.ReadUInt32();
                if (magic != PackageMagic)
                {
                    throw new InvalidDataException("Invalid replication package magic header.");
                }

                var package = new ZabWalReplicationPackage
                {
                    FromOffset = reader.ReadInt64(),
                    ToOffset = reader.ReadInt64()
                };

                int count = reader.ReadInt32();
                package.SerializedFrames = new List<byte[]>(count);
                for (int i = 0; i < count; i++)
                {
                    int len = reader.ReadInt32();
                    package.SerializedFrames.Add(reader.ReadBytes(len));
                }

                return package;
            }
        }
    }

    /// <summary>
    /// Peer-to-Peer Write-Ahead Log (WAL) replication engine for distributed agent swarms.
    /// Streams incremental transactional mutations from primary leaders to replica nodes
    /// without stopping write operations or transferring multi-gigabyte snapshot baselines.
    /// </summary>
    public static class ZabWalReplicator
    {
        /// <summary>
        /// Reads new WAL frames starting from the specified byte offset and bundles them into a transport package.
        /// </summary>
        public static ZabWalReplicationPackage ExtractIncrementalPackage(string walFilePath, long fromOffset, out long nextOffset)
        {
            if (string.IsNullOrWhiteSpace(walFilePath)) throw new ArgumentNullException(nameof(walFilePath));

            var package = new ZabWalReplicationPackage { FromOffset = fromOffset };

            if (!File.Exists(walFilePath))
            {
                nextOffset = fromOffset;
                package.ToOffset = fromOffset;
                return package;
            }

            using (var fs = new FileStream(walFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fromOffset >= fs.Length)
                {
                    nextOffset = fs.Length;
                    package.ToOffset = fs.Length;
                    return package;
                }

                fs.Position = fromOffset;

                while (fs.Position < fs.Length)
                {
                    long frameStart = fs.Position;
                    var frame = ZabWalFrame.TryRead(fs, out _, out bool isTorn);
                    if (isTorn || frame == null)
                    {
                        // Stop at safe boundary
                        fs.Position = frameStart;
                        break;
                    }

                    // Serialize raw frame bytes
                    long frameEnd = fs.Position;
                    int frameSize = (int)(frameEnd - frameStart);
                    fs.Position = frameStart;
                    byte[] rawFrame = new byte[frameSize];
                    fs.Read(rawFrame, 0, frameSize);
                    package.SerializedFrames.Add(rawFrame);
                }

                nextOffset = fs.Position;
                package.ToOffset = nextOffset;
            }

            return package;
        }

        /// <summary>
        /// Ingests a replication package into a target replica ZabDatabase, applying each mutation atomically.
        /// </summary>
        public static int IngestReplicationPackage(ZabDatabase targetDb, ZabWalReplicationPackage package)
        {
            if (targetDb == null) throw new ArgumentNullException(nameof(targetDb));
            if (package == null) throw new ArgumentNullException(nameof(package));

            int applied = 0;

            for (int i = 0; i < package.SerializedFrames.Count; i++)
            {
                byte[] frameBytes = package.SerializedFrames[i];
                using (var ms = new MemoryStream(frameBytes))
                {
                    var frame = ZabWalFrame.TryRead(ms, out _, out bool isTorn);
                    if (!isTorn && frame != null)
                    {
                        targetDb.ApplyWalMutation(frame.OpCode, frame.Payload);
                        // Also append to replica local WAL for durability
                        targetDb.Wal.AppendFrame(frame.OpCode, frame.Payload);
                        applied++;
                    }
                }
            }

            return applied;
        }
    }
}
