using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ZeroUI.Core.Input;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern container control for grouping and managing RadioEdit options in WPF.
    /// Powered by the headless <see cref="SelectionModel{T}"/> engine for single-selection synchronization.
    /// </summary>
    public class ZRadioGroup : ContentControl
    {
        private readonly SelectionModel<string> _selection = new SelectionModel<string>();
        private readonly StackPanel _panel = new StackPanel();
        private readonly List<RadioEdit> _radioButtons = new List<RadioEdit>();
        private bool _isUpdating = false;

        #region Dependency Properties

        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(nameof(Items), typeof(string[]), typeof(ZRadioGroup),
                new FrameworkPropertyMetadata(Array.Empty<string>(), OnItemsChanged));

        public static readonly DependencyProperty SelectedIndexProperty =
            DependencyProperty.Register(nameof(SelectedIndex), typeof(int), typeof(ZRadioGroup),
                new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedIndexChanged));

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(string), typeof(ZRadioGroup),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(ZRadioGroup),
                new FrameworkPropertyMetadata(Orientation.Vertical, OnOrientationChanged));

        public static readonly DependencyProperty ItemSpacingProperty =
            DependencyProperty.Register(nameof(ItemSpacing), typeof(double), typeof(ZRadioGroup),
                new FrameworkPropertyMetadata(8.0, OnItemSpacingChanged));

        #endregion

        #region Properties & Events

        public string[] Items
        {
            get => (string[])GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        public string? SelectedItem
        {
            get => (string?)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public double ItemSpacing
        {
            get => (double)GetValue(ItemSpacingProperty);
            set => SetValue(ItemSpacingProperty, value);
        }

        public SelectionModel<string> Selection => _selection;

        public event EventHandler? SelectedIndexChanged;

        #endregion

        public ZRadioGroup()
        {
            _panel.Orientation = Orientation.Vertical;
            Content = _panel;

            _selection.SelectionChanged += (s, e) =>
            {
                if (!_isUpdating)
                {
                    _isUpdating = true;
                    try
                    {
                        SelectedIndex = _selection.SelectedIndex;
                        SelectedItem = _selection.SelectedItem;
                        SyncRadioButtons();
                    }
                    finally
                    {
                        _isUpdating = false;
                    }
                    SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                }
            };
        }

        #region DP Callbacks

        private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZRadioGroup group)
            {
                group.RebuildRadioButtons();
            }
        }

        private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZRadioGroup group && !group._isUpdating)
            {
                group._isUpdating = true;
                try
                {
                    int newIndex = (int)e.NewValue;
                    group._selection.SelectIndex(newIndex);
                    group.SelectedItem = group._selection.SelectedItem;
                    group.SyncRadioButtons();
                }
                finally
                {
                    group._isUpdating = false;
                }
                group.SelectedIndexChanged?.Invoke(group, EventArgs.Empty);
            }
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZRadioGroup group && !group._isUpdating)
            {
                group._isUpdating = true;
                try
                {
                    string? newItem = (string?)e.NewValue;
                    if (newItem != null)
                    {
                        group._selection.SelectItem(newItem);
                        group.SelectedIndex = group._selection.SelectedIndex;
                        group.SyncRadioButtons();
                    }
                }
                finally
                {
                    group._isUpdating = false;
                }
                group.SelectedIndexChanged?.Invoke(group, EventArgs.Empty);
            }
        }

        private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZRadioGroup group)
            {
                group._panel.Orientation = (Orientation)e.NewValue;
                group.UpdateMargins();
            }
        }

        private static void OnItemSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZRadioGroup group)
            {
                group.UpdateMargins();
            }
        }

        #endregion

        private void RebuildRadioButtons()
        {
            _panel.Children.Clear();
            _radioButtons.Clear();

            var items = Items ?? Array.Empty<string>();
            _selection.SetSource(() => items.Length, i => items[i]);

            string groupName = "RG_" + Guid.NewGuid().ToString("N");

            for (int i = 0; i < items.Length; i++)
            {
                int index = i;
                var rb = new RadioEdit
                {
                    Text = items[i],
                    GroupName = groupName,
                    AutoCheck = true
                };

                rb.CheckedChanged += (s, isChecked) =>
                {
                    if (isChecked && !_isUpdating)
                    {
                        _isUpdating = true;
                        try
                        {
                            _selection.SelectIndex(index);
                            SelectedIndex = index;
                            SelectedItem = _selection.SelectedItem;
                            SyncRadioButtons();
                        }
                        finally
                        {
                            _isUpdating = false;
                        }
                        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                    }
                };

                _radioButtons.Add(rb);
                _panel.Children.Add(rb);
            }

            UpdateMargins();
            SyncRadioButtons();
        }

        private void UpdateMargins()
        {
            double spacing = ItemSpacing;
            bool isHoriz = Orientation == Orientation.Horizontal;

            for (int i = 0; i < _radioButtons.Count; i++)
            {
                if (i < _radioButtons.Count - 1)
                {
                    _radioButtons[i].Margin = isHoriz ? new Thickness(0, 0, spacing, 0) : new Thickness(0, 0, 0, spacing);
                }
                else
                {
                    _radioButtons[i].Margin = new Thickness(0);
                }
            }
        }

        private void SyncRadioButtons()
        {
            int selected = SelectedIndex;
            for (int i = 0; i < _radioButtons.Count; i++)
            {
                _radioButtons[i].IsChecked = (i == selected);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZRadioGroup"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("RadioGroup is deprecated and will be removed in 5 release cycles. Please migrate to ZRadioGroup instead.")]
    public class RadioGroup : ZRadioGroup { }

    /// <summary>
    /// Legacy alias for <see cref="ZRadioGroup"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroRadioGroup is deprecated and will be removed in 5 release cycles. Please migrate to ZRadioGroup instead.")]
    public class ZeroRadioGroup : ZRadioGroup { }

    #endregion

}
