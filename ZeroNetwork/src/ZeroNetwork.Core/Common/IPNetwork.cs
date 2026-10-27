using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace ZeroNetwork.Common
{
    /// <summary>
    /// Represents an IP network subnet (CIDR), providing high-performance, allocation-free subnet arithmetic,
    /// host containment tests, broadcast address calculation, and host enumeration.
    /// </summary>
    public readonly struct IPNetwork : IEquatable<IPNetwork>
    {
        private readonly IPAddress _networkAddress;
        private readonly IPAddress _netmask;
        private readonly IPAddress _broadcastAddress;
        private readonly int _cidrPrefix;
        private readonly bool _initialized;

        public IPAddress NetworkAddress => _networkAddress ?? IPAddress.Any;
        public IPAddress Netmask => _netmask ?? IPAddress.Any;
        public IPAddress BroadcastAddress => _broadcastAddress ?? IPAddress.Broadcast;
        public int CidrPrefix => _cidrPrefix;
        public AddressFamily AddressFamily => NetworkAddress.AddressFamily;

        /// <summary>
        /// Gets the total number of IP addresses in this subnet (including network and broadcast).
        /// </summary>
        public uint TotalHosts
        {
            get
            {
                if (AddressFamily != AddressFamily.InterNetwork) return 0;
                if (_cidrPrefix >= 32) return 1u;
                return 1u << (32 - _cidrPrefix);
            }
        }

        /// <summary>
        /// Gets the number of usable host IP addresses in this subnet.
        /// </summary>
        public uint UsableHostsCount
        {
            get
            {
                if (AddressFamily != AddressFamily.InterNetwork) return 0;
                if (_cidrPrefix == 32) return 1u;
                if (_cidrPrefix == 31) return 2u; // RFC 3021
                return TotalHosts > 2 ? TotalHosts - 2 : 0;
            }
        }

        public IPNetwork(IPAddress ip, int cidrPrefix)
        {
            if (ip == null) throw new ArgumentNullException(nameof(ip));
            if (ip.AddressFamily != AddressFamily.InterNetwork)
                throw new NotSupportedException("Only IPv4 networks are currently supported in arithmetic calculations.");

            if (cidrPrefix < 0 || cidrPrefix > 32)
                throw new ArgumentOutOfRangeException(nameof(cidrPrefix), "CIDR prefix must be between 0 and 32.");

            _cidrPrefix = cidrPrefix;
            uint ipUint = ToUint(ip);
            uint maskUint = cidrPrefix == 0 ? 0u : (0xFFFFFFFFu << (32 - cidrPrefix));
            uint netUint = ipUint & maskUint;
            uint bcastUint = netUint | ~maskUint;

            _netmask = FromUint(maskUint);
            _networkAddress = FromUint(netUint);
            _broadcastAddress = FromUint(bcastUint);
            _initialized = true;
        }

        public IPNetwork(IPAddress ip, IPAddress subnetMask)
        {
            if (ip == null) throw new ArgumentNullException(nameof(ip));
            if (subnetMask == null) throw new ArgumentNullException(nameof(subnetMask));

            int prefix = GetCidrPrefix(subnetMask);
            _cidrPrefix = prefix;

            uint ipUint = ToUint(ip);
            uint maskUint = ToUint(subnetMask);
            uint netUint = ipUint & maskUint;
            uint bcastUint = netUint | ~maskUint;

            _netmask = subnetMask;
            _networkAddress = FromUint(netUint);
            _broadcastAddress = FromUint(bcastUint);
            _initialized = true;
        }

        /// <summary>
        /// Tests whether the specified IP address is within this subnet.
        /// </summary>
        public bool Contains(IPAddress ip)
        {
            if (!_initialized || ip == null || ip.AddressFamily != AddressFamily)
                return false;

            uint ipUint = ToUint(ip);
            uint netUint = ToUint(NetworkAddress);
            uint maskUint = ToUint(Netmask);

            return (ipUint & maskUint) == netUint;
        }

        /// <summary>
        /// Tests whether the specified subnet is entirely contained within this subnet.
        /// </summary>
        public bool Contains(IPNetwork other)
        {
            if (!_initialized || !other._initialized || other.AddressFamily != AddressFamily)
                return false;

            return other._cidrPrefix >= _cidrPrefix && Contains(other.NetworkAddress);
        }

        /// <summary>
        /// Tests whether this subnet overlaps with another subnet.
        /// </summary>
        public bool Overlaps(IPNetwork other)
        {
            if (!_initialized || !other._initialized || other.AddressFamily != AddressFamily)
                return false;

            return Contains(other.NetworkAddress) || other.Contains(NetworkAddress);
        }

        /// <summary>
        /// Enumerates all usable host IP addresses in this subnet.
        /// </summary>
        public IEnumerable<IPAddress> EnumerateUsableHosts()
        {
            if (!_initialized || AddressFamily != AddressFamily.InterNetwork)
                yield break;

            uint netUint = ToUint(NetworkAddress);
            uint bcastUint = ToUint(BroadcastAddress);

            if (_cidrPrefix == 32)
            {
                yield return NetworkAddress;
                yield break;
            }

            if (_cidrPrefix == 31)
            {
                yield return NetworkAddress;
                yield return BroadcastAddress;
                yield break;
            }

            uint start = netUint + 1;
            uint end = bcastUint - 1;

            for (uint cur = start; cur <= end; cur++)
            {
                yield return FromUint(cur);
            }
        }

        /// <summary>
        /// Parses a CIDR network string (e.g. "192.168.1.0/24" or "10.0.0.1 255.255.255.0").
        /// </summary>
        public static IPNetwork Parse(string cidr)
        {
            if (TryParse(cidr, out var network))
            {
                return network;
            }
            throw new FormatException($"Invalid CIDR or subnet format: '{cidr}'");
        }

        /// <summary>
        /// Tries to parse a CIDR network string.
        /// </summary>
        public static bool TryParse(string? text, out IPNetwork network)
        {
            network = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string clean = text!.Trim();
            int slashIndex = clean.IndexOf('/');
            if (slashIndex >= 0)
            {
                string ipPart = clean.Substring(0, slashIndex);
                string prefixPart = clean.Substring(slashIndex + 1);

                if (!IPAddress.TryParse(ipPart, out var ip))
                    return false;

                if (int.TryParse(prefixPart, out int prefix))
                {
                    if (prefix < 0 || prefix > 32) return false;
                    network = new IPNetwork(ip, prefix);
                    return true;
                }

                if (IPAddress.TryParse(prefixPart, out var mask))
                {
                    network = new IPNetwork(ip, mask);
                    return true;
                }

                return false;
            }

            string[] parts = clean.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                if (IPAddress.TryParse(parts[0], out var ip) && IPAddress.TryParse(parts[1], out var mask))
                {
                    network = new IPNetwork(ip, mask);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates a subnet mask IPAddress for a given CIDR prefix.
        /// </summary>
        public static IPAddress CreateSubnetMask(int cidrPrefix)
        {
            if (cidrPrefix < 0 || cidrPrefix > 32)
                throw new ArgumentOutOfRangeException(nameof(cidrPrefix));

            uint mask = cidrPrefix == 0 ? 0u : (0xFFFFFFFFu << (32 - cidrPrefix));
            return FromUint(mask);
        }

        /// <summary>
        /// Calculates the CIDR prefix from a subnet mask IPAddress.
        /// </summary>
        public static int GetCidrPrefix(IPAddress subnetMask)
        {
            if (subnetMask == null) throw new ArgumentNullException(nameof(subnetMask));
            uint maskUint = ToUint(subnetMask);
            int count = 0;
            while (maskUint != 0)
            {
                count += (int)(maskUint & 1);
                maskUint >>= 1;
            }
            return count;
        }

        private static uint ToUint(IPAddress ip)
        {
#if NET8_0_OR_GREATER
            Span<byte> bytes = stackalloc byte[4];
            ip.TryWriteBytes(bytes, out _);
            return ((uint)bytes[0] << 24) |
                   ((uint)bytes[1] << 16) |
                   ((uint)bytes[2] << 8) |
                   bytes[3];
#else
            byte[] bytes = ip.GetAddressBytes();
            return ((uint)bytes[0] << 24) |
                   ((uint)bytes[1] << 16) |
                   ((uint)bytes[2] << 8) |
                   bytes[3];
#endif
        }

        private static IPAddress FromUint(uint value)
        {
#if NET8_0_OR_GREATER
            Span<byte> bytes = stackalloc byte[4];
            bytes[0] = (byte)((value >> 24) & 0xFF);
            bytes[1] = (byte)((value >> 16) & 0xFF);
            bytes[2] = (byte)((value >> 8) & 0xFF);
            bytes[3] = (byte)(value & 0xFF);
            return new IPAddress((ReadOnlySpan<byte>)bytes);
#else
            byte[] bytes = new byte[4];
            bytes[0] = (byte)((value >> 24) & 0xFF);
            bytes[1] = (byte)((value >> 16) & 0xFF);
            bytes[2] = (byte)((value >> 8) & 0xFF);
            bytes[3] = (byte)(value & 0xFF);
            return new IPAddress(bytes);
#endif
        }

        public string ToCidrString() => $"{NetworkAddress}/{_cidrPrefix}";

        public override string ToString() => ToCidrString();

        public bool Equals(IPNetwork other)
        {
            return _cidrPrefix == other._cidrPrefix &&
                   Equals(NetworkAddress, other.NetworkAddress);
        }

        public override bool Equals(object? obj)
        {
            return obj is IPNetwork other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (NetworkAddress?.GetHashCode() ?? 0);
                hash = (hash * 31) + _cidrPrefix;
                return hash;
            }
        }

        public static bool operator ==(IPNetwork left, IPNetwork right) => left.Equals(right);
        public static bool operator !=(IPNetwork left, IPNetwork right) => !left.Equals(right);
    }
}
