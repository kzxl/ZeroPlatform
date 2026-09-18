using System;
using System.IO;
using System.Security.Cryptography;
using ZeroCompression.Core.Buffers;

namespace ZeroCompression.Core.Crypto
{
    /// <summary>
    /// Authenticated payload encryption using AES-256-GCM in a chunked AEAD scheme.
    ///
    /// Layout: [16 salt][8 verifier][ chunk* ] where each chunk is
    /// [1 final-flag][4 ciphertext length][12 nonce][16 tag][ciphertext].
    /// Key derivation: PBKDF2 (SHA-256, 200k iterations).
    /// </summary>
    public static class PayloadCrypto
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;          // AES-256
        private const int VerifierSize = 8;
        private const int NonceSize = 12;        // GCM standard
        private const int TagSize = 16;          // GCM standard
        private const int Iterations = 200_000;
        private const int ChunkSize = 64 * 1024; // plaintext bytes per AEAD chunk

        public static Stream CreateEncryptor(Stream destination, string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] key = DeriveKey(password, salt);
            byte[] verifier = MakeVerifier(key, salt);

            destination.Write(salt, 0, salt.Length);
            destination.Write(verifier, 0, verifier.Length);

            return new GcmWriteStream(destination, key);
        }

        public static Stream CreateDecryptor(Stream source, string password)
        {
            byte[] salt = ReadExactly(source, SaltSize);
            byte[] verifier = ReadExactly(source, VerifierSize);

            byte[] key = DeriveKey(password, salt);
            byte[] expected = MakeVerifier(key, salt);
            if (!CryptographicOperations.FixedTimeEquals(verifier, expected))
                throw new InvalidDataException("Incorrect password.");

            return new GcmReadStream(source, key);
        }

        /// <summary>
        /// Validates a password against the 24-byte crypto header in &lt;5ms without decrypting payload.
        /// </summary>
        public static bool CheckPassword(Stream source, string password)
        {
            if (!source.CanSeek)
                throw new ArgumentException("Stream must be seekable to probe header.", nameof(source));

            long pos = source.Position;
            try
            {
                byte[] salt = ReadExactly(source, SaltSize);
                byte[] verifier = ReadExactly(source, VerifierSize);
                byte[] key = DeriveKey(password, salt);
                byte[] expected = MakeVerifier(key, salt);
                return CryptographicOperations.FixedTimeEquals(verifier, expected);
            }
            finally
            {
                source.Position = pos;
            }
        }

        internal static void FillNonce(Span<byte> nonce, long counter)
        {
            nonce.Clear();
            BitConverter.TryWriteBytes(nonce[..8], counter);
        }

        internal static byte[] MakeAad(long counter, bool isFinal)
        {
            byte[] aad = new byte[9];
            BitConverter.TryWriteBytes(aad.AsSpan(0, 8), counter);
            aad[8] = isFinal ? (byte)1 : (byte)0;
            return aad;
        }

        private static byte[] DeriveKey(string password, byte[] salt) =>
            Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        private static byte[] MakeVerifier(byte[] key, byte[] salt)
        {
            Span<byte> input = stackalloc byte[KeySize + SaltSize + 4];
            key.CopyTo(input);
            salt.CopyTo(input[KeySize..]);
            input[^4] = (byte)'S'; input[^3] = (byte)'Z'; input[^2] = (byte)'v'; input[^1] = 1;
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(input, hash);
            return hash[..VerifierSize].ToArray();
        }

        private static byte[] ReadExactly(Stream s, int count)
        {
            byte[] buf = new byte[count];
            int read = 0;
            while (read < count)
            {
                int n = s.Read(buf, read, count - read);
                if (n == 0) throw new EndOfStreamException("Encrypted payload truncated.");
                read += n;
            }
            return buf;
        }

        private static bool TryReadExactly(Stream s, byte[] buf, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = s.Read(buf, read, count - read);
                if (n == 0)
                {
                    if (read == 0) return false;
                    throw new EndOfStreamException("Encrypted payload truncated.");
                }
                read += n;
            }
            return true;
        }

        private sealed class GcmWriteStream : Stream
        {
            private readonly Stream _out;
            private readonly AesGcm _gcm;
            private readonly byte[] _buffer = new byte[ChunkSize];
            private int _bufferLen;
            private long _counter;
            private bool _finished;

            public GcmWriteStream(Stream output, byte[] key)
            {
                _out = output;
                _gcm = new AesGcm(key, TagSize);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                while (count > 0)
                {
                    int space = ChunkSize - _bufferLen;
                    int take = Math.Min(space, count);
                    Buffer.BlockCopy(buffer, offset, _buffer, _bufferLen, take);
                    _bufferLen += take;
                    offset += take;
                    count -= take;
                    if (_bufferLen == ChunkSize) WriteChunk(isFinal: false);
                }
            }

            private void WriteChunk(bool isFinal)
            {
                Span<byte> nonce = stackalloc byte[NonceSize];
                FillNonce(nonce, _counter);
                byte[] aad = MakeAad(_counter, isFinal);

                Span<byte> tag = stackalloc byte[TagSize];
                byte[] cipher = new byte[_bufferLen];
                _gcm.Encrypt(nonce, _buffer.AsSpan(0, _bufferLen), cipher, tag, aad);

                _out.WriteByte(isFinal ? (byte)1 : (byte)0);
                Span<byte> lenBuf = stackalloc byte[4];
                BitConverter.TryWriteBytes(lenBuf, _bufferLen);
                _out.Write(lenBuf);
                _out.Write(nonce);
                _out.Write(tag);
                _out.Write(cipher, 0, _bufferLen);

                _bufferLen = 0;
                _counter++;
            }

            private void Finish()
            {
                if (_finished) return;
                _finished = true;
                WriteChunk(isFinal: true);
                _out.Flush();
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() => _out.Flush();
            public override int Read(byte[] b, int o, int c) => throw new NotSupportedException();
            public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
            public override void SetLength(long v) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Finish();
                    _gcm.Dispose();
                }
                base.Dispose(disposing);
            }
        }

        private sealed class GcmReadStream : Stream
        {
            private readonly Stream _in;
            private readonly AesGcm _gcm;
            private readonly byte[] _lenBuf = new byte[4];
            private readonly byte[] _nonce = new byte[NonceSize];
            private readonly byte[] _tag = new byte[TagSize];
            private byte[] _plain = new byte[ChunkSize];
            private int _plainLen;
            private int _plainPos;
            private long _counter;
            private bool _sawFinal;

            public GcmReadStream(Stream input, byte[] key)
            {
                _in = input;
                _gcm = new AesGcm(key, TagSize);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int total = 0;
                while (count > 0)
                {
                    if (_plainPos == _plainLen)
                    {
                        if (!FillNextChunk()) break;
                    }
                    int available = _plainLen - _plainPos;
                    int take = Math.Min(available, count);
                    Buffer.BlockCopy(_plain, _plainPos, buffer, offset, take);
                    _plainPos += take;
                    offset += take;
                    count -= take;
                    total += take;
                }
                return total;
            }

            private bool FillNextChunk()
            {
                if (_sawFinal) return false;

                int flagByte = _in.ReadByte();
                if (flagByte < 0)
                    throw new InvalidDataException("Missing termination block; encrypted payload truncated.");
                bool isFinal = flagByte != 0;

                if (!TryReadExactly(_in, _lenBuf, 4))
                    throw new EndOfStreamException("Encrypted payload truncated.");

                int cipherLen = BitConverter.ToInt32(_lenBuf, 0);
                if (cipherLen < 0 || cipherLen > ChunkSize)
                    throw new InvalidDataException("Corrupted encryption frame.");

                if (!TryReadExactly(_in, _nonce, NonceSize) || !TryReadExactly(_in, _tag, TagSize))
                    throw new EndOfStreamException("Encrypted payload truncated.");

                byte[] cipher = new byte[cipherLen];
                if (cipherLen > 0 && !TryReadExactly(_in, cipher, cipherLen))
                    throw new EndOfStreamException("Encrypted payload truncated.");

                byte[] aad = MakeAad(_counter, isFinal);
                if (_plain.Length < cipherLen) _plain = new byte[cipherLen];
                try
                {
                    _gcm.Decrypt(_nonce, cipher, _tag, _plain.AsSpan(0, cipherLen), aad);
                }
                catch (CryptographicException)
                {
                    throw new InvalidDataException("Encrypted payload modified or corrupted (authentication failed).");
                }

                _plainLen = cipherLen;
                _plainPos = 0;
                _counter++;
                if (isFinal) _sawFinal = true;
                return _plainLen > 0;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
            public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
            public override void SetLength(long v) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing) _gcm.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
