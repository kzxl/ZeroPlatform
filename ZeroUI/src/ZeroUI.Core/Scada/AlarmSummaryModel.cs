using System;

namespace ZeroUI.Core.Scada;

/// <summary>
/// Represents the priority level of an alarm.
/// </summary>
public enum AlarmPriority
{
    Critical = 1,
    High = 2,
    Medium = 3,
    Low = 4,
    Diagnostic = 5
}

/// <summary>
/// Represents the current state of an alarm.
/// </summary>
public enum AlarmState
{
    Active,
    Acknowledged,
    Cleared,
    Shelved,
    Suppressed,
    Disabled
}

/// <summary>
/// Represents a single alarm record in the SCADA system.
/// </summary>
public class AlarmRecord
{
    /// <summary> Gets or sets the tag name. </summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary> Gets or sets the description. </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary> Gets or sets the priority. </summary>
    public AlarmPriority Priority { get; set; }

    /// <summary> Gets or sets the current state. </summary>
    public AlarmState State { get; set; }

    /// <summary> Gets or sets the time the alarm was activated. </summary>
    public DateTime ActivatedTime { get; set; }

    /// <summary> Gets or sets the time the alarm was acknowledged. </summary>
    public DateTime? AcknowledgedTime { get; set; }

    /// <summary> Gets or sets the time the alarm was cleared. </summary>
    public DateTime? ClearedTime { get; set; }

    /// <summary> Gets or sets the user who acknowledged the alarm. </summary>
    public string? AcknowledgedBy { get; set; }

    /// <summary> Gets or sets the area the alarm belongs to. </summary>
    public string? Area { get; set; }

    /// <summary> Gets or sets the associated value. </summary>
    public double? Value { get; set; }

    /// <summary> Gets or sets the limit for the alarm. </summary>
    public double? Limit { get; set; }

    /// <summary> Gets or sets the engineering unit. </summary>
    public string? Unit { get; set; }

    /// <summary> Gets or sets user-defined tag data. </summary>
    public object? Tag { get; set; }
}

/// <summary>
/// Event arguments for an action performed on an alarm.
/// </summary>
public class AlarmActionEventArgs : EventArgs
{
    /// <summary> Gets the alarm associated with the action. </summary>
    public AlarmRecord Alarm { get; }

    /// <summary> Initializes a new instance of the <see cref="AlarmActionEventArgs"/> class. </summary>
    /// <param name="alarm">The alarm.</param>
    public AlarmActionEventArgs(AlarmRecord alarm)
    {
        Alarm = alarm ?? throw new ArgumentNullException(nameof(alarm));
    }
}

/// <summary>
/// Event arguments when an alarm is selected.
/// </summary>
public class AlarmSelectedEventArgs : EventArgs
{
    /// <summary> Gets the alarm that was selected. </summary>
    public AlarmRecord Alarm { get; }

    /// <summary> Initializes a new instance of the <see cref="AlarmSelectedEventArgs"/> class. </summary>
    /// <param name="alarm">The alarm.</param>
    public AlarmSelectedEventArgs(AlarmRecord alarm)
    {
        Alarm = alarm ?? throw new ArgumentNullException(nameof(alarm));
    }
}
