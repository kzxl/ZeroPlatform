using System;

namespace ZeroUI.Core.Energy
{
    /// <summary>
    /// Physical Lockout/Tagout (LOTO) safety interlock state.
    /// </summary>
    public enum LotoLockState
    {
        Unlocked,
        LockedOut,
        PermitActive
    }

    /// <summary>
    /// Circuit breaker electrical trip coil continuous monitoring status.
    /// </summary>
    public enum TripCoilState
    {
        Healthy,
        OpenCircuit,
        Shorted
    }

    /// <summary>
    /// Mechanical, electrical, and life-cycle telemetry for medium/high-voltage switchgear.
    /// </summary>
    public class BreakerMechanism
    {
        public bool IsSpringCharged { get; set; } = true;
        public bool IsMotorCharging { get; set; } = false;
        public int TotalOperationsCount { get; set; } = 1420;
        public double VacuumBottleContactWearPct { get; set; } = 32.5; // 0% = pristine, 100% = end of life
        public double RatedShortCircuitKa { get; set; } = 25.0;
        public TripCoilState TripCoil1 { get; set; } = TripCoilState.Healthy;
        public TripCoilState TripCoil2 { get; set; } = TripCoilState.Healthy;
        public LotoLockState LotoState { get; set; } = LotoLockState.Unlocked;
        public double MechanismTemperatureC { get; set; } = 38.0;

        public bool IsContactWearCritical => VacuumBottleContactWearPct >= 85.0;
        public bool CanSafelyClose => IsSpringCharged && LotoState == LotoLockState.Unlocked && !IsContactWearCritical;
    }

    /// <summary>
    /// Microprocessor-based digital protection relay operational indicators.
    /// </summary>
    public class ProtectionRelayData
    {
        public string RelayTag { get; set; } = "SEL-751";
        public bool RelayHealthy { get; set; } = true;
        public string ActiveAnsiCode { get; set; } = "50/51"; // Overcurrent
        public bool IsPickupActive { get; set; } = false;
        public bool IsTripLatched { get; set; } = false;
    }

    /// <summary>
    /// Pure computation engine for switchgear safety interlocks, contact wear, and mechanism diagnostics.
    /// </summary>
    public class SwitchgearEngine
    {
        public BreakerMechanism Mechanism { get; set; } = new BreakerMechanism();
        public ProtectionRelayData Relay { get; set; } = new ProtectionRelayData();

        /// <summary>
        /// Estimates contact erosion wear percentage based on cumulative interrupted fault current.
        /// Typical vacuum bottle rated lifetime: e.g. sum of 500,000 interrupted kA.
        /// </summary>
        public static double CalculateEstimatedContactWearPct(double cumulativeInterruptedKa, double maxAllowedKaSum = 500000.0)
        {
            if (maxAllowedKaSum <= 0.0) return 100.0;
            return Math.Min(100.0, Math.Max(0.0, (cumulativeInterruptedKa / maxAllowedKaSum) * 100.0));
        }

        /// <summary>
        /// Evaluates whether closing the breaker is interlocked and prevented.
        /// </summary>
        public static bool ValidateClosePermit(BreakerMechanism mechanism, out string reason)
        {
            if (mechanism.LotoState == LotoLockState.LockedOut)
            {
                reason = "Interlocked: LOTO Padlock Locked Out";
                return false;
            }

            if (!mechanism.IsSpringCharged)
            {
                reason = "Interlocked: Operating Spring Discharged";
                return false;
            }

            if (mechanism.IsContactWearCritical)
            {
                reason = "Blocked: Vacuum Bottle Contact Wear Exceeds Safety Limit";
                return false;
            }

            reason = "Permit Granted: Ready to Close";
            return true;
        }
    }
}
