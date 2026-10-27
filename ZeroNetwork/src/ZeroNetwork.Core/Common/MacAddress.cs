using System;
using System.Net.NetworkInformation;
using System.Text;

namespace ZeroNetwork.Common
{
    /// <summary>
    /// Represents an allocation-free 6-byte IEEE 802 Media Access Control (MAC) hardware address.
    /// Provides fast parsing, span formatting, and IEEE OUI (Organizationally Unique Identifier) vendor identification.
    /// </summary>
    public readonly struct MacAddress : IEquatable<MacAddress>, IComparable<MacAddress>
    {
        private readonly byte _b0;
        private readonly byte _b1;
        private readonly byte _b2;
        private readonly byte _b3;
        private readonly byte _b4;
        private readonly byte _b5;
        private readonly bool _initialized;

        /// <summary>
        /// Gets an empty MAC address (00:00:00:00:00:00).
        /// </summary>
        public static readonly MacAddress Empty = new MacAddress(0, 0, 0, 0, 0, 0);

        /// <summary>
        /// Gets a broadcast MAC address (FF:FF:FF:FF:FF:FF).
        /// </summary>
        public static readonly MacAddress Broadcast = new MacAddress(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);

        public byte B0 => _b0;
        public byte B1 => _b1;
        public byte B2 => _b2;
        public byte B3 => _b3;
        public byte B4 => _b4;
        public byte B5 => _b5;
        public bool IsEmpty => !_initialized || (_b0 == 0 && _b1 == 0 && _b2 == 0 && _b3 == 0 && _b4 == 0 && _b5 == 0);
        public bool IsBroadcast => _b0 == 0xFF && _b1 == 0xFF && _b2 == 0xFF && _b3 == 0xFF && _b4 == 0xFF && _b5 == 0xFF;

        public MacAddress(byte b0, byte b1, byte b2, byte b3, byte b4, byte b5)
        {
            _b0 = b0;
            _b1 = b1;
            _b2 = b2;
            _b3 = b3;
            _b4 = b4;
            _b5 = b5;
            _initialized = true;
        }

        public MacAddress(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 6)
            {
                _b0 = _b1 = _b2 = _b3 = _b4 = _b5 = 0;
                _initialized = false;
                return;
            }

            _b0 = bytes[0];
            _b1 = bytes[1];
            _b2 = bytes[2];
            _b3 = bytes[3];
            _b4 = bytes[4];
            _b5 = bytes[5];
            _initialized = true;
        }

        public MacAddress(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 6)
            {
                _b0 = _b1 = _b2 = _b3 = _b4 = _b5 = 0;
                _initialized = false;
                return;
            }

            _b0 = bytes[0];
            _b1 = bytes[1];
            _b2 = bytes[2];
            _b3 = bytes[3];
            _b4 = bytes[4];
            _b5 = bytes[5];
            _initialized = true;
        }

        /// <summary>
        /// Retrieves the 24-bit OUI (Organizationally Unique Identifier) prefix as an integer.
        /// </summary>
        public int GetOuiPrefix() => (_b0 << 16) | (_b1 << 8) | _b2;

        /// <summary>
        /// Checks whether the MAC address belongs to a known virtual hypervisor or container vendor.
        /// </summary>
        public bool IsKnownVirtualVendor(out string vendorName)
        {
            int oui = GetOuiPrefix();
            switch (oui)
            {
                case 0x000569: // VMware
                case 0x000C29: // VMware
                case 0x005056: // VMware
                    vendorName = "VMware";
                    return true;
                case 0x00155D: // Microsoft Hyper-V
                    vendorName = "Hyper-V";
                    return true;
                case 0x080027: // Oracle VirtualBox
                    vendorName = "VirtualBox";
                    return true;
                case 0x525400: // QEMU / KVM
                    vendorName = "QEMU/KVM";
                    return true;
                case 0x00163E: // Xen
                    vendorName = "Xen";
                    return true;
                case 0x0242AC: // Docker Bridge
                    vendorName = "Docker";
                    return true;
                case 0x001C42: // Parallels
                    vendorName = "Parallels";
                    return true;
                default:
                    vendorName = string.Empty;
                    return false;
            }
        }

        /// <summary>
        /// Copies the 6 MAC address bytes into a new byte array.
        /// </summary>
        public byte[] GetAddressBytes()
        {
            return new byte[] { _b0, _b1, _b2, _b3, _b4, _b5 };
        }

        /// <summary>
        /// Copies the 6 MAC address bytes into a destination span.
        /// </summary>
        public void CopyTo(Span<byte> destination)
        {
            if (destination.Length < 6)
                throw new ArgumentException("Destination span must be at least 6 bytes.", nameof(destination));

            destination[0] = _b0;
            destination[1] = _b1;
            destination[2] = _b2;
            destination[3] = _b3;
            destination[4] = _b4;
            destination[5] = _b5;
        }

        /// <summary>
        /// Converts to BCL <see cref="PhysicalAddress"/>.
        /// </summary>
        public PhysicalAddress ToPhysicalAddress()
        {
            return new PhysicalAddress(GetAddressBytes());
        }

        /// <summary>
        /// Parses a MAC address string (supporting delimiters ':', '-', or continuous hex).
        /// </summary>
        public static MacAddress Parse(string text)
        {
            if (TryParse(text, out var mac))
            {
                return mac;
            }
            throw new FormatException($"Invalid MAC address format: '{text}'");
        }

        /// <summary>
        /// Tries to parse a MAC address string.
        /// </summary>
        public static bool TryParse(string? text, out MacAddress mac)
        {
            mac = Empty;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            return TryParse(text.AsSpan(), out mac);
        }

        /// <summary>
        /// Tries to parse a MAC address from a character span without string allocation.
        /// </summary>
        public static bool TryParse(ReadOnlySpan<char> span, out MacAddress mac)
        {
            mac = Empty;
            Span<byte> bytes = stackalloc byte[6];
            int byteIndex = 0;
            int currentVal = 0;
            int hexDigitCount = 0;

            for (int i = 0; i < span.Length; i++)
            {
                char c = span[i];
                if (c == '-' || c == ':')
                {
                    if (hexDigitCount == 0) continue;
                    if (hexDigitCount > 2 || byteIndex >= 6) return false;
                    bytes[byteIndex++] = (byte)currentVal;
                    currentVal = 0;
                    hexDigitCount = 0;
                }
                else
                {
                    int nibble = ParseHexNibble(c);
                    if (nibble < 0) return false;
                    currentVal = (currentVal << 4) | nibble;
                    hexDigitCount++;

                    if (hexDigitCount == 2 && (i + 1 == span.Length || span[i + 1] == '-' || span[i + 1] == ':'))
                    {
                        if (byteIndex >= 6) return false;
                        bytes[byteIndex++] = (byte)currentVal;
                        currentVal = 0;
                        hexDigitCount = 0;
                        if (i + 1 < span.Length && (span[i + 1] == '-' || span[i + 1] == ':'))
                        {
                            i++; // Skip delimiter
                        }
                    }
                }
            }

            if (hexDigitCount > 0)
            {
                if (hexDigitCount > 2 || byteIndex >= 6) return false;
                bytes[byteIndex++] = (byte)currentVal;
            }

            if (byteIndex == 6)
            {
                mac = new MacAddress(bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5]);
                return true;
            }

            return false;
        }

        private static int ParseHexNibble(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        public override string ToString() => ToString("-");

        public string ToString(string? separator)
        {
            separator ??= string.Empty;
            return $"{_b0:X2}{separator}{_b1:X2}{separator}{_b2:X2}{separator}{_b3:X2}{separator}{_b4:X2}{separator}{_b5:X2}";
        }

        public bool Equals(MacAddress other)
        {
            return _b0 == other._b0 &&
                   _b1 == other._b1 &&
                   _b2 == other._b2 &&
                   _b3 == other._b3 &&
                   _b4 == other._b4 &&
                   _b5 == other._b5;
        }

        public override bool Equals(object? obj)
        {
            return obj is MacAddress other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + _b0;
                hash = (hash * 31) + _b1;
                hash = (hash * 31) + _b2;
                hash = (hash * 31) + _b3;
                hash = (hash * 31) + _b4;
                hash = (hash * 31) + _b5;
                return hash;
            }
        }

        public int CompareTo(MacAddress other)
        {
            int c = _b0.CompareTo(other._b0); if (c != 0) return c;
            c = _b1.CompareTo(other._b1); if (c != 0) return c;
            c = _b2.CompareTo(other._b2); if (c != 0) return c;
            c = _b3.CompareTo(other._b3); if (c != 0) return c;
            c = _b4.CompareTo(other._b4); if (c != 0) return c;
            return _b5.CompareTo(other._b5);
        }

        public static bool operator ==(MacAddress left, MacAddress right) => left.Equals(right);
        public static bool operator !=(MacAddress left, MacAddress right) => !left.Equals(right);
    }
}
