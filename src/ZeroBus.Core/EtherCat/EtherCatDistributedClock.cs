using System;
using System.Collections.Generic;

namespace ZeroBus.EtherCat
{
    /// <summary>
    /// ESC Distributed Clocks (DC) Register Map (0x0900 - 0x09AF) per ETG.1000.4 specification.
    /// </summary>
    public static class EtherCatDcRegisters
    {
        public const ushort ReceiveTimePort0 = 0x0900;        // 32-bit local receive timestamp at Port 0 (ns)
        public const ushort ReceiveTimePort1 = 0x0904;        // 32-bit local receive timestamp at Port 1 (ns)
        public const ushort ReceiveTimePort2 = 0x0908;        // 32-bit local receive timestamp at Port 2 (ns)
        public const ushort ReceiveTimePort3 = 0x090C;        // 32-bit local receive timestamp at Port 3 (ns)
        public const ushort SystemTime = 0x0910;              // 64-bit Distributed Clock System Time (ns)
        public const ushort ReceiveTime64 = 0x0918;           // 64-bit local receive timestamp Port 0 (ns)
        public const ushort SystemTimeOffset = 0x0920;        // 64-bit offset added to local clock (ns)
        public const ushort SystemTimeDelay = 0x0928;         // 32-bit propagation delay from Reference Clock (ns)
        public const ushort SystemTimeDiff = 0x092C;          // 32-bit sign-magnitude difference (bit 31 = sign)
        public const ushort SpeedCounterStart = 0x0930;       // 16-bit rate drift bandwidth start value
        public const ushort SpeedCounterDiff = 0x0932;        // 16-bit local vs reference rate counter deviation
        public const ushort SystemTimeFilterDepth = 0x0934;   // 8-bit filter depth for System Time Difference
        public const ushort SpeedCounterFilterDepth = 0x0935; // 8-bit filter depth for Speed Counter
        public const ushort CyclicUnitControl = 0x0980;       // 8-bit SYNC unit ownership
        public const ushort SyncActivation = 0x0981;          // 8-bit SYNC0/SYNC1 activation flags
        public const ushort Sync0CycleTime = 0x09A0;          // 32-bit SYNC0 pulse period (ns)
        public const ushort Sync1CycleTime = 0x09A4;          // 32-bit SYNC1 pulse period / offset (ns)

        // SyncActivation bit masks (0x0981)
        public const byte SyncActivateOutUnit = 0x01;
        public const byte SyncEnableSync0 = 0x02;
        public const byte SyncEnableSync1 = 0x04;
    }

    /// <summary>
    /// Port receive timestamps and local clock sample captured from an ESC slave during DC topology measurement.
    /// </summary>
    public struct DcSlavePortMeasurement
    {
        public ushort StationAddress { get; set; }
        public uint Port0ReceiveTimeNs { get; set; }
        public uint Port1ReceiveTimeNs { get; set; }
        public uint Port2ReceiveTimeNs { get; set; }
        public uint Port3ReceiveTimeNs { get; set; }
        public long LocalSystemTimeNs { get; set; }

        public DcSlavePortMeasurement(
            ushort stationAddress,
            uint port0ReceiveTimeNs,
            uint port1ReceiveTimeNs,
            long localSystemTimeNs = 0,
            uint port2ReceiveTimeNs = 0,
            uint port3ReceiveTimeNs = 0)
        {
            StationAddress = stationAddress;
            Port0ReceiveTimeNs = port0ReceiveTimeNs;
            Port1ReceiveTimeNs = port1ReceiveTimeNs;
            Port2ReceiveTimeNs = port2ReceiveTimeNs;
            Port3ReceiveTimeNs = port3ReceiveTimeNs;
            LocalSystemTimeNs = localSystemTimeNs;
        }
    }

    /// <summary>
    /// Calculated Distributed Clock propagation delay and initial clock offset for an individual EtherCAT slave.
    /// </summary>
    public struct DcSlaveCalibration
    {
        public ushort StationAddress { get; set; }
        public uint PropagationDelayNs { get; set; }
        public long SystemTimeOffsetNs { get; set; }
    }

    /// <summary>
    /// EtherCAT Distributed Clocks (DC) Synchronization Engine &amp; Discrete PI Phase-Locked Loop (PI-PLL) Controller.
    /// Computes topological wire/ESC propagation delays (0x0900..0x0928), builds FRMW drift-compensation datagrams,
    /// and regulates the master cycle period to sub-microsecond phase lock with the Reference Clock slave.
    /// </summary>
    public sealed class EtherCatDistributedClock
    {
        public long NominalCycleTimeNs { get; }
        public double Kp { get; set; }
        public double Ki { get; set; }
        public long MaxAdjustmentNs { get; set; }
        public long LockThresholdNs { get; set; } = 500; // 500 ns (0.5 us) phase lock window

        public double IntegralTermNs { get; private set; }
        public long CurrentPhaseErrorNs { get; private set; }
        public long LastPeriodAdjustmentNs { get; private set; }
        public bool IsLocked => System.Math.Abs(CurrentPhaseErrorNs) <= LockThresholdNs;

        public EtherCatDistributedClock(
            long nominalCycleTimeNs = 1_000_000, // Default 1 ms (1 kHz servo cycle)
            double kp = 0.15,
            double ki = 0.01,
            long maxAdjustmentNs = 20_000)
        {
            if (nominalCycleTimeNs <= 0)
                throw new ArgumentOutOfRangeException(nameof(nominalCycleTimeNs), "Nominal cycle time must be positive.");
            if (maxAdjustmentNs <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxAdjustmentNs), "Max adjustment must be positive.");

            NominalCycleTimeNs = nominalCycleTimeNs;
            Kp = kp;
            Ki = ki;
            MaxAdjustmentNs = maxAdjustmentNs;
        }

        /// <summary>
        /// Computes one-way propagation delays (0x0928) and initial System Time Offsets (0x0920)
        /// for a daisy-chain / line topology of EtherCAT slaves starting from the Reference Clock (index 0).
        /// </summary>
        public static DcSlaveCalibration[] ComputePropagationDelays(
            IReadOnlyList<DcSlavePortMeasurement> measurements,
            long masterReferenceTimeNs = 0)
        {
            if (measurements == null) throw new ArgumentNullException(nameof(measurements));
            int count = measurements.Count;
            if (count == 0) return Array.Empty<DcSlaveCalibration>();

            var calibrations = new DcSlaveCalibration[count];
            uint cumulativeDelayNs = 0;

            for (int i = 0; i < count; i++)
            {
                var m = measurements[i];
                if (i > 0)
                {
                    var prev = measurements[i - 1];
                    // Loop delay at parent slave (time between Port 0 forward entry and Port 1 return entry)
                    uint parentLoopNs = unchecked(prev.Port1ReceiveTimeNs - prev.Port0ReceiveTimeNs);
                    // Downstream loop delay at current slave (if not terminal leaf)
                    uint childLoopNs = (i < count - 1 && m.Port1ReceiveTimeNs > m.Port0ReceiveTimeNs)
                        ? unchecked(m.Port1ReceiveTimeNs - m.Port0ReceiveTimeNs)
                        : 0u;

                    uint segmentRoundTripNs = parentLoopNs > childLoopNs ? (parentLoopNs - childLoopNs) : parentLoopNs;
                    cumulativeDelayNs += segmentRoundTripNs / 2u;
                }

                long targetSlaveTimeNs = masterReferenceTimeNs + cumulativeDelayNs;
                long offsetNs = targetSlaveTimeNs - m.LocalSystemTimeNs;

                calibrations[i] = new DcSlaveCalibration
                {
                    StationAddress = m.StationAddress,
                    PropagationDelayNs = cumulativeDelayNs,
                    SystemTimeOffsetNs = offsetNs
                };
            }

            return calibrations;
        }

        /// <summary>
        /// Builds a Broadcast Write (BWR) datagram to register 0x0900 that causes all slaves
        /// to latch their local receive timestamps on Port 0 as the frame passes through.
        /// </summary>
        public static EtherCatDatagram BuildLatchReceiveTimesDatagram(byte index = 1)
        {
            uint address = (uint)EtherCatDcRegisters.ReceiveTimePort0 << 16;
            return new EtherCatDatagram(EtherCatConstants.CmdBwr, index, address, new byte[4]);
        }

        /// <summary>
        /// Builds a Configured Address Write (FPWR) datagram to program a slave's 64-bit System Time Offset (0x0920).
        /// </summary>
        public static EtherCatDatagram BuildSystemTimeOffsetDatagram(
            ushort stationAddress,
            long offsetNs,
            byte index = 1)
        {
            uint address = CombineStationAndRegister(stationAddress, EtherCatDcRegisters.SystemTimeOffset);
            byte[] payload = BitConverter.GetBytes(offsetNs);
            return new EtherCatDatagram(EtherCatConstants.CmdFpwr, index, address, payload);
        }

        /// <summary>
        /// Builds a Configured Address Write (FPWR) datagram to program a slave's 32-bit Propagation Delay (0x0928).
        /// </summary>
        public static EtherCatDatagram BuildSystemTimeDelayDatagram(
            ushort stationAddress,
            uint propagationDelayNs,
            byte index = 1)
        {
            uint address = CombineStationAndRegister(stationAddress, EtherCatDcRegisters.SystemTimeDelay);
            byte[] payload = BitConverter.GetBytes(propagationDelayNs);
            return new EtherCatDatagram(EtherCatConstants.CmdFpwr, index, address, payload);
        }

        /// <summary>
        /// Builds a Configured Address Multiple Read Write (FRMW, 0x0E) datagram targeting 0x0910 (System Time).
        /// The Reference Clock slave (matching referenceStationAddress) places its 64-bit clock onto the frame,
        /// and all subsequent slaves write that timestamp into 0x0910 to compute hardware drift &amp; speed counter adjustment.
        /// </summary>
        public static EtherCatDatagram BuildReferenceDriftCorrectionDatagram(
            ushort referenceStationAddress,
            byte index = 1)
        {
            uint address = CombineStationAndRegister(referenceStationAddress, EtherCatDcRegisters.SystemTime);
            return new EtherCatDatagram(EtherCatConstants.CmdFrmw, index, address, new byte[8]);
        }

        /// <summary>
        /// Builds FPWR datagrams to configure a slave's SYNC0 cycle time (0x09A0) and activate SYNC0/SYNC1 generation (0x0981).
        /// </summary>
        public static EtherCatDatagram[] BuildSync0ConfigurationDatagrams(
            ushort stationAddress,
            uint sync0CycleTimeNs,
            bool enableSync1 = false,
            byte baseIndex = 1)
        {
            uint addrCycle = CombineStationAndRegister(stationAddress, EtherCatDcRegisters.Sync0CycleTime);
            byte[] cycleBytes = BitConverter.GetBytes(sync0CycleTimeNs);
            var dgramCycle = new EtherCatDatagram(EtherCatConstants.CmdFpwr, baseIndex, addrCycle, cycleBytes)
            {
                MoreDatagrams = true
            };

            byte syncFlags = (byte)(EtherCatDcRegisters.SyncActivateOutUnit | EtherCatDcRegisters.SyncEnableSync0);
            if (enableSync1)
            {
                syncFlags |= EtherCatDcRegisters.SyncEnableSync1;
            }

            uint addrActivate = CombineStationAndRegister(stationAddress, EtherCatDcRegisters.SyncActivation);
            var dgramActivate = new EtherCatDatagram(
                EtherCatConstants.CmdFpwr,
                unchecked((byte)(baseIndex + 1)),
                addrActivate,
                new byte[] { syncFlags });

            return new EtherCatDatagram[] { dgramCycle, dgramActivate };
        }

        /// <summary>
        /// Decodes the 32-bit ESC System Time Difference register (0x092C), which uses sign-magnitude encoding
        /// (Bit 31 = 1 for negative difference, Bits 0..30 = magnitude in nanoseconds).
        /// </summary>
        public static int DecodeSystemTimeDiff(uint rawRegister092C)
        {
            int magnitude = (int)(rawRegister092C & 0x7FFFFFFFu);
            bool isNegative = (rawRegister092C & 0x80000000u) != 0;
            return isNegative ? -magnitude : magnitude;
        }

        /// <summary>
        /// Encodes a signed nanosecond clock difference into ESC register 0x092C sign-magnitude format.
        /// </summary>
        public static uint EncodeSystemTimeDiff(int diffNs)
        {
            if (diffNs < 0)
            {
                uint mag = (uint)System.Math.Min(0x7FFFFFFFL, -(long)diffNs);
                return mag | 0x80000000u;
            }
            return (uint)diffNs & 0x7FFFFFFFu;
        }

        /// <summary>
        /// Executes one step of the discrete PI Phase-Locked Loop (PI-PLL) controller given a measured phase error (ns).
        /// Outputs the adjusted next cycle period in nanoseconds (NominalCycleTimeNs + LastPeriodAdjustmentNs).
        /// </summary>
        public long UpdatePllFromPhaseError(long phaseErrorNs)
        {
            CurrentPhaseErrorNs = phaseErrorNs;

            // Update integral with anti-windup clamping
            IntegralTermNs += Ki * phaseErrorNs;
            if (IntegralTermNs > MaxAdjustmentNs) IntegralTermNs = MaxAdjustmentNs;
            if (IntegralTermNs < -MaxAdjustmentNs) IntegralTermNs = -MaxAdjustmentNs;

            double rawControl = (Kp * phaseErrorNs) + IntegralTermNs;
            long clampedAdj = (long)System.Math.Round(rawControl);
            if (clampedAdj > MaxAdjustmentNs) clampedAdj = MaxAdjustmentNs;
            if (clampedAdj < -MaxAdjustmentNs) clampedAdj = -MaxAdjustmentNs;

            LastPeriodAdjustmentNs = clampedAdj;
            return NominalCycleTimeNs + clampedAdj;
        }

        /// <summary>
        /// Executes one step of the discrete PI-PLL synchronizing the Master clock to the Reference Slave's 64-bit System Time.
        /// </summary>
        public long UpdatePll(long masterTimeNs, long referenceClockTimeNs)
        {
            long phaseError = referenceClockTimeNs - masterTimeNs;
            return UpdatePllFromPhaseError(phaseError);
        }

        public void ResetPll()
        {
            IntegralTermNs = 0.0;
            CurrentPhaseErrorNs = 0;
            LastPeriodAdjustmentNs = 0;
        }

        public static uint CombineStationAndRegister(ushort stationAddress, ushort registerOffset)
        {
            return (uint)stationAddress | ((uint)registerOffset << 16);
        }
    }
}
