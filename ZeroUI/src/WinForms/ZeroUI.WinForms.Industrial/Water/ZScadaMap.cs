using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    public class MapStation { public double X { get; set; } public double Y { get; set; } public string Label { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; }
    public class PipelineSegment { public MapStation Start { get; set; } public MapStation End { get; set; } }
    /// <summary>
    /// ZScadaMap WinForms control.
    /// </summary>
    public class ZScadaMap : Control
    {        public object BackgroundImage { get; set; }
        public System.Collections.Generic.List<MapStation> Stations { get; set; } = new System.Collections.Generic.List<MapStation>();
        public System.Collections.Generic.List<PipelineSegment> Pipelines { get; set; } = new System.Collections.Generic.List<PipelineSegment>();
        public event EventHandler StationClicked;
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroScadaMap is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroScadaMap : ZScadaMap { }

    [Obsolete("ScadaMap is deprecated.")]
    [ToolboxItem(false)]
    public class ScadaMap : ZScadaMap { }
}
