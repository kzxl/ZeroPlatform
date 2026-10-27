using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Rendering;

namespace ZeroUI.WinForms.Industrial
{
    public partial class ZSevenSegment
    {
        // Segment Bitmask Definitions:
        // Bit 0 (0x01): Top (A)
        // Bit 1 (0x02): Top-Right (B)
        // Bit 2 (0x04): Bottom-Right (C)
        // Bit 3 (0x08): Bottom (D)
        // Bit 4 (0x10): Bottom-Left (E)
        // Bit 5 (0x20): Top-Left (F)
        // Bit 6 (0x40): Middle (G)
        private static readonly byte[] CharPatterns = new byte[128];

        static ZSevenSegment()
        {
            // Digits 0-9
            CharPatterns['0'] = 0x3F; // A B C D E F
            CharPatterns['1'] = 0x06; // B C
            CharPatterns['2'] = 0x5B; // A B D E G
            CharPatterns['3'] = 0x4F; // A B C D G
            CharPatterns['4'] = 0x66; // B C F G
            CharPatterns['5'] = 0x6D; // A C D F G
            CharPatterns['6'] = 0x7D; // A C D E F G
            CharPatterns['7'] = 0x07; // A B C
            CharPatterns['8'] = 0x7F; // A B C D E F G
            CharPatterns['9'] = 0x6F; // A B C D F G

            // Symbols
            CharPatterns['-'] = 0x40; // G
            CharPatterns['_'] = 0x08; // D
            CharPatterns['='] = 0x48; // D G
            CharPatterns[' '] = 0x00;
            CharPatterns['['] = 0x39; // A D E F
            CharPatterns[']'] = 0x0F; // A B C D

            // Letters for SCADA/MES status (STOP, RUN, PASS, FAIL, COOL, HEAT, ALARM, Err, OFF, ON)
            CharPatterns['A'] = 0x77; CharPatterns['a'] = 0x77;
            CharPatterns['B'] = 0x7C; CharPatterns['b'] = 0x7C; // b
            CharPatterns['C'] = 0x39; CharPatterns['c'] = 0x58; // C / c
            CharPatterns['D'] = 0x5E; CharPatterns['d'] = 0x5E; // d
            CharPatterns['E'] = 0x79; CharPatterns['e'] = 0x79;
            CharPatterns['F'] = 0x71; CharPatterns['f'] = 0x71;
            CharPatterns['G'] = 0x3D; CharPatterns['g'] = 0x6F;
            CharPatterns['H'] = 0x76; CharPatterns['h'] = 0x74; // H / h
            CharPatterns['I'] = 0x06; CharPatterns['i'] = 0x04;
            CharPatterns['J'] = 0x1E; CharPatterns['j'] = 0x1E;
            CharPatterns['K'] = 0x75; CharPatterns['k'] = 0x74;
            CharPatterns['L'] = 0x38; CharPatterns['l'] = 0x30;
            CharPatterns['M'] = 0x54; CharPatterns['m'] = 0x54; // Industrial 7-seg standard (displays as n)
            CharPatterns['N'] = 0x54; CharPatterns['n'] = 0x54; // n
            CharPatterns['O'] = 0x3F; CharPatterns['o'] = 0x5C; // O / o
            CharPatterns['P'] = 0x73; CharPatterns['p'] = 0x73;
            CharPatterns['Q'] = 0x67; CharPatterns['q'] = 0x67;
            CharPatterns['R'] = 0x50; CharPatterns['r'] = 0x50; // r
            CharPatterns['S'] = 0x6D; CharPatterns['s'] = 0x6D; // S (5)
            CharPatterns['T'] = 0x78; CharPatterns['t'] = 0x78; // t
            CharPatterns['U'] = 0x3E; CharPatterns['u'] = 0x1C; // U / u
            CharPatterns['V'] = 0x1C; CharPatterns['v'] = 0x1C;
            CharPatterns['W'] = 0x2A; CharPatterns['w'] = 0x2A;
            CharPatterns['X'] = 0x76; CharPatterns['x'] = 0x76;
            CharPatterns['Y'] = 0x6E; CharPatterns['y'] = 0x6E; // y
            CharPatterns['Z'] = 0x5B; CharPatterns['z'] = 0x5B;
        }

        #region Parsing & Layout Model

        private struct DisplayItem
        {
            public char Character;
            public bool HasDecimal;
            public bool IsColon;
            public bool IsDimmed;
        }

        private readonly List<DisplayItem> _rawItemsBuffer = new List<DisplayItem>(16);
        private readonly List<DisplayItem> _paddedItemsBuffer = new List<DisplayItem>(16);

        private List<DisplayItem> ParseValue()
        {
            _rawItemsBuffer.Clear();
            string text = _value ?? "";

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ':')
                {
                    _rawItemsBuffer.Add(new DisplayItem { IsColon = true });
                }
                else if (c == '.' || c == ',')
                {
                    if (_rawItemsBuffer.Count > 0 && !_rawItemsBuffer[_rawItemsBuffer.Count - 1].IsColon)
                    {
                        var last = _rawItemsBuffer[_rawItemsBuffer.Count - 1];
                        last.HasDecimal = true;
                        _rawItemsBuffer[_rawItemsBuffer.Count - 1] = last;
                    }
                    else
                    {
                        _rawItemsBuffer.Add(new DisplayItem { Character = ' ', HasDecimal = true });
                    }
                }
                else
                {
                    _rawItemsBuffer.Add(new DisplayItem { Character = c });
                }
            }

            // Check for clock format (colon)
            bool hasColon = false;
            for (int i = 0; i < _rawItemsBuffer.Count; i++)
            {
                if (_rawItemsBuffer[i].IsColon) { hasColon = true; break; }
            }

            if (!hasColon)
            {
                if (_leadingZeroMode == LeadingZeroDisplayMode.DimmedGhost)
                {
                    for (int i = 0; i < _rawItemsBuffer.Count - 1; i++)
                    {
                        if (_rawItemsBuffer[i].Character == '0' && !_rawItemsBuffer[i].HasDecimal)
                        {
                            var item = _rawItemsBuffer[i];
                            item.IsDimmed = true;
                            _rawItemsBuffer[i] = item;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else if (_leadingZeroMode == LeadingZeroDisplayMode.Blank)
                {
                    for (int i = 0; i < _rawItemsBuffer.Count - 1; i++)
                    {
                        if (_rawItemsBuffer[i].Character == '0' && !_rawItemsBuffer[i].HasDecimal)
                        {
                            var item = _rawItemsBuffer[i];
                            item.Character = ' ';
                            _rawItemsBuffer[i] = item;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            int digitCountInValue = 0;
            for (int i = 0; i < _rawItemsBuffer.Count; i++)
            {
                if (!_rawItemsBuffer[i].IsColon) digitCountInValue++;
            }

            int totalDigits = Math.Max(_digitCount, digitCountInValue);
            int padCount = totalDigits - digitCountInValue;

            if (padCount <= 0) return _rawItemsBuffer;

            _paddedItemsBuffer.Clear();
            char padChar = (_leadingZeroMode == LeadingZeroDisplayMode.LitZero || _leadingZeroMode == LeadingZeroDisplayMode.DimmedGhost) ? '0' : ' ';
            bool isDimmed = (_leadingZeroMode == LeadingZeroDisplayMode.DimmedGhost);

            DisplayItem padItem = new DisplayItem
            {
                Character = padChar,
                IsDimmed = isDimmed
            };

            if (_textAlignment == HorizontalAlignment.Right)
            {
                for (int i = 0; i < padCount; i++) _paddedItemsBuffer.Add(padItem);
                _paddedItemsBuffer.AddRange(_rawItemsBuffer);
            }
            else if (_textAlignment == HorizontalAlignment.Left)
            {
                _paddedItemsBuffer.AddRange(_rawItemsBuffer);
                DisplayItem blankItem = new DisplayItem { Character = ' ' };
                for (int i = 0; i < padCount; i++) _paddedItemsBuffer.Add(blankItem);
            }
            else // Center
            {
                int leftPad = padCount / 2;
                int rightPad = padCount - leftPad;
                for (int i = 0; i < leftPad; i++) _paddedItemsBuffer.Add(padItem);
                _paddedItemsBuffer.AddRange(_rawItemsBuffer);
                DisplayItem blankItem = new DisplayItem { Character = ' ' };
                for (int i = 0; i < rightPad; i++) _paddedItemsBuffer.Add(blankItem);
            }

            return _paddedItemsBuffer;
        }

        #endregion

        #region Rendering Engine

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = Width;
            int h = Height;

            // 1. Draw Enclosure Background & Bezel
            DrawEnclosure(g, w, h);

            // 2. Compute Layout Metrics
            int padX = (int)(8 * DpiScale);
            int padY = (int)(6 * DpiScale);
            int availableW = w - (padX * 2);
            int availableH = h - (padY * 2);

            // Reserve room for Unit badge if specified
            int unitReservedWidth = 0;
            if (!string.IsNullOrEmpty(_unit))
            {
                var testFont = ZeroFontCache.Get("Segoe UI", Math.Max(7.5f, h * 0.18f), FontStyle.Bold);
                var unitSize = g.MeasureString(_unit, testFont);
                unitReservedWidth = (int)unitSize.Width + (int)(6 * DpiScale);
                availableW -= unitReservedWidth;
            }

            var items = ParseValue();

            int digitSlotCount = 0;
            int colonSlotCount = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].IsColon) colonSlotCount++;
                else digitSlotCount++;
            }

            float digitSlotRatio = 1.0f;
            float colonSlotRatio = 0.38f;
            float itemGap = 3.5f * DpiScale;

            float totalUnits = (digitSlotCount * digitSlotRatio) + (colonSlotCount * colonSlotRatio);
            float totalGaps = Math.Max(0, items.Count - 1) * itemGap;
            float digitW = totalUnits > 0 ? (availableW - totalGaps) / totalUnits : availableW;
            digitW = Math.Max(8f * DpiScale, digitW);
            float colonW = digitW * colonSlotRatio;
            float digitH = availableH;

            int thickness = _segmentThickness > 0 ? (int)(_segmentThickness * DpiScale) : Math.Max(3, (int)(digitH * 0.115f));
            float gap = Math.Max(0.8f * DpiScale, _segmentGap * DpiScale);

            // Slant skew transformation factor
            float shearX = - (float)Math.Tan(_slantAngle * Math.PI / 180.0);

            // Global blink suppression
            bool isDisplayDark = _blink && !_blinkPhase;

            float currentX = padX;

            // Pre-allocated Pens & Brushes once for the entire display pass
            using var outerGlowPen = _showGlow ? new Pen(Color.FromArgb(32, _segmentColor), thickness * 1.5f) { LineJoin = LineJoin.Round } : null;
            using var innerGlowPen = _showGlow ? new Pen(Color.FromArgb(85, _segmentColor), thickness * 0.7f) { LineJoin = LineJoin.Round } : null;
            using var fillBrush = new SolidBrush(_segmentColor);
            Color coreColor = Color.FromArgb(
                255,
                Math.Min(255, _segmentColor.R + 75),
                Math.Min(255, _segmentColor.G + 75),
                Math.Min(255, _segmentColor.B + 75));
            using var corePen = new Pen(coreColor, Math.Max(1f, thickness * 0.26f)) { LineJoin = LineJoin.Round };
            using var ghostBrush = new SolidBrush(_dimColor);
            using var dimmedLitBrush = new SolidBrush(Color.FromArgb(100, _segmentColor));
            using var dpGlowBrush = new SolidBrush(Color.FromArgb(60, _segmentColor));
            using var coreBrush = new SolidBrush(Color.FromArgb(255, Math.Min(255, _segmentColor.R + 80), Math.Min(255, _segmentColor.G + 80), Math.Min(255, _segmentColor.B + 80)));

            // 3. Render Each Digit / Colon
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                if (item.IsColon)
                {
                    DrawColon(g, currentX, padY, colonW, digitH, shearX, isDisplayDark, fillBrush, ghostBrush, outerGlowPen, dpGlowBrush, coreBrush);
                    currentX += colonW + itemGap;
                }
                else
                {
                    DrawBeveledDigit(g, currentX, padY, digitW, digitH, item, thickness, gap, shearX, isDisplayDark, outerGlowPen, innerGlowPen, fillBrush, corePen, ghostBrush, dimmedLitBrush, dpGlowBrush);
                    currentX += digitW + itemGap;
                }
            }

            // 4. Render Unit Badge
            if (!string.IsNullOrEmpty(_unit))
            {
                DrawUnitBadge(g, w - padX - unitReservedWidth + 2, padY, unitReservedWidth, digitH);
            }

            // 5. Acrylic Glass Reflection Overlay
            if (_showGlassReflection)
            {
                DrawGlassSheen(g, w, h);
            }
        }

        private void DrawEnclosure(Graphics g, int w, int h)
        {
            if (_frameStyle == SevenSegmentFrameStyle.Borderless)
            {
                using var bgBrush = new SolidBrush(BackColor);
                g.FillRectangle(bgBrush, 0, 0, w, h);
                return;
            }

            // Recessed Dark Acrylic Bezel
            using (var brush = new LinearGradientBrush(
                new Point(0, 0), new Point(0, h),
                Color.FromArgb(14, 20, 34), Color.FromArgb(8, 12, 22)))
            {
                g.FillRectangle(brush, 0, 0, w, h);
            }

            if (_frameStyle == SevenSegmentFrameStyle.RecessedBezel)
            {
                // Outer industrial frame border
                using var outerPen = new Pen(Color.FromArgb(40, 52, 70), 1f);
                g.DrawRectangle(outerPen, 0, 0, w - 1, h - 1);

                // Top/Left inner shadow
                using var shadowPen = new Pen(Color.FromArgb(6, 9, 16), 1.2f);
                g.DrawLine(shadowPen, 1, 1, w - 2, 1);
                g.DrawLine(shadowPen, 1, 1, 1, h - 2);

                // Bottom/Right inner rim highlight
                using var rimPen = new Pen(Color.FromArgb(30, 42, 60), 1f);
                g.DrawLine(rimPen, 1, h - 2, w - 2, h - 2);
                g.DrawLine(rimPen, w - 2, 1, w - 2, h - 2);
            }
            else if (_frameStyle == SevenSegmentFrameStyle.AcrylicGlass)
            {
                using var borderPen = new Pen(Color.FromArgb(30, 41, 59), 1f);
                g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
            }
        }

        private void DrawBeveledDigit(
            Graphics g, float x, float y, float w, float h,
            DisplayItem item, int t, float gap, float shearX, bool isDisplayDark,
            Pen? outerGlowPen, Pen? innerGlowPen, SolidBrush fillBrush, Pen corePen,
            SolidBrush ghostBrush, SolidBrush dimmedLitBrush, SolidBrush dpGlowBrush)
        {
            byte mask = 0;
            char c = item.Character;

            if (c >= 0 && c < CharPatterns.Length)
            {
                mask = CharPatterns[c];
            }
            else if (c == '°')
            {
                mask = 0x63; // A, B, F, G
            }

            var state = g.Save();

            // Apply Italic Slant Transformation
            if (_slantAngle > 0)
            {
                float cx = x + w / 2f;
                float cy = y + h / 2f;
                using var matrix = new Matrix();
                matrix.Translate(cx, cy);
                matrix.Shear(shearX, 0);
                matrix.Translate(-cx, -cy);
                g.MultiplyTransform(matrix);
            }

            float halfH = h / 2f;
            float t2 = t * 0.5f;

            // Compute 7 Beveled Hexagonal Segment Polygons
            // Segment A (Top horizontal)
            var polyA = new PointF[]
            {
                new PointF(x + t2 + gap, y + t2),
                new PointF(x + t + gap, y),
                new PointF(x + w - t - gap, y),
                new PointF(x + w - t2 - gap, y + t2),
                new PointF(x + w - t - gap, y + t),
                new PointF(x + t + gap, y + t)
            };

            // Segment B (Top-Right vertical)
            float rx = x + w - t;
            var polyB = new PointF[]
            {
                new PointF(rx + t2, y + t2 + gap),
                new PointF(rx + t, y + t + gap),
                new PointF(rx + t, y + halfH - t2 - gap),
                new PointF(rx + t2, y + halfH - gap),
                new PointF(rx, y + halfH - t2 - gap),
                new PointF(rx, y + t + gap)
            };

            // Segment C (Bottom-Right vertical)
            var polyC = new PointF[]
            {
                new PointF(rx + t2, y + halfH + gap),
                new PointF(rx + t, y + halfH + t2 + gap),
                new PointF(rx + t, y + h - t - gap),
                new PointF(rx + t2, y + h - t2 - gap),
                new PointF(rx, y + h - t - gap),
                new PointF(rx, y + halfH + t2 + gap)
            };

            // Segment D (Bottom horizontal)
            var polyD = new PointF[]
            {
                new PointF(x + t2 + gap, y + h - t2),
                new PointF(x + t + gap, y + h - t),
                new PointF(x + w - t - gap, y + h - t),
                new PointF(x + w - t2 - gap, y + h - t2),
                new PointF(x + w - t - gap, y + h),
                new PointF(x + t + gap, y + h)
            };

            // Segment E (Bottom-Left vertical)
            var polyE = new PointF[]
            {
                new PointF(x + t2, y + halfH + gap),
                new PointF(x + t, y + halfH + t2 + gap),
                new PointF(x + t, y + h - t - gap),
                new PointF(x + t2, y + h - t2 - gap),
                new PointF(x, y + h - t - gap),
                new PointF(x, y + halfH + t2 + gap)
            };

            // Segment F (Top-Left vertical)
            var polyF = new PointF[]
            {
                new PointF(x + t2, y + t2 + gap),
                new PointF(x + t, y + t + gap),
                new PointF(x + t, y + halfH - t2 - gap),
                new PointF(x + t2, y + halfH - gap),
                new PointF(x, y + halfH - t2 - gap),
                new PointF(x, y + t + gap)
            };

            // Segment G (Center horizontal)
            var polyG = new PointF[]
            {
                new PointF(x + t2 + gap, y + halfH),
                new PointF(x + t + gap, y + halfH - t2),
                new PointF(x + w - t - gap, y + halfH - t2),
                new PointF(x + w - t2 - gap, y + halfH),
                new PointF(x + w - t - gap, y + halfH + t2),
                new PointF(x + t + gap, y + halfH + t2)
            };

            bool isDimmedLeading = item.IsDimmed;

            if (isDimmedLeading)
            {
                DrawDimmedSegment(g, polyA, (mask & 0x01) != 0, dimmedLitBrush);
                DrawDimmedSegment(g, polyB, (mask & 0x02) != 0, dimmedLitBrush);
                DrawDimmedSegment(g, polyC, (mask & 0x04) != 0, dimmedLitBrush);
                DrawDimmedSegment(g, polyD, (mask & 0x08) != 0, dimmedLitBrush);
                DrawDimmedSegment(g, polyE, (mask & 0x10) != 0, dimmedLitBrush);
                DrawDimmedSegment(g, polyF, (mask & 0x20) != 0, dimmedLitBrush);
                DrawDimmedSegment(g, polyG, (mask & 0x40) != 0, dimmedLitBrush);
            }
            else
            {
                DrawSingleSegment(g, polyA, (mask & 0x01) != 0, t, isDisplayDark, outerGlowPen, innerGlowPen, fillBrush, corePen, ghostBrush);
                DrawSingleSegment(g, polyB, (mask & 0x02) != 0, t, isDisplayDark, outerGlowPen, innerGlowPen, fillBrush, corePen, ghostBrush);
                DrawSingleSegment(g, polyC, (mask & 0x04) != 0, t, isDisplayDark, outerGlowPen, innerGlowPen, fillBrush, corePen, ghostBrush);
                DrawSingleSegment(g, polyD, (mask & 0x08) != 0, t, isDisplayDark, outerGlowPen, innerGlowPen, fillBrush, corePen, ghostBrush);
                DrawSingleSegment(g, polyE, (mask & 0x10) != 0, t, isDisplayDark, outerGlowPen, innerGlowPen, fillBrush, corePen, ghostBrush);
                DrawSingleSegment(g, polyF, (mask & 0x20) != 0, t, isDisplayDark, outerGlowPen, innerGlowPen, fillBrush, corePen, ghostBrush);
                DrawSingleSegment(g, polyG, (mask & 0x40) != 0, t, isDisplayDark, outerGlowPen, innerGlowPen, fillBrush, corePen, ghostBrush);
            }

            // Render Integrated Decimal Point (DP)
            float dpSize = Math.Max(2.5f, t * 0.9f);
            float dpX = x + w + gap * 0.5f;
            float dpY = y + h - dpSize;
            bool dpLit = item.HasDecimal && !isDisplayDark;

            if (dpLit)
            {
                if (_showGlow)
                {
                    g.FillEllipse(dpGlowBrush, dpX - 1.5f, dpY - 1.5f, dpSize + 3f, dpSize + 3f);
                }
                g.FillEllipse(fillBrush, dpX, dpY, dpSize, dpSize);
            }
            else if (_showGhostSegments)
            {
                g.FillEllipse(ghostBrush, dpX, dpY, dpSize, dpSize);
            }

            g.Restore(state);
        }

        private static void DrawDimmedSegment(Graphics g, PointF[] points, bool isZeroSegment, SolidBrush dimmedLitBrush)
        {
            if (isZeroSegment)
            {
                g.FillPolygon(dimmedLitBrush, points);
            }
        }

        private void DrawSingleSegment(
            Graphics g, PointF[] points, bool lit, int t, bool isDisplayDark,
            Pen? outerGlowPen, Pen? innerGlowPen, SolidBrush fillBrush, Pen corePen, SolidBrush ghostBrush)
        {
            if (lit && !isDisplayDark)
            {
                if (_showGlow && outerGlowPen != null && innerGlowPen != null)
                {
                    g.DrawPolygon(outerGlowPen, points);
                    g.DrawPolygon(innerGlowPen, points);
                }

                g.FillPolygon(fillBrush, points);
                g.DrawPolygon(corePen, points);
            }
            else if (_showGhostSegments)
            {
                g.FillPolygon(ghostBrush, points);
            }
        }

        private void DrawColon(
            Graphics g, float x, float y, float w, float h, float shearX, bool isDisplayDark,
            SolidBrush fillBrush, SolidBrush ghostBrush, Pen? outerGlowPen, SolidBrush dpGlowBrush, SolidBrush coreBrush)
        {
            var state = g.Save();

            if (_slantAngle > 0)
            {
                float cx = x + w / 2f;
                float cy = y + h / 2f;
                using var matrix = new Matrix();
                matrix.Translate(cx, cy);
                matrix.Shear(shearX, 0);
                matrix.Translate(-cx, -cy);
                g.MultiplyTransform(matrix);
            }

            bool colonLit = (!_blinkColon || _blinkPhase) && !isDisplayDark;
            float dotSize = Math.Max(3f, h * 0.095f);
            float dotX = x + (w / 2f) - (dotSize / 2f);
            float dot1Y = y + (h * 0.33f) - (dotSize / 2f);
            float dot2Y = y + (h * 0.67f) - (dotSize / 2f);

            if (colonLit)
            {
                if (_showGlow)
                {
                    g.FillEllipse(dpGlowBrush, dotX - 2, dot1Y - 2, dotSize + 4, dotSize + 4);
                    g.FillEllipse(dpGlowBrush, dotX - 2, dot2Y - 2, dotSize + 4, dotSize + 4);
                }

                g.FillEllipse(fillBrush, dotX, dot1Y, dotSize, dotSize);
                g.FillEllipse(fillBrush, dotX, dot2Y, dotSize, dotSize);

                float cSize = dotSize * 0.5f;
                float offset = (dotSize - cSize) / 2f;
                g.FillEllipse(coreBrush, dotX + offset, dot1Y + offset, cSize, cSize);
                g.FillEllipse(coreBrush, dotX + offset, dot2Y + offset, cSize, cSize);
            }
            else if (_showGhostSegments)
            {
                g.FillEllipse(ghostBrush, dotX, dot1Y, dotSize, dotSize);
                g.FillEllipse(ghostBrush, dotX, dot2Y, dotSize, dotSize);
            }

            g.Restore(state);
        }

        private void DrawUnitBadge(Graphics g, float x, float y, float w, float h)
        {
            Color uColor = _unitColor.IsEmpty ? Color.FromArgb(170, _segmentColor) : _unitColor;
            var font = ZeroFontCache.Get("Segoe UI", Math.Max(7.5f, h * 0.22f), FontStyle.Bold);
            using var brush = new SolidBrush(uColor);
            var rect = new RectangleF(x, y, w, h - 2);
            g.DrawString(_unit, font, brush, rect, ZeroStringFormats.FarFar);
        }

        private void DrawGlassSheen(Graphics g, int w, int h)
        {
            int sheenHeight = (int)(h * 0.44f);
            using (var glassBrush = new LinearGradientBrush(
                new Rectangle(0, 0, w, sheenHeight),
                Color.FromArgb(18, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(glassBrush, 1, 1, w - 2, sheenHeight);
            }

            // Crisp inner top glass reflection highlight line
            using var sheenPen = new Pen(Color.FromArgb(28, 255, 255, 255), 1f);
            g.DrawLine(sheenPen, 2, 1, w - 3, 1);
        }

        #endregion
    }
}
