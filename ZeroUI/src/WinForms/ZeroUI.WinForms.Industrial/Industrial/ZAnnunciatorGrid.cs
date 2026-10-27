using System;

using ZeroUI.WinForms.Icons;using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public enum IsaAlarmState
    {
        Normal,
        Unacknowledged,
        Acknowledged,
        ReturnToNormal
    }

    public enum IsaAlarmSeverity
    {
        Critical,
        High,
        Medium,
        Low
    }

    public class IsaAlarmTile
    {
        public string TagPath { get; set; } = "";
        public string Title { get; set; } = "";
        public IsaAlarmSeverity Severity { get; set; } = IsaAlarmSeverity.High;
        public IsaAlarmState State { get; set; } = IsaAlarmState.Normal;
        public Rectangle Bounds { get; internal set; }
        public DateTime TriggeredTime { get; set; }
    }

    /// <summary>
    /// Industrial Alarm Annunciator Grid adhering strictly to standard ISA-18.2
    /// Alarm Management Life Cycle (Unacknowledged Fast Flash, Acknowledged Steady,
    /// Return-To-Normal Slow Flash, Integrated Command Bar: ACK, SILENCE, RESET, TEST).
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroDefaultControl.bmp")]
    public class ZAnnunciatorGrid : Control, IScadaBindable
    {
        private readonly List<IsaAlarmTile> _tiles = new List<IsaAlarmTile>();
        private int _columns = 4;
        private int _rows = 3;
        private IDisposable? _clockToken;
        private bool _lastBlinkFast;
        private bool _lastBlinkSlow;
        private bool _isTestMode;
        private bool _isSilenced;

        /// <summary>
        /// Gets whether the alarm audible horn/buzzer is silenced.
        /// </summary>
        [Category("Process Dynamics")]
        [Browsable(false)]
        public bool IsSilenced => _isSilenced;

        // Command Bar Buttons
        private Rectangle _btnAckRect;
        private Rectangle _btnSilenceRect;
        private Rectangle _btnResetRect;
        private Rectangle _btnTestRect;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Appearance")]
        [DefaultValue(4)]
        public int Columns
        {
            get => _columns;
            set { _columns = Math.Max(1, value); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(3)]
        public int Rows
        {
            get => _rows;
            set { _rows = Math.Max(1, value); Invalidate(); }
        }

        public IReadOnlyList<IsaAlarmTile> Tiles => _tiles.AsReadOnly();

        public event EventHandler? AlarmAcknowledged;
        public event EventHandler? AlarmReset;

        public ZAnnunciatorGrid()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(420, 240);

            ZeroTheme.ThemeChanged += OnThemeChanged;

            // Populate default industrial factory alarm matrix
            AddAlarm("Line1.Alarm.EmergencyStop", "EMERGENCY STOP (E-STOP)", IsaAlarmSeverity.Critical);
            AddAlarm("Line1.Alarm.HighPressure", "BOILER HIGH PRESSURE (>85 PSI)", IsaAlarmSeverity.Critical);
            AddAlarm("Line1.Alarm.PumpTrip", "PUMP 101 OVERLOAD / TRIP", IsaAlarmSeverity.High);
            AddAlarm("Line1.Alarm.LowLevel", "TANK 101 LOW LEVEL (<20%)", IsaAlarmSeverity.High);
            AddAlarm("Line1.Alarm.ValveStuck", "DISCHARGE VALVE FAIL-TO-OPEN", IsaAlarmSeverity.Medium);
            AddAlarm("Line1.Alarm.HighTemp", "FURNACE TEMP EXCEEDED (550°C)", IsaAlarmSeverity.High);
            AddAlarm("Line1.Alarm.Vibration", "MOTOR BEARING VIBRATION HIGH", IsaAlarmSeverity.Medium);
            AddAlarm("Line1.Alarm.FilterClog", "SUCTION STRAINER DP HIGH", IsaAlarmSeverity.Low);
            AddAlarm("Line1.Alarm.GasLeak", "CH4 GAS DETECTOR WARN", IsaAlarmSeverity.Critical);
            AddAlarm("Line1.Alarm.UpsPower", "UPS ON BATTERY BACKUP", IsaAlarmSeverity.Medium);
            AddAlarm("Line1.Alarm.PlcComm", "PLC COMM TIMEOUT (ET200)", IsaAlarmSeverity.High);
            AddAlarm("Line1.Alarm.DoorInterlock", "SAFETY ENCLOSURE DOOR OPEN", IsaAlarmSeverity.Medium);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!ZeroDesignHelper.IsInDesignMode(this))
            {
                _clockToken = ZeroAnimationClock.Subscribe(OnAnimationFrameTick);
                ZeroTagEngine.RegisterBindable(this);
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            _clockToken?.Dispose();
            _clockToken = null;
            ZeroTagEngine.UnregisterBindable(this);
        }

        private void OnAnimationFrameTick(double deltaSeconds, long frameCount)
        {
            bool hasFlashingAlarm = false;
            for (int i = 0; i < _tiles.Count; i++)
            {
                if (_tiles[i].State == IsaAlarmState.Unacknowledged || _tiles[i].State == IsaAlarmState.ReturnToNormal)
                {
                    hasFlashingAlarm = true;
                    break;
                }
            }

            if (hasFlashingAlarm)
            {
                bool bf = ZeroAnimationClock.BlinkFast;
                bool bs = ZeroAnimationClock.BlinkSlow;
                if (bf != _lastBlinkFast || bs != _lastBlinkSlow)
                {
                    _lastBlinkFast = bf;
                    _lastBlinkSlow = bs;
                    if (IsHandleCreated && Visible)
                    {
                        Invalidate();
                    }
                }
            }
        }

        private void OnThemeChanged(object? sender, EventArgs e) => Invalidate();

        /// <summary>
        /// Clears all alarm tiles in the annunciator matrix.
        /// </summary>
        public void ClearTiles()
        {
            _tiles.Clear();
            Invalidate();
        }

        public void AddAlarm(string tagPath, string title, IsaAlarmSeverity severity)
        {
            var tile = new IsaAlarmTile
            {
                TagPath = tagPath,
                Title = title,
                Severity = severity,
                State = IsaAlarmState.Normal
            };
            _tiles.Add(tile);
            Invalidate();
        }

        public void TriggerAlarm(string tagPath, bool active)
        {
            for (int i = 0; i < _tiles.Count; i++)
            {
                var t = _tiles[i];
                if (string.Equals(t.TagPath, tagPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (active)
                    {
                        if (t.State == IsaAlarmState.Normal)
                        {
                            t.State = IsaAlarmState.Unacknowledged;
                            t.TriggeredTime = DateTime.Now;
                            _isSilenced = false;
                        }
                    }
                    else
                    {
                        if (t.State == IsaAlarmState.Acknowledged)
                        {
                            t.State = IsaAlarmState.Normal;
                        }
                        else if (t.State == IsaAlarmState.Unacknowledged)
                        {
                            t.State = IsaAlarmState.ReturnToNormal;
                        }
                    }
                }
            }
            Invalidate();
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnTagValueChanged(tag)));
                return;
            }

            if (tag.Value is bool b)
            {
                TriggerAlarm(tag.TagPath, b);
            }
        }

        public void AcknowledgeAll()
        {
            for (int i = 0; i < _tiles.Count; i++)
            {
                if (_tiles[i].State == IsaAlarmState.Unacknowledged)
                {
                    _tiles[i].State = IsaAlarmState.Acknowledged;
                }
            }
            _isSilenced = true;
            AlarmAcknowledged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        public void Silence()
        {
            _isSilenced = true;
            Invalidate();
        }

        public void ResetAlarms()
        {
            for (int i = 0; i < _tiles.Count; i++)
            {
                if (_tiles[i].State == IsaAlarmState.ReturnToNormal)
                {
                    _tiles[i].State = IsaAlarmState.Normal;
                }
            }
            AlarmReset?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        public void ToggleTestMode()
        {
            _isTestMode = !_isTestMode;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (_btnAckRect.Contains(e.Location))
            {
                AcknowledgeAll();
                return;
            }
            if (_btnSilenceRect.Contains(e.Location))
            {
                Silence();
                return;
            }
            if (_btnResetRect.Contains(e.Location))
            {
                ResetAlarms();
                return;
            }
            if (_btnTestRect.Contains(e.Location))
            {
                ToggleTestMode();
                return;
            }

            // Click individual tile to ACK single alarm
            for (int i = 0; i < _tiles.Count; i++)
            {
                if (_tiles[i].Bounds.Contains(e.Location))
                {
                    if (_tiles[i].State == IsaAlarmState.Unacknowledged)
                    {
                        _tiles[i].State = IsaAlarmState.Acknowledged;
                        Invalidate();
                        break;
                    }
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var palette = ZeroTheme.Colors;
            bool isDark = ZeroTheme.IsDark;

            // 1. Outer Frame
            var borderRect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var brushBg = new SolidBrush(palette.CardBackground))
            {
                g.FillRectangle(brushBg, borderRect);
            }
            using (var penBorder = new Pen(palette.Border, 1.5f))
            {
                g.DrawRectangle(penBorder, borderRect);
            }

            // 2. Command Bar at Bottom (ACK, SILENCE, RESET, TEST)
            int cmdH = 34;
            var cmdRect = new Rectangle(0, Height - cmdH, Width, cmdH);
            using (var brushCmd = new SolidBrush(palette.HeaderBackground))
            {
                g.FillRectangle(brushCmd, cmdRect);
            }
            using (var penCmd = new Pen(palette.Border, 1f))
            {
                g.DrawLine(penCmd, 0, Height - cmdH, Width, Height - cmdH);
            }

            int btnW = 85;
            int btnH = 24;
            int btnY = Height - cmdH + (cmdH - btnH) / 2;

            _btnAckRect = new Rectangle(8, btnY, btnW, btnH);
            _btnSilenceRect = new Rectangle(100, btnY, btnW, btnH);
            _btnResetRect = new Rectangle(192, btnY, btnW, btnH);
            _btnTestRect = new Rectangle(Width - btnW - 8, btnY, btnW, btnH);

            DrawButton(g, _btnAckRect, "ACK", palette.Primary, Color.White);
            DrawButton(g, _btnSilenceRect, "SILENCE", palette.Surface, palette.TextPrimary);
            DrawButton(g, _btnResetRect, "RESET", palette.Surface, palette.TextPrimary);
            DrawButton(g, _btnTestRect, _isTestMode ? "STOP TEST" : "LAMP TEST", _isTestMode ? palette.Warning : palette.Surface, _isTestMode ? Color.Black : palette.TextPrimary);

            // 3. Render Matrix Tiles
            int gridH = Height - cmdH - 8;
            int gridW = Width - 8;
            int tileW = (gridW - (_columns - 1) * 4) / _columns;
            int tileH = (gridH - (_rows - 1) * 4) / _rows;

            using var fontTile = new Font("Segoe UI", 7.5f, FontStyle.Bold);

            for (int i = 0; i < _tiles.Count && i < _columns * _rows; i++)
            {
                var tile = _tiles[i];
                int col = i % _columns;
                int row = i / _columns;

                int x = 4 + col * (tileW + 4);
                int y = 4 + row * (tileH + 4);
                tile.Bounds = new Rectangle(x, y, tileW, tileH);

                Color tileBg;
                Color tileText;
                Color tileBorder;

                if (_isTestMode)
                {
                    tileBg = palette.Danger;
                    tileText = Color.White;
                    tileBorder = Color.White;
                }
                else
                {
                    switch (tile.State)
                    {
                        case IsaAlarmState.Unacknowledged:
                            // Fast flash
                            bool fastLit = ZeroAnimationClock.BlinkFast;
                            Color alarmColor = tile.Severity == IsaAlarmSeverity.Critical ? palette.Danger : palette.Warning;
                            tileBg = fastLit ? alarmColor : palette.Surface;
                            tileText = fastLit ? Color.White : palette.TextPrimary;
                            tileBorder = alarmColor;
                            break;

                        case IsaAlarmState.Acknowledged:
                            // Steady ON
                            Color ackColor = tile.Severity == IsaAlarmSeverity.Critical ? palette.Danger : palette.Warning;
                            tileBg = ackColor;
                            tileText = Color.White;
                            tileBorder = palette.Border;
                            break;

                        case IsaAlarmState.ReturnToNormal:
                            // Slow Flash Green
                            bool slowLit = ZeroAnimationClock.BlinkSlow;
                            tileBg = slowLit ? palette.Success : palette.Surface;
                            tileText = slowLit ? Color.White : palette.TextPrimary;
                            tileBorder = palette.Success;
                            break;

                        case IsaAlarmState.Normal:
                        default:
                            tileBg = palette.Surface;
                            tileText = palette.TextSecondary;
                            tileBorder = palette.Border;
                            break;
                    }
                }

                using (var brushTile = new SolidBrush(tileBg))
                {
                    g.FillRectangle(brushTile, tile.Bounds);
                }
                using (var penTile = new Pen(tileBorder, 1.2f))
                {
                    g.DrawRectangle(penTile, tile.Bounds);
                }

                // Tile Title Text
                using (var brushText = new SolidBrush(tileText))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(tile.Title, fontTile, brushText, tile.Bounds, sf);
                }
            }
        }

        private static void DrawButton(Graphics g, Rectangle r, string text, Color bg, Color fg)
        {
            using (var brush = new SolidBrush(bg))
            {
                g.FillRectangle(brush, r);
            }
            using (var pen = new Pen(Color.FromArgb(120, Color.Gray), 1f))
            {
                g.DrawRectangle(pen, r);
            }
            using var font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            using var brushFg = new SolidBrush(fg);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, brushFg, r, sf);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _clockToken?.Dispose();
                _clockToken = null;
                ZeroTheme.ThemeChanged -= OnThemeChanged;
                ZeroTagEngine.UnregisterBindable(this);
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZAnnunciatorGrid"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroAnnunciatorGrid is deprecated and will be removed in 5 release cycles. Please migrate to ZAnnunciatorGrid instead.")]
    [ToolboxItem(false)]
    public class ZeroAnnunciatorGrid : ZAnnunciatorGrid
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZAnnunciatorGrid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("AnnunciatorGrid is deprecated and will be removed in 5 release cycles. Please migrate to ZAnnunciatorGrid instead.")]
    [ToolboxItem(false)]
    public class AnnunciatorGrid : ZAnnunciatorGrid
    {
    }

    #endregion
}
