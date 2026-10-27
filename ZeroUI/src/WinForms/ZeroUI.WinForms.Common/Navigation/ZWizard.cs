using System;

using ZeroUI.WinForms.Icons;using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Localization;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Navigation
{
    public class WizardPageValidatingEventArgs : CancelEventArgs
    {
        public int PageIndex { get; }
        public WizardPage Page { get; }
        public string? ErrorMessage { get; set; }

        public WizardPageValidatingEventArgs(int pageIndex, WizardPage page)
        {
            PageIndex = pageIndex;
            Page = page;
        }
    }

    /// <summary>
    /// Represents an individual step/page container in a ZeroWizard sequence.
    /// </summary>
    [ToolboxItem(false)]
    public class WizardPage : Panel
    {
        public string Title { get; set; } = "Step Title";
        public string Subtitle { get; set; } = "Please review and configure this step.";
        public string Icon { get; set; } = "📋";

        public event EventHandler<WizardPageValidatingEventArgs>? ValidatingStep;

        public WizardPage()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
        }

        public WizardPage(string title, string subtitle = "", string icon = "📋") : this()
        {
            Title = title;
            Subtitle = subtitle;
            Icon = icon;
        }

        internal bool ValidatePage(int index, out string? error)
        {
            error = null;
            if (ValidatingStep != null)
            {
                var args = new WizardPageValidatingEventArgs(index, this);
                ValidatingStep(this, args);
                if (args.Cancel)
                {
                    error = args.ErrorMessage;
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// Modern Enterprise Multi-Step Wizard container for WinForms.
    /// Guides users through sequential configuration steps with progress bar,
    /// per-step validation, back/next/finish navigation, and dark/light theming.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Layout & Navigation")]
    [DefaultEvent("Finished")]
    [Description("Enterprise multi-step guided process wizard with validation and step indicator")]
    [ToolboxBitmap(typeof(ZeroIcons), "WizardControl.bmp")]
    public class ZWizard : Control
    {
        private readonly List<WizardPage> _pages = new List<WizardPage>();
        private int _currentStep = 0;

        private readonly Panel _headerPanel = new Panel { Dock = DockStyle.Top, Height = 64 };
        private readonly Panel _contentPanel = new Panel { Dock = DockStyle.Fill };
        private readonly Panel _footerPanel = new Panel { Dock = DockStyle.Bottom, Height = 56 };

        private readonly ZButton _btnBack = new ZButton { Text = "◀ Back", Width = 90, Height = 34 };
        private readonly ZButton _btnNext = new ZButton { Text = "Next ▶", Width = 90, Height = 34 };
        private readonly ZButton _btnFinish = new ZButton { Text = "✔ Finish", Width = 100, Height = 34, Visible = false };
        private readonly ZButton _btnCancel = new ZButton { Text = "Cancel", Width = 80, Height = 34 };

        public event EventHandler? StepChanged;
        public event EventHandler? Finished;
        public event EventHandler? Cancelled;

        [Browsable(false)]
        public List<WizardPage> Pages => _pages;

        public int CurrentStep
        {
            get => _currentStep;
            set
            {
                if (value >= 0 && value < _pages.Count && _currentStep != value)
                {
                    _currentStep = value;
                    UpdateWizardView();
                }
            }
        }

        public ZWizard()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = ZeroTheme.Colors.Background;
            Size = new Size(680, 480);

            // Assemble layout panels
            Controls.Add(_contentPanel);
            Controls.Add(_footerPanel);
            Controls.Add(_headerPanel);

            _headerPanel.Paint += HeaderPanel_Paint;
            _footerPanel.Paint += FooterPanel_Paint;

            // Setup footer buttons
            _footerPanel.Controls.Add(_btnCancel);
            _footerPanel.Controls.Add(_btnBack);
            _footerPanel.Controls.Add(_btnNext);
            _footerPanel.Controls.Add(_btnFinish);

            _btnBack.Click += (s, e) => PreviousStep();
            _btnNext.Click += (s, e) => NextStep();
            _btnFinish.Click += (s, e) => CompleteWizard();
            _btnCancel.Click += (s, e) => Cancelled?.Invoke(this, EventArgs.Empty);

            _footerPanel.Resize += (s, e) => PositionFooterButtons();
            PositionFooterButtons();

            UpdateLocalizedStrings();
            Localizer.CultureChanged += (s, e) => UpdateLocalizedStrings();

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        private void PositionFooterButtons()
        {
            int margin = 14;
            int right = _footerPanel.Width - margin;
            int top = (_footerPanel.Height - 34) / 2;

            _btnCancel.Location = new Point(margin, top);

            _btnFinish.Location = new Point(right - _btnFinish.Width, top);
            _btnNext.Location = new Point(right - _btnNext.Width, top);
            _btnBack.Location = new Point(_btnNext.Left - _btnBack.Width - 10, top);
        }

        public void AddPage(WizardPage page)
        {
            if (page == null || _pages.Contains(page)) return;
            _pages.Add(page);
            if (_pages.Count == 1)
            {
                UpdateWizardView();
            }
            _headerPanel.Invalidate();
        }

        public bool NextStep()
        {
            if (_pages.Count == 0 || _currentStep >= _pages.Count - 1) return false;

            var curPage = _pages[_currentStep];
            if (!curPage.ValidatePage(_currentStep, out string? error))
            {
                if (!string.IsNullOrEmpty(error))
                {
                    MessageBox.Show(error, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }

            _currentStep++;
            UpdateWizardView();
            return true;
        }

        public void PreviousStep()
        {
            if (_currentStep > 0)
            {
                _currentStep--;
                UpdateWizardView();
            }
        }

        public void CompleteWizard()
        {
            if (_pages.Count == 0) return;
            var curPage = _pages[_currentStep];
            if (curPage.ValidatePage(_currentStep, out string? error))
            {
                Finished?.Invoke(this, EventArgs.Empty);
            }
            else if (!string.IsNullOrEmpty(error))
            {
                MessageBox.Show(error, Localizer.GetString(StringId.WizardValidationTitle), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateLocalizedStrings()
        {
            _btnBack.Text = Localizer.GetString(StringId.WizardBack);
            _btnNext.Text = Localizer.GetString(StringId.WizardNext);
            _btnFinish.Text = Localizer.GetString(StringId.WizardFinish);
            _btnCancel.Text = Localizer.GetString(StringId.WizardCancel);
            PositionFooterButtons();
            Invalidate(true);
        }

        private void UpdateWizardView()
        {
            _contentPanel.Controls.Clear();
            if (_pages.Count > 0 && _currentStep >= 0 && _currentStep < _pages.Count)
            {
                var page = _pages[_currentStep];
                _contentPanel.Controls.Add(page);
            }

            bool isLast = (_currentStep == _pages.Count - 1);
            _btnBack.Enabled = (_currentStep > 0);
            _btnNext.Visible = !isLast;
            _btnFinish.Visible = isLast;

            _headerPanel.Invalidate();
            StepChanged?.Invoke(this, EventArgs.Empty);
        }

        private void HeaderPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var colors = ZeroTheme.Colors;
            g.Clear(colors.Surface);

            // Bottom border
            using (var pen = new Pen(colors.Border))
            {
                g.DrawLine(pen, 0, _headerPanel.Height - 1, _headerPanel.Width, _headerPanel.Height - 1);
            }

            if (_pages.Count == 0) return;

            // Step dots / progress indicator
            int totalSteps = _pages.Count;
            int stepAreaW = Math.Min(320, _headerPanel.Width / 2);
            int stepX = _headerPanel.Width - stepAreaW - 20;
            int stepY = 28;

            int dotSize = 22;
            int stepSpacing = totalSteps > 1 ? (stepAreaW - totalSteps * dotSize) / (totalSteps - 1) : 0;

            for (int i = 0; i < totalSteps; i++)
            {
                int x = stepX + i * (dotSize + stepSpacing);

                // Connecting line
                if (i < totalSteps - 1)
                {
                    Color lineColor = (i < _currentStep) ? colors.Primary : colors.Border;
                    using (var pen = new Pen(lineColor, 2f))
                    {
                        g.DrawLine(pen, x + dotSize, stepY + dotSize / 2, x + dotSize + stepSpacing, stepY + dotSize / 2);
                    }
                }

                // Step Dot
                var dotRect = new Rectangle(x, stepY, dotSize, dotSize);
                bool isDone = i < _currentStep;
                bool isCurrent = i == _currentStep;

                using (var brush = new SolidBrush(isCurrent || isDone ? colors.Primary : colors.HeaderBackground))
                {
                    g.FillEllipse(brush, dotRect);
                }
                using (var pen = new Pen(isCurrent ? colors.Primary : colors.Border, 1.5f))
                {
                    g.DrawEllipse(pen, dotRect);
                }

                string numText = isDone ? "✓" : (i + 1).ToString();
                using (var brush = new SolidBrush(isCurrent || isDone ? Color.White : colors.TextSecondary))
                using (var font = new Font("Segoe UI", 9f, FontStyle.Bold))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(numText, font, brush, dotRect, sf);
                }
            }

            // Current Step Title & Subtitle
            var curPage = _pages[_currentStep];
            using (var titleFont = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var subFont = new Font("Segoe UI", 9f, FontStyle.Regular))
            using (var textBrush = new SolidBrush(colors.TextPrimary))
            using (var subBrush = new SolidBrush(colors.TextSecondary))
            {
                g.DrawString($"{curPage.Icon}  {curPage.Title}", titleFont, textBrush, 18, 12);
                g.DrawString(curPage.Subtitle, subFont, subBrush, 22, 36);
            }
        }

        private void FooterPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var colors = ZeroTheme.Colors;
            g.Clear(colors.Surface);

            // Top border
            using (var pen = new Pen(colors.Border))
            {
                g.DrawLine(pen, 0, 0, _footerPanel.Width, 0);
            }
        }
    
    private void OnThemeChanged(object sender, EventArgs e)
    {
                BackColor = ZeroTheme.Colors.Background;
                Invalidate(true);
            }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }

}

    /// <summary>
    /// Legacy alias for WizardControl.
    /// Preserved for 100% backward compatibility.
    /// </summary>
    [Obsolete("ZeroWizard is deprecated. Please use WizardControl instead.")]
    [ToolboxItem(false)]
    public class ZeroWizard : WizardControl
    {
    }

    [Obsolete("Use WizardPage instead.")]
    [ToolboxItem(false)]
    public class ZeroWizardPage : WizardPage
    {
        public ZeroWizardPage() : base() { }
        public ZeroWizardPage(string title, string subtitle = "", string icon = "📋") : base(title, subtitle, icon) { }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZWizard"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("WizardControl is deprecated and will be removed in 5 release cycles. Please migrate to ZWizard instead.")]
    [ToolboxItem(false)]
    public class WizardControl : ZWizard
    {
    }

    #endregion
}
