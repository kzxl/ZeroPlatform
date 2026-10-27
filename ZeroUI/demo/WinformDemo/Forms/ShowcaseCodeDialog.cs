using System;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.WinForms.Base;

namespace ZeroUI.Samples.WinformDemo.Forms
{
    public sealed class ShowcaseCodeDialog : BaseForm
    {
        private readonly RichTextBox _txtCode;

        public ShowcaseCodeDialog(string title, string codeSnippet)
        {
            Text = $"C# Code — {title}";
            Size = new Size(760, 520);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;
            ShowIcon = false;

            var topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.FromArgb(40, 44, 52),
                Padding = new Padding(12, 8, 12, 8)
            };

            var lblTitle = new Label
            {
                Dock = DockStyle.Left,
                Text = $"📄 {title} — C# Integration Code",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = true
            };

            var btnCopy = new Button
            {
                Dock = DockStyle.Right,
                Width = 110,
                Text = "📋 Copy Code",
                BackColor = Color.FromArgb(18, 86, 209),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCopy.FlatAppearance.BorderSize = 0;
            btnCopy.Click += (s, e) =>
            {
                Clipboard.SetText(codeSnippet);
                btnCopy.Text = "✅ Copied!";
                var t = new Timer { Interval = 1500 };
                t.Tick += (ts, te) =>
                {
                    btnCopy.Text = "📋 Copy Code";
                    t.Stop();
                    t.Dispose();
                };
                t.Start();
            };

            topBar.Controls.Add(lblTitle);
            topBar.Controls.Add(btnCopy);

            _txtCode = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 10.5f, FontStyle.Regular),
                ReadOnly = true,
                WordWrap = false
            };
            _txtCode.Text = codeSnippet;

            Controls.Add(_txtCode);
            Controls.Add(topBar);
        }
    }
}
