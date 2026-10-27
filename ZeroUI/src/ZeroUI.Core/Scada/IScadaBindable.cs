using System;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// Contract for visual components that dynamically bind to real-time SCADA telemetry tags.
    /// </summary>
    public interface IScadaBindable
    {
        /// <summary>
        /// Gets or sets the target SCADA tag path (e.g., "Line1.Boiler.Pressure").
        /// </summary>
        string? BoundTagPath { get; set; }

        /// <summary>
        /// Called when the bound tag value is updated by the Tag Engine.
        /// Guaranteed to run on the UI dispatcher/message pump if registered through TagEngine.
        /// </summary>
        /// <param name="tag">Snapshot of the updated telemetry tag.</param>
        void OnTagValueChanged(IScadaTag tag);
    }
}
