using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Petrochem
{
    /// <summary>
    /// Safety Integrity Level rating per IEC 61508 / IEC 61511.
    /// </summary>
    public enum SafetyIntegrityLevel
    {
        BPCS, // Basic Process Control System (Non-safety)
        SIL1,
        SIL2,
        SIL3,
        SIL4
    }

    /// <summary>
    /// Operating health and trip status of an ESD Cause (initiator / transmitter).
    /// </summary>
    public enum CauseStatus
    {
        Healthy,
        AlarmLow,
        AlarmHigh,
        Tripped,
        Bypassed,
        Fault
    }

    /// <summary>
    /// Actuation state of an ESD Effect (final safety element / valve / pump breaker).
    /// </summary>
    public enum EffectStatus
    {
        Energized,    // Healthy / Normal running
        DeEnergized,  // Tripped into fail-safe position
        Disarmed,
        Bypassed,
        Overridden
    }

    /// <summary>
    /// Protective actuation response type upon cause interlock activation.
    /// </summary>
    public enum InterlockAction
    {
        ImmediateTrip,
        TimeDelayedTrip,
        ConfirmationRequired,
        PermissiveOnly
    }

    /// <summary>
    /// Safety initiator (cause) mapping process sensors, gas/fire detectors, or manual ESD buttons.
    /// </summary>
    public class EsdCause
    {
        public string Tag { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public SafetyIntegrityLevel SilRating { get; set; } = SafetyIntegrityLevel.SIL2;
        public CauseStatus Status { get; set; } = CauseStatus.Healthy;
        public double ProcessValue { get; set; }
        public double TripSetpoint { get; set; }
        public string Unit { get; set; } = string.Empty;
        public bool IsBypassed { get; set; }
        public string BypassAuthorizedBy { get; set; } = string.Empty;
        public string VotingGroup { get; set; } = string.Empty;
        public bool TripOnLow { get; set; }

        public bool IsTripped
        {
            get
            {
                if (IsBypassed) return false;
                if (Status == CauseStatus.Tripped) return true;
                if (TripSetpoint <= 0.0) return false;
                return TripOnLow ? ProcessValue <= TripSetpoint : ProcessValue >= TripSetpoint;
            }
        }
    }

    /// <summary>
    /// Safety final element (effect) mapping shut-off valves, blowdown valves, and pump trip relays.
    /// </summary>
    public class EsdEffect
    {
        public string Tag { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public SafetyIntegrityLevel SilRating { get; set; } = SafetyIntegrityLevel.SIL2;
        public EffectStatus Status { get; set; } = EffectStatus.Energized;
        public bool FailSafeClosed { get; set; } = true; // True for ESDV (closes), False for BDV (opens)
        public double StrokeTimeMs { get; set; } = 2500.0;
        public bool IsOverridden { get; set; }
    }

    /// <summary>
    /// Intersection link between an ESD Cause and an ESD Effect.
    /// </summary>
    public class EsdInterlock
    {
        public int CauseIndex { get; set; }
        public int EffectIndex { get; set; }
        public InterlockAction Action { get; set; } = InterlockAction.ImmediateTrip;
        public int DelaySeconds { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Pure computation engine for IEC 61508/61511 Safety Instrumented System (SIS) Cause & Effect matrix,
    /// M-out-of-N transmitter voting, Maintenance Override Switch (MOS) bypass management, and trip propagation.
    /// </summary>
    public class EsdEngine
    {
        public string SisTag { get; set; } = "SIS-ESD-01";
        public string AreaDescription { get; set; } = "Crude Fractionation Unit Safety Matrix";

        public List<EsdCause> Causes { get; } = new List<EsdCause>();
        public List<EsdEffect> Effects { get; } = new List<EsdEffect>();
        public List<EsdInterlock> Interlocks { get; } = new List<EsdInterlock>();

        public int ActiveTripCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Causes.Count; i++)
                {
                    if (Causes[i].IsTripped) count++;
                }
                return count;
            }
        }

        public int ActiveBypassCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Causes.Count; i++)
                {
                    if (Causes[i].IsBypassed) count++;
                }
                return count;
            }
        }

        public bool SystemSafeState => ActiveTripCount == 0;

        public EsdEngine()
        {
            InitializeDefaultMatrix();
            EvaluateInterlocks();
        }

        /// <summary>
        /// Populates standard refinery Cause & Effect matrix with 8 Causes and 6 Effects.
        /// </summary>
        public void InitializeDefaultMatrix()
        {
            Causes.Clear();
            Effects.Clear();
            Interlocks.Clear();

            // Causes (Initiators)
            Causes.Add(new EsdCause { Tag = "PSHH-101A", Description = "Column Overhead High-High Pressure", SilRating = SafetyIntegrityLevel.SIL3, ProcessValue = 182.0, TripSetpoint = 230.0, Unit = "kPa", VotingGroup = "2oo3-P101" });
            Causes.Add(new EsdCause { Tag = "PSHH-101B", Description = "Column Overhead High-High Pressure", SilRating = SafetyIntegrityLevel.SIL3, ProcessValue = 183.0, TripSetpoint = 230.0, Unit = "kPa", VotingGroup = "2oo3-P101" });
            Causes.Add(new EsdCause { Tag = "PSHH-101C", Description = "Column Overhead High-High Pressure", SilRating = SafetyIntegrityLevel.SIL3, ProcessValue = 181.5, TripSetpoint = 230.0, Unit = "kPa", VotingGroup = "2oo3-P101" });
            Causes.Add(new EsdCause { Tag = "TSHH-204", Description = "Reboiler Fired Heater Tube Temp", SilRating = SafetyIntegrityLevel.SIL3, ProcessValue = 245.0, TripSetpoint = 295.0, Unit = "°C" });
            Causes.Add(new EsdCause { Tag = "LSLL-302", Description = "Column Bottom Sump Low-Low Level", SilRating = SafetyIntegrityLevel.SIL2, ProcessValue = 42.0, TripSetpoint = 15.0, Unit = "%", TripOnLow = true });
            Causes.Add(new EsdCause { Tag = "ASHH-401", Description = "Hydrocarbon Gas Vapor Detection LEL", SilRating = SafetyIntegrityLevel.SIL2, ProcessValue = 8.0, TripSetpoint = 40.0, Unit = "% LEL" });
            Causes.Add(new EsdCause { Tag = "FZHH-501", Description = "Process Area Deluge Fire Loop", SilRating = SafetyIntegrityLevel.SIL3, ProcessValue = 0.0, TripSetpoint = 1.0, Unit = "Loop" });
            Causes.Add(new EsdCause { Tag = "ESD-PB-01", Description = "CCR Manual Emergency Shutdown Button", SilRating = SafetyIntegrityLevel.SIL3, ProcessValue = 0.0, TripSetpoint = 1.0, Unit = "PB" });

            // Effects (Final Elements)
            Effects.Add(new EsdEffect { Tag = "XV-101", Description = "Crude Feed Inlet Emergency Shutoff", SilRating = SafetyIntegrityLevel.SIL3, FailSafeClosed = true, StrokeTimeMs = 2100.0 });
            Effects.Add(new EsdEffect { Tag = "BDV-102", Description = "Column Overhead Depressuring Blowdown", SilRating = SafetyIntegrityLevel.SIL3, FailSafeClosed = false, StrokeTimeMs = 1800.0 });
            Effects.Add(new EsdEffect { Tag = "XV-103", Description = "Reboiler Fuel Gas Shutoff Valve", SilRating = SafetyIntegrityLevel.SIL3, FailSafeClosed = true, StrokeTimeMs = 1200.0 });
            Effects.Add(new EsdEffect { Tag = "P-101-SD", Description = "Feed Charge Pump P-101 Motor Trip", SilRating = SafetyIntegrityLevel.SIL2, FailSafeClosed = true, StrokeTimeMs = 250.0 });
            Effects.Add(new EsdEffect { Tag = "P-102-SD", Description = "Column Bottoms Pump P-102 Motor Trip", SilRating = SafetyIntegrityLevel.SIL2, FailSafeClosed = true, StrokeTimeMs = 250.0 });
            Effects.Add(new EsdEffect { Tag = "DELUGE-01", Description = "Zone Deluge Water Foam Actuation", SilRating = SafetyIntegrityLevel.SIL3, FailSafeClosed = false, StrokeTimeMs = 3500.0 });

            // Interlocks (Matrix Intersections)
            // Manual PB trips everything
            for (int e = 0; e < Effects.Count; e++)
            {
                Interlocks.Add(new EsdInterlock { CauseIndex = 7, EffectIndex = e, Action = InterlockAction.ImmediateTrip });
            }

            // High pressure (2oo3 group) -> XV-101 close, BDV-102 open, XV-103 close, P-101 trip
            for (int c = 0; c < 3; c++)
            {
                Interlocks.Add(new EsdInterlock { CauseIndex = c, EffectIndex = 0, Action = InterlockAction.ImmediateTrip });
                Interlocks.Add(new EsdInterlock { CauseIndex = c, EffectIndex = 1, Action = InterlockAction.ImmediateTrip });
                Interlocks.Add(new EsdInterlock { CauseIndex = c, EffectIndex = 2, Action = InterlockAction.ImmediateTrip });
                Interlocks.Add(new EsdInterlock { CauseIndex = c, EffectIndex = 3, Action = InterlockAction.ImmediateTrip });
            }

            // Heater tube overheat (TSHH-204) -> XV-103 close fuel, P-101 trip
            Interlocks.Add(new EsdInterlock { CauseIndex = 3, EffectIndex = 2, Action = InterlockAction.ImmediateTrip });
            Interlocks.Add(new EsdInterlock { CauseIndex = 3, EffectIndex = 3, Action = InterlockAction.ImmediateTrip });

            // Low sump level (LSLL-302) -> P-102 bottoms pump trip (prevents cavitating dry run)
            Interlocks.Add(new EsdInterlock { CauseIndex = 4, EffectIndex = 4, Action = InterlockAction.ImmediateTrip });

            // Gas detection (ASHH-401) -> Feed valve XV-101 close, charge pump trip
            Interlocks.Add(new EsdInterlock { CauseIndex = 5, EffectIndex = 0, Action = InterlockAction.TimeDelayedTrip, DelaySeconds = 5 });
            Interlocks.Add(new EsdInterlock { CauseIndex = 5, EffectIndex = 3, Action = InterlockAction.TimeDelayedTrip, DelaySeconds = 5 });

            // Fire loop (FZHH-501) -> Deluge valve open, fuel shutoff, all pumps trip
            Interlocks.Add(new EsdInterlock { CauseIndex = 6, EffectIndex = 2, Action = InterlockAction.ImmediateTrip });
            Interlocks.Add(new EsdInterlock { CauseIndex = 6, EffectIndex = 3, Action = InterlockAction.ImmediateTrip });
            Interlocks.Add(new EsdInterlock { CauseIndex = 6, EffectIndex = 4, Action = InterlockAction.ImmediateTrip });
            Interlocks.Add(new EsdInterlock { CauseIndex = 6, EffectIndex = 5, Action = InterlockAction.ImmediateTrip });
        }

        /// <summary>
        /// Evaluates all Causes against the Interlock matrix, processing 2oo3 voting and de-energizing Effects.
        /// </summary>
        public void EvaluateInterlocks()
        {
            // Reset all effects to Energized unless overridden
            for (int i = 0; i < Effects.Count; i++)
            {
                if (!Effects[i].IsOverridden)
                {
                    Effects[i].Status = EffectStatus.Energized;
                }
            }

            // Reset all interlocks
            for (int i = 0; i < Interlocks.Count; i++)
            {
                Interlocks[i].IsActive = false;
            }

            // Check 2oo3 voting group for PSHH-101
            int pshhTrippedCount = 0;
            for (int i = 0; i < 3; i++)
            {
                if (Causes[i].IsTripped) pshhTrippedCount++;
            }
            bool pshhGroupTripped = pshhTrippedCount >= 2;

            for (int i = 0; i < Interlocks.Count; i++)
            {
                var link = Interlocks[i];
                if (link.CauseIndex < 0 || link.CauseIndex >= Causes.Count ||
                    link.EffectIndex < 0 || link.EffectIndex >= Effects.Count)
                {
                    continue;
                }

                var cause = Causes[link.CauseIndex];
                var effect = Effects[link.EffectIndex];

                bool tripTriggered = false;
                if (!string.IsNullOrEmpty(cause.VotingGroup) && cause.VotingGroup == "2oo3-P101")
                {
                    tripTriggered = pshhGroupTripped;
                }
                else
                {
                    tripTriggered = cause.IsTripped;
                }

                if (tripTriggered)
                {
                    link.IsActive = true;
                    if (!effect.IsOverridden)
                    {
                        effect.Status = EffectStatus.DeEnergized;
                    }
                }
            }
        }

        public void SetCauseBypass(int causeIndex, bool bypass, string author)
        {
            if (causeIndex >= 0 && causeIndex < Causes.Count)
            {
                Causes[causeIndex].IsBypassed = bypass;
                Causes[causeIndex].BypassAuthorizedBy = bypass ? author : string.Empty;
                EvaluateInterlocks();
            }
        }

        public void TriggerTrip(int causeIndex)
        {
            if (causeIndex >= 0 && causeIndex < Causes.Count)
            {
                Causes[causeIndex].Status = CauseStatus.Tripped;
                EvaluateInterlocks();
            }
        }

        public void ResetAllTrips()
        {
            for (int i = 0; i < Causes.Count; i++)
            {
                if (Causes[i].Status == CauseStatus.Tripped)
                {
                    Causes[i].Status = CauseStatus.Healthy;
                }
            }
            EvaluateInterlocks();
        }
    }
}
