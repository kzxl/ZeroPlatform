using System;
using ZeroSecurity.Common;

namespace ZeroSecurity.Hashing;

/// <summary>
/// Pure C# high-speed implementation of the BLAKE3 cryptographic hash function (256-bit).
/// Features tree hashing with zero heap allocation on single-chunk inputs (&lt;= 1024 bytes).
/// </summary>
public static class Blake3
{
    public const int HashSizeInBytes = 32;
    public const int ChunkSize = 1024;
    public const int BlockSize = 64;

    private const uint CHUNK_START = 1 << 0;
    private const uint CHUNK_END = 1 << 1;
    private const uint PARENT = 1 << 2;
    private const uint ROOT = 1 << 3;

    private static readonly uint[] IV =
    {
        0x6A09E667, 0xBB67AE85, 0x3C6EF372, 0xA54FF53A,
        0x510E527F, 0x9B05688C, 0x1F83D9AB, 0x5BE0CD19
    };

    private static readonly byte[] MSG_SCHEDULE =
    {
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
        2, 6, 3, 10, 7, 0, 4, 13, 1, 11, 12, 5, 9, 14, 15, 8,
        3, 4, 10, 12, 13, 2, 7, 14, 6, 5, 9, 0, 11, 15, 8, 1,
        10, 7, 12, 9, 14, 3, 13, 15, 4, 0, 11, 2, 5, 8, 1, 6,
        12, 13, 9, 11, 15, 10, 14, 8, 7, 2, 5, 3, 0, 1, 6, 4,
        9, 14, 11, 5, 8, 12, 15, 1, 13, 3, 0, 10, 2, 6, 4, 7,
        11, 15, 5, 0, 1, 9, 8, 6, 14, 10, 2, 12, 3, 4, 7, 13
    };

    /// <summary>
    /// Computes the 256-bit BLAKE3 hash of the input span.
    /// </summary>
    public static void Hash(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (output.Length < HashSizeInBytes)
            throw new ArgumentException("Output span must be at least 32 bytes.", nameof(output));

        if (input.Length <= ChunkSize)
        {
            // Single chunk mode (covers empty inputs and up to 1024 bytes)
            Span<uint> cv = stackalloc uint[8];
            IV.CopyTo(cv);

            HashChunk(input, cv, 0, CHUNK_START | CHUNK_END | ROOT, output);
            return;
        }

        // Multi-chunk tree mode
        Span<uint> cvStack = stackalloc uint[54 * 8]; // up to 54 levels deep
        int cvStackLen = 0;

        Span<uint> chunkCv = stackalloc uint[8];
        Span<byte> chunkOut = stackalloc byte[32];
        Span<uint> rightCv = stackalloc uint[8];
        Span<uint> parentCv = stackalloc uint[8];
        Span<byte> parentBlock = stackalloc byte[64];
        Span<uint> nextCv = stackalloc uint[8];

        ulong chunkCounter = 0;
        int offset = 0;

        while (offset < input.Length)
        {
            int take = Math.Min(ChunkSize, input.Length - offset);
            ReadOnlySpan<byte> chunkData = input.Slice(offset, take);
            offset += take;

            bool isLastChunk = (offset == input.Length);
            uint flags = CHUNK_START | CHUNK_END;
            if (isLastChunk && chunkCounter == 0) flags |= ROOT;

            IV.CopyTo(chunkCv);

            if (isLastChunk && cvStackLen == 0)
            {
                HashChunk(chunkData, chunkCv, chunkCounter, flags, output);
                return;
            }

            HashChunk(chunkData, chunkCv, chunkCounter, flags, chunkOut);
            BytesToWords(chunkOut, rightCv);

            // Merge with parents in stack
            ulong count = chunkCounter;
            while ((count & 1) != 0 && cvStackLen > 0)
            {
                cvStackLen--;
                ReadOnlySpan<uint> leftCv = cvStack.Slice(cvStackLen * 8, 8);

                IV.CopyTo(parentCv);
                WordsToBytes(leftCv, parentBlock.Slice(0, 32));
                WordsToBytes(rightCv, parentBlock.Slice(32, 32));

                Compress(parentCv, parentBlock, 0, 64, PARENT, rightCv);
                count >>= 1;
            }

            rightCv.CopyTo(cvStack.Slice(cvStackLen * 8, 8));
            cvStackLen++;
            chunkCounter++;
        }

        // Final tree reduction
        while (cvStackLen > 1)
        {
            cvStackLen--;
            ReadOnlySpan<uint> rCv = cvStack.Slice(cvStackLen * 8, 8);
            cvStackLen--;
            ReadOnlySpan<uint> lCv = cvStack.Slice(cvStackLen * 8, 8);

            IV.CopyTo(parentCv);
            WordsToBytes(lCv, parentBlock.Slice(0, 32));
            WordsToBytes(rCv, parentBlock.Slice(32, 32));

            uint flags = PARENT | (cvStackLen == 0 ? ROOT : 0);
            Compress(parentCv, parentBlock, 0, 64, flags, nextCv);

            if (cvStackLen == 0)
            {
                WordsToBytes(nextCv, output.Slice(0, 32));
                return;
            }

            nextCv.CopyTo(cvStack.Slice(cvStackLen * 8, 8));
            cvStackLen++;
        }

        WordsToBytes(cvStack.Slice(0, 8), output.Slice(0, 32));
    }

    /// <summary>
    /// Computes the BLAKE3 hash and formats as a 64-character lowercase hex string.
    /// </summary>
    public static string HashHex(ReadOnlySpan<byte> input)
    {
        Span<byte> output = stackalloc byte[32];
        Hash(input, output);
        return FastHex.ToHex(output);
    }

    private static void HashChunk(ReadOnlySpan<byte> chunk, Span<uint> cv, ulong chunkIndex, uint flags, Span<byte> output)
    {
        Span<byte> block = stackalloc byte[BlockSize];
        int offset = 0;

        while (offset + BlockSize < chunk.Length)
        {
            uint f = flags & (CHUNK_START);
            Compress(cv, chunk.Slice(offset, BlockSize), chunkIndex, BlockSize, f, cv);
            offset += BlockSize;
            flags &= ~CHUNK_START;
        }

        // Last block in chunk
        int rem = chunk.Length - offset;
        block.Clear();
        if (rem > 0)
        {
            chunk.Slice(offset, rem).CopyTo(block);
        }

        uint finalFlags = flags | CHUNK_END;
        Span<uint> outWords = stackalloc uint[8];
        Compress(cv, block, chunkIndex, (uint)rem, finalFlags, outWords);
        WordsToBytes(outWords, output.Slice(0, 32));
    }

    private static void Compress(ReadOnlySpan<uint> cv, ReadOnlySpan<byte> block, ulong counter, uint byteCount, uint flags, Span<uint> outWords)
    {
        Span<uint> m = stackalloc uint[16];
        for (int i = 0; i < 16; i++)
        {
            int idx = i * 4;
            m[i] = (uint)block[idx] | ((uint)block[idx + 1] << 8) | ((uint)block[idx + 2] << 16) | ((uint)block[idx + 3] << 24);
        }

        Span<uint> v = stackalloc uint[16];
        for (int i = 0; i < 8; i++) v[i] = cv[i];
        v[8] = IV[0];
        v[9] = IV[1];
        v[10] = IV[2];
        v[11] = IV[3];
        v[12] = (uint)counter;
        v[13] = (uint)(counter >> 32);
        v[14] = byteCount;
        v[15] = flags;

        for (int round = 0; round < 7; round++)
        {
            int schedOffset = round * 16;
            G(ref v[0], ref v[4], ref v[8],  ref v[12], m[MSG_SCHEDULE[schedOffset + 0]], m[MSG_SCHEDULE[schedOffset + 1]]);
            G(ref v[1], ref v[5], ref v[9],  ref v[13], m[MSG_SCHEDULE[schedOffset + 2]], m[MSG_SCHEDULE[schedOffset + 3]]);
            G(ref v[2], ref v[6], ref v[10], ref v[14], m[MSG_SCHEDULE[schedOffset + 4]], m[MSG_SCHEDULE[schedOffset + 5]]);
            G(ref v[3], ref v[7], ref v[11], ref v[15], m[MSG_SCHEDULE[schedOffset + 6]], m[MSG_SCHEDULE[schedOffset + 7]]);

            G(ref v[0], ref v[5], ref v[10], ref v[15], m[MSG_SCHEDULE[schedOffset + 8]], m[MSG_SCHEDULE[schedOffset + 9]]);
            G(ref v[1], ref v[6], ref v[11], ref v[12], m[MSG_SCHEDULE[schedOffset + 10]], m[MSG_SCHEDULE[schedOffset + 11]]);
            G(ref v[2], ref v[7], ref v[8],  ref v[13], m[MSG_SCHEDULE[schedOffset + 12]], m[MSG_SCHEDULE[schedOffset + 13]]);
            G(ref v[3], ref v[4], ref v[9],  ref v[14], m[MSG_SCHEDULE[schedOffset + 14]], m[MSG_SCHEDULE[schedOffset + 15]]);
        }

        for (int i = 0; i < 8; i++)
        {
            outWords[i] = v[i] ^ v[i + 8];
        }
    }

    private static void G(ref uint a, ref uint b, ref uint c, ref uint d, uint mx, uint my)
    {
        a = a + b + mx;
        d = RotateRight(d ^ a, 16);
        c = c + d;
        b = RotateRight(b ^ c, 12);
        a = a + b + my;
        d = RotateRight(d ^ a, 8);
        c = c + d;
        b = RotateRight(b ^ c, 7);
    }

    private static uint RotateRight(uint val, int bits) => (val >> bits) | (val << (32 - bits));

    private static void WordsToBytes(ReadOnlySpan<uint> words, Span<byte> bytes)
    {
        for (int i = 0; i < words.Length; i++)
        {
            uint w = words[i];
            int idx = i * 4;
            bytes[idx] = (byte)w;
            bytes[idx + 1] = (byte)(w >> 8);
            bytes[idx + 2] = (byte)(w >> 16);
            bytes[idx + 3] = (byte)(w >> 24);
        }
    }

    private static void BytesToWords(ReadOnlySpan<byte> bytes, Span<uint> words)
    {
        for (int i = 0; i < words.Length; i++)
        {
            int idx = i * 4;
            words[i] = (uint)bytes[idx] | ((uint)bytes[idx + 1] << 8) | ((uint)bytes[idx + 2] << 16) | ((uint)bytes[idx + 3] << 24);
        }
    }
}
