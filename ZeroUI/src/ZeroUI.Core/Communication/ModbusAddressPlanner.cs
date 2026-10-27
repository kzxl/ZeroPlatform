using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroUI.Core.Communication
{
    /// <summary>
    /// Represents a compiled, contiguous Modbus register read block.
    /// Replaces N individual tag requests with a single coalesced block read request.
    /// </summary>
    public sealed class ModbusReadBlock
    {
        /// <summary>
        /// Modbus function code (typically 0x03 for Holding Registers or 0x04 for Input Registers).
        /// </summary>
        public byte FunctionCode { get; }

        /// <summary>
        /// Starting register address (0-based or 1-based, matching field address format).
        /// </summary>
        public ushort StartAddress { get; }

        /// <summary>
        /// Number of 16-bit registers to read in this block.
        /// </summary>
        public ushort RegisterCount { get; }

        /// <summary>
        /// Tag mappings contained inside this block with their relative register offsets.
        /// </summary>
        public IReadOnlyList<ModbusBlockTagMapping> TagMappings { get; }

        public ModbusReadBlock(
            byte functionCode,
            ushort startAddress,
            ushort registerCount,
            IReadOnlyList<ModbusBlockTagMapping> tagMappings)
        {
            FunctionCode = functionCode;
            StartAddress = startAddress;
            RegisterCount = registerCount;
            TagMappings = tagMappings ?? throw new ArgumentNullException(nameof(tagMappings));
        }

        public override string ToString() =>
            $"FC{FunctionCode:X2} @ 0x{StartAddress:X4} ({StartAddress}) x {RegisterCount} regs ({TagMappings.Count} tags)";
    }

    /// <summary>
    /// Represents a tag definition mapped inside a coalesced Modbus read block.
    /// </summary>
    public sealed class ModbusBlockTagMapping
    {
        public AdapterTagDefinition TagDefinition { get; }

        /// <summary>
        /// Relative register offset from the block's StartAddress.
        /// Byte offset in response buffer data payload is RelativeRegisterOffset * 2.
        /// </summary>
        public ushort RelativeRegisterOffset { get; }

        /// <summary>
        /// Number of registers occupied by this tag's data type.
        /// </summary>
        public ushort RegisterCount { get; }

        public ModbusBlockTagMapping(AdapterTagDefinition tagDefinition, ushort relativeRegisterOffset, ushort registerCount)
        {
            TagDefinition = tagDefinition ?? throw new ArgumentNullException(nameof(tagDefinition));
            RelativeRegisterOffset = relativeRegisterOffset;
            RegisterCount = registerCount;
        }
    }

    /// <summary>
    /// Optimizer that clusters discrete tag definitions into optimal, contiguous Modbus register blocks.
    /// Solves the N+1 polling bottleneck by grouping proximate addresses into batch reads.
    /// </summary>
    public static class ModbusAddressPlanner
    {
        /// <summary>
        /// Maximum registers per standard Modbus FC03/FC04 request (Protocol max is 125).
        /// Default 120 leaves headroom for gateways.
        /// </summary>
        public const int DefaultMaxBlockRegisters = 120;

        /// <summary>
        /// Maximum allowed register gap (unused registers) between two tags before splitting into a separate request.
        /// Default 5 registers (10 bytes) avoids wasted bandwidth while preventing fragmented round trips.
        /// </summary>
        public const int DefaultMaxRegisterGap = 5;

        /// <summary>
        /// Calculates the number of 16-bit registers required for a given tag data type.
        /// </summary>
        public static ushort GetRegisterCount(TagDataType dataType)
        {
            switch (dataType)
            {
                case TagDataType.Double64:
                    return 4;
                case TagDataType.Float32:
                case TagDataType.Int32:
                case TagDataType.UInt32:
                    return 2;
                default:
                    return 1;
            }
        }

        /// <summary>
        /// Plans and compiles a collection of tag definitions into an optimal set of coalesced read blocks.
        /// </summary>
        /// <param name="tags">The registered tag definitions.</param>
        /// <param name="maxBlockRegisters">Maximum registers in a single block (max 125).</param>
        /// <param name="maxRegisterGap">Maximum allowed empty registers to bridge between tags.</param>
        /// <param name="defaultFunctionCode">Default Modbus function code (0x03 for Holding Registers).</param>
        /// <returns>Compiled list of read blocks.</returns>
        public static IReadOnlyList<ModbusReadBlock> PlanBlocks(
            IEnumerable<AdapterTagDefinition> tags,
            int maxBlockRegisters = DefaultMaxBlockRegisters,
            int maxRegisterGap = DefaultMaxRegisterGap,
            byte defaultFunctionCode = 0x03)
        {
            if (tags == null) return Array.Empty<ModbusReadBlock>();

            maxBlockRegisters = Math.Max(1, Math.Min(125, maxBlockRegisters));
            maxRegisterGap = Math.Max(0, maxRegisterGap);

            // 1. Extract valid tags with parsed ushort addresses
            var parsedList = new List<(AdapterTagDefinition Def, ushort StartAddr, ushort RegCount)>();
            foreach (var tag in tags)
            {
                if (ushort.TryParse(tag.FieldAddress, out ushort addr))
                {
                    ushort count = GetRegisterCount(tag.DataType);
                    parsedList.Add((tag, addr, count));
                }
            }

            if (parsedList.Count == 0)
            {
                return Array.Empty<ModbusReadBlock>();
            }

            // 2. Sort tags by starting address ascending
            parsedList.Sort((a, b) => a.StartAddr.CompareTo(b.StartAddr));

            var blocks = new List<ModbusReadBlock>();
            var currentMappings = new List<(AdapterTagDefinition Def, ushort StartAddr, ushort RegCount)>();

            ushort currentBlockStart = parsedList[0].StartAddr;
            ushort currentBlockEnd = (ushort)(parsedList[0].StartAddr + parsedList[0].RegCount);
            currentMappings.Add(parsedList[0]);

            for (int i = 1; i < parsedList.Count; i++)
            {
                var next = parsedList[i];
                ushort nextStart = next.StartAddr;
                ushort nextEnd = (ushort)(next.StartAddr + next.RegCount);

                int gap = nextStart > currentBlockEnd ? (nextStart - currentBlockEnd) : 0;
                int potentialTotalRegisters = Math.Max(currentBlockEnd, nextEnd) - currentBlockStart;

                // Check if we can coalesce this tag into the current block
                if (gap <= maxRegisterGap && potentialTotalRegisters <= maxBlockRegisters)
                {
                    currentMappings.Add(next);
                    if (nextEnd > currentBlockEnd)
                    {
                        currentBlockEnd = nextEnd;
                    }
                }
                else
                {
                    // Finalize current block
                    blocks.Add(BuildBlock(defaultFunctionCode, currentBlockStart, currentBlockEnd, currentMappings));

                    // Start new block
                    currentMappings.Clear();
                    currentBlockStart = nextStart;
                    currentBlockEnd = nextEnd;
                    currentMappings.Add(next);
                }
            }

            // Finalize remaining block
            if (currentMappings.Count > 0)
            {
                blocks.Add(BuildBlock(defaultFunctionCode, currentBlockStart, currentBlockEnd, currentMappings));
            }

            return blocks;
        }

        private static ModbusReadBlock BuildBlock(
            byte functionCode,
            ushort blockStart,
            ushort blockEnd,
            List<(AdapterTagDefinition Def, ushort StartAddr, ushort RegCount)> items)
        {
            ushort totalRegisters = (ushort)(blockEnd - blockStart);
            var mappings = new List<ModbusBlockTagMapping>(items.Count);

            foreach (var item in items)
            {
                ushort relOffset = (ushort)(item.StartAddr - blockStart);
                mappings.Add(new ModbusBlockTagMapping(item.Def, relOffset, item.RegCount));
            }

            return new ModbusReadBlock(functionCode, blockStart, totalRegisters, mappings);
        }
    }
}
