using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// Industrial Single-Loop Process Controller Faceplate (PID Faceplate).
    /// Features simultaneous Process Variable (PV) and Setpoint (SP) vertical comparison bars,
    /// horizontal Manipulated Variable (MV 0-100%) output gauge, mode selectors (Auto/Man/Cas),
    /// Per-Monitor V2 High-DPI auto-scaling, and IScadaBindable tag engine integration.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroPidFaceplate.bmp")]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Industrial single-loop PID controller faceplate with PV, SP, MV and tuning metrics")]
    public partial class ZPidFaceplate : ControlBase, IScadaBindable
    {
        private string _loopTag = "PIC-101";
        private string _loopDescription = "Boiler Steam Header Pressure";
        private string _engineeringUnit = "PSI";
        private double _processVariable = 48.2;
        private double _setPoint = 50.0;
        private double _manipulatedVariable = 62.0; // 0 - 100%
        private double _minScale = 0.0;
        private double _maxScale = 100.0;
        private string _valueFormat = "0.0";
        private ZeroPidMode _mode = ZeroPidMode.Auto;

        // Tuning parameters
        private double _kp = 1.25;
        private double _ti = 18.0;
        private double _td = 2.5;

        // Interaction bounds (computed dynamically in OnPaint according to DpiScale)
        private Rectangle _btnAutoRect;
        private Rectangle _btnManRect;
        private Rectangle _btnCasRect;
        private Rectangle _btnSpPlusRect;
        private Rectangle _btnSpMinusRect;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Loop Identification")]
        [DefaultValue("PIC-101")]
        public string LoopTag
        {
            get => _loopTag;
            set { _loopTag = value ?? ""; Invalidate(); }
        }

        [Category("Loop Identification")]
        [DefaultValue("Boiler Steam Header Pressure")]
        public string LoopDescription
        {
            get => _loopDescription;
            set { _loopDescription = value ?? ""; Invalidate(); }
        }

        [Category("Loop Identification")]
        [DefaultValue("PSI")]
        public string EngineeringUnit
        {
            get => _engineeringUnit;
            set { _engineeringUnit = value ?? ""; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(48.2)]
        public double ProcessVariable
        {
            get => _processVariable;
            set { _processVariable = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(50.0)]
        public double SetPoint
        {
            get => _setPoint;
            set { _setPoint = Math.Max(_minScale, Math.Min(_maxScale, value)); Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(62.0)]
        public double ManipulatedVariable
        {
            get => _manipulatedVariable;
            set { _manipulatedVariable = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Format string for PV and SP numerical readouts. Defaults to '0.0'.")]
        [DefaultValue("0.0")]
        public string ValueFormat
        {
            get => _valueFormat;
            set { _valueFormat = value ?? "0.0"; Invalidate(); }
        }

        [Category("Scale Limits")]
        [DefaultValue(0.0)]
        public double MinScale
        {
            get => _minScale;
            set { _minScale = value; Invalidate(); }
        }

        [Category("Scale Limits")]
        [DefaultValue(100.0)]
        public double MaxScale
        {
            get => _maxScale;
            set { _maxScale = value; Invalidate(); }
        }

        [Category("Tuning")]
        [DefaultValue(1.25)]
        public double Kp
        {
            get => _kp;
            set { _kp = value; Invalidate(); }
        }

        [Category("Tuning")]
        [DefaultValue(18.0)]
        public double Ti
        {
            get => _ti;
            set { _ti = value; Invalidate(); }
        }

        [Category("Tuning")]
        [DefaultValue(2.5)]
        public double Td
        {
            get => _td;
            set { _td = value; Invalidate(); }
        }

        [Category("Control Loop")]
        [DefaultValue(ZeroPidMode.Auto)]
        public ZeroPidMode Mode
        {
            get => _mode;
            set { _mode = value; Invalidate(); }
        }

        public event EventHandler? SetPointChanged;
        public event EventHandler? ModeChanged;

        public ZPidFaceplate()
        {
            BackColor = Color.Transparent;
            Size = new Size(260, 310);
            Cursor = Cursors.Default;
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag == null) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnTagValueChanged(tag)));
                return;
            }

            if (tag.TagPath != null && tag.TagPath.EndsWith(".PV", StringComparison.OrdinalIgnoreCase) && tag.Value is double pv)
            {
                ProcessVariable = pv;
            }
            else if (tag.TagPath != null && tag.TagPath.EndsWith(".SP", StringComparison.OrdinalIgnoreCase) && tag.Value is double sp)
            {
                SetPoint = sp;
            }
            else if (tag.TagPath != null && tag.TagPath.EndsWith(".MV", StringComparison.OrdinalIgnoreCase) && tag.Value is double mv)
            {
                ManipulatedVariable = mv;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (_btnAutoRect.Contains(e.Location))
            {
                Mode = ZeroPidMode.Auto;
                ModeChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (_btnManRect.Contains(e.Location))
            {
                Mode = ZeroPidMode.Manual;
                ModeChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (_btnCasRect.Contains(e.Location))
            {
                Mode = ZeroPidMode.Cascade;
                ModeChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (_btnSpPlusRect.Contains(e.Location))
            {
                SetPoint = Math.Min(_maxScale, _setPoint + 1.0);
                SimulatedPlcDriver.PidSetPoint = _setPoint;
                SetPointChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (_btnSpMinusRect.Contains(e.Location))
            {
                SetPoint = Math.Max(_minScale, _setPoint - 1.0);
                SimulatedPlcDriver.PidSetPoint = _setPoint;
                SetPointChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZPidFaceplate"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroPidFaceplate is deprecated and will be removed in 5 release cycles. Please migrate to ZPidFaceplate instead.")]
    [ToolboxItem(false)]
    public class ZeroPidFaceplate : ZPidFaceplate
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPidFaceplate"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PidFaceplate is deprecated and will be removed in 5 release cycles. Please migrate to ZPidFaceplate instead.")]
    [ToolboxItem(false)]
    public class PidFaceplate : ZPidFaceplate
    {
    }

    #endregion
}
