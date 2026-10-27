using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Process
{
    /// <summary>
    /// ISO 14644-1 and EU GMP Annex 1 Cleanroom classification grades.
    /// </summary>
    public enum IsoCleanroomClass
    {
        IsoClass5, // EU GMP Grade A/B (Aseptic Core / Laminar Flow Hoods)
        IsoClass7, // EU GMP Grade C (Buffer Prep / Component Processing)
        IsoClass8, // EU GMP Grade D (Gowning / Material Transfer Airlocks)
        Unclassified // CNC (Controlled Non-Classified)
    }

    /// <summary>
    /// Telemetry and air containment parameters for an individual cleanroom suite or airlock.
    /// </summary>
    public class CleanroomZone
    {
        public string ZoneTag { get; set; } = "CR-101";
        public string ZoneName { get; set; } = "Aseptic Core";
        public IsoCleanroomClass IsoClass { get; set; } = IsoCleanroomClass.IsoClass5;

        // Differential Pressure Cascade (Pascals relative to ambient atmospheric baseline)
        public double DifferentialPressurePa { get; set; } = 48.5; // Pa
        public double MinRequiredPressurePa { get; set; } = 45.0; // Pa

        // Airborne Non-Viable Particulates (Counts per cubic meter)
        public double ParticleCount05Um { get; set; } = 850.0; // 0.5 um particles/m3 (ISO 5 limit: 3,520)
        public double ParticleCount50Um { get; set; } = 4.0;   // 5.0 um particles/m3 (ISO 5 limit: 29)

        // Air Handling & Ventilation
        public double AirChangesPerHour { get; set; } = 65.0; // ACH (ISO 5 typically 60 - 240 ACH)
        public double MinRequiredAch { get; set; } = 50.0;

        // Environmental Comfort
        public double RoomTempC { get; set; } = 19.5; // 18 - 22°C standard
        public double RelativeHumidityPct { get; set; } = 44.0; // 30 - 50% standard

        public double MaxAllowed05Um => IsoClass switch
        {
            IsoCleanroomClass.IsoClass5 => 3520.0,
            IsoCleanroomClass.IsoClass7 => 352000.0,
            IsoCleanroomClass.IsoClass8 => 3520000.0,
            _ => 10000000.0
        };

        public double MaxAllowed50Um => IsoClass switch
        {
            IsoCleanroomClass.IsoClass5 => 29.0,
            IsoCleanroomClass.IsoClass7 => 2900.0,
            IsoCleanroomClass.IsoClass8 => 29000.0,
            _ => 100000.0
        };

        public bool IsPressureOk => DifferentialPressurePa >= MinRequiredPressurePa;
        public bool AreParticlesOk => ParticleCount05Um <= MaxAllowed05Um && ParticleCount50Um <= MaxAllowed50Um;
        public bool IsAchOk => AirChangesPerHour >= MinRequiredAch;
        public bool IsCompliant => IsPressureOk && AreParticlesOk && IsAchOk;
    }

    /// <summary>
    /// Pure computation engine for cleanroom pressure cascade validation,
    /// ISO 14644-1 particulate classification compliance, and cross-contamination barrier monitoring.
    /// </summary>
    public class CleanroomEngine
    {
        private readonly List<CleanroomZone> _zones = new List<CleanroomZone>();

        public List<CleanroomZone> Zones => _zones;

        public CleanroomEngine()
        {
            SeedDefaultCascade();
        }

        public void SeedDefaultCascade()
        {
            _zones.Clear();
            // Airlock -> Gowning -> Buffer Prep -> Aseptic Core (Positive Pressure Cascade Staircase)
            _zones.Add(new CleanroomZone
            {
                ZoneTag = "PAL-101",
                ZoneName = "Personnel Airlock (PAL)",
                IsoClass = IsoCleanroomClass.IsoClass8,
                DifferentialPressurePa = 15.0,
                MinRequiredPressurePa = 12.5,
                AirChangesPerHour = 25.0,
                MinRequiredAch = 20.0,
                ParticleCount05Um = 125000.0,
                ParticleCount50Um = 980.0
            });

            _zones.Add(new CleanroomZone
            {
                ZoneTag = "GOWN-102",
                ZoneName = "Primary Gowning Room",
                IsoClass = IsoCleanroomClass.IsoClass7,
                DifferentialPressurePa = 30.0,
                MinRequiredPressurePa = 27.5,
                AirChangesPerHour = 38.0,
                MinRequiredAch = 30.0,
                ParticleCount05Um = 42000.0,
                ParticleCount50Um = 210.0
            });

            _zones.Add(new CleanroomZone
            {
                ZoneTag = "PREP-103",
                ZoneName = "Buffer Preparation Suite",
                IsoClass = IsoCleanroomClass.IsoClass7,
                DifferentialPressurePa = 45.0,
                MinRequiredPressurePa = 42.5,
                AirChangesPerHour = 45.0,
                MinRequiredAch = 40.0,
                ParticleCount05Um = 18000.0,
                ParticleCount50Um = 75.0
            });

            _zones.Add(new CleanroomZone
            {
                ZoneTag = "CORE-104",
                ZoneName = "Aseptic Filling Core (Grade A/B)",
                IsoClass = IsoCleanroomClass.IsoClass5,
                DifferentialPressurePa = 60.0,
                MinRequiredPressurePa = 57.5,
                AirChangesPerHour = 75.0,
                MinRequiredAch = 60.0,
                ParticleCount05Um = 620.0,
                ParticleCount50Um = 3.0
            });
        }

        /// <summary>
        /// Validates that the differential pressure gradient increases monotonically from entry airlock to sterile core.
        /// An outward cascade gradient prevents airborne microbial ingress into high-classification zones.
        /// </summary>
        public bool ValidateCascadeGradient(out string? breachDescription)
        {
            breachDescription = null;

            for (int i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                if (!z.IsPressureOk)
                {
                    breachDescription = $"Low pressure in zone {z.ZoneTag} ({z.DifferentialPressurePa:F1} Pa < min {z.MinRequiredPressurePa:F1} Pa)";
                    return false;
                }

                if (i > 0)
                {
                    var prevZ = _zones[i - 1];
                    if (z.DifferentialPressurePa <= prevZ.DifferentialPressurePa)
                    {
                        breachDescription = $"Pressure reversal detected: {z.ZoneTag} ({z.DifferentialPressurePa:F1} Pa) <= {prevZ.ZoneTag} ({prevZ.DifferentialPressurePa:F1} Pa)";
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// True if all cleanroom zones comply with ISO particulate limits and pressure envelopes.
        /// </summary>
        public bool IsOverallFacilityCompliant => ValidateCascadeGradient(out _) && _zones.TrueForAll(z => z.IsCompliant);
    }
}
