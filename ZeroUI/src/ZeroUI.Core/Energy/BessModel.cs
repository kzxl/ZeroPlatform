using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Energy
{
    /// <summary>
    /// Battery Energy Storage System (BESS) thermal runaway risk severity.
    /// </summary>
    public enum ThermalRunawayRisk
    {
        Normal,
        Elevated,
        Warning,
        PrecursorAlarm
    }

    /// <summary>
    /// Battery module containing a series string of lithium cells (LFP or NMC).
    /// </summary>
    public class BessModule
    {
        public int ModuleId { get; set; } = 1;
        public string Name { get; set; } = "Module 1";
        public double[] CellVoltages { get; set; }
        public double TemperatureC { get; set; } = 28.5;
        public double RateOfTemperatureRiseCpm { get; set; } = 0.1; // °C / minute

        public BessModule(int moduleId, string name, int cellCount = 16, double nominalV = 3.25)
        {
            ModuleId = moduleId;
            Name = name;
            CellVoltages = new double[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                CellVoltages[i] = nominalV + (i % 3) * 0.005; // Slight normal variation
            }
        }

        public double MinCellVoltageV
        {
            get
            {
                if (CellVoltages == null || CellVoltages.Length == 0) return 0.0;
                double min = CellVoltages[0];
                for (int i = 1; i < CellVoltages.Length; i++)
                    if (CellVoltages[i] < min) min = CellVoltages[i];
                return min;
            }
        }

        public double MaxCellVoltageV
        {
            get
            {
                if (CellVoltages == null || CellVoltages.Length == 0) return 0.0;
                double max = CellVoltages[0];
                for (int i = 1; i < CellVoltages.Length; i++)
                    if (CellVoltages[i] > max) max = CellVoltages[i];
                return max;
            }
        }

        /// <summary>
        /// Maximum cell voltage imbalance in millivolts (mV).
        /// </summary>
        public double DeltaCellMv => (MaxCellVoltageV - MinCellVoltageV) * 1000.0;
    }

    /// <summary>
    /// BESS high-voltage battery storage rack enclosure.
    /// </summary>
    public class BessRack
    {
        public string RackId { get; set; } = "RACK-01";
        public string Name { get; set; } = "BESS Rack Unit 1";
        public List<BessModule> Modules { get; } = new List<BessModule>();
        public double StringVoltageV { get; set; } = 832.0;
        public double CurrentAmps { get; set; } = 125.0; // Positive = Charging, Negative = Discharging
        public double PowerKw => (StringVoltageV * CurrentAmps) / 1000.0;
        public double SocPct { get; set; } = 84.0; // State of Charge %
        public double SohPct { get; set; } = 97.5; // State of Health %

        public BessRack()
        {
            // Seed 8 modules per rack
            for (int i = 1; i <= 8; i++)
            {
                Modules.Add(new BessModule(i, $"MOD-{i:00}", 16, 3.25));
            }
        }
    }

    /// <summary>
    /// Pure computation engine for BESS cell voltage balancing, state of charge, and thermal runaway heuristics.
    /// </summary>
    public class BessEngine
    {
        public BessRack Rack { get; } = new BessRack();

        /// <summary>
        /// Evaluates thermal runaway precursor risk across a battery module.
        /// Benchmark: High rate of temperature rise (dT/dt >= 1.0°C/min) combined with high cell delta (>= 50mV)
        /// indicates internal short-circuit / venting precursor.
        /// </summary>
        public static ThermalRunawayRisk EvaluateRunawayRisk(BessModule module)
        {
            if (module == null) return ThermalRunawayRisk.Normal;

            double deltaMv = module.DeltaCellMv;
            double tempRise = module.RateOfTemperatureRiseCpm;
            double temp = module.TemperatureC;

            if (temp >= 65.0 || (tempRise >= 1.0 && deltaMv >= 50.0))
                return ThermalRunawayRisk.PrecursorAlarm;

            if (temp >= 50.0 || tempRise >= 0.6 || deltaMv >= 45.0)
                return ThermalRunawayRisk.Warning;

            if (temp >= 40.0 || deltaMv >= 30.0)
                return ThermalRunawayRisk.Elevated;

            return ThermalRunawayRisk.Normal;
        }

        /// <summary>
        /// Maximum cell voltage delta across all modules in the rack.
        /// </summary>
        public double RackMaxDeltaCellMv
        {
            get
            {
                double max = 0.0;
                for (int i = 0; i < Rack.Modules.Count; i++)
                {
                    double d = Rack.Modules[i].DeltaCellMv;
                    if (d > max) max = d;
                }
                return max;
            }
        }

        /// <summary>
        /// Overall highest thermal runaway risk evaluated across all modules in the rack.
        /// </summary>
        public ThermalRunawayRisk OverallRisk
        {
            get
            {
                ThermalRunawayRisk worst = ThermalRunawayRisk.Normal;
                for (int i = 0; i < Rack.Modules.Count; i++)
                {
                    var r = EvaluateRunawayRisk(Rack.Modules[i]);
                    if (r > worst) worst = r;
                }
                return worst;
            }
        }
    }
}
