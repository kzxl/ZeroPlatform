using System;
using System.Net;

namespace ZeroSecurity.Matching;

/// <summary>
/// High-speed bitwise Radix Trie for IPv4 Longest Prefix Matching (LPM).
/// Enables sub-microsecond matching against thousands of malicious IP ranges / CIDR blocks.
/// </summary>
public sealed class RadixIpTrie
{
    private sealed class Node
    {
        public Node? Left;  // 0 bit
        public Node? Right; // 1 bit
        public string? MatchTag;
    }

    private readonly Node _root = new();
    private int _count;

    public int Count => _count;

    /// <summary>
    /// Adds an IPv4 CIDR prefix (e.g. "185.220.101.0/24") with an associated threat label.
    /// </summary>
    public void AddCidr(string cidr, string threatTag)
    {
        if (string.IsNullOrWhiteSpace(cidr)) throw new ArgumentNullException(nameof(cidr));
        if (threatTag == null) throw new ArgumentNullException(nameof(threatTag));

        int slash = cidr.IndexOf('/');
        string ipPart = slash >= 0 ? cidr.Substring(0, slash) : cidr;
        int prefixLen = slash >= 0 ? int.Parse(cidr.Substring(slash + 1)) : 32;

        if (prefixLen < 0 || prefixLen > 32)
            throw new ArgumentOutOfRangeException(nameof(cidr), "Prefix length must be 0..32");

        if (!IPAddress.TryParse(ipPart, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new ArgumentException("Only IPv4 addresses supported.", nameof(cidr));

        uint ipNum = IpToUint(ip);
        var current = _root;

        for (int i = 31; i >= 32 - prefixLen; i--)
        {
            uint bit = (ipNum >> i) & 1;
            if (bit == 0)
            {
                current.Left ??= new Node();
                current = current.Left;
            }
            else
            {
                current.Right ??= new Node();
                current = current.Right;
            }
        }

        current.MatchTag = threatTag;
        _count++;
    }

    /// <summary>
    /// Evaluates whether an IP address falls within any registered malicious CIDR ranges.
    /// Returns true if matched, outputting the most specific matching threat tag.
    /// </summary>
    public bool TryMatch(IPAddress ip, out string? matchedTag)
    {
        matchedTag = null;
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;

        uint ipNum = IpToUint(ip);
        var current = _root;
        string? lastMatched = null;

        for (int i = 31; i >= 0; i--)
        {
            if (current.MatchTag != null)
            {
                lastMatched = current.MatchTag;
            }

            uint bit = (ipNum >> i) & 1;
            current = (bit == 0) ? current.Left : current.Right;

            if (current == null) break;
        }

        if (current?.MatchTag != null)
        {
            lastMatched = current.MatchTag;
        }

        matchedTag = lastMatched;
        return matchedTag != null;
    }

    /// <summary>
    /// Evaluates IP string against registered prefixes.
    /// </summary>
    public bool TryMatch(string ipString, out string? matchedTag)
    {
        matchedTag = null;
        if (IPAddress.TryParse(ipString, out var ip))
        {
            return TryMatch(ip, out matchedTag);
        }
        return false;
    }

    private static uint IpToUint(IPAddress ip)
    {
        Span<byte> bytes = stackalloc byte[4];
#if NET8_0_OR_GREATER
        ip.TryWriteBytes(bytes, out _);
#else
        byte[] b = ip.GetAddressBytes();
        bytes[0] = b[0]; bytes[1] = b[1]; bytes[2] = b[2]; bytes[3] = b[3];
#endif
        return ((uint)bytes[0] << 24) |
               ((uint)bytes[1] << 16) |
               ((uint)bytes[2] << 8) |
               (uint)bytes[3];
    }
}
