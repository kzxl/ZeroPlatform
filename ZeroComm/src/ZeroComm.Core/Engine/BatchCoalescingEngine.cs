using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using ZeroComm.Core.Abstractions;

namespace ZeroComm.Core.Engine
{
    /// <summary>
    /// Describes an individual tag's mapped slice within a coalesced contiguous block.
    /// </summary>
    public readonly struct TagSliceMapping
    {
        public int TagId { get; }
        public int RelativeOffsetInBlock { get; }
        public int ByteLength { get; }
        public TagDataType DataType { get; }
        public byte BitOffset { get; }
        public bool IsBitAccess { get; }

        public TagSliceMapping(
            int tagId,
            int relativeOffsetInBlock,
            int byteLength,
            TagDataType dataType,
            byte bitOffset,
            bool isBitAccess)
        {
            TagId = tagId;
            RelativeOffsetInBlock = relativeOffsetInBlock;
            ByteLength = byteLength;
            DataType = dataType;
            BitOffset = bitOffset;
            IsBitAccess = isBitAccess;
        }
    }

    /// <summary>
    /// Represents a coalesced contiguous memory block combining multiple adjacent tag read requests.
    /// </summary>
    public sealed class CoalescedMemoryBlock
    {
        public PlcVendor Vendor { get; }
        public byte AreaCode { get; }
        public ushort DbNumber { get; }
        public int StartOffset { get; }
        public int ByteLength { get; }
        public string? SymbolicTag { get; }
        public IReadOnlyList<TagSliceMapping> Mappings { get; }

        public CoalescedMemoryBlock(
            PlcVendor vendor,
            byte areaCode,
            ushort dbNumber,
            int startOffset,
            int byteLength,
            IReadOnlyList<TagSliceMapping> mappings,
            string? symbolicTag = null)
        {
            Vendor = vendor;
            AreaCode = areaCode;
            DbNumber = dbNumber;
            StartOffset = startOffset;
            ByteLength = byteLength;
            Mappings = mappings;
            SymbolicTag = symbolicTag;
        }

        /// <summary>
        /// Unpacks raw block bytes into decoded <see cref="PlcTagValue"/> samples with zero heap allocation.
        /// </summary>
        public int Unpack(ReadOnlySpan<byte> blockData, Span<PlcTagValue> destination, long timestampUtcMs)
        {
            int written = 0;
            int count = Math.Min(Mappings.Count, destination.Length);

            for (int i = 0; i < count; i++)
            {
                var mapping = Mappings[i];
                int offset = mapping.RelativeOffsetInBlock;

                if (offset < 0 || offset + mapping.ByteLength > blockData.Length)
                {
                    // Tag slice exceeds received block data bounds
                    destination[written++] = new PlcTagValue(
                        mapping.TagId, 0.0, false, ScadaQuality.DeviceFailure, timestampUtcMs);
                    continue;
                }

                ReadOnlySpan<byte> slice = blockData.Slice(offset, mapping.ByteLength);
                double numVal = 0.0;
                bool boolVal = false;

                if (mapping.IsBitAccess)
                {
                    byte b = slice[0];
                    boolVal = (b & (1 << mapping.BitOffset)) != 0;
                    numVal = boolVal ? 1.0 : 0.0;
                }
                else
                {
                    switch (mapping.DataType)
                    {
                        case TagDataType.Bool:
                            boolVal = slice[0] != 0;
                            numVal = boolVal ? 1.0 : 0.0;
                            break;

                        case TagDataType.Byte:
                            numVal = slice[0];
                            break;

                        case TagDataType.Int16:
                            numVal = (Vendor == PlcVendor.SiemensS7 || Vendor == PlcVendor.OmronFins)
                                ? BinaryPrimitives.ReadInt16BigEndian(slice)
                                : BinaryPrimitives.ReadInt16LittleEndian(slice);
                            break;

                        case TagDataType.UInt16:
                            numVal = (Vendor == PlcVendor.SiemensS7 || Vendor == PlcVendor.OmronFins)
                                ? BinaryPrimitives.ReadUInt16BigEndian(slice)
                                : BinaryPrimitives.ReadUInt16LittleEndian(slice);
                            break;

                        case TagDataType.Int32:
                            numVal = (Vendor == PlcVendor.SiemensS7 || Vendor == PlcVendor.OmronFins)
                                ? BinaryPrimitives.ReadInt32BigEndian(slice)
                                : BinaryPrimitives.ReadInt32LittleEndian(slice);
                            break;

                        case TagDataType.UInt32:
                            numVal = (Vendor == PlcVendor.SiemensS7 || Vendor == PlcVendor.OmronFins)
                                ? BinaryPrimitives.ReadUInt32BigEndian(slice)
                                : BinaryPrimitives.ReadUInt32LittleEndian(slice);
                            break;

                        case TagDataType.Int64:
                            numVal = (Vendor == PlcVendor.SiemensS7 || Vendor == PlcVendor.OmronFins)
                                ? BinaryPrimitives.ReadInt64BigEndian(slice)
                                : BinaryPrimitives.ReadInt64LittleEndian(slice);
                            break;

                        case TagDataType.UInt64:
                            numVal = (Vendor == PlcVendor.SiemensS7 || Vendor == PlcVendor.OmronFins)
                                ? BinaryPrimitives.ReadUInt64BigEndian(slice)
                                : BinaryPrimitives.ReadUInt64LittleEndian(slice);
                            break;

                        case TagDataType.Float:
#if NET8_0_OR_GREATER
                            numVal = (Vendor == PlcVendor.SiemensS7 || Vendor == PlcVendor.OmronFins)
                                ? BinaryPrimitives.ReadSingleBigEndian(slice)
                                : BinaryPrimitives.ReadSingleLittleEndian(slice);
#else
                            int intVal = (Vendor == PlcVendor.SiemensS7 || Vendor == PlcVendor.OmronFins)
                                ? BinaryPrimitives.ReadInt32BigEndian(slice)
                                : BinaryPrimitives.ReadInt32LittleEndian(slice);
                            numVal = BitConverter.ToSingle(BitConverter.GetBytes(intVal), 0);
#endif
                            break;

                        case TagDataType.Double:
#if NET8_0_OR_GREATER
                            numVal = (Vendor == PlcVendor.SiemensS7 || Vendor == PlcVendor.OmronFins)
                                ? BinaryPrimitives.ReadDoubleBigEndian(slice)
                                : BinaryPrimitives.ReadDoubleLittleEndian(slice);
#else
                            long longVal = (Vendor == PlcVendor.SiemensS7 || Vendor == PlcVendor.OmronFins)
                                ? BinaryPrimitives.ReadInt64BigEndian(slice)
                                : BinaryPrimitives.ReadInt64LittleEndian(slice);
                            numVal = BitConverter.ToDouble(BitConverter.GetBytes(longVal), 0);
#endif
                            break;

                        default:
                            numVal = slice[0];
                            break;
                    }
                }

                destination[written++] = new PlcTagValue(
                    mapping.TagId, numVal, boolVal, ScadaQuality.Good, timestampUtcMs);
            }

            return written;
        }
    }

    /// <summary>
    /// Intelligent tag coalescing engine that merges hundreds of discrete tag addresses
    /// into optimal contiguous block read requests to minimize round-trip network latency.
    /// </summary>
    public static class BatchCoalescingEngine
    {
        /// <summary>
        /// Analyzes a set of registered tag read requests and coalesces them into optimal contiguous blocks.
        /// </summary>
        /// <param name="requests">List of registered tag read requests.</param>
        /// <param name="maxContiguousBytes">Maximum allowed contiguous block size supported by the PLC driver.</param>
        /// <param name="maxAllowedGapBytes">Maximum address gap to bridge into a single block (default: 10 bytes).</param>
        public static IReadOnlyList<CoalescedMemoryBlock> CoalesceRequests(
            IReadOnlyList<PlcBatchReadRequest> requests,
            int maxContiguousBytes = 500,
            int maxAllowedGapBytes = 10)
        {
            if (requests == null || requests.Count == 0)
                return Array.Empty<CoalescedMemoryBlock>();

            var blocks = new List<CoalescedMemoryBlock>();

            // Group requests by memory partition key (Vendor, AreaCode, DbNumber, SymbolicPath)
            var groups = requests
                .Where(r => r.Address.IsValid)
                .GroupBy(r => (
                    r.Address.Vendor,
                    r.Address.AreaCode,
                    r.Address.DbNumber,
                    Symbolic: r.Address.SymbolicPath ?? string.Empty
                ));

            foreach (var group in groups)
            {
                var key = group.Key;

                // If symbolic (e.g. Allen-Bradley tag names), cannot easily coalesce by numeric offset: keep individual
                if (!string.IsNullOrEmpty(key.Symbolic))
                {
                    foreach (var req in group)
                    {
                        var mapping = new TagSliceMapping(
                            req.TagId, 0, req.ByteLength, req.Address.DataType, req.Address.BitOffset, req.Address.IsBitAccess);

                        blocks.Add(new CoalescedMemoryBlock(
                            key.Vendor, key.AreaCode, key.DbNumber, 0, req.ByteLength, new[] { mapping }, key.Symbolic));
                    }
                    continue;
                }

                // Sort items by numeric offset ascending
                var sorted = group.OrderBy(r => r.Address.Offset).ToList();

                int currentBlockStart = -1;
                int currentBlockEnd = -1;
                var currentMappings = new List<TagSliceMapping>();

                foreach (var req in sorted)
                {
                    int tagStart = req.Address.Offset;
                    int tagLen = Math.Max(1, req.ByteLength);
                    int tagEnd = tagStart + tagLen;

                    if (currentBlockStart < 0)
                    {
                        // Initialize new block
                        currentBlockStart = tagStart;
                        currentBlockEnd = tagEnd;
                        currentMappings.Add(new TagSliceMapping(
                            req.TagId, 0, tagLen, req.Address.DataType, req.Address.BitOffset, req.Address.IsBitAccess));
                    }
                    else
                    {
                        // Check if tag fits into current contiguous block with gap tolerance
                        int potentialBlockEnd = Math.Max(currentBlockEnd, tagEnd);
                        int potentialLength = potentialBlockEnd - currentBlockStart;
                        int gap = tagStart - currentBlockEnd;

                        if (gap <= maxAllowedGapBytes && potentialLength <= maxContiguousBytes)
                        {
                            // Merge into current block
                            currentBlockEnd = potentialBlockEnd;
                            int relOffset = tagStart - currentBlockStart;
                            currentMappings.Add(new TagSliceMapping(
                                req.TagId, relOffset, tagLen, req.Address.DataType, req.Address.BitOffset, req.Address.IsBitAccess));
                        }
                        else
                        {
                            // Close current block and start a new one
                            int blockLen = currentBlockEnd - currentBlockStart;
                            blocks.Add(new CoalescedMemoryBlock(
                                key.Vendor, key.AreaCode, key.DbNumber, currentBlockStart, blockLen, currentMappings.ToArray()));

                            currentBlockStart = tagStart;
                            currentBlockEnd = tagEnd;
                            currentMappings = new List<TagSliceMapping>
                            {
                                new TagSliceMapping(req.TagId, 0, tagLen, req.Address.DataType, req.Address.BitOffset, req.Address.IsBitAccess)
                            };
                        }
                    }
                }

                if (currentBlockStart >= 0 && currentMappings.Count > 0)
                {
                    int blockLen = currentBlockEnd - currentBlockStart;
                    blocks.Add(new CoalescedMemoryBlock(
                        key.Vendor, key.AreaCode, key.DbNumber, currentBlockStart, blockLen, currentMappings.ToArray()));
                }
            }

            return blocks;
        }
    }
}
