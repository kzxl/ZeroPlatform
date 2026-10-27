using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ZeroUI.Core.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Icons
{
    /// <summary>
    /// High-precision, anti-aliased GDI+ vector renderer for all standard <see cref="IconKey"/> glyphs.
    /// Eliminates external asset dependencies and renders sharp, scalable icons at any DPI resolution.
    /// </summary>
    public static class ZeroVectorIcons
    {
        /// <summary>
        /// Draws the vector icon directly onto the target graphics surface inside the specified bounding box.
        /// </summary>
        public static void Draw(Graphics g, IconKey key, RectangleF bounds, Color? tintColor = null)
        {
            if (bounds.Width <= 1 || bounds.Height <= 1) return;

            Color color = tintColor ?? ZeroTheme.Colors.TextPrimary;
            float strokeThick = Math.Max(1.2f, bounds.Width / 12f);

            var oldSmoothing = g.SmoothingMode;
            var oldPixelOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Inset bounds slightly for padding
            float pad = bounds.Width * 0.12f;
            var r = new RectangleF(bounds.X + pad, bounds.Y + pad, bounds.Width - pad * 2, bounds.Height - pad * 2);

            switch (key)
            {
                case IconKey.Save:
                    DrawSave(g, r, color, strokeThick);
                    break;
                case IconKey.Edit:
                    DrawEdit(g, r, color, strokeThick);
                    break;
                case IconKey.Add:
                    DrawPlus(g, r, color, strokeThick);
                    break;
                case IconKey.Delete:
                    DrawTrash(g, r, color, strokeThick);
                    break;
                case IconKey.Refresh:
                    DrawRefresh(g, r, color, strokeThick);
                    break;
                case IconKey.Search:
                    DrawSearch(g, r, color, strokeThick);
                    break;
                case IconKey.Filter:
                    DrawFilter(g, r, color, strokeThick);
                    break;
                case IconKey.Clear:
                case IconKey.Close:
                    DrawClose(g, r, color, strokeThick);
                    break;
                case IconKey.Copy:
                case IconKey.Duplicate:
                    DrawCopy(g, r, color, strokeThick);
                    break;
                case IconKey.Cut:
                    DrawCut(g, r, color, strokeThick);
                    break;
                case IconKey.Paste:
                    DrawPaste(g, r, color, strokeThick);
                    break;
                case IconKey.Undo:
                    DrawUndo(g, r, color, strokeThick);
                    break;
                case IconKey.Redo:
                    DrawRedo(g, r, color, strokeThick);
                    break;
                case IconKey.Export:
                    DrawExport(g, r, color, strokeThick);
                    break;
                case IconKey.Import:
                    DrawImport(g, r, color, strokeThick);
                    break;
                case IconKey.Print:
                    DrawPrint(g, r, color, strokeThick);
                    break;
                case IconKey.Download:
                    DrawDownload(g, r, color, strokeThick);
                    break;
                case IconKey.Upload:
                    DrawUpload(g, r, color, strokeThick);
                    break;
                case IconKey.Folder:
                    DrawFolder(g, r, color, strokeThick);
                    break;
                case IconKey.Document:
                case IconKey.File:
                    DrawDocument(g, r, color, strokeThick);
                    break;
                case IconKey.ArrowLeft:
                case IconKey.Back:
                    DrawArrow(g, r, color, strokeThick, 180);
                    break;
                case IconKey.ArrowRight:
                case IconKey.Forward:
                    DrawArrow(g, r, color, strokeThick, 0);
                    break;
                case IconKey.ArrowUp:
                    DrawArrow(g, r, color, strokeThick, 270);
                    break;
                case IconKey.ArrowDown:
                    DrawArrow(g, r, color, strokeThick, 90);
                    break;
                case IconKey.Home:
                    DrawHome(g, r, color, strokeThick);
                    break;
                case IconKey.Menu:
                    DrawMenu(g, r, color, strokeThick);
                    break;
                case IconKey.More:
                    DrawMore(g, r, color);
                    break;
                case IconKey.AlignLeft:
                    DrawAlign(g, r, color, strokeThick, 0);
                    break;
                case IconKey.AlignCenter:
                    DrawAlign(g, r, color, strokeThick, 1);
                    break;
                case IconKey.AlignRight:
                    DrawAlign(g, r, color, strokeThick, 2);
                    break;
                case IconKey.AlignTop:
                    DrawAlign(g, r, color, strokeThick, 3);
                    break;
                case IconKey.AlignMiddle:
                    DrawAlign(g, r, color, strokeThick, 4);
                    break;
                case IconKey.AlignBottom:
                    DrawAlign(g, r, color, strokeThick, 5);
                    break;
                case IconKey.DistributeHorizontal:
                    DrawDistributeH(g, r, color, strokeThick);
                    break;
                case IconKey.DistributeVertical:
                    DrawDistributeV(g, r, color, strokeThick);
                    break;
                case IconKey.Connect:
                    DrawConnect(g, r, color, strokeThick);
                    break;
                case IconKey.Unlink:
                    DrawUnlink(g, r, color, strokeThick);
                    break;
                case IconKey.Shape:
                    DrawShape(g, r, color, strokeThick);
                    break;
                case IconKey.Palette:
                    DrawPalette(g, r, color, strokeThick);
                    break;
                case IconKey.Settings:
                    DrawSettings(g, r, color, strokeThick);
                    break;
                case IconKey.Info:
                    DrawInfo(g, r, color, strokeThick);
                    break;
                case IconKey.Warning:
                    DrawWarning(g, r, color, strokeThick);
                    break;
                case IconKey.Success:
                    DrawSuccess(g, r, color, strokeThick);
                    break;
                case IconKey.Error:
                    DrawError(g, r, color, strokeThick);
                    break;
                case IconKey.Lock:
                    DrawLock(g, r, color, strokeThick, false);
                    break;
                case IconKey.Unlock:
                    DrawLock(g, r, color, strokeThick, true);
                    break;
                case IconKey.ZoomIn:
                    DrawZoom(g, r, color, strokeThick, 1);
                    break;
                case IconKey.ZoomOut:
                    DrawZoom(g, r, color, strokeThick, -1);
                    break;
                case IconKey.ZoomFit:
                    DrawZoom(g, r, color, strokeThick, 0);
                    break;
                case IconKey.Play:
                    DrawPlay(g, r, color);
                    break;
                case IconKey.Pause:
                    DrawPause(g, r, color);
                    break;
                case IconKey.Stop:
                    DrawStop(g, r, color);
                    break;
                case IconKey.Step:
                    DrawStep(g, r, color, strokeThick);
                    break;
                case IconKey.Swimlane:
                    DrawSwimlane(g, r, color, strokeThick);
                    break;
                case IconKey.AutoLayout:
                    DrawAutoLayout(g, r, color, strokeThick);
                    break;
                case IconKey.Fullscreen:
                    DrawFullscreen(g, r, color, strokeThick);
                    break;
                default:
                    DrawDocument(g, r, color, strokeThick);
                    break;
            }

            g.SmoothingMode = oldSmoothing;
            g.PixelOffsetMode = oldPixelOffset;
        }

        #region Vector Draw Routines

        private static void DrawSave(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { LineJoin = LineJoin.Round };
            using var brush = new SolidBrush(color);

            // Outer disk body
            float cut = r.Width * 0.2f;
            var path = new GraphicsPath();
            path.AddLine(r.X, r.Y, r.Right - cut, r.Y);
            path.AddLine(r.Right, r.Y + cut, r.Right, r.Bottom);
            path.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
            path.CloseFigure();
            g.DrawPath(pen, path);

            // Top slider plate
            var topRect = new RectangleF(r.X + r.Width * 0.2f, r.Y, r.Width * 0.5f, r.Height * 0.35f);
            g.DrawRectangle(pen, topRect.X, topRect.Y, topRect.Width, topRect.Height);

            // Bottom label area
            var btmRect = new RectangleF(r.X + r.Width * 0.2f, r.Bottom - r.Height * 0.4f, r.Width * 0.6f, r.Height * 0.4f);
            g.DrawRectangle(pen, btmRect.X, btmRect.Y, btmRect.Width, btmRect.Height);
        }

        private static void DrawEdit(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            PointF tip = new PointF(r.X, r.Bottom);
            PointF top = new PointF(r.Right - r.Width * 0.15f, r.Y);
            PointF bottomBase = new PointF(r.X + r.Width * 0.3f, r.Bottom);
            PointF leftBase = new PointF(r.X, r.Bottom - r.Height * 0.3f);

            // Pencil body line
            g.DrawLine(pen, leftBase, new PointF(r.Right - r.Width * 0.3f, r.Y));
            g.DrawLine(pen, bottomBase, new PointF(r.Right, r.Y + r.Height * 0.3f));
            g.DrawLine(pen, tip, bottomBase);
            g.DrawLine(pen, tip, leftBase);
            g.DrawLine(pen, new PointF(r.Right - r.Width * 0.3f, r.Y), top);
            g.DrawLine(pen, top, new PointF(r.Right, r.Y + r.Height * 0.3f));
        }

        private static void DrawPlus(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick * 1.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float midX = r.X + r.Width / 2f;
            float midY = r.Y + r.Height / 2f;
            g.DrawLine(pen, midX, r.Y, midX, r.Bottom);
            g.DrawLine(pen, r.X, midY, r.Right, midY);
        }

        private static void DrawTrash(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { LineJoin = LineJoin.Round };
            // Lid
            g.DrawLine(pen, r.X, r.Y + r.Height * 0.25f, r.Right, r.Y + r.Height * 0.25f);
            g.DrawLine(pen, r.X + r.Width * 0.35f, r.Y + r.Height * 0.12f, r.Right - r.Width * 0.35f, r.Y + r.Height * 0.12f);
            // Body
            var body = new PointF[]
            {
                new PointF(r.X + r.Width * 0.15f, r.Y + r.Height * 0.25f),
                new PointF(r.X + r.Width * 0.25f, r.Bottom),
                new PointF(r.Right - r.Width * 0.25f, r.Bottom),
                new PointF(r.Right - r.Width * 0.15f, r.Y + r.Height * 0.25f)
            };
            g.DrawPolygon(pen, body);
            // Ribs
            g.DrawLine(pen, r.X + r.Width * 0.42f, r.Y + r.Height * 0.4f, r.X + r.Width * 0.42f, r.Bottom - r.Height * 0.15f);
            g.DrawLine(pen, r.Right - r.Width * 0.42f, r.Y + r.Height * 0.4f, r.Right - r.Width * 0.42f, r.Bottom - r.Height * 0.15f);
        }

        private static void DrawRefresh(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var brush = new SolidBrush(color);

            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            float rad = r.Width * 0.42f;

            // Arc 1
            g.DrawArc(pen, cx - rad, cy - rad, rad * 2, rad * 2, 45, 120);
            // Arc 2
            g.DrawArc(pen, cx - rad, cy - rad, rad * 2, rad * 2, 225, 120);

            // Arrowheads
            float arrowS = rad * 0.35f;
            PointF p1 = new PointF(cx + rad * 0.65f, cy - rad * 0.7f);
            g.FillPolygon(brush, new PointF[] { p1, new PointF(p1.X + arrowS, p1.Y), new PointF(p1.X, p1.Y - arrowS) });

            PointF p2 = new PointF(cx - rad * 0.65f, cy + rad * 0.7f);
            g.FillPolygon(brush, new PointF[] { p2, new PointF(p2.X - arrowS, p2.Y), new PointF(p2.X, p2.Y + arrowS) });
        }

        private static void DrawSearch(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick);
            float d = r.Width * 0.62f;
            g.DrawEllipse(pen, r.X, r.Y, d, d);
            g.DrawLine(pen, r.X + d * 0.85f, r.Y + d * 0.85f, r.Right, r.Bottom);
        }

        private static void DrawFilter(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { LineJoin = LineJoin.Round };
            PointF[] funnel = new PointF[]
            {
                new PointF(r.X, r.Y),
                new PointF(r.Right, r.Y),
                new PointF(r.X + r.Width * 0.62f, r.Y + r.Height * 0.5f),
                new PointF(r.X + r.Width * 0.62f, r.Bottom),
                new PointF(r.X + r.Width * 0.38f, r.Bottom - r.Height * 0.15f),
                new PointF(r.X + r.Width * 0.38f, r.Y + r.Height * 0.5f)
            };
            g.DrawPolygon(pen, funnel);
        }

        private static void DrawClose(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick * 1.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, r.X, r.Y, r.Right, r.Bottom);
            g.DrawLine(pen, r.Right, r.Y, r.X, r.Bottom);
        }

        private static void DrawCopy(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick);
            float w = r.Width * 0.65f;
            float h = r.Height * 0.65f;

            // Back sheet
            g.DrawRectangle(pen, r.X + r.Width * 0.35f, r.Y, w, h);
            // Front sheet
            using var bg = new SolidBrush(ZeroTheme.Colors.Surface);
            g.FillRectangle(bg, r.X, r.Y + r.Height * 0.35f, w, h);
            g.DrawRectangle(pen, r.X, r.Y + r.Height * 0.35f, w, h);
        }

        private static void DrawCut(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick);
            float rR = r.Width * 0.18f;
            // Two finger rings
            g.DrawEllipse(pen, r.X, r.Bottom - rR * 2, rR * 2, rR * 2);
            g.DrawEllipse(pen, r.Right - rR * 2, r.Bottom - rR * 2, rR * 2, rR * 2);
            // Two blades
            g.DrawLine(pen, r.X + rR, r.Bottom - rR, r.Right - rR, r.Y);
            g.DrawLine(pen, r.Right - rR, r.Bottom - rR, r.X + rR, r.Y);
        }

        private static void DrawPaste(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick);
            // Board
            g.DrawRectangle(pen, r.X + r.Width * 0.1f, r.Y + r.Height * 0.18f, r.Width * 0.8f, r.Height * 0.82f);
            // Clip top
            g.DrawRectangle(pen, r.X + r.Width * 0.32f, r.Y, r.Width * 0.36f, r.Height * 0.22f);
        }

        private static void DrawUndo(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var brush = new SolidBrush(color);
            // Arch
            g.DrawArc(pen, r.X, r.Y + r.Height * 0.1f, r.Width * 0.85f, r.Height * 0.8f, 180, 160);
            // Arrowhead left
            g.FillPolygon(brush, new PointF[]
            {
                new PointF(r.X, r.Y + r.Height * 0.45f),
                new PointF(r.X + r.Width * 0.3f, r.Y + r.Height * 0.25f),
                new PointF(r.X + r.Width * 0.3f, r.Y + r.Height * 0.65f)
            });
        }

        private static void DrawRedo(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var brush = new SolidBrush(color);
            // Arch
            g.DrawArc(pen, r.X + r.Width * 0.15f, r.Y + r.Height * 0.1f, r.Width * 0.85f, r.Height * 0.8f, 240, 160);
            // Arrowhead right
            g.FillPolygon(brush, new PointF[]
            {
                new PointF(r.Right, r.Y + r.Height * 0.45f),
                new PointF(r.Right - r.Width * 0.3f, r.Y + r.Height * 0.25f),
                new PointF(r.Right - r.Width * 0.3f, r.Y + r.Height * 0.65f)
            });
        }

        private static void DrawExport(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { LineJoin = LineJoin.Round };
            // Tray
            g.DrawLines(pen, new PointF[]
            {
                new PointF(r.X, r.Y + r.Height * 0.4f),
                new PointF(r.X, r.Bottom),
                new PointF(r.Right, r.Bottom),
                new PointF(r.Right, r.Y + r.Height * 0.4f)
            });
            // Arrow up
            float midX = r.X + r.Width / 2f;
            g.DrawLine(pen, midX, r.Y, midX, r.Bottom - r.Height * 0.25f);
            g.DrawLines(pen, new PointF[]
            {
                new PointF(midX - r.Width * 0.25f, r.Y + r.Height * 0.3f),
                new PointF(midX, r.Y),
                new PointF(midX + r.Width * 0.25f, r.Y + r.Height * 0.3f)
            });
        }

        private static void DrawImport(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { LineJoin = LineJoin.Round };
            // Tray
            g.DrawLines(pen, new PointF[]
            {
                new PointF(r.X, r.Y + r.Height * 0.4f),
                new PointF(r.X, r.Bottom),
                new PointF(r.Right, r.Bottom),
                new PointF(r.Right, r.Y + r.Height * 0.4f)
            });
            // Arrow down
            float midX = r.X + r.Width / 2f;
            g.DrawLine(pen, midX, r.Y, midX, r.Bottom - r.Height * 0.25f);
            g.DrawLines(pen, new PointF[]
            {
                new PointF(midX - r.Width * 0.25f, r.Bottom - r.Height * 0.5f),
                new PointF(midX, r.Bottom - r.Height * 0.25f),
                new PointF(midX + r.Width * 0.25f, r.Bottom - r.Height * 0.5f)
            });
        }

        private static void DrawPrint(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick);
            // Printer body
            g.DrawRectangle(pen, r.X, r.Y + r.Height * 0.3f, r.Width, r.Height * 0.45f);
            // Top paper
            g.DrawRectangle(pen, r.X + r.Width * 0.2f, r.Y, r.Width * 0.6f, r.Height * 0.3f);
            // Bottom feed sheet
            g.DrawRectangle(pen, r.X + r.Width * 0.2f, r.Y + r.Height * 0.55f, r.Width * 0.6f, r.Height * 0.45f);
        }

        private static void DrawDownload(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float midX = r.X + r.Width / 2f;
            g.DrawLine(pen, midX, r.Y, midX, r.Bottom - r.Height * 0.25f);
            g.DrawLines(pen, new PointF[]
            {
                new PointF(midX - r.Width * 0.25f, r.Bottom - r.Height * 0.5f),
                new PointF(midX, r.Bottom - r.Height * 0.25f),
                new PointF(midX + r.Width * 0.25f, r.Bottom - r.Height * 0.5f)
            });
            g.DrawLine(pen, r.X, r.Bottom, r.Right, r.Bottom);
        }

        private static void DrawUpload(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float midX = r.X + r.Width / 2f;
            g.DrawLine(pen, midX, r.Y, midX, r.Bottom - r.Height * 0.25f);
            g.DrawLines(pen, new PointF[]
            {
                new PointF(midX - r.Width * 0.25f, r.Y + r.Height * 0.25f),
                new PointF(midX, r.Y),
                new PointF(midX + r.Width * 0.25f, r.Y + r.Height * 0.25f)
            });
            g.DrawLine(pen, r.X, r.Bottom, r.Right, r.Bottom);
        }

        private static void DrawFolder(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { LineJoin = LineJoin.Round };
            PointF[] folder = new PointF[]
            {
                new PointF(r.X, r.Y + r.Height * 0.25f),
                new PointF(r.X + r.Width * 0.35f, r.Y + r.Height * 0.25f),
                new PointF(r.X + r.Width * 0.48f, r.Y + r.Height * 0.1f),
                new PointF(r.Right, r.Y + r.Height * 0.1f),
                new PointF(r.Right, r.Bottom),
                new PointF(r.X, r.Bottom)
            };
            g.DrawPolygon(pen, folder);
        }

        private static void DrawDocument(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { LineJoin = LineJoin.Round };
            float fold = r.Width * 0.3f;
            PointF[] doc = new PointF[]
            {
                new PointF(r.X, r.Y),
                new PointF(r.Right - fold, r.Y),
                new PointF(r.Right, r.Y + fold),
                new PointF(r.Right, r.Bottom),
                new PointF(r.X, r.Bottom)
            };
            g.DrawPolygon(pen, doc);
            g.DrawLine(pen, r.Right - fold, r.Y, r.Right - fold, r.Y + fold);
            g.DrawLine(pen, r.Right - fold, r.Y + fold, r.Right, r.Y + fold);
        }

        private static void DrawArrow(Graphics g, RectangleF r, Color color, float thick, float angle)
        {
            using var pen = new Pen(color, thick) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            var state = g.Save();
            g.TranslateTransform(r.X + r.Width / 2f, r.Y + r.Height / 2f);
            g.RotateTransform(angle);

            float half = r.Width * 0.42f;
            g.DrawLine(pen, -half, 0, half, 0);
            g.DrawLine(pen, half - half * 0.55f, -half * 0.55f, half, 0);
            g.DrawLine(pen, half - half * 0.55f, half * 0.55f, half, 0);

            g.Restore(state);
        }

        private static void DrawHome(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { LineJoin = LineJoin.Round };
            PointF[] roof = new PointF[]
            {
                new PointF(r.X, r.Y + r.Height * 0.42f),
                new PointF(r.X + r.Width / 2f, r.Y),
                new PointF(r.Right, r.Y + r.Height * 0.42f)
            };
            g.DrawLines(pen, roof);
            g.DrawRectangle(pen, r.X + r.Width * 0.18f, r.Y + r.Height * 0.42f, r.Width * 0.64f, r.Height * 0.58f);
        }

        private static void DrawMenu(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, r.X, r.Y + r.Height * 0.2f, r.Right, r.Y + r.Height * 0.2f);
            g.DrawLine(pen, r.X, r.Y + r.Height * 0.5f, r.Right, r.Y + r.Height * 0.5f);
            g.DrawLine(pen, r.X, r.Y + r.Height * 0.8f, r.Right, r.Y + r.Height * 0.8f);
        }

        private static void DrawMore(Graphics g, RectangleF r, Color color)
        {
            using var brush = new SolidBrush(color);
            float dotR = r.Width * 0.08f;
            float midY = r.Y + r.Height / 2f;
            g.FillEllipse(brush, r.X + r.Width * 0.2f - dotR, midY - dotR, dotR * 2, dotR * 2);
            g.FillEllipse(brush, r.X + r.Width * 0.5f - dotR, midY - dotR, dotR * 2, dotR * 2);
            g.FillEllipse(brush, r.X + r.Width * 0.8f - dotR, midY - dotR, dotR * 2, dotR * 2);
        }

        private static void DrawAlign(Graphics g, RectangleF r, Color color, float thick, int mode)
        {
            using var pen = new Pen(color, thick);
            using var brush = new SolidBrush(color);

            switch (mode)
            {
                case 0: // Align Left
                    g.DrawLine(pen, r.X, r.Y, r.X, r.Bottom);
                    g.FillRectangle(brush, r.X + thick * 2, r.Y + r.Height * 0.18f, r.Width * 0.65f, r.Height * 0.25f);
                    g.FillRectangle(brush, r.X + thick * 2, r.Y + r.Height * 0.58f, r.Width * 0.45f, r.Height * 0.25f);
                    break;
                case 1: // Align Center H
                    float midX = r.X + r.Width / 2f;
                    g.DrawLine(pen, midX, r.Y, midX, r.Bottom);
                    g.FillRectangle(brush, midX - r.Width * 0.35f, r.Y + r.Height * 0.18f, r.Width * 0.7f, r.Height * 0.25f);
                    g.FillRectangle(brush, midX - r.Width * 0.22f, r.Y + r.Height * 0.58f, r.Width * 0.44f, r.Height * 0.25f);
                    break;
                case 2: // Align Right
                    g.DrawLine(pen, r.Right, r.Y, r.Right, r.Bottom);
                    g.FillRectangle(brush, r.Right - thick * 2 - r.Width * 0.65f, r.Y + r.Height * 0.18f, r.Width * 0.65f, r.Height * 0.25f);
                    g.FillRectangle(brush, r.Right - thick * 2 - r.Width * 0.45f, r.Y + r.Height * 0.58f, r.Width * 0.45f, r.Height * 0.25f);
                    break;
                case 3: // Align Top
                    g.DrawLine(pen, r.X, r.Y, r.Right, r.Y);
                    g.FillRectangle(brush, r.X + r.Width * 0.18f, r.Y + thick * 2, r.Width * 0.25f, r.Height * 0.65f);
                    g.FillRectangle(brush, r.X + r.Width * 0.58f, r.Y + thick * 2, r.Width * 0.25f, r.Height * 0.45f);
                    break;
                case 4: // Align Middle V
                    float midY = r.Y + r.Height / 2f;
                    g.DrawLine(pen, r.X, midY, r.Right, midY);
                    g.FillRectangle(brush, r.X + r.Width * 0.18f, midY - r.Height * 0.35f, r.Width * 0.25f, r.Height * 0.7f);
                    g.FillRectangle(brush, r.X + r.Width * 0.58f, midY - r.Height * 0.22f, r.Width * 0.25f, r.Height * 0.44f);
                    break;
                case 5: // Align Bottom
                    g.DrawLine(pen, r.X, r.Bottom, r.Right, r.Bottom);
                    g.FillRectangle(brush, r.X + r.Width * 0.18f, r.Bottom - thick * 2 - r.Height * 0.65f, r.Width * 0.25f, r.Height * 0.65f);
                    g.FillRectangle(brush, r.X + r.Width * 0.58f, r.Bottom - thick * 2 - r.Height * 0.45f, r.Width * 0.25f, r.Height * 0.45f);
                    break;
            }
        }

        private static void DrawDistributeH(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick);
            using var brush = new SolidBrush(color);
            g.DrawLine(pen, r.X, r.Y, r.X, r.Bottom);
            g.DrawLine(pen, r.Right, r.Y, r.Right, r.Bottom);
            g.FillRectangle(brush, r.X + r.Width * 0.32f, r.Y + r.Height * 0.2f, r.Width * 0.36f, r.Height * 0.6f);
        }

        private static void DrawDistributeV(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick);
            using var brush = new SolidBrush(color);
            g.DrawLine(pen, r.X, r.Y, r.Right, r.Y);
            g.DrawLine(pen, r.X, r.Bottom, r.Right, r.Bottom);
            g.FillRectangle(brush, r.X + r.Width * 0.2f, r.Y + r.Height * 0.32f, r.Width * 0.6f, r.Height * 0.36f);
        }

        private static void DrawConnect(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { LineJoin = LineJoin.Round };
            float size = r.Width * 0.45f;
            g.DrawRectangle(pen, r.X, r.Y, size, size);
            g.DrawRectangle(pen, r.Right - size, r.Bottom - size, size, size);
            g.DrawLine(pen, r.X + size * 0.8f, r.Y + size * 0.8f, r.Right - size * 0.8f, r.Bottom - size * 0.8f);
        }

        private static void DrawUnlink(Graphics g, RectangleF r, Color color, float thick)
        {
            DrawConnect(g, r, color, thick);
            using var pen = new Pen(Color.FromArgb(239, 68, 68), thick * 1.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, r.X + r.Width * 0.25f, r.Y + r.Height * 0.75f, r.Right - r.Width * 0.25f, r.Y + r.Height * 0.25f);
        }

        private static void DrawShape(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick);
            PointF[] diamond = new PointF[]
            {
                new PointF(r.X + r.Width / 2f, r.Y),
                new PointF(r.Right, r.Y + r.Height / 2f),
                new PointF(r.X + r.Width / 2f, r.Bottom),
                new PointF(r.X, r.Y + r.Height / 2f)
            };
            g.DrawPolygon(pen, diamond);
        }

        private static void DrawPalette(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick);
            using var brush = new SolidBrush(color);
            g.DrawEllipse(pen, r);
            float dotR = r.Width * 0.08f;
            g.FillEllipse(brush, r.X + r.Width * 0.3f, r.Y + r.Height * 0.25f, dotR * 2, dotR * 2);
            g.FillEllipse(brush, r.X + r.Width * 0.65f, r.Y + r.Height * 0.35f, dotR * 2, dotR * 2);
            g.FillEllipse(brush, r.X + r.Width * 0.45f, r.Y + r.Height * 0.6f, dotR * 2, dotR * 2);
        }

        private static void DrawSettings(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick);
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            float ro = r.Width * 0.45f;
            float ri = r.Width * 0.22f;

            // Cog teeth
            for (int i = 0; i < 6; i++)
            {
                double rad = i * Math.PI / 3.0;
                float tx = cx + (float)(Math.Cos(rad) * ro);
                float ty = cy + (float)(Math.Sin(rad) * ro);
                g.DrawLine(pen, cx, cy, tx, ty);
            }
            g.DrawEllipse(pen, cx - ri, cy - ri, ri * 2, ri * 2);
        }

        private static void DrawInfo(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick);
            using var brush = new SolidBrush(color);
            g.DrawEllipse(pen, r);
            float midX = r.X + r.Width / 2f;
            float dotR = r.Width * 0.07f;
            g.FillEllipse(brush, midX - dotR, r.Y + r.Height * 0.22f, dotR * 2, dotR * 2);
            g.DrawLine(pen, midX, r.Y + r.Height * 0.42f, midX, r.Bottom - r.Height * 0.22f);
        }

        private static void DrawWarning(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { LineJoin = LineJoin.Round };
            using var brush = new SolidBrush(color);
            PointF[] tri = new PointF[]
            {
                new PointF(r.X + r.Width / 2f, r.Y),
                new PointF(r.Right, r.Bottom),
                new PointF(r.X, r.Bottom)
            };
            g.DrawPolygon(pen, tri);
            float midX = r.X + r.Width / 2f;
            g.DrawLine(pen, midX, r.Y + r.Height * 0.4f, midX, r.Y + r.Height * 0.65f);
            float dotR = r.Width * 0.06f;
            g.FillEllipse(brush, midX - dotR, r.Bottom - r.Height * 0.2f, dotR * 2, dotR * 2);
        }

        private static void DrawSuccess(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick * 1.3f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            PointF[] check = new PointF[]
            {
                new PointF(r.X + r.Width * 0.15f, r.Y + r.Height * 0.52f),
                new PointF(r.X + r.Width * 0.42f, r.Bottom - r.Height * 0.2f),
                new PointF(r.Right - r.Width * 0.12f, r.Y + r.Height * 0.25f)
            };
            g.DrawLines(pen, check);
        }

        private static void DrawError(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick);
            g.DrawEllipse(pen, r);
            DrawClose(g, new RectangleF(r.X + r.Width * 0.22f, r.Y + r.Height * 0.22f, r.Width * 0.56f, r.Height * 0.56f), color, thick);
        }

        private static void DrawLock(Graphics g, RectangleF r, Color color, float thick, bool open)
        {
            using var pen = new Pen(color, thick);
            // Body
            g.DrawRectangle(pen, r.X + r.Width * 0.15f, r.Y + r.Height * 0.4f, r.Width * 0.7f, r.Height * 0.58f);
            // Shackle
            float sW = r.Width * 0.4f;
            float sH = r.Height * 0.35f;
            float sX = r.X + (r.Width - sW) / 2f;
            float sY = open ? r.Y - r.Height * 0.1f : r.Y + r.Height * 0.08f;
            g.DrawArc(pen, sX, sY, sW, sH * 2, 180, 180);
        }

        private static void DrawZoom(Graphics g, RectangleF r, Color color, float thick, int mode)
        {
            DrawSearch(g, r, color, thick);
            using var pen = new Pen(color, thick * 0.9f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float cx = r.X + r.Width * 0.31f;
            float cy = r.Y + r.Height * 0.31f;
            float len = r.Width * 0.16f;

            if (mode == 1) // Zoom In (+)
            {
                g.DrawLine(pen, cx - len, cy, cx + len, cy);
                g.DrawLine(pen, cx, cy - len, cx, cy + len);
            }
            else if (mode == -1) // Zoom Out (-)
            {
                g.DrawLine(pen, cx - len, cy, cx + len, cy);
            }
            else // Zoom Fit (center dot / target)
            {
                using var brush = new SolidBrush(color);
                g.FillEllipse(brush, cx - len * 0.5f, cy - len * 0.5f, len, len);
            }
        }

        private static void DrawPlay(Graphics g, RectangleF r, Color color)
        {
            using var brush = new SolidBrush(color);
            PointF[] tri = new PointF[]
            {
                new PointF(r.X + r.Width * 0.2f, r.Y + r.Height * 0.15f),
                new PointF(r.Right - r.Width * 0.15f, r.Y + r.Height * 0.5f),
                new PointF(r.X + r.Width * 0.2f, r.Bottom - r.Height * 0.15f)
            };
            g.FillPolygon(brush, tri);
        }

        private static void DrawPause(Graphics g, RectangleF r, Color color)
        {
            using var brush = new SolidBrush(color);
            float barW = r.Width * 0.25f;
            g.FillRectangle(brush, r.X + r.Width * 0.15f, r.Y + r.Height * 0.15f, barW, r.Height * 0.7f);
            g.FillRectangle(brush, r.Right - r.Width * 0.15f - barW, r.Y + r.Height * 0.15f, barW, r.Height * 0.7f);
        }

        private static void DrawStop(Graphics g, RectangleF r, Color color)
        {
            using var brush = new SolidBrush(color);
            g.FillRectangle(brush, r.X + r.Width * 0.18f, r.Y + r.Height * 0.18f, r.Width * 0.64f, r.Height * 0.64f);
        }

        private static void DrawStep(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick);
            g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
            g.DrawLine(pen, r.X, r.Y + r.Height * 0.32f, r.Right, r.Y + r.Height * 0.32f);
        }

        private static void DrawSwimlane(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick);
            g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
            g.DrawLine(pen, r.X + r.Width * 0.3f, r.Y, r.X + r.Width * 0.3f, r.Bottom);
        }

        private static void DrawAutoLayout(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick);
            using var brush = new SolidBrush(color);
            float s = r.Width * 0.35f;
            g.DrawRectangle(pen, r.X, r.Y, s, s);
            g.DrawRectangle(pen, r.Right - s, r.Y + r.Height * 0.15f, s, s);
            g.DrawRectangle(pen, r.X + r.Width * 0.25f, r.Bottom - s, s, s);
            g.DrawLine(pen, r.X + s, r.Y + s / 2f, r.Right - s, r.Y + r.Height * 0.15f + s / 2f);
        }

        private static void DrawFullscreen(Graphics g, RectangleF r, Color color, float thick)
        {
            using var pen = new Pen(color, thick) { StartCap = LineCap.Square, EndCap = LineCap.Square };
            float corner = r.Width * 0.28f;
            // Top Left
            g.DrawLines(pen, new PointF[] { new PointF(r.X, r.Y + corner), new PointF(r.X, r.Y), new PointF(r.X + corner, r.Y) });
            // Top Right
            g.DrawLines(pen, new PointF[] { new PointF(r.Right - corner, r.Y), new PointF(r.Right, r.Y), new PointF(r.Right, r.Y + corner) });
            // Bottom Right
            g.DrawLines(pen, new PointF[] { new PointF(r.Right, r.Bottom - corner), new PointF(r.Right, r.Bottom), new PointF(r.Right - corner, r.Bottom) });
            // Bottom Left
            g.DrawLines(pen, new PointF[] { new PointF(r.X + corner, r.Bottom), new PointF(r.X, r.Bottom), new PointF(r.X, r.Bottom - corner) });
        }

        #endregion
    }
}
