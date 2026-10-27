using System;

namespace ZeroUI.Core.Water
{
    /// <summary>
    /// Clarifier and sedimentation basin mechanical arrangement.
    /// </summary>
    public enum ClarifierType
    {
        CircularCenterFeed,
        CircularPeripheralFeed,
        RectangularChainScraper
    }

    /// <summary>
    /// Operational alarm status for the clarifier sludge scraper mechanism.
    /// </summary>
    public enum ScraperTorqueStatus
    {
        Normal,
        HighTorqueWarning,
        OverTorqueTrip
    }

    /// <summary>
    /// Operating state and mechanical feedback of the clarifier scraper bridge/rake.
    /// </summary>
    public class ClarifierScraperState
    {
        public bool IsRunning { get; set; } = true;
        public double RotationSpeedRpm { get; set; } = 0.03; // Typical clarifier bridge 0.02 - 0.05 RPM
        public double TorquePct { get; set; } = 38.5; // 0.0 - 100.0%
        public double CurrentAngleDeg { get; set; } = 45.0; // 0.0 - 360.0 degrees
        public ScraperTorqueStatus TorqueStatus =>
            TorquePct >= 85.0 ? ScraperTorqueStatus.OverTorqueTrip :
            TorquePct >= 70.0 ? ScraperTorqueStatus.HighTorqueWarning :
            ScraperTorqueStatus.Normal;
    }

    /// <summary>
    /// Sludge blanket interface depth and effluent clarification metrics.
    /// </summary>
    public class ClarifierBlanketState
    {
        public double BlanketDepthM { get; set; } = 1.25; // meters from basin floor
        public double MaxAllowedDepthM { get; set; } = 2.20; // meters
        public double TssMgL { get; set; } = 3200.0; // Total Suspended Solids (mg/L) in sludge zone
        public double EffluentTurbidityNtu { get; set; } = 1.45; // NTU (< 2.0 is excellent)
        public bool IsBlanketHigh => BlanketDepthM >= MaxAllowedDepthM;
    }

    /// <summary>
    /// Basin physical dimensions and hydraulic process state.
    /// </summary>
    public class ClarifierHydraulics
    {
        public double DiameterM { get; set; } = 28.0; // 28-meter diameter clarifier
        public double SideWaterDepthM { get; set; } = 4.0; // 4.0-meter water depth
        public double InfluentFlowM3H { get; set; } = 850.0; // m3/h
        public double ReturnActivatedSludgeFlowM3H { get; set; } = 280.0; // RAS flow m3/h
        public double WasteActivatedSludgeFlowM3H { get; set; } = 15.0; // WAS flow m3/h

        /// <summary>
        /// Basin surface settling area in square meters: pi * r^2
        /// </summary>
        public double SurfaceAreaM2 => Math.PI * Math.Pow(DiameterM * 0.5, 2);

        /// <summary>
        /// Total water volume in cubic meters: Area * Depth
        /// </summary>
        public double VolumeM3 => SurfaceAreaM2 * SideWaterDepthM;
    }

    /// <summary>
    /// Pure computation engine for water/wastewater clarifier hydraulics,
    /// solids loading rates, retention times, and scraper protection logic.
    /// </summary>
    public class ClarifierEngine
    {
        public ClarifierType BasinType { get; set; } = ClarifierType.CircularCenterFeed;
        public ClarifierHydraulics Hydraulics { get; set; } = new ClarifierHydraulics();
        public ClarifierScraperState Scraper { get; set; } = new ClarifierScraperState();
        public ClarifierBlanketState Blanket { get; set; } = new ClarifierBlanketState();

        /// <summary>
        /// Surface Overflow Rate (SOR) in m3 / (m2 * day).
        /// Standard municipal primary clarifier: 30 - 50 m3/(m2*d); secondary: 16 - 32 m3/(m2*d).
        /// </summary>
        public double SurfaceOverflowRateM3M2Day
        {
            get
            {
                double area = Hydraulics.SurfaceAreaM2;
                if (area <= 0.0) return 0.0;
                double flowDaily = Hydraulics.InfluentFlowM3H * 24.0;
                return flowDaily / area;
            }
        }

        /// <summary>
        /// Hydraulic Retention Time (HRT) in hours: Volume / Flow.
        /// Typical range: 2.0 - 4.0 hours.
        /// </summary>
        public double HydraulicRetentionTimeHours
        {
            get
            {
                if (Hydraulics.InfluentFlowM3H <= 0.0) return 0.0;
                return Hydraulics.VolumeM3 / Hydraulics.InfluentFlowM3H;
            }
        }

        /// <summary>
        /// Solids Loading Rate (SLR) in kg / (m2 * day).
        /// SLR = (Influent Flow + RAS Flow) * MLSS / Area.
        /// </summary>
        public double SolidsLoadingRateKgM2Day
        {
            get
            {
                double area = Hydraulics.SurfaceAreaM2;
                if (area <= 0.0) return 0.0;

                double totalFlowM3Day = (Hydraulics.InfluentFlowM3H + Hydraulics.ReturnActivatedSludgeFlowM3H) * 24.0;
                // Tss in mg/L is equivalent to g/m3. Divided by 1000 gives kg/m3.
                double solidsKgM3 = Blanket.TssMgL / 1000.0;
                double totalSolidsKgDay = totalFlowM3Day * solidsKgM3;
                return totalSolidsKgDay / area;
            }
        }

        /// <summary>
        /// Advances the bridge rotation angle by a time step.
        /// </summary>
        /// <param name="deltaSeconds">Elapsed time in seconds.</param>
        public void AdvanceRotation(double deltaSeconds)
        {
            if (!Scraper.IsRunning || Scraper.TorqueStatus == ScraperTorqueStatus.OverTorqueTrip)
                return;

            double degPerSec = (Scraper.RotationSpeedRpm * 360.0) / 60.0;
            Scraper.CurrentAngleDeg = (Scraper.CurrentAngleDeg + degPerSec * deltaSeconds) % 360.0;
            if (Scraper.CurrentAngleDeg < 0.0)
                Scraper.CurrentAngleDeg += 360.0;
        }

        /// <summary>
        /// Validates whether operating parameters remain within safe process design envelopes.
        /// </summary>
        public bool IsOperatingNormally =>
            Scraper.TorqueStatus == ScraperTorqueStatus.Normal &&
            !Blanket.IsBlanketHigh &&
            Blanket.EffluentTurbidityNtu <= 5.0 &&
            SurfaceOverflowRateM3M2Day <= 60.0;
    }
}
