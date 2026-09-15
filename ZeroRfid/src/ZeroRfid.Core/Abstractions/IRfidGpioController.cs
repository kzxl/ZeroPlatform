using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroRfid.Core.Models;

namespace ZeroRfid.Core.Abstractions;

/// <summary>
/// Capability interface for RFID readers supporting industrial General Purpose Input/Output (GPIO) pins.
/// </summary>
public interface IRfidGpioController
{
    /// <summary>
    /// Sets the digital output state of a General Purpose Output (GPO) pin (e.g. tower lights, buzzers, diverters).
    /// </summary>
    /// <param name="pinIndex">1-based GPO pin index.</param>
    /// <param name="isHigh">True to drive high (active), false to drive low (inactive).</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetGpoAsync(int pinIndex, bool isHigh, CancellationToken ct = default);

    /// <summary>
    /// Reads the current digital input state of a General Purpose Input (GPI) pin (e.g. photoelectric photocell sensors).
    /// </summary>
    /// <param name="pinIndex">1-based GPI pin index.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> GetGpiAsync(int pinIndex, CancellationToken ct = default);

    /// <summary>
    /// Event triggered when a GPI pin state changes.
    /// </summary>
    event EventHandler<GpioPinChangedEvent>? GpiChanged;
}
