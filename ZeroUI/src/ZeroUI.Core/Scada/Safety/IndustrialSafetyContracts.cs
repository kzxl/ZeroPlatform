using System;

namespace ZeroUI.Core.Scada.Safety
{
    /// <summary>
    /// Operating state of an IEC 60947-5-5 Emergency Stop pushbutton.
    /// </summary>
    public enum EStopStatus
    {
        /// <summary>Normal operation: Safety contacts closed, machine permitted to run.</summary>
        Normal = 0,

        /// <summary>Tripped / Depressed: Mushroom head latched down, safety contacts open.</summary>
        Tripped = 1,

        /// <summary>Dual-channel mismatch / contact welding fault (e.g. SIL 3 safety relay fault).</summary>
        ChannelFault = 2
    }

    /// <summary>
    /// Mechanical unlatching / reset mode for Emergency Stop pushbutton.
    /// </summary>
    public enum EStopResetMode
    {
        /// <summary>Twist actuator clockwise to release (standard IEC 60947-5-5 mushroom head).</summary>
        TwistToReset = 0,

        /// <summary>Pull actuator outwards to release.</summary>
        PullToReset = 1,

        /// <summary>Key release mechanism (lockout key required to reset).</summary>
        KeyRelease = 2
    }

    /// <summary>
    /// Visual and functional state of an industrial multi-state indicator LED.
    /// Follows ISA-101 and IEC 60073 conventions.
    /// </summary>
    public enum MultiStateLedState
    {
        /// <summary>De-energized / Dark grey inactive state.</summary>
        Off = 0,

        /// <summary>Normal operation / Active running (Green).</summary>
        Normal = 1,

        /// <summary>Warning / Attention required / Standby ready (Amber / Yellow).</summary>
        Warning = 2,

        /// <summary>Alarm / Fault / Emergency trip condition (Red).</summary>
        Alarm = 3,

        /// <summary>Maintenance mode / Manual override / Test mode (Blue / Cyan).</summary>
        Maintenance = 4,

        /// <summary>Standby / Ready for cycle start (White / Light blue).</summary>
        Standby = 5,

        /// <summary>Unknown telemetry / Communication link loss (Muted grey with dotted contour).</summary>
        Unknown = 6
    }

    /// <summary>
    /// Blinking rate and modulation mode for industrial indicator LEDs.
    /// </summary>
    public enum MultiStateLedBlink
    {
        /// <summary>Steady continuous illumination.</summary>
        None = 0,

        /// <summary>Slow blink (1 Hz, 50% duty cycle) - advisory/warning.</summary>
        Slow = 1,

        /// <summary>Fast blink (2 Hz, 50% duty cycle) - unacknowledged alarm / hazard.</summary>
        Fast = 2,

        /// <summary>Sinusoidal breathing pulse glow (gentle industrial heartbeat).</summary>
        Pulse = 3
    }

    /// <summary>
    /// Physical geometric form factor for industrial indicator LEDs.
    /// </summary>
    public enum MultiStateLedShape
    {
        /// <summary>Classic circular 22mm pilot light bezel.</summary>
        Circle = 0,

        /// <summary>Square push-to-test or matrix annunciator block.</summary>
        Square = 1,

        /// <summary>Rounded rectangle panel status badge.</summary>
        RoundedRectangle = 2
    }

    /// <summary>
    /// Placement position of the text label relative to the indicator LED lens.
    /// </summary>
    public enum LedLabelPosition
    {
        /// <summary>No text label rendered.</summary>
        None = 0,

        /// <summary>Label centered below the LED lens.</summary>
        Bottom = 1,

        /// <summary>Label to the right of the LED lens.</summary>
        Right = 2,

        /// <summary>Label centered above the LED lens.</summary>
        Top = 3,

        /// <summary>Label to the left of the LED lens.</summary>
        Left = 4
    }

    /// <summary>
    /// Layout orientation of an edgewise panel meter.
    /// </summary>
    public enum EdgewiseOrientation
    {
        /// <summary>Vertical profile edgewise meter (typical SCADA high-density rack).</summary>
        Vertical = 0,

        /// <summary>Horizontal profile edgewise meter.</summary>
        Horizontal = 1
    }

    /// <summary>
    /// Visual pointer and readout style for an edgewise meter.
    /// </summary>
    public enum EdgewisePointerStyle
    {
        /// <summary>Mechanical triangular flag / blade pointer traveling along the scale edge.</summary>
        Flag = 0,

        /// <summary>Solid illuminated bargraph rising from Minimum to Value.</summary>
        Bar = 1,

        /// <summary>Fine luminous hairline reticle passing across the entire scale track.</summary>
        Line = 2
    }

    /// <summary>
    /// Safety operating state of an IEC 61496-1/-2 electro-sensitive protective light curtain.
    /// </summary>
    public enum LightCurtainState
    {
        /// <summary>All optical beams uninterrupted. Safety OSSD outputs energized (Machine permitted to run).</summary>
        Clear = 0,

        /// <summary>One or more optical beams interrupted by obstacle. OSSD outputs de-energized immediately (Trip).</summary>
        Tripped = 1,

        /// <summary>Muting sequence active (temporary safe bypass for legitimate material passage).</summary>
        Muted = 2,

        /// <summary>Fixed or floating blanking configured (selected beam zones disabled for conveyor/tooling).</summary>
        Blanked = 3,

        /// <summary>Hardware internal fault or optical alignment misalignment error.</summary>
        Fault = 4
    }

    /// <summary>
    /// Physical optical role of the light curtain bar.
    /// </summary>
    public enum LightCurtainRole
    {
        /// <summary>Optical Transmitter (Tx) with infrared emitting diodes.</summary>
        Transmitter = 0,

        /// <summary>Optical Receiver (Rx) with photodetectors and OSSD outputs.</summary>
        Receiver = 1,

        /// <summary>Integrated transceiver pair showing both bars with active infrared beam paths.</summary>
        IntegratedPair = 2
    }

    /// <summary>
    /// Event arguments for light curtain beam interruption events.
    /// </summary>
    public class LightCurtainTrippedEventArgs : EventArgs
    {
        /// <summary>0-based index of the first optical beam interrupted.</summary>
        public int BeamIndex { get; }

        /// <summary>Number of consecutive optical beams interrupted by the obstacle.</summary>
        public int BeamSpan { get; }

        /// <summary>UTC timestamp when the trip was detected.</summary>
        public DateTime Timestamp { get; }

        public LightCurtainTrippedEventArgs(int beamIndex, int beamSpan)
        {
            BeamIndex = beamIndex;
            BeamSpan = Math.Max(1, beamSpan);
            Timestamp = DateTime.UtcNow;
        }
    }
}
