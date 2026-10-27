using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Network
{
    public enum IpHostState
    {
        Free,
        DhcpLeased,
        StaticReserved,
        Gateway,
        Offline,
        Conflict,
        Rogue
    }

    public class IpHostEntry
    {
        public int HostOctet { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public IpHostState State { get; set; } = IpHostState.Free;
        public string Hostname { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public double PingRttMs { get; set; }
        public double PacketLossPercent { get; set; }

        private const int HistoryCapacity = 20;
        private readonly float[] _pingHistory = new float[HistoryCapacity];
        private int _historyCount;
        private int _historyIndex;

        public IReadOnlyList<float> PingHistory
        {
            get
            {
                var list = new List<float>(_historyCount);
                for (int i = 0; i < _historyCount; i++)
                {
                    int idx = (_historyIndex - _historyCount + i + HistoryCapacity) % HistoryCapacity;
                    list.Add(_pingHistory[idx]);
                }
                return list;
            }
        }

        public void AddPingSample(float rttMs)
        {
            _pingHistory[_historyIndex] = rttMs;
            _historyIndex = (_historyIndex + 1) % HistoryCapacity;
            if (_historyCount < HistoryCapacity) _historyCount++;
            PingRttMs = rttMs;
        }
    }

    /// <summary>
    /// IP Address Management (IPAM) 2D Subnet engine.
    /// Manages coordinate grid mapping (16x16 matrix for /24 IPv4 subnet), host state tracking, and ping RTT history.
    /// </summary>
    public class IpSubnetEngine
    {
        private readonly IpHostEntry[] _hosts = new IpHostEntry[256];

        public string SubnetPrefix { get; set; } = "192.168.1";
        public int CidrMask { get; set; } = 24;
        public int TotalHosts => 256;

        public IReadOnlyList<IpHostEntry> Hosts => _hosts;

        public IpSubnetEngine(string subnetPrefix = "192.168.1")
        {
            SubnetPrefix = subnetPrefix;
            InitializeSubnet();
        }

        public void InitializeSubnet()
        {
            for (int i = 0; i < 256; i++)
            {
                _hosts[i] = new IpHostEntry
                {
                    HostOctet = i,
                    IpAddress = $"{SubnetPrefix}.{i}",
                    State = IpHostState.Free
                };
            }

            // Default standard reservations
            _hosts[0].State = IpHostState.StaticReserved; // Network address
            _hosts[0].Hostname = "Network Address";

            _hosts[1].State = IpHostState.Gateway;
            _hosts[1].Hostname = "gateway.local";
            _hosts[1].MacAddress = "00:50:56:FE:10:01";
            _hosts[1].Vendor = "Cisco Systems";
            _hosts[1].AddPingSample(0.8f);

            _hosts[255].State = IpHostState.StaticReserved; // Broadcast
            _hosts[255].Hostname = "Broadcast";
        }

        public IpHostEntry GetHost(int octet)
        {
            if (octet < 0 || octet >= 256)
                throw new ArgumentOutOfRangeException(nameof(octet), "Octet must be in range 0..255.");
            return _hosts[octet];
        }

        public static void OctetToGrid(int octet, out int row, out int col)
        {
            row = octet / 16;
            col = octet % 16;
        }

        public static int GridToOctet(int row, int col)
        {
            return row * 16 + col;
        }

        public int CountByState(IpHostState state)
        {
            int count = 0;
            for (int i = 0; i < 256; i++)
            {
                if (_hosts[i].State == state) count++;
            }
            return count;
        }

        public void PopulateDemoData()
        {
            var rng = new Random(42);
            for (int i = 2; i < 254; i++)
            {
                if (i <= 20)
                {
                    _hosts[i].State = IpHostState.StaticReserved;
                    _hosts[i].Hostname = $"srv-core-{i:D2}.lan";
                    _hosts[i].MacAddress = $"00:15:5D:{i:X2}:01:AA";
                    _hosts[i].Vendor = "Dell Enterprise";
                    for (int s = 0; s < 5; s++)
                        _hosts[i].AddPingSample((float)(1.2 + rng.NextDouble() * 1.5));
                }
                else if (i >= 50 && i <= 150)
                {
                    if (i % 7 == 0)
                    {
                        _hosts[i].State = IpHostState.Conflict;
                        _hosts[i].Hostname = $"CONFLICT-IP";
                        _hosts[i].MacAddress = $"E0:D5:5E:AA:BB:CC";
                    }
                    else if (i % 11 == 0)
                    {
                        _hosts[i].State = IpHostState.Rogue;
                        _hosts[i].Hostname = $"unknown-iot-device";
                        _hosts[i].MacAddress = $"B4:E6:2D:11:22:33";
                    }
                    else if (i % 5 == 0)
                    {
                        _hosts[i].State = IpHostState.Offline;
                    }
                    else
                    {
                        _hosts[i].State = IpHostState.DhcpLeased;
                        _hosts[i].Hostname = $"ws-node-{i}.lan";
                        _hosts[i].MacAddress = $"00:1A:2B:{i:X2}:44:55";
                        _hosts[i].Vendor = "Intel Corporate";
                        for (int s = 0; s < 5; s++)
                            _hosts[i].AddPingSample((float)(2.0 + rng.NextDouble() * 4.0));
                    }
                }
                else
                {
                    _hosts[i].State = IpHostState.Free;
                }
            }
        }
    }
}
