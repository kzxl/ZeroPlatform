using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Bms
{
    /// <summary>
    /// Zone occupancy and thermal conditioning operational state.
    /// </summary>
    public enum ZoneOccupancyMode
    {
        Unoccupied,
        Standby,
        Occupied,
        HolidayOverride
    }

    /// <summary>
    /// Represents an assigned time window within a weekly schedule.
    /// </summary>
    public class ScheduleTimeBlock
    {
        public DayOfWeek Day { get; set; } = DayOfWeek.Monday;
        public TimeSpan StartTime { get; set; } = new TimeSpan(8, 0, 0);
        public TimeSpan EndTime { get; set; } = new TimeSpan(18, 0, 0);
        public ZoneOccupancyMode Mode { get; set; } = ZoneOccupancyMode.Occupied;
        public double CoolingSetpointC { get; set; } = 23.0;
        public double HeatingSetpointC { get; set; } = 20.5;

        public bool Contains(TimeSpan time)
        {
            return time >= StartTime && time < EndTime;
        }
    }

    /// <summary>
    /// Holds the complete 7-day schedule configuration for an individual building zone.
    /// </summary>
    public class ZoneSchedule
    {
        public string ZoneId { get; set; } = "Z-01";
        public string ZoneName { get; set; } = "Floor 3 - Open Office";
        public ZoneOccupancyMode DefaultMode { get; set; } = ZoneOccupancyMode.Unoccupied;
        public double DefaultCoolingSetpointC { get; set; } = 28.0;
        public double DefaultHeatingSetpointC { get; set; } = 16.0;
        public List<ScheduleTimeBlock> Blocks { get; } = new List<ScheduleTimeBlock>();
        public HashSet<DateTime> Holidays { get; } = new HashSet<DateTime>();

        public ZoneSchedule()
        {
        }

        public ZoneSchedule(string id, string name)
        {
            ZoneId = id;
            ZoneName = name;
        }
    }

    /// <summary>
    /// Pure computation engine for Multi-Zone 7-Day Time Schedules, occupancy determination, and deadband validation.
    /// </summary>
    public class ZoneSchedulerEngine
    {
        public List<ZoneSchedule> Schedules { get; } = new List<ZoneSchedule>();

        public ZoneSchedulerEngine()
        {
            // Seed a default standard commercial zone schedule
            var defaultZone = CreateDefaultCommercialSchedule("Z-01", "Level 1 Office Zone");
            Schedules.Add(defaultZone);
        }

        /// <summary>
        /// Creates a standard commercial Monday–Friday schedule (8:00–18:00 Occupied, 18:00–21:00 Standby).
        /// </summary>
        public static ZoneSchedule CreateDefaultCommercialSchedule(string id, string name)
        {
            var schedule = new ZoneSchedule(id, name);

            DayOfWeek[] workdays = new[]
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                DayOfWeek.Thursday, DayOfWeek.Friday
            };

            foreach (var day in workdays)
            {
                // Core comfort occupied block (08:00 - 18:00)
                schedule.Blocks.Add(new ScheduleTimeBlock
                {
                    Day = day,
                    StartTime = new TimeSpan(8, 0, 0),
                    EndTime = new TimeSpan(18, 0, 0),
                    Mode = ZoneOccupancyMode.Occupied,
                    CoolingSetpointC = 23.0,
                    HeatingSetpointC = 20.5
                });

                // Evening standby block (18:00 - 21:00)
                schedule.Blocks.Add(new ScheduleTimeBlock
                {
                    Day = day,
                    StartTime = new TimeSpan(18, 0, 0),
                    EndTime = new TimeSpan(21, 0, 0),
                    Mode = ZoneOccupancyMode.Standby,
                    CoolingSetpointC = 25.5,
                    HeatingSetpointC = 18.5
                });
            }

            return schedule;
        }

        /// <summary>
        /// Resolves the effective occupancy mode for a given schedule at the specified timestamp.
        /// </summary>
        public static ZoneOccupancyMode GetEffectiveMode(ZoneSchedule schedule, DateTime dateTime)
        {
            if (schedule == null)
                return ZoneOccupancyMode.Unoccupied;

            // Check holiday calendar
            if (schedule.Holidays.Contains(dateTime.Date))
                return ZoneOccupancyMode.HolidayOverride;

            DayOfWeek day = dateTime.DayOfWeek;
            TimeSpan time = dateTime.TimeOfDay;

            for (int i = 0; i < schedule.Blocks.Count; i++)
            {
                var block = schedule.Blocks[i];
                if (block.Day == day && block.Contains(time))
                    return block.Mode;
            }

            return schedule.DefaultMode;
        }

        /// <summary>
        /// Resolves the active cooling and heating temperature setpoints for a given zone at a timestamp.
        /// </summary>
        public static void GetActiveSetpoints(
            ZoneSchedule schedule,
            DateTime dateTime,
            out double coolingC,
            out double heatingC)
        {
            if (schedule == null)
            {
                coolingC = 28.0;
                heatingC = 16.0;
                return;
            }

            if (schedule.Holidays.Contains(dateTime.Date))
            {
                coolingC = schedule.DefaultCoolingSetpointC;
                heatingC = schedule.DefaultHeatingSetpointC;
                return;
            }

            DayOfWeek day = dateTime.DayOfWeek;
            TimeSpan time = dateTime.TimeOfDay;

            for (int i = 0; i < schedule.Blocks.Count; i++)
            {
                var block = schedule.Blocks[i];
                if (block.Day == day && block.Contains(time))
                {
                    coolingC = block.CoolingSetpointC;
                    heatingC = block.HeatingSetpointC;
                    return;
                }
            }

            coolingC = schedule.DefaultCoolingSetpointC;
            heatingC = schedule.DefaultHeatingSetpointC;
        }

        /// <summary>
        /// Validates that cooling setpoint is sufficiently higher than heating setpoint to prevent simultaneous heating and cooling.
        /// Standard ASHRAE deadband minimum is 2.0°C (approx 4.0°F).
        /// </summary>
        public static bool ValidateDeadband(double coolingSetpointC, double heatingSetpointC, double minDeadbandC = 2.0)
        {
            return (coolingSetpointC - heatingSetpointC) >= minDeadbandC;
        }

        /// <summary>
        /// Validates that time blocks for the same day do not have conflicting, overlapping time ranges.
        /// </summary>
        public static bool DetectTimeBlockOverlaps(ZoneSchedule schedule)
        {
            if (schedule == null || schedule.Blocks.Count <= 1)
                return false;

            for (int i = 0; i < schedule.Blocks.Count; i++)
            {
                var a = schedule.Blocks[i];
                for (int j = i + 1; j < schedule.Blocks.Count; j++)
                {
                    var b = schedule.Blocks[j];
                    if (a.Day == b.Day)
                    {
                        // Overlap condition: StartA < EndB and EndA > StartB
                        if (a.StartTime < b.EndTime && a.EndTime > b.StartTime)
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
