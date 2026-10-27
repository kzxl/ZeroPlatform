using System;
using System.IO;
using System.Text;
using ZeroPrimitives.Cryptography;

namespace ZeroAgent.Core.Database
{
    /// <summary>
    /// Operation codes for Write-Ahead Log (WAL) transaction frames.
    /// </summary>
    public enum ZabWalOpCode : byte
    {
        Unknown = 0,
        AddKnowledge = 1,
        UpdateKnowledge = 2,
        DeleteKnowledge = 3,
        StoreVector = 4,
        DeleteVector = 5,
        AddReflexion = 6,
        DeleteReflexion = 7,
        CachePlan = 8,
        DeletePlan = 9,
        PrunePlans = 10,
        UpdateNeuralPolicy = 11,
        Checkpoint = 12
    }

    /// <summary>
    /// Binary transaction frame envelope in the Write-Ahead Log (WAL).
    /// Structure: [Magic: 4B ('ZWAL')][OpCode: 1B][Length: 4B][Payload: N Bytes][CRC32C: 4B].
    /// Guarantees torn-write isolation: any partially written or corrupted frame is rejected.
    /// </summary>
    public sealed class ZabWalFrame
    {
        public const uint FrameMagic = 0x4C41575A; // 'Z', 'W', 'A', 'L' in little-endian
        public const int EnvelopeHeaderSize = 9; // 4 bytes magic + 1 byte opcode + 4 bytes length
        public const int ChecksumSize = 4;       // 4 bytes CRC32C

        public ZabWalOpCode OpCode { get; }
        public byte[] Payload { get; }
        public uint ChecksumCrc32C { get; }

        public ZabWalFrame(ZabWalOpCode opCode, byte[] payload)
        {
            OpCode = opCode;
            Payload = payload ?? Array.Empty<byte>();

            // Calculate CRC32C over OpCode + Payload
            byte[] crcBuffer = new byte[1 + Payload.Length];
            crcBuffer[0] = (byte)OpCode;
            if (Payload.Length > 0)
            {
                Buffer.BlockCopy(Payload, 0, crcBuffer, 1, Payload.Length);
            }
            ChecksumCrc32C = FastCrc.Crc32C(crcBuffer);
        }

        private ZabWalFrame(ZabWalOpCode opCode, byte[] payload, uint checksum)
        {
            OpCode = opCode;
            Payload = payload;
            ChecksumCrc32C = checksum;
        }

        public void WriteTo(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(FrameMagic);
                writer.Write((byte)OpCode);
                writer.Write(Payload.Length);
                if (Payload.Length > 0)
                {
                    writer.Write(Payload);
                }
                writer.Write(ChecksumCrc32C);
            }
        }

        /// <summary>
        /// Attempts to parse a WAL frame from stream.
        /// Returns null if end of stream or corrupted frame (torn write) is encountered.
        /// </summary>
        public static ZabWalFrame? TryRead(Stream stream, out long frameStartPosition, out bool isTorn)
        {
            isTorn = false;
            frameStartPosition = stream.Position;

            if (stream.Length - frameStartPosition < EnvelopeHeaderSize + ChecksumSize)
            {
                // Not enough bytes remaining even for a minimal frame
                if (stream.Position < stream.Length)
                {
                    isTorn = true; // Truncated / torn frame at EOF
                }
                return null;
            }

            using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
            {
                uint magic = reader.ReadUInt32();
                if (magic != FrameMagic)
                {
                    isTorn = true;
                    return null;
                }

                byte opByte = reader.ReadByte();
                var opCode = (ZabWalOpCode)opByte;

                int payloadLen = reader.ReadInt32();
                if (payloadLen < 0 || payloadLen > 100 * 1024 * 1024) // 100MB sanity bound
                {
                    isTorn = true;
                    return null;
                }

                if (stream.Length - stream.Position < payloadLen + ChecksumSize)
                {
                    isTorn = true; // Payload truncated mid-write
                    return null;
                }

                byte[] payload = reader.ReadBytes(payloadLen);
                uint storedCrc = reader.ReadUInt32();

                // Verify hardware CRC32C
                byte[] crcBuffer = new byte[1 + payload.Length];
                crcBuffer[0] = (byte)opCode;
                if (payload.Length > 0)
                {
                    Buffer.BlockCopy(payload, 0, crcBuffer, 1, payload.Length);
                }
                uint computedCrc = FastCrc.Crc32C(crcBuffer);

                if (storedCrc != computedCrc)
                {
                    isTorn = true; // Corrupted bytes detected
                    return null;
                }

                return new ZabWalFrame(opCode, payload, storedCrc);
            }
        }
    }
}
