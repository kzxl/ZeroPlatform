using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Network
{
    public enum SwitchPortType
    {
        RJ45,
        Sfp,
        SfpPlus,
        Console
    }

    public enum PortSpeed
    {
        Speed10M,
        Speed100M,
        Speed1G,
        Speed10G,
        Speed25G
    }

    public enum PortAdminState
    {
        Up,
        Down
    }

    public enum PortOperationalStatus
    {
        LinkDown,
        LinkUp,
        Forwarding,
        Blocking,
        Flapping,
        ErrorDisabled
    }

    public class SwitchPort
    {
        public int PortIndex { get; set; } = 1;
        public SwitchPortType PortType { get; set; } = SwitchPortType.RJ45;
        public PortSpeed Speed { get; set; } = PortSpeed.Speed1G;
        public PortAdminState AdminState { get; set; } = PortAdminState.Up;
        public PortOperationalStatus OperationalStatus { get; set; } = PortOperationalStatus.Forwarding;
        public int VlanId { get; set; } = 1;
        public bool IsTagged { get; set; }
        public double PoeWatts { get; set; }
        public double PoeMaxWatts { get; set; } = 30.0;
        public double RxBytesPerSec { get; set; }
        public double TxBytesPerSec { get; set; }
        public double CableLengthMeters { get; set; } = 15.0;
        public bool CableFaultDetected { get; set; }
        public string Description { get; set; } = string.Empty;

        public bool IsLinkUp => AdminState == PortAdminState.Up && 
                                OperationalStatus != PortOperationalStatus.LinkDown && 
                                OperationalStatus != PortOperationalStatus.ErrorDisabled;

        public bool HasTraffic => IsLinkUp && (RxBytesPerSec > 1024 || TxBytesPerSec > 1024);
    }

    /// <summary>
    /// Geometry and layout coordination for physical switch faceplates.
    /// Handles standard dual-row interleaved RJ45 port arrays and SFP/SFP+ uplink blocks.
    /// </summary>
    public class SwitchPortLayout
    {
        private readonly List<SwitchPort> _ports = new List<SwitchPort>();

        public int Rj45Count { get; private set; }
        public int SfpCount { get; private set; }
        public double MaxPoeBudgetWatts { get; set; } = 370.0;

        public IReadOnlyList<SwitchPort> Ports => _ports;

        public SwitchPortLayout(int rj45Count = 24, int sfpCount = 4)
        {
            Rj45Count = rj45Count;
            SfpCount = sfpCount;
            InitializeDefaultPorts();
        }

        public void InitializeDefaultPorts()
        {
            _ports.Clear();
            for (int i = 1; i <= Rj45Count; i++)
            {
                _ports.Add(new SwitchPort
                {
                    PortIndex = i,
                    PortType = SwitchPortType.RJ45,
                    Speed = PortSpeed.Speed1G,
                    OperationalStatus = (i % 3 == 0) ? PortOperationalStatus.LinkDown : PortOperationalStatus.Forwarding,
                    VlanId = (i > 16) ? 20 : 10,
                    PoeWatts = (i % 4 == 0) ? 12.5 : 0.0,
                    RxBytesPerSec = (i % 3 != 0) ? 1024 * 512 : 0,
                    TxBytesPerSec = (i % 3 != 0) ? 1024 * 256 : 0,
                    Description = $"GigabitEthernet1/0/{i}"
                });
            }

            for (int j = 1; j <= SfpCount; j++)
            {
                int portNum = Rj45Count + j;
                _ports.Add(new SwitchPort
                {
                    PortIndex = portNum,
                    PortType = SwitchPortType.SfpPlus,
                    Speed = PortSpeed.Speed10G,
                    OperationalStatus = (j <= 2) ? PortOperationalStatus.Forwarding : PortOperationalStatus.LinkDown,
                    VlanId = 1,
                    IsTagged = true,
                    Description = $"TenGigabitEthernet1/0/{portNum}"
                });
            }
        }

        public SwitchPort? FindPort(int portIndex)
        {
            for (int i = 0; i < _ports.Count; i++)
            {
                if (_ports[i].PortIndex == portIndex)
                    return _ports[i];
            }
            return null;
        }

        public double CalculateTotalPoeWatts()
        {
            double sum = 0;
            for (int i = 0; i < _ports.Count; i++)
            {
                sum += _ports[i].PoeWatts;
            }
            return sum;
        }

        public int CountActiveLinks()
        {
            int count = 0;
            for (int i = 0; i < _ports.Count; i++)
            {
                if (_ports[i].IsLinkUp) count++;
            }
            return count;
        }

        /// <summary>
        /// Maps a port index into front panel grid coordinates:
        /// RJ45 ports are arranged in 2 rows: Top row (odd index: 1, 3, 5...), Bottom row (even index: 2, 4, 6...).
        /// SFP ports are arranged in a 2x(SfpCount/2) block on the right.
        /// </summary>
        public static void GetPortGridCoordinates(int portIndex, int rj45Count, out int row, out int column, out bool isSfp)
        {
            if (portIndex <= rj45Count)
            {
                isSfp = false;
                row = (portIndex % 2 == 1) ? 0 : 1;
                column = (portIndex - 1) / 2;
            }
            else
            {
                isSfp = true;
                int sfpIndex = portIndex - rj45Count;
                row = (sfpIndex % 2 == 1) ? 0 : 1;
                column = (sfpIndex - 1) / 2;
            }
        }
    }
}
