using System.Collections.Generic;

namespace ZeroRfid.Simulator;

/// <summary>
/// Predefined industrial operational scenarios for RFID simulation.
/// </summary>
public enum SimulationScenario
{
    /// <summary>Simulates a pallet passing from outside to inside through a gate (Antenna 1 -> Antenna 2).</summary>
    WarehouseGateInbound = 0,

    /// <summary>Simulates a pallet passing from inside to outside through a gate (Antenna 2 -> Antenna 1).</summary>
    WarehouseGateOutbound = 1,

    /// <summary>Simulates reading a collection of static tags on shelves or boxes (bursts of repeated reads).</summary>
    StaticShelfInventory = 2,

    /// <summary>Simulates products passing periodically on a conveyor belt, synchronizing with photocell GPI.</summary>
    ConveyorBeltFeed = 3
}

/// <summary>
/// Options for configuring the SimulatorRfidReader.
/// </summary>
public sealed class SimulatorOptions
{
    /// <summary>Gets or sets the simulation scenario to execute. Default is StaticShelfInventory.</summary>
    public SimulationScenario Scenario { get; set; } = SimulationScenario.StaticShelfInventory;

    /// <summary>Gets or sets the number of simulated tags. Default is 20.</summary>
    public int TagCount { get; set; } = 20;

    /// <summary>Gets or sets the emission delay between tag reads in milliseconds. Default is 20ms.</summary>
    public int EmissionIntervalMs { get; set; } = 20;

    /// <summary>Gets or sets the number of active antennas on the simulated reader. Default is 4.</summary>
    public int AntennaCount { get; set; } = 4;

    /// <summary>Gets or sets optional custom EPCs to cycle through.</summary>
    public IReadOnlyList<string>? CustomEpcs { get; set; }

    /// <summary>Gets default simulator configuration.</summary>
    public static SimulatorOptions Default => new();
}
