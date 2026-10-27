using System;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Design.Forms
{
    /// <summary>
    /// Modern modal dialog displaying generated C# Fluent Setup code or XAML markup with 1-click clipboard copy.
    /// </summary>
    public class CodeExportForm : Form
    {
        private readonly TextBox _txtCode;
        private readonly Button _btnCopy;
        private readonly Button _btnClose;
        private readonly Label _lblStatus;

        public CodeExportForm(string title, string codeSnippet)
            : this(title, "C# / XAML", codeSnippet)
        {
        }

        public CodeExportForm(string title, string language, string codeSnippet)
        {
            Text = title;
            Size = new Size(680, 520);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(500, 360);
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            BackColor = Color.FromArgb(246, 248, 250);

            // Banner
            var pnlBanner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 55,
                BackColor = Color.FromArgb(30, 35, 45),
                Padding = new Padding(16, 10, 16, 10)
            };
            var lblBanner = new Label
            {
                Text = $"Generated {language} Code Snippet",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                AutoSize = true
            };
            var lblSub = new Label
            {
                Text = "Copy and paste this snippet directly into your view or viewmodel code.",
                Font = new Font("Segoe UI", 8.25f),
                ForeColor = Color.FromArgb(180, 190, 205),
                Dock = DockStyle.Bottom,
                AutoSize = true
            };
            pnlBanner.Controls.Add(lblBanner);
            pnlBanner.Controls.Add(lblSub);

            // Code Editor TextBox
            _txtCode = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10f, FontStyle.Regular),
                BackColor = Color.FromArgb(28, 30, 36),
                ForeColor = Color.FromArgb(220, 225, 235),
                Text = codeSnippet
            };

            var pnlEditor = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 12, 16, 12)
            };
            pnlEditor.Controls.Add(_txtCode);

            // Bottom action bar
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = Color.FromArgb(235, 238, 242),
                Padding = new Padding(16, 8, 16, 8)
            };

            _lblStatus = new Label
            {
                Text = string.Empty,
                ForeColor = Color.FromArgb(16, 149, 100),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Dock = DockStyle.Left,
                AutoSize = true,
                Padding = new Padding(0, 8, 0, 0)
            };

            _btnClose = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                Width = 90,
                Height = 32,
                Dock = DockStyle.Right
            };

            _btnCopy = new Button
            {
                Text = "Copy to Clipboard",
                Width = 140,
                Height = 32,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(79, 70, 229),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnCopy.FlatAppearance.BorderSize = 0;
            _btnCopy.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(_txtCode.Text);
                    _lblStatus.Text = "✓ Copied to clipboard!";
                }
                catch
                {
                    _lblStatus.Text = "Select and use Ctrl+C";
                }
            };

            pnlBottom.Controls.Add(_lblStatus);
            pnlBottom.Controls.Add(_btnCopy);
            pnlBottom.Controls.Add(new Label { Width = 10, Dock = DockStyle.Right });
            pnlBottom.Controls.Add(_btnClose);

            Controls.Add(pnlEditor);
            Controls.Add(pnlBottom);
            Controls.Add(pnlBanner);
        }

        /// <summary>
        /// Generates standard C# setup code for any supported ZeroUI control instance.
        /// </summary>
        public static string GenerateCSharpCode(string controlName, object controlInstance)
        {
            if (controlInstance is ZeroUI.WinForms.DataGrid.ZGrid grid)
            {
                return GridDesignerForm.GenerateCSharpSetupCode(grid);
            }
            if (controlInstance is ZeroUI.WinForms.Charts.ZChart chart)
            {
                return ChartWizardForm.GenerateCSharpSetupCode(chart);
            }
            if (controlInstance is ZeroUI.WinForms.Data.ZTreeList treeList)
            {
                return TreeListDesignerForm.GenerateCSharpSetupCode(treeList);
            }
            return $"// Setup code for {controlName}\nvar ctrl = new {controlName}();\n// Configure properties as desired.";
        }

        /// <summary>
        /// Generates standard XAML setup markup for any supported ZeroUI control instance.
        /// </summary>
        public static string GenerateXamlCode(string controlName, object controlInstance)
        {
            if (controlInstance is ZeroUI.WinForms.DataGrid.ZGrid grid)
            {
                return GridDesignerForm.GenerateXamlSetupCode(grid);
            }
            if (controlInstance is ZeroUI.WinForms.Charts.ZChart chart)
            {
                return ChartWizardForm.GenerateXamlSetupCode(chart);
            }
            if (controlInstance is ZeroUI.WinForms.Data.ZTreeList treeList)
            {
                return TreeListDesignerForm.GenerateXamlSetupCode(treeList);
            }
            return $"<!-- XAML Setup for {controlName} -->\n<controls:{controlName} />";
        }
    }
}
