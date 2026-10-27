using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using ZeroUI.Core.Theme;

namespace ZeroUI.Wpf.Theme
{
    /// <summary>
    /// Interactive Skin Studio Dialog.
    /// Provides live seed color adjustments, WCAG AAA contrast verification,
    /// dynamic palette generation, and JSON skin file persistence.
    /// </summary>
    public partial class SkinStudioDialog : Window
    {
        private ZeroSkin _generatedSkin = null!;
        private bool _isUpdating = true;

        private readonly struct PresetSeed
        {
            public readonly string Name;
            public readonly string PrimaryHex;
            public readonly string SecondaryHex;

            public PresetSeed(string name, string primaryHex, string secondaryHex)
            {
                Name = name;
                PrimaryHex = primaryHex;
                SecondaryHex = secondaryHex;
            }
        }

        private static readonly PresetSeed[] Presets = new[]
        {
            new PresetSeed("Electric Indigo", "#6366F1", "#EC4899"),
            new PresetSeed("Royal Violet", "#8B5CF6", "#06B6D4"),
            new PresetSeed("Amber Gold", "#F59E0B", "#FBBF24"),
            new PresetSeed("Emerald Jade", "#10B981", "#34D399"),
            new PresetSeed("Vivid Rose", "#F43F5E", "#8B5CF6"),
            new PresetSeed("Ruby Crimson", "#E11D48", "#FB7185"),
            new PresetSeed("Arctic Cyan", "#06B6D4", "#38BDF8"),
            new PresetSeed("Sky Blue", "#0EA5E9", "#6366F1"),
            new PresetSeed("Teal Oasis", "#14B8A6", "#10B981"),
            new PresetSeed("Obsidian Slate", "#64748B", "#94A3B8")
        };

        public SkinStudioDialog()
        {
            _isUpdating = true;
            InitializeComponent();
            PopulatePresetButtons();
            LoadFromCurrentSkin();
            _isUpdating = false;
            RegeneratePalette();
        }

        private void PopulatePresetButtons()
        {
            PanelPresets.Children.Clear();
            foreach (var p in Presets)
            {
                var btn = new Button
                {
                    Content = p.Name,
                    Height = 28,
                    Padding = new Thickness(10, 0, 10, 0),
                    Margin = new Thickness(0, 0, 8, 8),
                    FontSize = 11,
                    Tag = p
                };
                btn.Click += PresetButton_Click;
                PanelPresets.Children.Add(btn);
            }
        }

        private void PresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PresetSeed seed)
            {
                _isUpdating = true;
                TxtDisplayName.Text = seed.Name;
                TxtSkinName.Text = seed.Name.ToLowerInvariant().Replace(" ", "_");
                TxtPrimaryHex.Text = seed.PrimaryHex;
                TxtSecondaryHex.Text = seed.SecondaryHex;
                _isUpdating = false;

                RegeneratePalette();
            }
        }

        private void LoadFromCurrentSkin()
        {
            var cur = ZeroSkinManager.CurrentSkin;
            _isUpdating = true;
            TxtDisplayName.Text = cur.DisplayName + " Custom";
            TxtSkinName.Text = cur.Name + "_custom";
            TxtPrimaryHex.Text = cur.Tokens.PrimaryAccent;
            TxtSecondaryHex.Text = cur.Tokens.SecondaryAccent;
            if (cur.IsDark)
            {
                RbDarkMode.IsChecked = true;
            }
            else
            {
                RbLightMode.IsChecked = true;
            }
            _isUpdating = false;

            RegeneratePalette();
        }

        private void OnInputChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isUpdating)
            {
                RegeneratePalette();
            }
        }

        private void OnModeChanged(object sender, RoutedEventArgs e)
        {
            if (!_isUpdating)
            {
                RegeneratePalette();
            }
        }

        private void BtnRegenerate_Click(object sender, RoutedEventArgs e)
        {
            RegeneratePalette();
        }

        private void RegeneratePalette()
        {
            if (TxtSkinName == null || TxtDisplayName == null || TxtPrimaryHex == null || TxtSecondaryHex == null ||
                RbDarkMode == null || BoxPrimaryColor == null || BoxSecondaryColor == null ||
                TxtContrastRatio == null || PreviewContainer == null || PrevCard == null ||
                PrevTitle == null || PrevSubtitle == null || PrevSelection == null || PrevSelText == null ||
                PrevInput == null || PrevBtnPrimary == null || PrevBtnOutline == null || SwatchGrid == null)
            {
                return;
            }

            string name = string.IsNullOrWhiteSpace(TxtSkinName.Text) ? "custom_skin" : TxtSkinName.Text.Trim();
            string displayName = string.IsNullOrWhiteSpace(TxtDisplayName.Text) ? "Custom Skin" : TxtDisplayName.Text.Trim();
            bool isDark = RbDarkMode.IsChecked == true;
            string primaryHex = string.IsNullOrWhiteSpace(TxtPrimaryHex.Text) ? "#6366F1" : TxtPrimaryHex.Text.Trim();
            string secondaryHex = string.IsNullOrWhiteSpace(TxtSecondaryHex.Text) ? "#EC4899" : TxtSecondaryHex.Text.Trim();

            try
            {
                _generatedSkin = ZeroSkinBuilder.FromSeedColor(name, displayName, isDark, primaryHex, secondaryHex);

                // Update seed indicator boxes
                var pRgb = ZeroColorUtils.ParseHex(primaryHex);
                BoxPrimaryColor.Background = new SolidColorBrush(Color.FromRgb(pRgb.R, pRgb.G, pRgb.B));

                var sRgb = ZeroColorUtils.ParseHex(secondaryHex);
                BoxSecondaryColor.Background = new SolidColorBrush(Color.FromRgb(sRgb.R, sRgb.G, sRgb.B));

                // Calculate WCAG contrast
                double ratio = ZeroColorUtils.GetContrastRatio(_generatedSkin.Tokens.SelectionForeground, _generatedSkin.Tokens.SelectionBackground);
                string rating = ratio >= 7.0 ? "AAA" : (ratio >= 4.5 ? "AA" : "FAIL");
                TxtContrastRatio.Text = $"WCAG Contrast: {ratio:F1}:1 ({rating})";
                TxtContrastRatio.Foreground = ratio >= 7.0 ? Brushes.LightGreen : (ratio >= 4.5 ? Brushes.Orange : Brushes.Red);

                // Update Sandbox Preview Controls
                UpdatePreviewDisplay();
            }
            catch
            {
                // Ignore incomplete hex typing
            }
        }

        private void UpdatePreviewDisplay()
        {
            var t = _generatedSkin.Tokens;

            PreviewContainer.Background = ToBrush(t.BgPrimary);
            PrevCard.Background = ToBrush(t.BgCard);
            PrevCard.BorderBrush = ToBrush(t.BorderDefault);

            PrevTitle.Foreground = ToBrush(t.TextPrimary);
            PrevSubtitle.Foreground = ToBrush(t.TextSecondary);

            PrevSelection.Background = ToBrush(t.SelectionBackground);
            PrevSelText.Foreground = ToBrush(t.SelectionForeground);

            PrevInput.Background = ToBrush(t.BgInput);
            PrevInput.BorderBrush = ToBrush(t.BorderDefault);
            PrevInput.Foreground = ToBrush(t.TextPrimary);

            PrevBtnPrimary.Background = ToBrush(t.PrimaryAccent);
            PrevBtnPrimary.Foreground = ToBrush(ZeroColorUtils.GetBestContrastTextColor(t.PrimaryAccent));

            PrevBtnOutline.BorderBrush = ToBrush(t.BorderDefault);
            PrevBtnOutline.Foreground = ToBrush(t.TextPrimary);

            // Rebuild token swatch grid
            SwatchGrid.Children.Clear();
            AddSwatch("Primary", t.PrimaryAccent);
            AddSwatch("AccentDark", t.PrimaryAccentDark);
            AddSwatch("Secondary", t.SecondaryAccent);
            AddSwatch("Card", t.BgCard);
            AddSwatch("Selection", t.SelectionBackground);
            AddSwatch("Border", t.BorderDefault);
        }

        private void AddSwatch(string label, string hex)
        {
            var border = new Border
            {
                Background = ToBrush(hex),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(2),
                ToolTip = $"{label}: {hex}"
            };
            SwatchGrid.Children.Add(border);
        }

        private static SolidColorBrush ToBrush(string hex)
        {
            var rgb = ZeroColorUtils.ParseHex(hex);
            return new SolidColorBrush(Color.FromRgb(rgb.R, rgb.G, rgb.B));
        }

        private void BtnApplyLive_Click(object sender, RoutedEventArgs e)
        {
            if (_generatedSkin != null)
            {
                ZeroSkinManager.ApplySkin(_generatedSkin);
            }
        }

        private void BtnSaveToCatalog_Click(object sender, RoutedEventArgs e)
        {
            if (_generatedSkin != null)
            {
                ZeroSkinManager.RegisterSkin(_generatedSkin);
                ZeroSkinManager.ApplySkin(_generatedSkin);
                MessageBox.Show($"Custom skin '{_generatedSkin.DisplayName}' has been registered in the ZeroUI Skin Catalog!",
                                "Skin Studio", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_generatedSkin == null) return;

            var sfd = new SaveFileDialog
            {
                Title = "Export ZeroUI Skin to JSON",
                Filter = "ZeroUI Skin (*.zeroskin.json)|*.zeroskin.json|JSON File (*.json)|*.json",
                FileName = $"{_generatedSkin.Name}.zeroskin.json"
            };

            if (sfd.ShowDialog() == true)
            {
                ZeroSkinSerializer.SaveToFile(_generatedSkin, sfd.FileName);
                MessageBox.Show($"Skin exported successfully to:\n{sfd.FileName}", "Export Skin", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Title = "Import ZeroUI Skin JSON",
                Filter = "ZeroUI Skin Files (*.zeroskin.json;*.json)|*.zeroskin.json;*.json|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var skin = ZeroSkinSerializer.LoadFromFile(ofd.FileName);
                    ZeroSkinManager.RegisterSkin(skin);

                    _isUpdating = true;
                    TxtDisplayName.Text = skin.DisplayName;
                    TxtSkinName.Text = skin.Name;
                    TxtPrimaryHex.Text = skin.Tokens.PrimaryAccent;
                    TxtSecondaryHex.Text = skin.Tokens.SecondaryAccent;
                    if (skin.IsDark) RbDarkMode.IsChecked = true;
                    else RbLightMode.IsChecked = true;
                    _isUpdating = false;

                    _generatedSkin = skin;
                    UpdatePreviewDisplay();

                    var result = MessageBox.Show($"Skin '{skin.DisplayName}' loaded successfully.\n\nDo you want to apply this skin immediately?",
                                                 "Skin Imported", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        ZeroSkinManager.ApplySkin(skin);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load skin: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    /// <summary>
    /// Obsolete alias for <see cref="SkinStudioDialog"/> to maintain backward compatibility.
    /// </summary>
    [Obsolete("Use SkinStudioDialog instead.")]
    public class ZeroSkinStudioDialog : SkinStudioDialog
    {
    }
}
