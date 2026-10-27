using System;

namespace ZeroUI.Core.Scada.Safety
{
    /// <summary>
    /// Industrial equipment safety, maintenance, and interlock status flags bitmask.
    /// Follows OSHA Lockout/Tagout (LOTO) 1910.147 and ISA-84 / IEC-61511 safety standards.
    /// </summary>
    [Flags]
    public enum DeviceStatusFlags
    {
        /// <summary>No safety restrictions or abnormal conditions active.</summary>
        None = 0,

        /// <summary>Physical lockout safety padlock applied (LOTO).</summary>
        LockedOut = 1 << 0,

        /// <summary>Warning tagout label attached (equipment must not be operated).</summary>
        TaggedOut = 1 << 1,

        /// <summary>Hardware or software safety interlock active (prevents start/open command).</summary>
        Interlocked = 1 << 2,

        /// <summary>Equipment is marked for scheduled or active maintenance/service.</summary>
        Maintenance = 1 << 3,

        /// <summary>Hardware trip, communication timeout, or sensor fault condition.</summary>
        Fault = 1 << 4,

        /// <summary>Manual override mode active (bypasses automatic DCS/PLC sequence).</summary>
        ManualOverride = 1 << 5
    }
}
