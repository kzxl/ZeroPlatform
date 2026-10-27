using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Visual Cron expression builder for WPF with presets, 5-part fields, and human-readable preview.
    /// </summary>
    public class ZCronEditor : Control
    {
        public static readonly DependencyProperty CronExpressionProperty =
            DependencyProperty.Register(nameof(CronExpression), typeof(string), typeof(ZCronEditor),
                new FrameworkPropertyMetadata("0 8 * * 1-5", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCronExpressionChanged));

        public static readonly DependencyProperty ShowPreviewProperty =
            DependencyProperty.Register(nameof(ShowPreview), typeof(bool), typeof(ZCronEditor),
                new PropertyMetadata(true));

        private readonly ComboBox _presetCombo;
        private readonly TextBox _minBox;
        private readonly TextBox _hourBox;
        private readonly TextBox _dayBox;
        private readonly TextBox _monthBox;
        private readonly TextBox _dowBox;
        private readonly TextBlock _humanPreview;
        private readonly Border _rootBorder;
        private bool _isUpdatingInternally;

        public event EventHandler? CronChanged;
        public event EventHandler? ExpressionChanged { add => CronChanged += value; remove => CronChanged -= value; }

        public string CronExpression
        {
            get => (string)GetValue(CronExpressionProperty);
            set => SetValue(CronExpressionProperty, value);
        }

        public bool ShowPreview
        {
            get => (bool)GetValue(ShowPreviewProperty);
            set => SetValue(ShowPreviewProperty, value);
        }

        public ZCronEditor()
        {
            Width = 460;
            Height = 140;
            Background = ZeroWpfTheme.BgCard;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            BorderThickness = new Thickness(1);

            var stack = new StackPanel { Margin = new Thickness(10) };

            // Row 1: Presets
            var presetRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var presetLabel = new TextBlock
            {
                Text = "Preset Schedule:",
                FontWeight = FontWeights.Bold,
                Foreground = ZeroWpfTheme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 110
            };
            _presetCombo = new ComboBox();
            _presetCombo.Items.Add("Every Minute (* * * * *)");
            _presetCombo.Items.Add("Every 15 Minutes (*/15 * * * *)");
            _presetCombo.Items.Add("Daily at 08:00 AM (0 8 * * *)");
            _presetCombo.Items.Add("Weekdays at 08:00 AM (0 8 * * 1-5)");
            _presetCombo.Items.Add("Weekly on Sunday (0 0 * * 0)");
            _presetCombo.SelectedIndex = 3;
            _presetCombo.SelectionChanged += OnPresetSelectionChanged;

            presetRow.Children.Add(presetLabel);
            presetRow.Children.Add(_presetCombo);
            stack.Children.Add(presetRow);

            // Row 2: 5 fields
            var fieldsGrid = new UniformGrid { Columns = 5, Margin = new Thickness(0, 0, 0, 8) };

            _minBox = CreateFieldItem("Minute", "0", fieldsGrid);
            _hourBox = CreateFieldItem("Hour", "8", fieldsGrid);
            _dayBox = CreateFieldItem("Day", "*", fieldsGrid);
            _monthBox = CreateFieldItem("Month", "*", fieldsGrid);
            _dowBox = CreateFieldItem("D.O.W.", "1-5", fieldsGrid);

            stack.Children.Add(fieldsGrid);

            // Row 3: Human preview
            _humanPreview = new TextBlock
            {
                Text = "\"Monday through Friday at 08:00 AM\"",
                FontStyle = FontStyles.Italic,
                Foreground = ZeroWpfTheme.PrimaryAccent,
                Margin = new Thickness(0, 4, 0, 0)
            };
            stack.Children.Add(_humanPreview);

            var border = new Border
            {
                Background = Background,
                BorderBrush = BorderBrush,
                BorderThickness = BorderThickness,
                CornerRadius = new CornerRadius(4),
                Child = stack
            };

            _rootBorder = border;
            AddVisualChild(border);
            AddLogicalChild(border);

            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private TextBox CreateFieldItem(string label, string val, UniformGrid parent)
        {
            var pnl = new StackPanel { Margin = new Thickness(2) };
            var lbl = new TextBlock { Text = label, FontSize = 10, Foreground = ZeroWpfTheme.TextSecondary, HorizontalAlignment = HorizontalAlignment.Center };
            var tb = new TextBox
            {
                Text = val,
                TextAlignment = TextAlignment.Center,
                Background = ZeroWpfTheme.BgInput,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderBrush = ZeroWpfTheme.BorderDefault
            };
            tb.TextChanged += (s, e) => RebuildCron();
            pnl.Children.Add(lbl);
            pnl.Children.Add(tb);
            parent.Children.Add(pnl);
            return tb;
        }

        private void OnPresetSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingInternally) return;

            string selected = _presetCombo.SelectedItem?.ToString() ?? string.Empty;
            if (selected.Contains("(* * * * *)")) CronExpression = "* * * * *";
            else if (selected.Contains("(*/15 * * * *)")) CronExpression = "*/15 * * * *";
            else if (selected.Contains("(0 8 * * *)")) CronExpression = "0 8 * * *";
            else if (selected.Contains("(0 8 * * 1-5)")) CronExpression = "0 8 * * 1-5";
            else if (selected.Contains("(0 0 * * 0)")) CronExpression = "0 0 * * 0";
        }

        private void RebuildCron()
        {
            if (_isUpdatingInternally) return;
            string expr = $"{_minBox.Text} {_hourBox.Text} {_dayBox.Text} {_monthBox.Text} {_dowBox.Text}";
            if (CronExpression != expr)
            {
                CronExpression = expr;
                UpdatePreview();
                CronChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdatePreview()
        {
            string c = CronExpression.Trim();
            string text = c switch
            {
                "* * * * *" => "Every minute",
                "*/15 * * * *" => "Every 15 minutes",
                "0 8 * * *" => "Every day at 08:00 AM",
                "0 8 * * 1-5" => "Monday through Friday at 08:00 AM",
                "0 0 * * 0" => "Every Sunday at midnight",
                _ => $"Custom schedule ({c})"
            };
            _humanPreview.Text = $"\"{text}\"";
        }

        private static void OnCronExpressionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZCronEditor ctrl)
            {
                ctrl._isUpdatingInternally = true;
                string expr = (string)e.NewValue ?? "* * * * *";
                var parts = expr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5)
                {
                    ctrl._minBox.Text = parts[0];
                    ctrl._hourBox.Text = parts[1];
                    ctrl._dayBox.Text = parts[2];
                    ctrl._monthBox.Text = parts[3];
                    ctrl._dowBox.Text = parts[4];
                }
                ctrl.UpdatePreview();
                ctrl._isUpdatingInternally = false;
                ctrl.CronChanged?.Invoke(ctrl, EventArgs.Empty);
            }
        }

        public static string DescribeCron(string cron)
        {
            string c = (cron ?? string.Empty).Trim();
            if (c == "* * * * *") return "Every minute";
            if (c == "*/5 * * * *") return "Every 5 minutes";
            if (c == "*/15 * * * *") return "Every 15 minutes";
            if (c == "0 * * * *") return "Every hour on the hour";
            if (c == "0 0 * * *") return "Every day at midnight (00:00)";
            if (c == "0 8 * * *") return "Every day at 08:00 AM";
            if (c == "0 8 * * 1-5") return "Monday through Friday at 08:00 AM";
            if (c == "0 0 * * 0") return "Every Sunday at midnight";
            if (c == "0 0 1 * *") return "First day of every month at midnight";

            return $"Custom schedule ({c})";
        }

        private void OnThemeChanged()
        {
            if (Dispatcher.CheckAccess())
            {
                Background = ZeroWpfTheme.BgCard;
                BorderBrush = ZeroWpfTheme.BorderDefault;
            }
            else
            {
                Dispatcher.BeginInvoke((Action)OnThemeChanged);
            }
        }

        protected override int VisualChildrenCount => _rootBorder != null ? 1 : 0;
        protected override Visual GetVisualChild(int index)
        {
            if (index != 0 || _rootBorder == null) throw new ArgumentOutOfRangeException(nameof(index));
            return _rootBorder;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            _rootBorder?.Measure(constraint);
            return _rootBorder?.DesiredSize ?? base.MeasureOverride(constraint);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _rootBorder?.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }
    }

    [Obsolete("CronEditor is deprecated. Use ZCronEditor instead.")]
    public class CronEditor : ZCronEditor { }

    [Obsolete("ZeroCronEditor is deprecated. Use ZCronEditor instead.")]
    public class ZeroCronEditor : ZCronEditor { }
}
