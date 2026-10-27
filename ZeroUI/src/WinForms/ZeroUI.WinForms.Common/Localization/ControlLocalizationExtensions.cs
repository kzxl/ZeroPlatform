using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ZeroUI.Core.Localization;

namespace ZeroUI.WinForms.Localization
{
    /// <summary>
    /// Real-time UI localization extensions for WinForms controls and forms.
    /// Provides zero-leak Clean-on-Dispatch weak binding and automatic recursive Tag-based form localization.
    /// </summary>
    public static class ControlLocalizationExtensions
    {
        private static readonly object SyncLock = new object();
        private static readonly List<BindingRecord> Bindings = new List<BindingRecord>(128);
        private static bool _isSubscribed;

        private readonly struct BindingRecord
        {
            public readonly WeakReference<Control> TargetRef;
            public readonly string Key;
            public readonly Action<Control, string>? CustomSetter;

            public BindingRecord(Control control, string key, Action<Control, string>? customSetter)
            {
                TargetRef = new WeakReference<Control>(control);
                Key = key;
                CustomSetter = customSetter;
            }
        }

        private static void EnsureSubscribed()
        {
            if (!_isSubscribed)
            {
                lock (SyncLock)
                {
                    if (!_isSubscribed)
                    {
                        LocalizationManager.CultureChanged += OnCultureChanged;
                        _isSubscribed = true;
                    }
                }
            }
        }

        private static void OnCultureChanged(object? sender, EventArgs e)
        {
            lock (SyncLock)
            {
                int writeIndex = 0;
                int count = Bindings.Count;

                for (int i = 0; i < count; i++)
                {
                    var record = Bindings[i];
                    if (record.TargetRef.TryGetTarget(out var ctrl) && !ctrl.IsDisposed)
                    {
                        // Control is alive and valid
                        string text = LocalizationManager.Get(record.Key);

                        try
                        {
                            if (ctrl.InvokeRequired)
                            {
                                ctrl.BeginInvoke(new Action(() => ApplyText(ctrl, text, record.CustomSetter)));
                            }
                            else
                            {
                                ApplyText(ctrl, text, record.CustomSetter);
                            }
                        }
                        catch
                        {
                            // Ignore controls during disposal race conditions
                        }

                        // Compact alive elements
                        Bindings[writeIndex++] = record;
                    }
                    // Controls that are collected or Disposed are dropped (Zero-Leak)
                }

                if (writeIndex < count)
                {
                    Bindings.RemoveRange(writeIndex, count - writeIndex);
                }
            }
        }

        private static void ApplyText(Control ctrl, string text, Action<Control, string>? customSetter)
        {
            if (ctrl.IsDisposed) return;

            if (customSetter != null)
            {
                customSetter(ctrl, text);
            }
            else
            {
                ctrl.Text = text;
            }
        }

        /// <summary>
        /// Binds control text to a localization key with automatic real-time updates upon language changes.
        /// </summary>
        public static T BindText<T>(this T control, string key, Action<T, string>? customSetter = null) where T : Control
        {
            if (control == null || string.IsNullOrWhiteSpace(key)) return control!;

            EnsureSubscribed();

            // Set initial localized value
            string initialText = LocalizationManager.Get(key);
            if (customSetter != null)
            {
                customSetter(control, initialText);
            }
            else
            {
                control.Text = initialText;
            }

            lock (SyncLock)
            {
                Action<Control, string>? genericSetter = customSetter != null
                    ? (c, s) => customSetter((T)c, s)
                    : null;

                Bindings.Add(new BindingRecord(control, key, genericSetter));
            }

            return control;
        }

        /// <summary>
        /// Binds a ToolTip component to a localized key for the given control.
        /// </summary>
        public static T BindToolTip<T>(this T control, string key, ToolTip toolTip) where T : Control
        {
            if (control == null || toolTip == null || string.IsNullOrWhiteSpace(key)) return control!;

            return control.BindText(key, (ctrl, text) =>
            {
                toolTip.SetToolTip(ctrl, text);
            });
        }

        /// <summary>
        /// Automatically localizes all child controls whose Tag property starts with "loc:" (e.g. Tag="loc:Common.Ok").
        /// </summary>
        public static void ApplyLocalization(this Control container, bool recursive = true)
        {
            if (container == null || container.IsDisposed) return;

            CheckAndBindControl(container);

            if (recursive && container.HasChildren)
            {
                foreach (Control child in container.Controls)
                {
                    ApplyLocalization(child, true);
                }
            }
        }

        private static void CheckAndBindControl(Control control)
        {
            if (control.Tag is string tagStr && tagStr.StartsWith("loc:", StringComparison.OrdinalIgnoreCase))
            {
                string key = tagStr.Substring(4).Trim();
                if (!string.IsNullOrEmpty(key))
                {
                    control.BindText(key);
                }
            }
        }
    }
}
