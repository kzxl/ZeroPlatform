using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace ZeroUI.WinForms.Design.Editors
{
    /// <summary>
    /// DropDown UITypeEditor presenting an interactive Obsidian & Modern Enterprise Color Swatch Matrix.
    /// </summary>
    public class ColorPickPaletteEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context) => UITypeEditorEditStyle.DropDown;

        public override object? EditValue(ITypeDescriptorContext? context, IServiceProvider provider, object? value)
        {
            if (provider?.GetService(typeof(IWindowsFormsEditorService)) is IWindowsFormsEditorService editorService)
            {
                Color current = value is Color c ? c : Color.FromArgb(0, 210, 255);
                using var paletteControl = new ColorSwatchDropDown(current, color =>
                {
                    current = color;
                    editorService.CloseDropDown();
                });
                editorService.DropDownControl(paletteControl);
                return current;
            }
            return value;
        }

        public override bool GetPaintValueSupported(ITypeDescriptorContext? context) => true;

        public override void PaintValue(PaintValueEventArgs e)
        {
            if (e.Value is Color c)
            {
                using var brush = new SolidBrush(c);
                e.Graphics.FillRectangle(brush, e.Bounds);
                e.Graphics.DrawRectangle(Pens.Gray, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
            }
        }

        public class ColorSwatchDropDown : UserControl
        {
            private readonly Action<Color> _onSelect;
            private static readonly Color[] EnterpriseSwatches = new[]
            {
                // Row 1: Primary Accents
                Color.FromArgb(0, 210, 255), Color.FromArgb(14, 165, 233), Color.FromArgb(59, 130, 246), Color.FromArgb(99, 102, 241),
                // Row 2: Status & Safety
                Color.FromArgb(16, 185, 129), Color.FromArgb(245, 158, 11), Color.FromArgb(239, 68, 68), Color.FromArgb(217, 70, 239),
                // Row 3: Industrial Neutrals
                Color.FromArgb(248, 250, 252), Color.FromArgb(203, 213, 225), Color.FromArgb(100, 116, 139), Color.FromArgb(30, 41, 59),
                // Row 4: Obsidian Darks
                Color.FromArgb(15, 23, 42), Color.FromArgb(18, 24, 38), Color.FromArgb(10, 14, 23), Color.FromArgb(0, 0, 0)
            };

            public ColorSwatchDropDown(Color initial, Action<Color> onSelect)
            {
                _onSelect = onSelect;
                Size = new Size(160, 160);
                BackColor = Color.FromArgb(24, 30, 42);
                Padding = new Padding(6);
                DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.Clear(BackColor);

                int cellW = 32;
                int cellH = 32;
                int pad = 4;

                for (int i = 0; i < EnterpriseSwatches.Length; i++)
                {
                    int row = i / 4;
                    int col = i % 4;
                    var rect = new Rectangle(6 + col * (cellW + pad), 6 + row * (cellH + pad), cellW, cellH);

                    using var brush = new SolidBrush(EnterpriseSwatches[i]);
                    g.FillRectangle(brush, rect);
                    g.DrawRectangle(Pens.Black, rect);
                }
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                int cellW = 32;
                int cellH = 32;
                int pad = 4;

                for (int i = 0; i < EnterpriseSwatches.Length; i++)
                {
                    int row = i / 4;
                    int col = i % 4;
                    var rect = new Rectangle(6 + col * (cellW + pad), 6 + row * (cellH + pad), cellW, cellH);
                    if (rect.Contains(e.Location))
                    {
                        _onSelect(EnterpriseSwatches[i]);
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Interactive dialog displaying an enterprise color swatch matrix.
    /// </summary>
    public class ZeroColorPaletteDialog : Form
    {
        public Color SelectedColor { get; private set; }

        public ZeroColorPaletteDialog(Color initial)
        {
            SelectedColor = initial;
            Text = "Select Palette Color";
            Size = new Size(200, 240);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(24, 30, 42);

            var dropdown = new ColorPickPaletteEditor.ColorSwatchDropDown(initial, c =>
            {
                SelectedColor = c;
                DialogResult = DialogResult.OK;
                Close();
            })
            {
                Dock = DockStyle.Fill
            };
            Controls.Add(dropdown);
        }
    }
}
