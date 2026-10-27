using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ZeroComm.Core.Abstractions
{
    /// <summary>
    /// High-performance compiled address parser for industrial PLC tags across Siemens S7,
    /// Mitsubishi MELSEC, Modbus TCP/RTU, Omron FINS, and Allen-Bradley CIP.
    /// </summary>
    public static class PlcAddressParser
    {
        #region Regular Expressions (Compiled)

        // Siemens S7 patterns
        private static readonly Regex S7DbRegex = new Regex(
            @"^DB(?<db>\d+)\.DB(?<type>[XBWD])(?<offset>\d+)(\.(?<bit>[0-7]))?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex S7MemoryRegex = new Regex(
            @"^(?<area>[IMQ])(?<type>[XBWD]?)(?<offset>\d+)(\.(?<bit>[0-7]))?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Mitsubishi MELSEC patterns
        private static readonly Regex MelsecRegex = new Regex(
            @"^(?<device>[DWRXMYLBF]|ZR)(?<offset>[0-9A-Fa-f]+)(\.(?<bit>[0-9A-Fa-f]+))?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Modbus Prefixed patterns: HR100, IR50, COIL1, DI10
        private static readonly Regex ModbusPrefixedRegex = new Regex(
            @"^(?<prefix>HR|IR|COIL|DI)(?<addr>\d+)(:(?<type>F|D|I|U|S|B))?(\.(?<bit>\d+))?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Omron FINS patterns: D100, CIO50, W10.01, H20, A400, E0_100
        private static readonly Regex OmronRegex = new Regex(
            @"^(?<area>CIO|WR?|HR?|AR?|D|E(?<bank>\d+)_)(?<offset>\d+)(\.(?<bit>\d+))?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion

        /// <summary>
        /// Parses an address string for the specified target PLC vendor.
        /// </summary>
        public static PlcAddressResolution Parse(string rawAddress, PlcVendor vendor)
        {
            if (string.IsNullOrWhiteSpace(rawAddress))
                return PlcAddressResolution.Failed(vendor, rawAddress, "Address string is null or empty.");

            string clean = rawAddress.Trim();

            return vendor switch
            {
                PlcVendor.SiemensS7 => ParseSiemens(clean),
                PlcVendor.MitsubishiMelsec => ParseMelsec(clean),
                PlcVendor.ModbusTcp or PlcVendor.ModbusRtu => ParseModbus(clean, vendor),
                PlcVendor.OmronFins => ParseOmron(clean),
                PlcVendor.AllenBradleyCip => ParseAllenBradley(clean),
                _ => PlcAddressResolution.Failed(vendor, clean, $"Unsupported PLC vendor: {vendor}")
            };
        }

        #region Siemens S7 Parser

        private static PlcAddressResolution ParseSiemens(string address)
        {
            // 1. Data Block (DB) syntax: DB1.DBD0, DB5.DBW10, DB2.DBX4.0, DB1.DBB10
            var dbMatch = S7DbRegex.Match(address);
            if (dbMatch.Success)
            {
                ushort dbNumber = ushort.Parse(dbMatch.Groups["db"].Value, CultureInfo.InvariantCulture);
                string type = dbMatch.Groups["type"].Value.ToUpperInvariant();
                int offset = int.Parse(dbMatch.Groups["offset"].Value, CultureInfo.InvariantCulture);
                bool hasBit = dbMatch.Groups["bit"].Success;
                byte bit = hasBit ? byte.Parse(dbMatch.Groups["bit"].Value, CultureInfo.InvariantCulture) : (byte)0;

                const byte s7DbArea = 0x84; // Siemens DB Area Code

                return type switch
                {
                    "X" => PlcAddressResolution.Success(PlcVendor.SiemensS7, address, s7DbArea, dbNumber, offset, bit, true, 1, TagDataType.Bool),
                    "B" => PlcAddressResolution.Success(PlcVendor.SiemensS7, address, s7DbArea, dbNumber, offset, 0, false, 1, TagDataType.Byte),
                    "W" => PlcAddressResolution.Success(PlcVendor.SiemensS7, address, s7DbArea, dbNumber, offset, 0, false, 2, TagDataType.Int16),
                    "D" => PlcAddressResolution.Success(PlcVendor.SiemensS7, address, s7DbArea, dbNumber, offset, 0, false, 4, TagDataType.Float),
                    _ => PlcAddressResolution.Failed(PlcVendor.SiemensS7, address, $"Unsupported S7 DB type: {type}")
                };
            }

            // 2. Memory areas: Inputs (I/0x81), Outputs (Q/0x82), Merkers (M/0x83)
            var memMatch = S7MemoryRegex.Match(address);
            if (memMatch.Success)
            {
                char areaChar = char.ToUpperInvariant(memMatch.Groups["area"].Value[0]);
                byte areaCode = areaChar switch
                {
                    'I' => (byte)0x81,
                    'Q' => (byte)0x82,
                    'M' => (byte)0x83,
                    _ => 0
                };

                string type = memMatch.Groups["type"].Value.ToUpperInvariant();
                int offset = int.Parse(memMatch.Groups["offset"].Value, CultureInfo.InvariantCulture);
                bool hasBit = memMatch.Groups["bit"].Success;
                byte bit = hasBit ? byte.Parse(memMatch.Groups["bit"].Value, CultureInfo.InvariantCulture) : (byte)0;

                if (hasBit || string.IsNullOrEmpty(type) || type == "X")
                {
                    return PlcAddressResolution.Success(PlcVendor.SiemensS7, address, areaCode, 0, offset, bit, true, 1, TagDataType.Bool);
                }

                return type switch
                {
                    "B" => PlcAddressResolution.Success(PlcVendor.SiemensS7, address, areaCode, 0, offset, 0, false, 1, TagDataType.Byte),
                    "W" => PlcAddressResolution.Success(PlcVendor.SiemensS7, address, areaCode, 0, offset, 0, false, 2, TagDataType.Int16),
                    "D" => PlcAddressResolution.Success(PlcVendor.SiemensS7, address, areaCode, 0, offset, 0, false, 4, TagDataType.Int32),
                    _ => PlcAddressResolution.Failed(PlcVendor.SiemensS7, address, $"Unsupported memory type: {type}")
                };
            }

            return PlcAddressResolution.Failed(PlcVendor.SiemensS7, address, "Invalid Siemens S7 address format.");
        }

        #endregion

        #region Mitsubishi MELSEC Parser

        private static PlcAddressResolution ParseMelsec(string address)
        {
            var match = MelsecRegex.Match(address);
            if (!match.Success)
                return PlcAddressResolution.Failed(PlcVendor.MitsubishiMelsec, address, "Invalid Mitsubishi MELSEC address format.");

            string device = match.Groups["device"].Value.ToUpperInvariant();
            string offsetStr = match.Groups["offset"].Value;
            bool hasBit = match.Groups["bit"].Success;
            byte bit = hasBit ? byte.Parse(match.Groups["bit"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) : (byte)0;

            // MC Protocol Device Codes & Radix (Hex for X, Y, B, W; Dec for D, M, L, R, ZR)
            byte deviceCode;
            int offset;
            bool isBit;
            TagDataType dataType;

            switch (device)
            {
                case "D":
                    deviceCode = 0xA8; // D register
                    offset = int.Parse(offsetStr, CultureInfo.InvariantCulture);
                    isBit = hasBit;
                    dataType = hasBit ? TagDataType.Bool : TagDataType.Int16;
                    break;
                case "W":
                    deviceCode = 0xB4; // Link register W (Hex)
                    offset = int.Parse(offsetStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    isBit = hasBit;
                    dataType = hasBit ? TagDataType.Bool : TagDataType.Int16;
                    break;
                case "R":
                    deviceCode = 0xAF; // File register R
                    offset = int.Parse(offsetStr, CultureInfo.InvariantCulture);
                    isBit = false;
                    dataType = TagDataType.Int16;
                    break;
                case "ZR":
                    deviceCode = 0xB0; // File register ZR
                    offset = int.Parse(offsetStr, CultureInfo.InvariantCulture);
                    isBit = false;
                    dataType = TagDataType.Int16;
                    break;
                case "M":
                    deviceCode = 0x90; // Internal relay M
                    offset = int.Parse(offsetStr, CultureInfo.InvariantCulture);
                    isBit = true;
                    dataType = TagDataType.Bool;
                    break;
                case "X":
                    deviceCode = 0x9C; // Input X (Hex)
                    offset = int.Parse(offsetStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    isBit = true;
                    dataType = TagDataType.Bool;
                    break;
                case "Y":
                    deviceCode = 0x9D; // Output Y (Hex)
                    offset = int.Parse(offsetStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    isBit = true;
                    dataType = TagDataType.Bool;
                    break;
                case "L":
                    deviceCode = 0x92; // Latch relay L
                    offset = int.Parse(offsetStr, CultureInfo.InvariantCulture);
                    isBit = true;
                    dataType = TagDataType.Bool;
                    break;
                case "B":
                    deviceCode = 0xA0; // Link relay B (Hex)
                    offset = int.Parse(offsetStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    isBit = true;
                    dataType = TagDataType.Bool;
                    break;
                default:
                    return PlcAddressResolution.Failed(PlcVendor.MitsubishiMelsec, address, $"Unknown MELSEC device: {device}");
            }

            int byteLength = isBit ? 1 : 2;
            return PlcAddressResolution.Success(PlcVendor.MitsubishiMelsec, address, deviceCode, 0, offset, bit, isBit, byteLength, dataType);
        }

        #endregion

        #region Modbus Parser

        private static PlcAddressResolution ParseModbus(string address, PlcVendor vendor)
        {
            // 1. Prefixed syntax: HR100, IR50, COIL1, DI10
            var prefMatch = ModbusPrefixedRegex.Match(address);
            if (prefMatch.Success)
            {
                string prefix = prefMatch.Groups["prefix"].Value.ToUpperInvariant();
                int addr = int.Parse(prefMatch.Groups["addr"].Value, CultureInfo.InvariantCulture);
                string typeSuffix = prefMatch.Groups["type"].Value.ToUpperInvariant();
                bool hasBit = prefMatch.Groups["bit"].Success;
                byte bit = hasBit ? byte.Parse(prefMatch.Groups["bit"].Value, CultureInfo.InvariantCulture) : (byte)0;

                byte fc = prefix switch
                {
                    "COIL" => 1,
                    "DI" => 2,
                    "IR" => 4,
                    _ => 3 // HR
                };

                TagDataType dt = ResolveModbusDataType(typeSuffix, fc, hasBit);
                int byteLen = dt switch
                {
                    TagDataType.Bool => 1,
                    TagDataType.Float or TagDataType.Int32 or TagDataType.UInt32 => 4,
                    TagDataType.Double or TagDataType.Int64 or TagDataType.UInt64 => 8,
                    _ => 2
                };

                return PlcAddressResolution.Success(vendor, address, fc, 0, addr, bit, hasBit || fc <= 2, byteLen, dt);
            }

            // 2. Modicon 5/6 digit numeric format (e.g. 40001, 40100:F, 30050, 00010, 10005)
            string baseAddr = address;
            string suffix = "";
            int colonIdx = address.IndexOf(':');
            if (colonIdx > 0)
            {
                baseAddr = address.Substring(0, colonIdx);
                suffix = address.Substring(colonIdx + 1).ToUpperInvariant();
            }

            int dotIdx = baseAddr.IndexOf('.');
            byte subBit = 0;
            bool isSubBit = false;
            if (dotIdx > 0)
            {
                isSubBit = byte.TryParse(baseAddr.Substring(dotIdx + 1), out subBit);
                baseAddr = baseAddr.Substring(0, dotIdx);
            }

            if (int.TryParse(baseAddr, out int modicon))
            {
                byte fc;
                int offset;

                if (modicon >= 400001) { fc = 3; offset = modicon - 400001; }
                else if (modicon >= 300001) { fc = 4; offset = modicon - 300001; }
                else if (modicon >= 100001) { fc = 2; offset = modicon - 100001; }
                else if (modicon >= 40001) { fc = 3; offset = modicon - 40001; }
                else if (modicon >= 30001) { fc = 4; offset = modicon - 30001; }
                else if (modicon >= 10001) { fc = 2; offset = modicon - 10001; }
                else { fc = 1; offset = Math.Max(0, modicon - 1); }

                TagDataType dt = ResolveModbusDataType(suffix, fc, isSubBit);
                int byteLen = dt switch
                {
                    TagDataType.Bool => 1,
                    TagDataType.Float or TagDataType.Int32 or TagDataType.UInt32 => 4,
                    TagDataType.Double or TagDataType.Int64 or TagDataType.UInt64 => 8,
                    _ => 2
                };

                return PlcAddressResolution.Success(vendor, address, fc, 0, offset, subBit, isSubBit || fc <= 2, byteLen, dt);
            }

            return PlcAddressResolution.Failed(vendor, address, "Invalid Modbus address format.");
        }

        private static TagDataType ResolveModbusDataType(string suffix, byte fc, bool isBit)
        {
            if (isBit || fc <= 2) return TagDataType.Bool;

            return suffix switch
            {
                "F" => TagDataType.Float,
                "D" => TagDataType.Double,
                "I" => TagDataType.Int32,
                "U" => TagDataType.UInt32,
                "S" => TagDataType.Int16,
                "B" => TagDataType.Byte,
                _ => TagDataType.UInt16
            };
        }

        #endregion

        #region Omron FINS Parser

        private static PlcAddressResolution ParseOmron(string address)
        {
            var match = OmronRegex.Match(address);
            if (!match.Success)
                return PlcAddressResolution.Failed(PlcVendor.OmronFins, address, "Invalid Omron FINS address format.");

            string areaStr = match.Groups["area"].Value.ToUpperInvariant();
            int offset = int.Parse(match.Groups["offset"].Value, CultureInfo.InvariantCulture);
            bool hasBit = match.Groups["bit"].Success;
            byte bit = hasBit ? byte.Parse(match.Groups["bit"].Value, CultureInfo.InvariantCulture) : (byte)0;

            byte areaCode;
            bool isBit = hasBit;

            if (areaStr.StartsWith("D", StringComparison.OrdinalIgnoreCase))
            {
                areaCode = (byte)(isBit ? 0x02 : 0x82); // DM Area
            }
            else if (areaStr.StartsWith("CIO", StringComparison.OrdinalIgnoreCase))
            {
                areaCode = (byte)(isBit ? 0x30 : 0xB0); // CIO Area
            }
            else if (areaStr.StartsWith("W", StringComparison.OrdinalIgnoreCase))
            {
                areaCode = (byte)(isBit ? 0x31 : 0xB1); // Work Area
            }
            else if (areaStr.StartsWith("H", StringComparison.OrdinalIgnoreCase))
            {
                areaCode = (byte)(isBit ? 0x32 : 0xB2); // Holding Area
            }
            else if (areaStr.StartsWith("A", StringComparison.OrdinalIgnoreCase))
            {
                areaCode = (byte)(isBit ? 0x33 : 0xB3); // Auxiliary Area
            }
            else if (areaStr.StartsWith("E", StringComparison.OrdinalIgnoreCase))
            {
                byte bank = byte.Parse(match.Groups["bank"].Value, CultureInfo.InvariantCulture);
                areaCode = (byte)(isBit ? (0x20 + bank) : (0x50 + bank)); // EM Bank
            }
            else
            {
                return PlcAddressResolution.Failed(PlcVendor.OmronFins, address, $"Unsupported Omron memory area: {areaStr}");
            }

            TagDataType dt = isBit ? TagDataType.Bool : TagDataType.UInt16;
            int byteLen = isBit ? 1 : 2;

            return PlcAddressResolution.Success(PlcVendor.OmronFins, address, areaCode, 0, offset, bit, isBit, byteLen, dt);
        }

        #endregion

        #region Allen-Bradley CIP Parser

        private static PlcAddressResolution ParseAllenBradley(string address)
        {
            // CIP utilizes symbolic paths (e.g. "Motor.Speed", "Tank1_Temp", "StatusWord.3", "Recipes[5]")
            if (address.Length > 255)
                return PlcAddressResolution.Failed(PlcVendor.AllenBradleyCip, address, "CIP tag name exceeds 255 characters.");

            return PlcAddressResolution.Success(
                PlcVendor.AllenBradleyCip,
                address,
                areaCode: 0,
                dbNumber: 0,
                offset: 0,
                bitOffset: 0,
                isBitAccess: address.Contains("."),
                elementCount: 1,
                dataType: TagDataType.Float,
                symbolicPath: address);
        }

        #endregion
    }
}
