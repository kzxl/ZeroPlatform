using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Action button embedded within an editor slot.
    /// </summary>
    public class EditorButton : Button
    {
        public static readonly DependencyProperty KindProperty =
            DependencyProperty.Register(
                nameof(Kind),
                typeof(EditorButtonKind),
                typeof(EditorButton),
                new PropertyMetadata(EditorButtonKind.Custom, OnKindChanged));

        public static readonly DependencyProperty GlyphDataProperty =
            DependencyProperty.Register(
                nameof(GlyphData),
                typeof(Geometry),
                typeof(EditorButton),
                new PropertyMetadata(null));

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register(
                nameof(Glyph),
                typeof(string),
                typeof(EditorButton),
                new PropertyMetadata(null, OnGlyphChanged));

        public EditorButtonKind Kind
        {
            get => (EditorButtonKind)GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }

        public Geometry? GlyphData
        {
            get => (Geometry?)GetValue(GlyphDataProperty);
            set => SetValue(GlyphDataProperty, value);
        }

        public string? Glyph
        {
            get => (string?)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        private static void OnGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EditorButton btn && e.NewValue is string s)
            {
                btn.Content = s;
            }
        }

        public EditorButton()
        {
            Focusable = false;
            Cursor = Cursors.Hand;
            ApplyDefaultKindSettings(Kind);
        }

        public EditorButton(EditorButtonKind kind) : this()
        {
            Kind = kind;
        }

        private static void OnKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is EditorButton btn && e.NewValue is EditorButtonKind kind)
            {
                btn.ApplyDefaultKindSettings(kind);
            }
        }

        private void ApplyDefaultKindSettings(EditorButtonKind kind)
        {
            if (ToolTip == null || ToolTip is string s && string.IsNullOrEmpty(s))
            {
                ToolTip = EditorButtonModel.GetDefaultToolTip(kind);
            }

            if (GlyphData == null)
            {
                GlyphData = GetDefaultGlyph(kind);
            }
        }

        public static Geometry? GetDefaultGlyph(EditorButtonKind kind)
        {
            return kind switch
            {
                EditorButtonKind.BrowseFile => Geometry.Parse("M2,4 H8 L10,6 H20 V18 H2 Z M4,8 V16 H18 V8 Z"),
                EditorButtonKind.BrowseFolder => Geometry.Parse("M2,4 H8 L10,6 H20 V18 H2 Z"),
                EditorButtonKind.Clear => Geometry.Parse("M6,6 L14,14 M14,6 L6,14"),
                EditorButtonKind.Copy => Geometry.Parse("M4,4 H14 V14 H4 Z M8,8 H18 V18 H8 Z"),
                EditorButtonKind.Search => Geometry.Parse("M7,7 A4,4 0 1 0 13,13 A4,4 0 1 0 7,7 M12,12 L17,17"),
                EditorButtonKind.DropDown => Geometry.Parse("M6,8 L10,12 L14,8 Z"),
                EditorButtonKind.Undo => Geometry.Parse("M12,4 L8,8 L12,12 M8,8 H16 A4,4 0 0 1 20,12"),
                EditorButtonKind.Redo => Geometry.Parse("M12,4 L16,8 L12,12 M16,8 H8 A4,4 0 0 0 4,12"),
                _ => null
            };
        }
    }

    /// <summary>
    /// Universal action slot text editor with configurable left and right embedded action buttons.
    /// Provides built-in support for File/Folder pickers, clipboard copy, and 1-click clear.
    /// </summary>
    public class ZButtonEdit : TextEdit
    {
        public static readonly DependencyProperty FileFilterProperty =
            DependencyProperty.Register(
                nameof(FileFilter),
                typeof(string),
                typeof(ZButtonEdit),
                new PropertyMetadata("All Files (*.*)|*.*"));

        public static readonly DependencyProperty DialogTitleProperty =
            DependencyProperty.Register(
                nameof(DialogTitle),
                typeof(string),
                typeof(ZButtonEdit),
                new PropertyMetadata("Select File"));

        public string FileFilter
        {
            get => (string)GetValue(FileFilterProperty);
            set => SetValue(FileFilterProperty, value);
        }

        public string DialogTitle
        {
            get => (string)GetValue(DialogTitleProperty);
            set => SetValue(DialogTitleProperty, value);
        }

        public ObservableCollection<EditorButton> LeftButtons { get; } = new ObservableCollection<EditorButton>();
        public ObservableCollection<EditorButton> RightButtons { get; } = new ObservableCollection<EditorButton>();

        public event EventHandler<EditorButtonClickEventArgs>? ButtonClick;

        public ZButtonEdit()
        {
            LeftButtons.CollectionChanged += (s, e) => HookButtons(e.NewItems);
            RightButtons.CollectionChanged += (s, e) => HookButtons(e.NewItems);
        }

        private void HookButtons(System.Collections.IList? items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (item is EditorButton btn)
                {
                    btn.Click -= OnEditorButtonClicked;
                    btn.Click += OnEditorButtonClicked;
                }
            }
        }

        private void OnEditorButtonClicked(object sender, RoutedEventArgs e)
        {
            if (sender is not EditorButton btn) return;

            var args = new EditorButtonClickEventArgs(btn.Kind, btn.Tag);
            ButtonClick?.Invoke(this, args);

            if (!args.Handled)
            {
                ExecuteDefaultAction(btn);
            }
        }

        protected virtual void ExecuteDefaultAction(EditorButton btn)
        {
            switch (btn.Kind)
            {
                case EditorButtonKind.BrowseFile:
                    var dlg = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = FileFilter,
                        Title = DialogTitle
                    };
                    if (dlg.ShowDialog() == true)
                    {
                        Text = dlg.FileName;
                        EditValue = dlg.FileName;
                    }
                    break;

                case EditorButtonKind.BrowseFolder:
                    BrowseForFolder();
                    break;

                case EditorButtonKind.Clear:
                    Clear();
                    EditValue = null;
                    break;

                case EditorButtonKind.Copy:
                    if (!string.IsNullOrEmpty(Text))
                    {
                        Clipboard.SetText(Text);
                    }
                    break;
            }
        }

        private void BrowseForFolder()
        {
            try
            {
                var fbdType = Type.GetType("System.Windows.Forms.FolderBrowserDialog, System.Windows.Forms");
                if (fbdType != null)
                {
                    object? dialog = Activator.CreateInstance(fbdType);
                    if (dialog != null)
                    {
                        var descProp = fbdType.GetProperty("Description");
                        descProp?.SetValue(dialog, DialogTitle, null);

                        var showDialogMethod = fbdType.GetMethod("ShowDialog", Type.EmptyTypes);
                        object? result = showDialogMethod?.Invoke(dialog, null);

                        if (result != null && (int)result == 1) // DialogResult.OK
                        {
                            var pathProp = fbdType.GetProperty("SelectedPath");
                            string? selectedPath = pathProp?.GetValue(dialog, null) as string;
                            if (!string.IsNullOrEmpty(selectedPath))
                            {
                                Text = selectedPath!;
                                EditValue = selectedPath;
                            }
                        }

                        if (dialog is IDisposable disp) disp.Dispose();
                    }
                }
            }
            catch
            {
                // Graceful fallback
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZButtonEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ButtonEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZButtonEdit instead.")]
    public class ButtonEdit : ZButtonEdit { }

    /// <summary>
    /// Legacy alias for <see cref="ZButtonEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroButtonEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZButtonEdit instead.")]
    public class ZeroButtonEdit : ZButtonEdit { }

    #endregion

}
