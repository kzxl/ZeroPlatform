using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Bms
{
    /// <summary>
    /// Chiller compressor mechanical technology type.
    /// </summary>
    public enum ChillerCompressorType
    {
        Centrifugal,
        Screw,
        Scroll,
        MagneticLevitation
    }

    /// <summary>
    /// Current operational status of a water chiller.
    /// </summary>
    public enum ChillerStatus
    {
        Offline,
        Starting,
        Running,
        Faulted
    }

    /// <summary>
    /// Represents an individual industrial water-cooled or air-cooled chiller unit.
    /// </summary>
    public class ChillerUnit
    {
        public int ChillerId { get; set; } = 1;
        public string Name { get; set; } = "Chiller #1";
        public ChillerCompressorType CompressorType { get; set; } = ChillerCompressorType.Centrifugal;
        public ChillerStatus Status { get; set; } = ChillerStatus.Running;
        public double RatedTons { get; set; } = 500.0;
        public double PowerKw { get; set; } = 280.0;
        public double ChwSupplyTempC { get; set; } = 6.5; // Chilled Water Supply
        public double ChwReturnTempC { get; set; } = 12.0; // Chilled Water Return
        public double CwSupplyTempC { get; set; } = 29.5; // Condenser Water Supply
        public double CwReturnTempC { get; set; } = 35.0; // Condenser Water Return
        public double WaterFlowGpm { get; set; } = 1200.0; // Gallons per minute

        /// <summary>
        /// Chilled water delta temperature (°C).
        /// </summary>
        public double ChwDeltaTempC => Math.Max(0.0, ChwReturnTempC - ChwSupplyTempC);

        /// <summary>
        /// Active cooling load produced by this chiller in refrigeration tons (TR).
        /// </summary>
        public double ActualTons => Status == ChillerStatus.Running
            ? ChillerPlantEngine.CalculateCoolingLoadTons(WaterFlowGpm, ChwDeltaTempC)
            : 0.0;

        /// <summary>
        /// Specific power consumption efficiency in kW/Ton. Lower is more efficient.
        /// </summary>
        public double EfficiencyKwPerTon => ChillerPlantEngine.CalculateEfficiencyKwPerTon(PowerKw, ActualTons);

        /// <summary>
        /// Thermodynamic Coefficient of Performance (COP). Higher is more efficient.
        /// </summary>
        public double Cop => ChillerPlantEngine.CalculateCop(EfficiencyKwPerTon);
    }

    /// <summary>
    /// Represents an induced-draft or forced-draft cooling tower cell.
    /// </summary>
    public class CoolingTowerUnit
    {
        public int TowerId { get; set; } = 1;
        public string Name { get; set; } = "Tower #1";
        public bool IsFanRunning { get; set; } = true;
        public double FanRpm { get; set; } = 720.0;
        public double WaterTempInC { get; set; } = 35.0; // Entering condenser water
        public double WaterTempOutC { get; set; } = 29.5; // Leaving cold water basin
        public double AmbientWetBulbTempC { get; set; } = 24.0; // Outdoor wet-bulb

        /// <summary>
        /// Tower approach temperature (°C) = Leaving Water - Ambient Wet Bulb.
        /// Benchmark for heat rejection performance.
        /// </summary>
        public double ApproachTempC => ChillerPlantEngine.CalculateTowerApproach(WaterTempOutC, AmbientWetBulbTempC);

        /// <summary>
        /// Cooling tower range (°C) = Entering Water - Leaving Water.
        /// </summary>
        public double RangeTempC => Math.Max(0.0, WaterTempInC - WaterTempOutC);
    }

    /// <summary>
    /// Pure computation engine for Central Chiller Plants, thermodynamic efficiencies, and cooling loads.
    /// </summary>
    public class ChillerPlantEngine
    {
        public List<ChillerUnit> Chillers { get; } = new List<ChillerUnit>();
        public List<CoolingTowerUnit> Towers { get; } = new List<CoolingTowerUnit>();

        public ChillerPlantEngine()
        {
            // Default 2-Chiller, 2-Tower central plant configuration
            Chillers.Add(new ChillerUnit { ChillerId = 1, Name = "Chiller #1", RatedTons = 500, PowerKw = 280, Status = ChillerStatus.Running });
            Chillers.Add(new ChillerUnit { ChillerId = 2, Name = "Chiller #2", RatedTons = 500, PowerKw = 295, Status = ChillerStatus.Running });

            Towers.Add(new CoolingTowerUnit { TowerId = 1, Name = "Tower #1", FanRpm = 750, IsFanRunning = true });
            Towers.Add(new CoolingTowerUnit { TowerId = 2, Name = "Tower #2", FanRpm = 750, IsFanRunning = true });
        }

        /// <summary>
        /// Calculates cooling output in Refrigeration Tons (TR).
        /// Formula: Tons = (GPM * DeltaT_F) / 24 = (GPM * DeltaT_C * 1.8) / 24
        /// </summary>
        public static double CalculateCoolingLoadTons(double flowGpm, double deltaTempC)
        {
            if (flowGpm <= 0.0 || deltaTempC <= 0.0)
                return 0.0;

            double deltaTempF = deltaTempC * 1.8;
            return (flowGpm * deltaTempF) / 24.0;
        }

        /// <summary>
        /// Calculates chiller specific efficiency in kW/Ton.
        /// Typical centrifugal values: 0.50 - 0.70 kW/Ton (High efficiency), > 0.85 kW/Ton (Degraded).
        /// </summary>
        public static double CalculateEfficiencyKwPerTon(double powerKw, double loadTons)
        {
            if (loadTons <= 0.001 || powerKw <= 0.0)
                return 0.0;

            return powerKw / loadTons;
        }

        /// <summary>
        /// Converts kW/Ton to dimensionless Coefficient of Performance (COP).
        /// Standard: 1 Refrigeration Ton = 3.51685 kW thermal.
        /// COP = 3.51685 / (kW/Ton).
        /// </summary>
        public static double CalculateCop(double efficiencyKwPerTon)
        {
            if (efficiencyKwPerTon <= 0.001)
                return 0.0;

            return 3.51685 / efficiencyKwPerTon;
        }

        /// <summary>
        /// Calculates cooling tower approach temperature (°C).
        /// </summary>
        public static double CalculateTowerApproach(double leavingWaterTempC, double wetBulbTempC)
        {
            return Math.Max(0.0, leavingWaterTempC - wetBulbTempC);
        }

        /// <summary>
        /// Aggregates total rated tonnage across all configured chillers.
        /// </summary>
        public double TotalRatedTons
        {
            get
            {
                double total = 0.0;
                for (int i = 0; i < Chillers.Count; i++)
                    total += Chillers[i].RatedTons;
                return total;
            }
        }

        /// <summary>
        /// Aggregates active cooling load produced across all running chillers.
        /// </summary>
        public double TotalActualTons
        {
            get
            {
                double total = 0.0;
                for (int i = 0; i < Chillers.Count; i++)
                    total += Chillers[i].ActualTons;
                return total;
            }
        }

        /// <summary>
        /// Aggregates electrical power drawn across all running chillers in kW.
        /// </summary>
        public double TotalPowerKw
        {
            get
            {
                double total = 0.0;
                for (int i = 0; i < Chillers.Count; i++)
                {
                    if (Chillers[i].Status == ChillerStatus.Running)
                        total += Chillers[i].PowerKw;
                }
                return total;
            }
        }

        /// <summary>
        /// Plant-wide overall average efficiency in kW/Ton.
        /// </summary>
        public double PlantAverageKwPerTon => CalculateEfficiencyKwPerTon(TotalPowerKw, TotalActualTons);

        /// <summary>
        /// Plant-wide overall average COP.
        /// </summary>
        public double PlantAverageCop => CalculateCop(PlantAverageKwPerTon);
    }
}
