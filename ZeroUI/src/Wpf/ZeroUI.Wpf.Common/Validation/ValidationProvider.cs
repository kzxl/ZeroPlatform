using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Validation;

namespace ZeroUI.Wpf.Validation
{
    /// <summary>
    /// Modern Enterprise Validation Provider for ZeroUI WPF.
    /// Provides Attached Properties for XAML data-binding, programmatic rule registration,
    /// dynamic Adorner vector error badges, hover tooltips, and automated focus shifting.
    /// </summary>
    public class ValidationProvider
    {
        #region Attached Properties

        public static readonly DependencyProperty ErrorTextProperty =
            DependencyProperty.RegisterAttached(
                "ErrorText",
                typeof(string),
                typeof(ValidationProvider),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnErrorTextChanged));

        public static readonly DependencyProperty SeverityProperty =
            DependencyProperty.RegisterAttached(
                "Severity",
                typeof(ValidationSeverity),
                typeof(ValidationProvider),
                new FrameworkPropertyMetadata(ValidationSeverity.Error, FrameworkPropertyMetadataOptions.AffectsRender, OnSeverityChanged));

        public static readonly DependencyProperty HasErrorProperty =
            DependencyProperty.RegisterAttached(
                "HasError",
                typeof(bool),
                typeof(ValidationProvider),
                new FrameworkPropertyMetadata(false));

        public static string? GetErrorText(DependencyObject obj) => (string?)obj.GetValue(ErrorTextProperty);
        public static void SetErrorText(DependencyObject obj, string? value) => obj.SetValue(ErrorTextProperty, value);

        public static ValidationSeverity GetSeverity(DependencyObject obj) => (ValidationSeverity)obj.GetValue(SeverityProperty);
        public static void SetSeverity(DependencyObject obj, ValidationSeverity value) => obj.SetValue(SeverityProperty, value);

        public static bool GetHasError(DependencyObject obj) => (bool)obj.GetValue(HasErrorProperty);
        private static void SetHasError(DependencyObject obj, bool value) => obj.SetValue(HasErrorProperty, value);

        private static void OnErrorTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                string? text = e.NewValue as string;
                bool hasErr = !string.IsNullOrWhiteSpace(text);
                SetHasError(element, hasErr);

                UpdateAdorner(element, text, GetSeverity(element));
            }
        }

        private static void OnSeverityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                string? text = GetErrorText(element);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    UpdateAdorner(element, text, (ValidationSeverity)e.NewValue);
                }
            }
        }

        #endregion

        #region Programmatic Instance Management

        private class WpfRuleBinding
        {
            public List<IControlValidationRule> Rules { get; } = new List<IControlValidationRule>();
            public ValidationTrigger Trigger { get; set; } = ValidationTrigger.ValueChanged;
        }

        private readonly Dictionary<FrameworkElement, WpfRuleBinding> _rules = new Dictionary<FrameworkElement, WpfRuleBinding>();

        public void SetRule(FrameworkElement element, IControlValidationRule rule, ValidationTrigger trigger = ValidationTrigger.ValueChanged)
        {
            if (element == null || rule == null) return;

            if (!_rules.TryGetValue(element, out var binding))
            {
                binding = new WpfRuleBinding { Trigger = trigger };
                _rules[element] = binding;

                if (element is IZeroEditor editor)
                {
                    editor.EditValueChanged += (s, e) =>
                    {
                        if (binding.Trigger == ValidationTrigger.ValueChanged)
                        {
                            Validate(element);
                        }
                    };
                }
                else if (element is TextBox tb)
                {
                    tb.TextChanged += (s, e) =>
                    {
                        if (binding.Trigger == ValidationTrigger.ValueChanged)
                        {
                            Validate(element);
                        }
                    };
                }

                element.LostFocus += (s, e) =>
                {
                    if (binding.Trigger == ValidationTrigger.FocusLost)
                    {
                        Validate(element);
                    }
                };
            }

            binding.Trigger = trigger;
            if (!binding.Rules.Contains(rule))
            {
                binding.Rules.Add(rule);
            }
        }

        public bool Validate(FrameworkElement element)
        {
            if (element == null) return true;

            if (!_rules.TryGetValue(element, out var binding) || binding.Rules.Count == 0)
            {
                SetErrorText(element, null);
                return true;
            }

            object? val = element is IZeroEditor editor ? editor.EditValue : (element as TextBox)?.Text;
            foreach (var rule in binding.Rules)
            {
                var res = rule.Validate(val);
                if (!res.IsValid)
                {
                    var msg = res.PrimaryErrorMessage ?? res.PrimaryMessage ?? "Validation failed";
                    var sev = res.HasErrors ? ValidationSeverity.Error : (res.HasWarnings ? ValidationSeverity.Warning : ValidationSeverity.Information);
                    SetSeverity(element, sev);
                    SetErrorText(element, msg);
                    return false;
                }
            }

            SetErrorText(element, null);
            return true;
        }

        public bool Validate()
        {
            bool allValid = true;
            foreach (var element in _rules.Keys)
            {
                if (!Validate(element))
                {
                    allValid = false;
                }
            }
            return allValid;
        }

        public FrameworkElement? GetFirstInvalid()
        {
            foreach (var element in _rules.Keys)
            {
                if (GetHasError(element) && element.Focusable)
                {
                    return element;
                }
            }
            return null;
        }

        public bool FocusFirstInvalid()
        {
            var el = GetFirstInvalid();
            if (el != null)
            {
                return el.Focus();
            }
            return false;
        }

        public void ClearErrors()
        {
            foreach (var el in _rules.Keys)
            {
                SetErrorText(el, null);
            }
        }

        #endregion

        #region Adorner Rendering

        private static void UpdateAdorner(FrameworkElement element, string? errorText, ValidationSeverity severity)
        {
            if (!element.IsLoaded)
            {
                RoutedEventHandler? onLoaded = null;
                onLoaded = (s, e) =>
                {
                    element.Loaded -= onLoaded;
                    UpdateAdorner(element, GetErrorText(element), GetSeverity(element));
                };
                element.Loaded += onLoaded;
                return;
            }

            var layer = AdornerLayer.GetAdornerLayer(element);
            if (layer == null) return;

            var existing = layer.GetAdorners(element);
            if (existing != null)
            {
                foreach (var ad in existing)
                {
                    if (ad is ErrorBadgeAdorner badgeAdorner)
                    {
                        layer.Remove(badgeAdorner);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(errorText))
            {
                var newAdorner = new ErrorBadgeAdorner(element, errorText!, severity);
                layer.Add(newAdorner);
            }
        }

        private sealed class ErrorBadgeAdorner : Adorner
        {
            private readonly string _errorText;
            private readonly ValidationSeverity _severity;

            public ErrorBadgeAdorner(UIElement adornedElement, string errorText, ValidationSeverity severity)
                : base(adornedElement)
            {
                _errorText = errorText;
                _severity = severity;
                IsHitTestVisible = true;
                Cursor = Cursors.Help;
                ToolTip = new ToolTip { Content = _errorText };
            }

            protected override void OnRender(DrawingContext dc)
            {
                base.OnRender(dc);

                double badgeSize = 16.0;
                double padding = 4.0;
                double x = AdornedElement.RenderSize.Width - badgeSize - padding;
                double y = (AdornedElement.RenderSize.Height - badgeSize) / 2.0;

                Brush bgBrush = _severity switch
                {
                    ValidationSeverity.Warning => new SolidColorBrush(Color.FromRgb(245, 158, 11)),   // Amber 500
                    ValidationSeverity.Information => new SolidColorBrush(Color.FromRgb(59, 130, 246)), // Blue 500
                    _ => new SolidColorBrush(Color.FromRgb(239, 68, 68))                               // Red 500
                };
                bgBrush.Freeze();

                var center = new Point(x + badgeSize / 2.0, y + badgeSize / 2.0);
                dc.DrawEllipse(bgBrush, null, center, badgeSize / 2.0, badgeSize / 2.0);

                var pen = new Pen(Brushes.White, 1.6);
                pen.Freeze();

                if (_severity == ValidationSeverity.Information)
                {
                    // 'i' glyph: dot + bar
                    dc.DrawEllipse(Brushes.White, null, new Point(center.X, y + 4.5), 1.1, 1.1);
                    dc.DrawLine(pen, new Point(center.X, y + 7.5), new Point(center.X, y + 12.0));
                }
                else
                {
                    // '!' exclamation glyph: bar + dot
                    dc.DrawLine(pen, new Point(center.X, y + 4.0), new Point(center.X, y + 9.5));
                    dc.DrawEllipse(Brushes.White, null, new Point(center.X, y + 12.0), 1.1, 1.1);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ValidationProvider"/>.
    /// </summary>
    public class ZeroErrorProvider : ValidationProvider
    {
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ValidationProvider"/>.
    /// </summary>
    public class ZeroValidationProvider : ValidationProvider
    {
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ValidationProvider"/>.
    /// </summary>
    public class ErrorProvider : ValidationProvider
    {
    }
}
