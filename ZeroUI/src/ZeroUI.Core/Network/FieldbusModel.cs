using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Network
{
    public enum FieldbusProtocol
    {
        Profinet,
        EtherCat,
        ModbusRtu,
        EtherNetIp,
        CanOpen
    }

    public enum FieldbusTopology
    {
        LinearDaisyChain,
        RedundantRing,
        Star
    }

    public enum StationStatus
    {
        Normal,
        CommunicationLost,
        Degraded
    }

    public enum SegmentStatus
    {
        Normal,
        Degraded,
        SeveredBreak
    }

    public class FieldbusStation
    {
        public int StationIndex { get; set; } = 1;
        public string Name { get; set; } = "Station";
        public string DeviceType { get; set; } = "Remote I/O";
        public string Address { get; set; } = "Node 1";
        public bool IsTerminated { get; set; }
        public StationStatus Status { get; set; } = StationStatus.Normal;
        public long CrcErrorCount { get; set; }
        public long DroppedFrames { get; set; }
        public double ResponseTimeMs { get; set; } = 1.2;
    }

    public class FieldbusSegment
    {
        public int SegmentIndex { get; set; }
        public int FromStationIndex { get; set; }
        public int ToStationIndex { get; set; }
        public SegmentStatus Status { get; set; } = SegmentStatus.Normal;
        public double CableLengthMeters { get; set; } = 10.0;
        public double SignalAttenuationDb { get; set; } = 1.5;
    }

    /// <summary>
    /// Industrial fieldbus telemetry model and cable break localization solver.
    /// Supports linear daisy-chain and redundant industrial rings (Profinet MRP / EtherCAT / EtherNet/IP DLR).
    /// </summary>
    public class FieldbusNetwork
    {
        private readonly List<FieldbusStation> _stations = new List<FieldbusStation>();
        private readonly List<FieldbusSegment> _segments = new List<FieldbusSegment>();

        public FieldbusProtocol Protocol { get; set; } = FieldbusProtocol.Profinet;
        public FieldbusTopology Topology { get; set; } = FieldbusTopology.LinearDaisyChain;
        public string MasterName { get; set; } = "PLC Main Master";
        public double MasterCycleTimeMs { get; set; } = 2.0;
        public double JitterMicroseconds { get; set; } = 35.0;
        public bool RingRedundancyActive { get; set; }
        public double RingRecoveryTimeMs { get; set; } = 8.5;

        public IReadOnlyList<FieldbusStation> Stations => _stations;
        public IReadOnlyList<FieldbusSegment> Segments => _segments;

        public void AddStation(FieldbusStation station)
        {
            if (station == null) throw new ArgumentNullException(nameof(station));
            _stations.Add(station);
            RebuildSegments();
        }

        public void ClearStations()
        {
            _stations.Clear();
            _segments.Clear();
        }

        public void RebuildSegments()
        {
            _segments.Clear();
            if (_stations.Count < 2) return;

            for (int i = 0; i < _stations.Count - 1; i++)
            {
                _segments.Add(new FieldbusSegment
                {
                    SegmentIndex = i + 1,
                    FromStationIndex = _stations[i].StationIndex,
                    ToStationIndex = _stations[i + 1].StationIndex,
                    Status = SegmentStatus.Normal
                });
            }

            if (Topology == FieldbusTopology.RedundantRing && _stations.Count > 2)
            {
                // Closing link for ring
                _segments.Add(new FieldbusSegment
                {
                    SegmentIndex = _stations.Count,
                    FromStationIndex = _stations[_stations.Count - 1].StationIndex,
                    ToStationIndex = _stations[0].StationIndex,
                    Status = SegmentStatus.Normal
                });
            }
        }

        /// <summary>
        /// Analyzes sequential station communication states to locate an exact physical cable break segment.
        /// In linear daisy chain: if stations 1..k respond and stations k+1..N are unreachable,
        /// the physical severed cable is precisely between station k and station k+1.
        /// </summary>
        public FieldbusSegment? LocateCableBreak()
        {
            if (_stations.Count < 2 || _segments.Count == 0) return null;

            // Check if any segment is explicitly marked severed
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i].Status == SegmentStatus.SeveredBreak)
                    return _segments[i];
            }

            // In linear topology: find the first boundary where status transitions from Normal to CommunicationLost
            if (Topology == FieldbusTopology.LinearDaisyChain)
            {
                for (int i = 0; i < _stations.Count - 1; i++)
                {
                    if (_stations[i].Status == StationStatus.Normal && 
                        _stations[i + 1].Status == StationStatus.CommunicationLost)
                    {
                        var seg = _segments.Find(s => s.FromStationIndex == _stations[i].StationIndex && 
                                                      s.ToStationIndex == _stations[i + 1].StationIndex);
                        if (seg != null)
                        {
                            seg.Status = SegmentStatus.SeveredBreak;
                            return seg;
                        }
                    }
                }
            }

            return null;
        }

        public void PopulateDemoIndustrialLine()
        {
            _stations.Clear();
            _stations.Add(new FieldbusStation { StationIndex = 1, Name = "Main PLC Controller", DeviceType = "S7-1500 Master", Address = "192.168.0.1" });
            _stations.Add(new FieldbusStation { StationIndex = 2, Name = "VFD Inverter Spindle", DeviceType = "G120 Drive", Address = "192.168.0.10" });
            _stations.Add(new FieldbusStation { StationIndex = 3, Name = "Remote I/O Island A", DeviceType = "ET 200SP", Address = "192.168.0.20" });
            _stations.Add(new FieldbusStation { StationIndex = 4, Name = "Valve Terminal Island", DeviceType = "Festo CPX", Address = "192.168.0.30" });
            _stations.Add(new FieldbusStation { StationIndex = 5, Name = "Inspection Vision PC", DeviceType = "Cognex InSight", Address = "192.168.0.40" });
            _stations.Add(new FieldbusStation { StationIndex = 6, Name = "Safety Guard Door I/O", DeviceType = "Pilz PNOZ", Address = "192.168.0.50", IsTerminated = true });
            RebuildSegments();
        }
    }
}
