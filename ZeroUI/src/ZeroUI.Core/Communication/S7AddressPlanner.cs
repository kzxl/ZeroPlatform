using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Communication
{
    /// <summary>
    /// Represents a compiled, contiguous Siemens S7 Data Block (DB) read block.
    /// Coalesces multiple disjoint tag queries within the same DB into a single PDU read request.
    /// </summary>
    public sealed class S7ReadBlock
    {
        public ushort DbNumber { get; }
        public int StartByteOffset { get; }
        public int ByteCount { get; }
        public IReadOnlyList<S7BlockTagMapping> TagMappings { get; }

        public S7ReadBlock(
            ushort dbNumber,
            int startByteOffset,
            int byteCount,
            IReadOnlyList<S7BlockTagMapping> tagMappings)
        {
            DbNumber = dbNumber;
            StartByteOffset = startByteOffset;
            ByteCount = byteCount;
            TagMappings = tagMappings ?? throw new ArgumentNullException(nameof(tagMappings));
        }

        public override string ToString() =>
            $"DB{DbNumber}.DBB{StartByteOffset} x {ByteCount} bytes ({TagMappings.Count} tags)";
    }

    /// <summary>
    /// Represents a tag definition mapped inside a coalesced Siemens S7 read block.
    /// </summary>
    public sealed class S7BlockTagMapping
    {
        public AdapterTagDefinition TagDefinition { get; }
        public int RelativeByteOffset { get; }
        public int BitOffset { get; }
        public int ByteLength { get; }

        public S7BlockTagMapping(
            AdapterTagDefinition tagDefinition,
            int relativeByteOffset,
            int bitOffset,
            int byteLength)
        {
            TagDefinition = tagDefinition ?? throw new ArgumentNullException(nameof(tagDefinition));
            RelativeByteOffset = relativeByteOffset;
            BitOffset = bitOffset;
            ByteLength = byteLength;
        }
    }

    /// <summary>
    /// Industrial address optimizer that clusters Siemens S7 Data Block (DB) tags into contiguous PDU read requests.
    /// Solves the single-tag polling bottleneck by grouping proximate DB offsets into batch reads.
    /// </summary>
    public static class S7AddressPlanner
    {
        /// <summary>
        /// Maximum data payload bytes per S7 Read Var item.
        /// Default 222 bytes leaves comfortable headroom inside standard 240-byte and 480-byte S7 PDUs.
        /// </summary>
        public const int DefaultMaxBlockBytes = 222;

        /// <summary>
        /// Maximum allowed byte gap (unused bytes) between two tags before splitting into a separate request.
        /// Default 10 bytes prevents network packet fragmentation without wasting excessive bandwidth.
        /// </summary>
        public const int DefaultMaxByteGap = 10;

        private static readonly Regex _s7AddressRegex =
            new Regex(@"^DB(\d+)\.DB[A-Z]+(\d+)(?:\.(\d+))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Computes the payload byte count required for a given SCADA data type.
        /// </summary>
        public static int GetByteLength(TagDataType dataType)
        {
            switch (dataType)
            {
                case TagDataType.Double64:
                    return 8;
                case TagDataType.Float32:
                case TagDataType.Int32:
                case TagDataType.UInt32:
                    return 4;
                case TagDataType.Int16:
                case TagDataType.UInt16:
                    return 2;
                case TagDataType.Boolean:
                default:
                    return 1;
            }
        }

        /// <summary>
        /// Parses an S7 DB field address (e.g. DB1.DBD0, DB5.DBW10, DB2.DBX4.0).
        /// </summary>
        public static bool TryParseAddress(string address, out int dbNumber, out int byteOffset, out int bitOffset)
        {
            dbNumber = 0;
            byteOffset = 0;
            bitOffset = 0;

            if (string.IsNullOrWhiteSpace(address)) return false;

            var match = _s7AddressRegex.Match(address.Trim());
            if (!match.Success) return false;

            dbNumber = int.Parse(match.Groups[1].Value);
            byteOffset = int.Parse(match.Groups[2].Value);
            if (match.Groups[3].Success)
            {
                bitOffset = int.Parse(match.Groups[3].Value);
            }

            return true;
        }

        /// <summary>
        /// Compiles a set of discrete tag definitions into optimal, contiguous S7 read blocks grouped by DB number.
        /// </summary>
        public static IReadOnlyList<S7ReadBlock> PlanReadBlocks(
            IEnumerable<AdapterTagDefinition> tags,
            int maxBlockBytes = DefaultMaxBlockBytes,
            int maxByteGap = DefaultMaxByteGap)
        {
            if (tags == null) return Array.Empty<S7ReadBlock>();

            var parsedTags = new List<(AdapterTagDefinition Tag, int Db, int Offset, int Bit, int Length)>();

            foreach (var tag in tags)
            {
                if (TryParseAddress(tag.FieldAddress, out int db, out int offset, out int bit))
                {
                    int len = GetByteLength(tag.DataType);
                    parsedTags.Add((tag, db, offset, bit, len));
                }
            }

            if (parsedTags.Count == 0) return Array.Empty<S7ReadBlock>();

            var result = new List<S7ReadBlock>();

            // Group by DB Number
            var groups = parsedTags.GroupBy(t => t.Db).OrderBy(g => g.Key);

            foreach (var group in groups)
            {
                ushort dbNumber = (ushort)group.Key;
                // Sort by byte offset, then by bit offset
                var sorted = group.OrderBy(t => t.Offset).ThenBy(t => t.Bit).ToList();

                int blockStart = sorted[0].Offset;
                int blockEnd = sorted[0].Offset + sorted[0].Length;
                var currentMappings = new List<S7BlockTagMapping>
                {
                    new S7BlockTagMapping(sorted[0].Tag, relativeByteOffset: 0, sorted[0].Bit, sorted[0].Length)
                };

                for (int i = 1; i < sorted.Count; i++)
                {
                    var item = sorted[i];
                    int itemEnd = item.Offset + item.Length;
                    int newBlockEnd = Math.Max(blockEnd, itemEnd);
                    int prospectiveSpan = newBlockEnd - blockStart;
                    int gap = item.Offset - blockEnd;

                    if (gap <= maxByteGap && prospectiveSpan <= maxBlockBytes)
                    {
                        // Coalesce into current block
                        blockEnd = newBlockEnd;
                        currentMappings.Add(new S7BlockTagMapping(
                            item.Tag,
                            relativeByteOffset: item.Offset - blockStart,
                            item.Bit,
                            item.Length));
                    }
                    else
                    {
                        // Finalize current block and start a new one
                        result.Add(new S7ReadBlock(dbNumber, blockStart, blockEnd - blockStart, currentMappings));

                        blockStart = item.Offset;
                        blockEnd = itemEnd;
                        currentMappings = new List<S7BlockTagMapping>
                        {
                            new S7BlockTagMapping(item.Tag, relativeByteOffset: 0, item.Bit, item.Length)
                        };
                    }
                }

                // Finalize trailing block
                if (currentMappings.Count > 0)
                {
                    result.Add(new S7ReadBlock(dbNumber, blockStart, blockEnd - blockStart, currentMappings));
                }
            }

            return result;
        }
    }
}
