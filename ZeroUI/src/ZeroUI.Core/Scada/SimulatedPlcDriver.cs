using System;
using System.Threading;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// Autonomous real-time simulation driver generating industrial PLC telemetry
    /// for closed-loop plant pipelines (Tanks, Pipes, Pumps, Valves, PID Loops, Alarms).
    /// </summary>
    public static class SimulatedPlcDriver
    {
        private static Timer? _simTimer;
        private static bool _isRunning;
        private static double _timeStep;

        // Dynamic process simulation states
        public static double TankLevel { get; set; } = 65.0;      // 0 - 100 %
        public static double BoilerPressure { get; set; } = 42.5; // 0 - 120 PSI
        public static double PumpRpm { get; set; } = 2850.0;      // 0 - 3600 RPM
        public static double ValvePosition { get; set; } = 75.0;  // 0 - 100 % (Open)
        public static double FlowVelocity { get; set; } = 2.4;    // 0 - 5.0 m/s
        public static bool PumpRunning { get; set; } = true;
        public static bool ValveOpen { get; set; } = true;
        public static bool EmergencyStop { get; set; } = false;

        // PID Closed-Loop states
        public static double PidSetPoint { get; set; } = 50.0;
        public static double PidProcessVariable { get; set; } = 48.2;
        public static double PidOutputMv { get; set; } = 62.0;

        // Alarm flags
        public static bool AlarmHighPressure { get; set; } = false;
        public static bool AlarmLowLevel { get; set; } = false;
        public static bool AlarmPumpTrip { get; set; } = false;

        public static bool IsRunning => _isRunning;

        /// <summary>
        /// Starts the PLC background telemetry broadcast engine (50ms scan interval).
        /// </summary>
        public static void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _simTimer = new Timer(OnTick, null, 100, 50);
        }

        /// <summary>
        /// Stops the background telemetry engine.
        /// </summary>
        public static void Stop()
        {
            _isRunning = false;
            _simTimer?.Dispose();
            _simTimer = null;
        }

        private static void OnTick(object? state)
        {
            if (!_isRunning) return;

            _timeStep += 0.05;
            double noise = (Math.Sin(_timeStep * 3.7) * 0.4) + (Math.Cos(_timeStep * 7.1) * 0.2);

            if (EmergencyStop)
            {
                PumpRunning = false;
                ValveOpen = false;
                FlowVelocity = Math.Max(0, FlowVelocity - 0.2);
                PumpRpm = Math.Max(0, PumpRpm - 200);
                BoilerPressure = Math.Max(10, BoilerPressure - 0.5);
            }
            else
            {
                // Process simulation dynamics
                if (ValveOpen && PumpRunning)
                {
                    FlowVelocity = Math.Max(0.5, Math.Min(4.8, 1.2 + (ValvePosition / 100.0 * 2.8) + noise * 0.3));
                    PumpRpm = Math.Max(0, Math.Min(3600, 2400 + (ValvePosition * 10) + noise * 30));
                    BoilerPressure = Math.Max(15, Math.Min(110, 35 + (PumpRpm / 100.0 * 0.7) + (ValveOpen ? 0 : 25) + noise));
                    TankLevel = 50.0 + (Math.Sin(_timeStep * 0.4) * 25.0);
                }
                else if (!ValveOpen && PumpRunning)
                {
                    // Deadhead backpressure builds rapidly
                    FlowVelocity = 0.0;
                    PumpRpm = 2900 + noise * 20;
                    BoilerPressure = Math.Min(115, BoilerPressure + 1.2);
                }
                else
                {
                    FlowVelocity = 0.0;
                    PumpRpm = Math.Max(0, PumpRpm - 150);
                    BoilerPressure = Math.Max(14, BoilerPressure - 0.8);
                }

                // Automatic alarm triggers
                AlarmHighPressure = BoilerPressure > 85.0;
                AlarmLowLevel = TankLevel < 20.0;
                AlarmPumpTrip = PumpRunning && BoilerPressure > 95.0;

                // PID closed loop simulation: PV asymptotically approaches SP
                double error = PidSetPoint - PidProcessVariable;
                PidOutputMv = Math.Max(0, Math.Min(100, PidOutputMv + (error * 0.15)));
                PidProcessVariable += (error * 0.08) + (noise * 0.1);
            }

            // Publish through ZeroTagEngine
            ZeroTagEngine.SetTagValue("Line1.Tank.Level", Math.Round(TankLevel, 1));
            ZeroTagEngine.SetTagValue("Line1.Boiler.Pressure", Math.Round(BoilerPressure, 1));
            ZeroTagEngine.SetTagValue("Line1.Pump.SpeedRpm", (int)PumpRpm);
            ZeroTagEngine.SetTagValue("Line1.Pump.Running", PumpRunning);
            ZeroTagEngine.SetTagValue("Line1.Valve.Open", ValveOpen);
            ZeroTagEngine.SetTagValue("Line1.Valve.Position", (int)ValvePosition);
            ZeroTagEngine.SetTagValue("Line1.Flow.Velocity", Math.Round(FlowVelocity, 2));
            ZeroTagEngine.SetTagValue("Line1.PID.SP", Math.Round(PidSetPoint, 1));
            ZeroTagEngine.SetTagValue("Line1.PID.PV", Math.Round(PidProcessVariable, 1));
            ZeroTagEngine.SetTagValue("Line1.PID.MV", Math.Round(PidOutputMv, 1));
            ZeroTagEngine.SetTagValue("Line1.Alarm.HighPressure", AlarmHighPressure);
            ZeroTagEngine.SetTagValue("Line1.Alarm.LowLevel", AlarmLowLevel);
            ZeroTagEngine.SetTagValue("Line1.Alarm.PumpTrip", AlarmPumpTrip);
            ZeroTagEngine.SetTagValue("Line1.Alarm.EmergencyStop", EmergencyStop);
        }

        public static void ToggleValve()
        {
            ValveOpen = !ValveOpen;
            ValvePosition = ValveOpen ? 80.0 : 0.0;
        }

        public static void TogglePump()
        {
            PumpRunning = !PumpRunning;
        }

        public static void InjectPressureSpike()
        {
            BoilerPressure = 98.5;
            AlarmHighPressure = true;
        }

        public static void ToggleEmergencyStop()
        {
            EmergencyStop = !EmergencyStop;
        }
    }
}
