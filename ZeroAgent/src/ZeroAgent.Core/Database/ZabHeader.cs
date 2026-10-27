using System;
using System.IO;

namespace ZeroAgent.Core.Database
{
    /// <summary>
    /// Binary file header for Zero Agent Binary (ZAB1) sovereign database container.
    /// Exactly 128 bytes fixed size, aligned for memory-mapped I/O and hardware checksum verification.
    /// </summary>
    public sealed class ZabHeader
    {
        public const uint MagicValue = 0x3142415A; // 'Z', 'A', 'B', '1' in little-endian
        public const uint CurrentVersion = 1;
        public const int HeaderSize = 128;

        public uint Magic { get; set; } = MagicValue;
        public uint Version { get; set; } = CurrentVersion;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
        public uint ChecksumCrc32C { get; set; }
        public uint Flags { get; set; }
        public long WalSequenceNumber { get; set; } // Log Sequence Number (LSN)

        // Section Offsets and Lengths (Table of Contents)
        public long ManifestOffset { get; set; }
        public int ManifestLength { get; set; }

        public long KnowledgeOffset { get; set; }
        public int KnowledgeLength { get; set; }

        public long VectorsOffset { get; set; }
        public int VectorsLength { get; set; }

        public long ReflexionsOffset { get; set; }
        public int ReflexionsLength { get; set; }

        public long PlansOffset { get; set; }
        public int PlansLength { get; set; }

        public long NeuralOffset { get; set; }
        public int NeuralLength { get; set; }

        public bool IsValid => Magic == MagicValue && Version == CurrentVersion;

        public void Write(BinaryWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            long startPos = writer.BaseStream.Position;

            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(CreatedUtc.ToBinary());
            writer.Write(LastModifiedUtc.ToBinary());
            writer.Write(ChecksumCrc32C);
            writer.Write(Flags);
            writer.Write(WalSequenceNumber);

            writer.Write(ManifestOffset);
            writer.Write(ManifestLength);

            writer.Write(KnowledgeOffset);
            writer.Write(KnowledgeLength);

            writer.Write(VectorsOffset);
            writer.Write(VectorsLength);

            writer.Write(ReflexionsOffset);
            writer.Write(ReflexionsLength);

            writer.Write(PlansOffset);
            writer.Write(PlansLength);

            writer.Write(NeuralOffset);
            writer.Write(NeuralLength);

            // Pad remaining bytes up to HeaderSize (128 bytes)
            long written = writer.BaseStream.Position - startPos;
            int remaining = HeaderSize - (int)written;
            if (remaining > 0)
            {
                writer.Write(new byte[remaining]);
            }
        }

        public static ZabHeader Read(BinaryReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            long startPos = reader.BaseStream.Position;

            var header = new ZabHeader
            {
                Magic = reader.ReadUInt32(),
                Version = reader.ReadUInt32(),
                CreatedUtc = DateTime.FromBinary(reader.ReadInt64()),
                LastModifiedUtc = DateTime.FromBinary(reader.ReadInt64()),
                ChecksumCrc32C = reader.ReadUInt32(),
                Flags = reader.ReadUInt32(),
                WalSequenceNumber = reader.ReadInt64(),

                ManifestOffset = reader.ReadInt64(),
                ManifestLength = reader.ReadInt32(),

                KnowledgeOffset = reader.ReadInt64(),
                KnowledgeLength = reader.ReadInt32(),

                VectorsOffset = reader.ReadInt64(),
                VectorsLength = reader.ReadInt32(),

                ReflexionsOffset = reader.ReadInt64(),
                ReflexionsLength = reader.ReadInt32(),

                PlansOffset = reader.ReadInt64(),
                PlansLength = reader.ReadInt32(),

                NeuralOffset = reader.ReadInt64(),
                NeuralLength = reader.ReadInt32()
            };

            // Skip remaining padding to align to HeaderSize (128 bytes)
            long readBytes = reader.BaseStream.Position - startPos;
            int remaining = HeaderSize - (int)readBytes;
            if (remaining > 0)
            {
                reader.BaseStream.Seek(remaining, SeekOrigin.Current);
            }

            return header;
        }
    }
}
