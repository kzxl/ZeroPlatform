using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Scada.Safety;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// Industrial safety optical light curtain compliant with IEC 61496-1/-2 (Type 4 ESPE).
    /// Features safety-yellow extruded housing, multi-beam optical array, dual OSSD output status,
    /// interactive obstacle simulation (finger/hand intrusion detection), and muting/blanking support.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroSafetyBarrier.bmp")]
    [Category("ZeroUI - SCADA Safety")]
    public class ZLightCurtainBar : ControlBase, IScadaBindable
    {
        private int _beamCount = 16;
        private LightCurtainState _curtainState = LightCurtainState.Clear;
        private LightCurtainRole _role = LightCurtainRole.IntegratedPair;
        private bool _ossd1Active = true;
        private bool _ossd2Active = true;
        private int _interruptedBeamIndex = -1;
        private int _interruptedBeamCount = 0;
        private double _protectionHeightMm = 600.0;
        private double _resolutionMm = 30.0;
        private bool _interactiveSimulation = true;

        private Color _housingColor = Color.FromArgb(245, 196, 0);

        public ZLightCurtainBar()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            Size = new Size(180, 280);
            Cursor = Cursors.Hand;
        }

        #region Properties

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Safety Configuration")]
        [DefaultValue(16)]
        public int BeamCount
        {
            get => _beamCount;
            set
            {
                _beamCount = Math.Max(4, Math.Min(64, value));
                if (_interruptedBeamIndex >= _beamCount) ClearObstacle();
                Invalidate();
            }
        }

        [Category("Safety State")]
        [DefaultValue(LightCurtainState.Clear)]
        public LightCurtainState CurtainState
        {
            get => _curtainState;
            set
            {
                if (_curtainState != value)
                {
                    _curtainState = value;
                    UpdateOssdOutputs();
                    Invalidate();
                }
            }
        }

        [Category("Safety State")]
        [DefaultValue(LightCurtainRole.IntegratedPair)]
        public LightCurtainRole Role
        {
            get => _role;
            set { _role = value; Invalidate(); }
        }

        [Category("Safety State")]
        [DefaultValue(true)]
        public bool Ossd1Active
        {
            get => _ossd1Active;
            set { _ossd1Active = value; Invalidate(); }
        }

        [Category("Safety State")]
        [DefaultValue(true)]
        public bool Ossd2Active
        {
            get => _ossd2Active;
            set { _ossd2Active = value; Invalidate(); }
        }

        [Category("Safety State")]
        [DefaultValue(-1)]
        public int InterruptedBeamIndex
        {
            get => _interruptedBeamIndex;
            set
            {
                _interruptedBeamIndex = value;
                EvaluateInterruption();
                Invalidate();
            }
        }

        [Category("Safety State")]
        [DefaultValue(0)]
        public int InterruptedBeamCount
        {
            get => _interruptedBeamCount;
            set
            {
                _interruptedBeamCount = value;
                EvaluateInterruption();
                Invalidate();
            }
        }

        [Category("Optical Specs")]
        [DefaultValue(600.0)]
        public double ProtectionHeightMm
        {
            get => _protectionHeightMm;
            set { _protectionHeightMm = Math.Max(100.0, value); Invalidate(); }
        }

        [Category("Optical Specs")]
        [DefaultValue(30.0)]
        public double ResolutionMm
        {
            get => _resolutionMm;
            set { _resolutionMm = Math.Max(10.0, value); Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool InteractiveSimulation
        {
            get => _interactiveSimulation;
            set => _interactiveSimulation = value;
        }

        [Category("Appearance")]
        public Color HousingColor
        {
            get => _housingColor;
            set { _housingColor = value; Invalidate(); }
        }

        #endregion

        #region Events

        public event EventHandler<LightCurtainTrippedEventArgs>? Tripped;
        public event EventHandler? Cleared;

        #endregion

        #region Simulation Methods

        public void SimulateObstacle(int beamIndex, int beamSpan = 2)
        {
            if (beamIndex >= 0 && beamIndex < _beamCount)
            {
                _interruptedBeamIndex = beamIndex;
                _interruptedBeamCount = Math.Max(1, Math.Min(beamSpan, _beamCount - beamIndex));
                _curtainState = LightCurtainState.Tripped;
                UpdateOssdOutputs();
                Tripped?.Invoke(this, new LightCurtainTrippedEventArgs(_interruptedBeamIndex, _interruptedBeamCount));
                Invalidate();
            }
        }

        public void ClearObstacle()
        {
            if (_interruptedBeamIndex != -1 || _curtainState == LightCurtainState.Tripped)
            {
                _interruptedBeamIndex = -1;
                _interruptedBeamCount = 0;
                _curtainState = LightCurtainState.Clear;
                UpdateOssdOutputs();
                Cleared?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag?.Value == null) return;
            string valStr = tag.Value?.ToString() ?? string.Empty;
            if (Enum.TryParse<LightCurtainState>(valStr, true, out var st))
            {
                CurtainState = st;
            }
            else if (bool.TryParse(valStr, out var b))
            {
                CurtainState = b ? LightCurtainState.Clear : LightCurtainState.Tripped;
            }
        }

        private void EvaluateInterruption()
        {
            if (_interruptedBeamIndex >= 0 && _interruptedBeamCount > 0)
            {
                _curtainState = LightCurtainState.Tripped;
            }
            else
            {
                _curtainState = LightCurtainState.Clear;
            }
            UpdateOssdOutputs();
        }

        private void UpdateOssdOutputs()
        {
            bool safe = _curtainState == LightCurtainState.Clear || _curtainState == LightCurtainState.Muted;
            _ossd1Active = safe;
            _ossd2Active = safe;
        }

        #endregion

        #region Mouse Interaction

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (!_interactiveSimulation || e.Button != MouseButtons.Left) return;

            // Check if clicking in beam zone to toggle obstacle
            float topY = 40f;
            float botY = Height - 36f;
            float totalH = botY - topY;

            if (e.Y >= topY && e.Y <= botY)
            {
                float frac = (e.Y - topY) / totalH;
                int clickedBeam = (int)(frac * _beamCount);
                clickedBeam = Math.Max(0, Math.Min(_beamCount - 1, clickedBeam));

                if (_curtainState == LightCurtainState.Tripped &&
                    clickedBeam >= _interruptedBeamIndex &&
                    clickedBeam < _interruptedBeamIndex + _interruptedBeamCount)
                {
                    ClearObstacle();
                }
                else
                {
                    SimulateObstacle(clickedBeam, 2);
                }
            }
        }

        #endregion

        #region Painting

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int w = Width;
            int h = Height;
            if (w < 40 || h < 60) return;

            // Background
            using (var bgBrush = new SolidBrush(Color.FromArgb(20, 22, 26)))
            {
                g.FillRectangle(bgBrush, 0, 0, w, h);
            }

            // Top Status Bar (OSSD Status & Height info)
            DrawHeader(g, w);

            // Active Optical Window
            float topY = 40f;
            float botY = h - 34f;
            float opticalH = botY - topY;

            if (_role == LightCurtainRole.IntegratedPair)
            {
                DrawIntegratedPair(g, w, topY, botY, opticalH);
            }
            else
            {
                DrawSingleBar(g, w, topY, botY, opticalH);
            }

            // Bottom Diagnostics Footer
            DrawFooter(g, w, h);
        }

        private void DrawHeader(Graphics g, int w)
        {
            var titleFont = ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Bold);
            var statusFont = ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Bold);

            // Header Background
            using (var headerBrush = new SolidBrush(Color.FromArgb(28, 32, 38)))
            {
                g.FillRectangle(headerBrush, 0, 0, w, 36f);
            }
            using (var borderPen = new Pen(Color.FromArgb(48, 52, 60), 1f))
            {
                g.DrawLine(borderPen, 0, 36f, w, 36f);
            }

            // Safety Title
            using (var textBrush = new SolidBrush(Color.FromArgb(220, 225, 230)))
            {
                g.DrawString("TYPE 4 ESPE", titleFont, textBrush, 8f, 4f);
            }

            // OSSD Status Pill
            Color ossdCol = _ossd1Active && _ossd2Active ? Color.FromArgb(40, 200, 60) : Color.FromArgb(235, 40, 45);
            string ossdText = _ossd1Active && _ossd2Active ? "OSSD ON" : "TRIPPED";

            using (var pillBrush = new SolidBrush(Color.FromArgb(50, ossdCol.R, ossdCol.G, ossdCol.B)))
            using (var pillPen = new Pen(ossdCol, 1.5f))
            {
                var pillRect = new RectangleF(w - 78f, 6f, 70f, 22f);
                g.FillRectangle(pillBrush, pillRect);
                g.DrawRectangle(pillPen, pillRect.X, pillRect.Y, pillRect.Width, pillRect.Height);

                using (var valBrush = new SolidBrush(ossdCol))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(ossdText, statusFont, valBrush, pillRect, sf);
                }
            }
        }

        private void DrawIntegratedPair(Graphics g, int w, float topY, float botY, float opticalH)
        {
            float barW = 28f;
            float txLeft = 14f;
            float rxLeft = w - barW - 14f;

            // Draw Tx Bar (Left)
            DrawCurtainHousing(g, txLeft, topY - 2f, barW, opticalH + 4f, "TX");

            // Draw Rx Bar (Right)
            DrawCurtainHousing(g, rxLeft, topY - 2f, barW, opticalH + 4f, "RX");

            // Draw Optical Beams Between Tx and Rx
            float beamSpan = opticalH / _beamCount;
            float beamLeft = txLeft + barW - 2f;
            float beamRight = rxLeft + 2f;

            for (int i = 0; i < _beamCount; i++)
            {
                float beamY = topY + i * beamSpan + beamSpan / 2f;
                bool isBroken = _curtainState == LightCurtainState.Tripped &&
                                i >= _interruptedBeamIndex &&
                                i < _interruptedBeamIndex + _interruptedBeamCount;

                Color beamCol;
                float penWidth = 1.5f;

                if (isBroken)
                {
                    beamCol = Color.FromArgb(255, 30, 40);
                    penWidth = 2.5f;
                }
                else if (_curtainState == LightCurtainState.Muted)
                {
                    beamCol = Color.FromArgb(240, 180, 20);
                }
                else if (_curtainState == LightCurtainState.Blanked)
                {
                    beamCol = Color.FromArgb(160, 60, 220);
                }
                else
                {
                    beamCol = Color.FromArgb(40, 220, 90);
                }

                using (var beamPen = new Pen(beamCol, penWidth))
                {
                    if (isBroken)
                    {
                        // Broken beam interrupted by obstacle block
                        float obsX = (beamLeft + beamRight) / 2f;
                        g.DrawLine(beamPen, beamLeft, beamY, obsX - 12f, beamY);
                        g.DrawLine(beamPen, obsX + 12f, beamY, beamRight, beamY);

                        // Draw Obstacle symbol
                        using (var obsBrush = new SolidBrush(Color.FromArgb(235, 40, 45)))
                        {
                            g.FillEllipse(obsBrush, obsX - 9f, beamY - 6f, 18f, 12f);
                        }
                    }
                    else
                    {
                        g.DrawLine(beamPen, beamLeft, beamY, beamRight, beamY);
                    }
                }

                // LED Indicators on Tx and Rx bar faces
                Color ledCol = isBroken ? Color.FromArgb(235, 40, 45) : Color.FromArgb(40, 220, 90);
                using (var ledBrush = new SolidBrush(ledCol))
                {
                    g.FillEllipse(ledBrush, txLeft + barW - 6f, beamY - 2.5f, 5f, 5f);
                    g.FillEllipse(ledBrush, rxLeft + 1f, beamY - 2.5f, 5f, 5f);
                }
            }
        }

        private void DrawSingleBar(Graphics g, int w, float topY, float botY, float opticalH)
        {
            float barW = 34f;
            float barX = (w - barW) / 2f;
            string roleLabel = _role == LightCurtainRole.Transmitter ? "TX" : "RX";

            DrawCurtainHousing(g, barX, topY - 2f, barW, opticalH + 4f, roleLabel);

            // Channel optical lenses
            float beamSpan = opticalH / _beamCount;
            for (int i = 0; i < _beamCount; i++)
            {
                float beamY = topY + i * beamSpan + beamSpan / 2f;
                bool isBroken = _curtainState == LightCurtainState.Tripped &&
                                i >= _interruptedBeamIndex &&
                                i < _interruptedBeamIndex + _interruptedBeamCount;

                Color ledCol = isBroken ? Color.FromArgb(235, 40, 45) : Color.FromArgb(40, 220, 90);
                using (var ledBrush = new SolidBrush(ledCol))
                {
                    g.FillEllipse(ledBrush, barX + (barW - 8f) / 2f, beamY - 4f, 8f, 8f);
                }
            }
        }

        private void DrawCurtainHousing(Graphics g, float x, float y, float w, float h, string label)
        {
            var housingRect = new RectangleF(x, y, w, h);

            // Yellow Aluminum Extrusion
            using (var brush = new LinearGradientBrush(housingRect, _housingColor, Color.FromArgb(210, 165, 0), 0f))
            {
                g.FillRectangle(brush, housingRect);
            }
            using (var pen = new Pen(Color.FromArgb(170, 130, 0), 1.5f))
            {
                g.DrawRectangle(pen, housingRect.X, housingRect.Y, housingRect.Width, housingRect.Height);
            }

            // Black End Caps (Top and Bottom)
            using (var capBrush = new SolidBrush(Color.FromArgb(24, 26, 30)))
            {
                g.FillRectangle(capBrush, x, y, w, 8f);
                g.FillRectangle(capBrush, x, y + h - 8f, w, 8f);
            }

            // Dark Optical Filter Lens Channel
            float lensInset = 4f;
            var lensRect = new RectangleF(x + lensInset, y + 10f, w - lensInset * 2f, h - 20f);
            using (var lensBrush = new SolidBrush(Color.FromArgb(20, 22, 26)))
            {
                g.FillRectangle(lensBrush, lensRect);
            }

            // Role label (TX/RX)
            var font = ZeroFontCache.Get("Segoe UI", 6.5f, FontStyle.Bold);
            using (var labelBrush = new SolidBrush(Color.FromArgb(24, 26, 30)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(label, font, labelBrush, new RectangleF(x, y + 1f, w, 7f), sf);
            }
        }

        private void DrawFooter(Graphics g, int w, int h)
        {
            float footerY = h - 30f;
            using (var footerBrush = new SolidBrush(Color.FromArgb(24, 26, 30)))
            {
                g.FillRectangle(footerBrush, 0, footerY, w, 30f);
            }
            using (var borderPen = new Pen(Color.FromArgb(44, 48, 56), 1f))
            {
                g.DrawLine(borderPen, 0, footerY, w, footerY);
            }

            var font = ZeroFontCache.Get("Segoe UI", 7f, FontStyle.Regular);
            using (var brush = new SolidBrush(Color.FromArgb(160, 165, 175)))
            {
                string infoLeft = $"H: {_protectionHeightMm:F0}mm | d: {_resolutionMm:F0}mm";
                g.DrawString(infoLeft, font, brush, 8f, footerY + 8f);

                string infoRight = $"{_beamCount} Beams";
                using (var sf = new StringFormat { Alignment = StringAlignment.Far })
                {
                    g.DrawString(infoRight, font, brush, w - 8f, footerY + 8f, sf);
                }
            }
        }

        #endregion
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZLightCurtainBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("LightCurtainBar is deprecated and will be removed in 5 release cycles. Please migrate to ZLightCurtainBar instead.")]
    [ToolboxItem(false)]
    public class LightCurtainBar : ZLightCurtainBar
    {
    }

    #endregion
}
