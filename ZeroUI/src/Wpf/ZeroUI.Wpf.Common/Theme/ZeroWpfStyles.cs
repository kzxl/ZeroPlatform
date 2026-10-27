using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

namespace ZeroUI.Wpf.Theme
{
    /// <summary>
    /// Modern Fluent ControlTemplates and implicit Styles for WPF standard controls.
    /// Overrides default Windows Aero chrome across all controls (Buttons, ComboBox, ScrollBar, CheckBox, Radio, TextBox, ToolTip, ContextMenu, Tabs).
    /// </summary>
    public static class ZeroWpfStyles
    {
        private static ResourceDictionary? _stylesDict;

        public static ResourceDictionary Dictionary => _stylesDict ??= LoadStylesDictionary();

        public static Style ButtonStyle => (Style)Dictionary[typeof(Button)];
        public static Style ComboBoxStyle => (Style)Dictionary[typeof(ComboBox)];
        public static Style ComboBoxItemStyle => (Style)Dictionary[typeof(ComboBoxItem)];
        public static Style ScrollBarStyle => (Style)Dictionary[typeof(ScrollBar)];
        public static Style ScrollViewerStyle => (Style)Dictionary[typeof(ScrollViewer)];
        public static Style CheckBoxStyle => (Style)Dictionary[typeof(CheckBox)];
        public static Style RadioButtonStyle => (Style)Dictionary[typeof(RadioButton)];
        public static Style TextBoxStyle => (Style)Dictionary[typeof(TextBox)];
        public static Style ToolTipStyle => (Style)Dictionary[typeof(ToolTip)];
        public static Style ContextMenuStyle => (Style)Dictionary[typeof(ContextMenu)];
        public static Style MenuItemStyle => (Style)Dictionary[typeof(MenuItem)];
        public static Style TabControlStyle => (Style)Dictionary[typeof(TabControl)];
        public static Style TabItemStyle => (Style)Dictionary[typeof(TabItem)];

        public static Style PasswordBoxStyle => (Style)Dictionary[typeof(PasswordBox)];
        public static Style ListBoxStyle => (Style)Dictionary[typeof(ListBox)];
        public static Style ListBoxItemStyle => (Style)Dictionary[typeof(ListBoxItem)];
        public static Style ListViewStyle => (Style)Dictionary[typeof(ListView)];
        public static Style ListViewItemStyle => (Style)Dictionary[typeof(ListViewItem)];
        public static Style GridViewColumnHeaderStyle => (Style)Dictionary[typeof(GridViewColumnHeader)];
        public static Style TreeViewStyle => (Style)Dictionary[typeof(TreeView)];
        public static Style TreeViewItemStyle => (Style)Dictionary[typeof(TreeViewItem)];
        public static Style ProgressBarStyle => (Style)Dictionary[typeof(ProgressBar)];
        public static Style SliderStyle => (Style)Dictionary[typeof(Slider)];
        public static Style ExpanderStyle => (Style)Dictionary[typeof(Expander)];
        public static Style GroupBoxStyle => (Style)Dictionary[typeof(GroupBox)];
        public static Style DatePickerStyle => (Style)Dictionary[typeof(DatePicker)];
        public static Style CalendarStyle => (Style)Dictionary[typeof(Calendar)];
        public static Style StatusBarStyle => (Style)Dictionary[typeof(StatusBar)];
        public static Style StatusBarItemStyle => (Style)Dictionary[typeof(StatusBarItem)];
        public static Style SeparatorStyle => (Style)Dictionary[typeof(Separator)];

        public static Style ZTitleBarStyle => (Style)Dictionary[typeof(ZeroUI.Wpf.Layout.ZTitleBar)];
        public static Style ZWindowStyle => (Style)Dictionary[typeof(ZeroUI.Wpf.Layout.ZWindow)];
        public static Style ZInfoBarStyle => (Style)Dictionary[typeof(ZeroUI.Wpf.Feedback.ZInfoBar)];
        public static Style ZBadgeStyle => (Style)Dictionary[typeof(ZeroUI.Wpf.Feedback.ZBadge)];
        public static Style ZFlyoutStyle => (Style)Dictionary[typeof(ZeroUI.Wpf.Overlays.ZFlyout)];

        [Obsolete("Use ZTitleBarStyle instead.")]
        public static Style TitleBarStyle => ZTitleBarStyle;

        [Obsolete("Use ZWindowStyle instead.")]
        public static Style ChromeWindowStyle => ZWindowStyle;

        [Obsolete("Use ZInfoBarStyle instead.")]
        public static Style InfoBarStyle => ZInfoBarStyle;

        [Obsolete("Use ZBadgeStyle instead.")]
        public static Style BadgeStyle => ZBadgeStyle;

        [Obsolete("Use ZFlyoutStyle instead.")]
        public static Style FlyoutControlStyle => ZFlyoutStyle;

        public static Style? ButtonEditStyle => GetStyleByName("ZeroUI.Wpf.Editors.ButtonEdit");
        public static Style? PictureEditStyle => GetStyleByName("ZeroUI.Wpf.Editors.PictureEdit");
        public static Style? IPAddressEditStyle => GetStyleByName("ZeroUI.Wpf.Editors.IPAddressEdit");
        public static Style? RangeSliderStyle => GetStyleByName("ZeroUI.Wpf.Editors.RangeSlider");
        public static Style? TokenEditStyle => GetStyleByName("ZeroUI.Wpf.Editors.TokenEdit");

        private static Style? GetStyleByName(string fullTypeName)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType(fullTypeName);
                    if (t != null && Dictionary.Contains(t)) return (Style)Dictionary[t];
                }
            }
            catch { }
            return null;
        }

        #region Obsolete Styles

        [Obsolete("Use TitleBarStyle instead.")]
        public static Style ZeroTitleBarStyle => TitleBarStyle;

        [Obsolete("Use ChromeWindowStyle instead.")]
        public static Style ZeroWindowStyle => ChromeWindowStyle;

        [Obsolete("Use InfoBarStyle instead.")]
        public static Style ZeroInfoBarStyle => InfoBarStyle;

        [Obsolete("Use BadgeStyle instead.")]
        public static Style ZeroBadgeStyle => BadgeStyle;

        [Obsolete("Use FlyoutControlStyle instead.")]
        public static Style ZeroFlyoutStyle => FlyoutControlStyle;

        #endregion

        public static void ApplyStyles(Application? app = null)
        {
            var targetApp = app ?? Application.Current;
            if (targetApp == null) return;

            ZeroWpfTheme.UpdateApplicationResources();

            var dict = Dictionary;
            if (!targetApp.Resources.MergedDictionaries.Contains(dict))
            {
                targetApp.Resources.MergedDictionaries.Add(dict);
            }
        }

        private static ResourceDictionary LoadStylesDictionary()
        {
            const string xaml = @"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                    xmlns:edit=""clr-namespace:ZeroUI.Wpf.Editors;assembly=ZeroUI.Wpf.Editors""
                    xmlns:editors=""clr-namespace:ZeroUI.Wpf.Editors;assembly=ZeroUI.Wpf.Editors""
                    xmlns:ind=""clr-namespace:ZeroUI.Wpf.Industrial;assembly=ZeroUI.Wpf.Industrial""
                    xmlns:layout=""clr-namespace:ZeroUI.Wpf.Layout;assembly=ZeroUI.Wpf.Common""
                    xmlns:feed=""clr-namespace:ZeroUI.Wpf.Feedback;assembly=ZeroUI.Wpf.Common""
                    xmlns:ovl=""clr-namespace:ZeroUI.Wpf.Overlays;assembly=ZeroUI.Wpf.Common"">

    <!-- 0. DEFAULT TEXTBLOCK FOREGROUND STYLE -->
    <Style TargetType=""{x:Type TextBlock}"">
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
    </Style>

    <!-- 1. SLIM FLUENT SCROLLBAR -->
    <ControlTemplate x:Key=""ZeroVerticalScrollBar"" TargetType=""{x:Type ScrollBar}"">
        <Grid Width=""8"" Background=""Transparent"">
            <Track x:Name=""PART_Track"" IsDirectionReversed=""True"">
                <Track.Thumb>
                    <Thumb>
                        <Thumb.Template>
                            <ControlTemplate TargetType=""{x:Type Thumb}"">
                                <Border x:Name=""thumbBorder""
                                        Background=""{DynamicResource ZeroUI.ScrollThumb}""
                                        CornerRadius=""4""
                                        Margin=""1,0,1,0""
                                        SnapsToDevicePixels=""True"" />
                                <ControlTemplate.Triggers>
                                    <Trigger Property=""IsMouseOver"" Value=""True"">
                                        <Setter TargetName=""thumbBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.ScrollThumbHover}"" />
                                    </Trigger>
                                </ControlTemplate.Triggers>
                            </ControlTemplate>
                        </Thumb.Template>
                    </Thumb>
                </Track.Thumb>
            </Track>
        </Grid>
    </ControlTemplate>

    <ControlTemplate x:Key=""ZeroHorizontalScrollBar"" TargetType=""{x:Type ScrollBar}"">
        <Grid Height=""8"" Background=""Transparent"">
            <Track x:Name=""PART_Track"" IsDirectionReversed=""False"">
                <Track.Thumb>
                    <Thumb>
                        <Thumb.Template>
                            <ControlTemplate TargetType=""{x:Type Thumb}"">
                                <Border x:Name=""thumbBorder""
                                        Background=""{DynamicResource ZeroUI.ScrollThumb}""
                                        CornerRadius=""4""
                                        Margin=""0,1,0,1""
                                        SnapsToDevicePixels=""True"" />
                                <ControlTemplate.Triggers>
                                    <Trigger Property=""IsMouseOver"" Value=""True"">
                                        <Setter TargetName=""thumbBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.ScrollThumbHover}"" />
                                    </Trigger>
                                </ControlTemplate.Triggers>
                            </ControlTemplate>
                        </Thumb.Template>
                    </Thumb>
                </Track.Thumb>
            </Track>
        </Grid>
    </ControlTemplate>

    <Style TargetType=""{x:Type ScrollBar}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Style.Triggers>
            <Trigger Property=""Orientation"" Value=""Vertical"">
                <Setter Property=""Width"" Value=""8"" />
                <Setter Property=""Template"" Value=""{StaticResource ZeroVerticalScrollBar}"" />
            </Trigger>
            <Trigger Property=""Orientation"" Value=""Horizontal"">
                <Setter Property=""Height"" Value=""8"" />
                <Setter Property=""Template"" Value=""{StaticResource ZeroHorizontalScrollBar}"" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <Style TargetType=""{x:Type ScrollViewer}"">
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type ScrollViewer}"">
                    <Grid SnapsToDevicePixels=""True"">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width=""*"" />
                            <ColumnDefinition Width=""Auto"" />
                        </Grid.ColumnDefinitions>
                        <Grid.RowDefinitions>
                            <RowDefinition Height=""*"" />
                            <RowDefinition Height=""Auto"" />
                        </Grid.RowDefinitions>
                        <ScrollContentPresenter Grid.Column=""0"" Grid.Row=""0"" Margin=""{TemplateBinding Padding}"" />
                        <ScrollBar x:Name=""PART_VerticalScrollBar""
                                   Grid.Column=""1"" Grid.Row=""0""
                                   Width=""8""
                                   Value=""{TemplateBinding VerticalOffset}""
                                   Maximum=""{TemplateBinding ScrollableHeight}""
                                   ViewportSize=""{TemplateBinding ViewportHeight}""
                                   Visibility=""{TemplateBinding ComputedVerticalScrollBarVisibility}"" />
                        <ScrollBar x:Name=""PART_HorizontalScrollBar""
                                   Grid.Column=""0"" Grid.Row=""1""
                                   Height=""8""
                                   Orientation=""Horizontal""
                                   Value=""{TemplateBinding HorizontalOffset}""
                                   Maximum=""{TemplateBinding ScrollableWidth}""
                                   ViewportSize=""{TemplateBinding ViewportWidth}""
                                   Visibility=""{TemplateBinding ComputedHorizontalScrollBarVisibility}"" />
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""ComputedVerticalScrollBarVisibility"" Value=""Collapsed"">
                            <Setter TargetName=""PART_VerticalScrollBar"" Property=""Visibility"" Value=""Collapsed"" />
                        </Trigger>
                        <Trigger Property=""ComputedVerticalScrollBarVisibility"" Value=""Hidden"">
                            <Setter TargetName=""PART_VerticalScrollBar"" Property=""Visibility"" Value=""Collapsed"" />
                        </Trigger>
                        <Trigger Property=""ComputedHorizontalScrollBarVisibility"" Value=""Collapsed"">
                            <Setter TargetName=""PART_HorizontalScrollBar"" Property=""Visibility"" Value=""Collapsed"" />
                        </Trigger>
                        <Trigger Property=""ComputedHorizontalScrollBarVisibility"" Value=""Hidden"">
                            <Setter TargetName=""PART_HorizontalScrollBar"" Property=""Visibility"" Value=""Collapsed"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 2. BUTTON STYLE -->
    <Style TargetType=""{x:Type Button}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgInput}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Padding"" Value=""12,5,12,5"" />
        <Setter Property=""FontSize"" Value=""12"" />
        <Setter Property=""Cursor"" Value=""Hand"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type Button}"">
                    <Border x:Name=""btnBorder""
                            Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""5""
                            Padding=""{TemplateBinding Padding}""
                            SnapsToDevicePixels=""True"">
                        <ContentPresenter HorizontalAlignment=""Center""
                                          VerticalAlignment=""Center""
                                          RecognizesAccessKey=""True"" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""btnBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgHover}"" />
                            <Setter TargetName=""btnBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                        </Trigger>
                        <Trigger Property=""IsPressed"" Value=""True"">
                            <Setter TargetName=""btnBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgActive}"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter TargetName=""btnBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgDisabled}"" />
                            <Setter TargetName=""btnBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderSubtle}"" />
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextMuted}"" />
                            <Setter Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 3. COMBOBOX ITEM STYLE -->
    <Style TargetType=""{x:Type ComboBoxItem}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""FontSize"" Value=""12"" />
        <Setter Property=""Cursor"" Value=""Hand"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type ComboBoxItem}"">
                    <Border x:Name=""itemBorder""
                            Background=""Transparent""
                            CornerRadius=""4""
                            Margin=""2,1,2,1""
                            Padding=""8,5,8,5""
                            SnapsToDevicePixels=""True"">
                        <ContentPresenter />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""itemBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgHover}"" />
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
                        </Trigger>
                        <Trigger Property=""IsSelected"" Value=""True"">
                            <Setter TargetName=""itemBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.SelectionBackground}"" />
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.SelectionForeground}"" />
                            <Setter Property=""FontWeight"" Value=""SemiBold"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextMuted}"" />
                            <Setter Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 4. COMBOBOX STYLE -->
    <Style TargetType=""{x:Type ComboBox}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgInput}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""FontSize"" Value=""12"" />
        <Setter Property=""ScrollViewer.HorizontalScrollBarVisibility"" Value=""Auto"" />
        <Setter Property=""ScrollViewer.VerticalScrollBarVisibility"" Value=""Auto"" />
        <Setter Property=""ItemContainerStyle"" Value=""{DynamicResource {x:Type ComboBoxItem}}"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type ComboBox}"">
                    <Grid>
                        <ToggleButton x:Name=""toggleBtn""
                                      Focusable=""False""
                                      IsChecked=""{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}""
                                      ClickMode=""Press"">
                            <ToggleButton.Template>
                                <ControlTemplate TargetType=""{x:Type ToggleButton}"">
                                    <Border x:Name=""tBorder""
                                            Background=""{DynamicResource ZeroUI.BgInput}""
                                            BorderBrush=""{DynamicResource ZeroUI.BorderDefault}""
                                            BorderThickness=""1""
                                            CornerRadius=""5""
                                            SnapsToDevicePixels=""True"">
                                        <Grid>
                                            <Grid.ColumnDefinitions>
                                                <ColumnDefinition Width=""*"" />
                                                <ColumnDefinition Width=""22"" />
                                            </Grid.ColumnDefinitions>
                                            <Path x:Name=""arrow""
                                                  Grid.Column=""1""
                                                  Data=""M 0 0 L 4 4 L 8 0 Z""
                                                  Fill=""{DynamicResource ZeroUI.TextMuted}""
                                                  HorizontalAlignment=""Center""
                                                  VerticalAlignment=""Center"" />
                                        </Grid>
                                    </Border>
                                    <ControlTemplate.Triggers>
                                        <Trigger Property=""IsMouseOver"" Value=""True"">
                                            <Setter TargetName=""tBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgHover}"" />
                                            <Setter TargetName=""tBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                                            <Setter TargetName=""arrow"" Property=""Fill"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                                        </Trigger>
                                        <Trigger Property=""IsChecked"" Value=""True"">
                                            <Setter TargetName=""tBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                                            <Setter TargetName=""arrow"" Property=""Fill"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                                        </Trigger>
                                    </ControlTemplate.Triggers>
                                </ControlTemplate>
                            </ToggleButton.Template>
                        </ToggleButton>
                        <ContentPresenter x:Name=""ContentSite""
                                          IsHitTestVisible=""False""
                                          Content=""{TemplateBinding SelectionBoxItem}""
                                          ContentTemplate=""{TemplateBinding SelectionBoxItemTemplate}""
                                          ContentTemplateSelector=""{TemplateBinding ItemTemplateSelector}""
                                          VerticalAlignment=""Center""
                                          HorizontalAlignment=""Left""
                                          Margin=""8,2,22,2"">
                            <ContentPresenter.Resources>
                                <Style TargetType=""{x:Type TextBlock}"">
                                    <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
                                </Style>
                            </ContentPresenter.Resources>
                        </ContentPresenter>
                        <Popup x:Name=""PART_Popup""
                               Placement=""Bottom""
                               IsOpen=""{TemplateBinding IsDropDownOpen}""
                               AllowsTransparency=""True""
                               Focusable=""False""
                               PopupAnimation=""Slide"">
                            <Grid x:Name=""DropDown""
                                  SnapsToDevicePixels=""True""
                                  MinWidth=""{TemplateBinding ActualWidth}""
                                  MaxHeight=""{TemplateBinding MaxDropDownHeight}"">
                                <Border x:Name=""DropDownBorder""
                                        Background=""{DynamicResource ZeroUI.BgCard}""
                                        BorderBrush=""{DynamicResource ZeroUI.BorderDefault}""
                                        BorderThickness=""1""
                                        CornerRadius=""6""
                                        Margin=""0,3,0,6""
                                        Padding=""3"">
                                    <Border.Effect>
                                        <DropShadowEffect BlurRadius=""10"" ShadowDepth=""3"" Direction=""270"" Opacity=""0.4"" Color=""#000000"" />
                                    </Border.Effect>
                                    <ScrollViewer SnapsToDevicePixels=""True"">
                                        <StackPanel IsItemsHost=""True"" KeyboardNavigation.DirectionalNavigation=""Contained"" />
                                    </ScrollViewer>
                                </Border>
                            </Grid>
                        </Popup>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 5. CHECKBOX STYLE -->
    <Style TargetType=""{x:Type CheckBox}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""FontSize"" Value=""12.5"" />
        <Setter Property=""Cursor"" Value=""Hand"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type CheckBox}"">
                    <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"">
                        <Border x:Name=""chkBorder""
                                Width=""18"" Height=""18""
                                Background=""{DynamicResource ZeroUI.BgInput}""
                                BorderBrush=""{DynamicResource ZeroUI.BorderDefault}""
                                BorderThickness=""1""
                                CornerRadius=""4""
                                VerticalAlignment=""Center""
                                SnapsToDevicePixels=""True"">
                            <Path x:Name=""chkPath""
                                  Data=""M 3 8 L 7 12 L 13 4""
                                  Stroke=""White""
                                  StrokeThickness=""2""
                                  StrokeStartLineCap=""Round""
                                  StrokeEndLineCap=""Round""
                                  StrokeLineJoin=""Round""
                                  Visibility=""Collapsed""
                                  HorizontalAlignment=""Center""
                                  VerticalAlignment=""Center"" />
                        </Border>
                        <ContentPresenter Margin=""8,0,0,0""
                                          VerticalAlignment=""Center""
                                          RecognizesAccessKey=""True"" />
                    </StackPanel>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""chkBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                            <Setter TargetName=""chkBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgHover}"" />
                        </Trigger>
                        <Trigger Property=""IsChecked"" Value=""True"">
                            <Setter TargetName=""chkBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                            <Setter TargetName=""chkBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                            <Setter TargetName=""chkPath"" Property=""Visibility"" Value=""Visible"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter TargetName=""chkBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgDisabled}"" />
                            <Setter TargetName=""chkBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderSubtle}"" />
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextMuted}"" />
                            <Setter Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 6. RADIOBUTTON STYLE -->
    <Style TargetType=""{x:Type RadioButton}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""FontSize"" Value=""12.5"" />
        <Setter Property=""Cursor"" Value=""Hand"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type RadioButton}"">
                    <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"">
                        <Border x:Name=""radioBorder""
                                Width=""18"" Height=""18""
                                Background=""{DynamicResource ZeroUI.BgInput}""
                                BorderBrush=""{DynamicResource ZeroUI.BorderDefault}""
                                BorderThickness=""1""
                                CornerRadius=""9""
                                VerticalAlignment=""Center""
                                SnapsToDevicePixels=""True"">
                            <Ellipse x:Name=""radioDot""
                                     Width=""8"" Height=""8""
                                     Fill=""{DynamicResource ZeroUI.PrimaryAccent}""
                                     Visibility=""Collapsed""
                                     HorizontalAlignment=""Center""
                                     VerticalAlignment=""Center"" />
                        </Border>
                        <ContentPresenter Margin=""8,0,0,0""
                                          VerticalAlignment=""Center""
                                          RecognizesAccessKey=""True"" />
                    </StackPanel>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""radioBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                            <Setter TargetName=""radioBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgHover}"" />
                        </Trigger>
                        <Trigger Property=""IsChecked"" Value=""True"">
                            <Setter TargetName=""radioBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                            <Setter TargetName=""radioDot"" Property=""Visibility"" Value=""Visible"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter TargetName=""radioBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgDisabled}"" />
                            <Setter TargetName=""radioBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderSubtle}"" />
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextMuted}"" />
                            <Setter Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 7. TEXTBOX STYLE -->
    <Style TargetType=""{x:Type TextBox}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgInput}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Padding"" Value=""10,0,10,0"" />
        <Setter Property=""VerticalContentAlignment"" Value=""Center"" />
        <Setter Property=""FontSize"" Value=""12.5"" />
        <Setter Property=""CaretBrush"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
        <Setter Property=""SelectionBrush"" Value=""{DynamicResource ZeroUI.SelectionBackground}"" />
        <Setter Property=""SelectionOpacity"" Value=""0.6"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type TextBox}"">
                    <Border x:Name=""txtBorder""
                            Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""5""
                            SnapsToDevicePixels=""True"">
                        <ScrollViewer x:Name=""PART_ContentHost""
                                      Focusable=""False""
                                      HorizontalScrollBarVisibility=""Hidden""
                                      VerticalScrollBarVisibility=""Hidden""
                                      VerticalAlignment=""Center""
                                      Margin=""{TemplateBinding Padding}"" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""txtBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderFocus}"" />
                        </Trigger>
                        <Trigger Property=""IsKeyboardFocused"" Value=""True"">
                            <Setter TargetName=""txtBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderFocus}"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter TargetName=""txtBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgDisabled}"" />
                            <Setter TargetName=""txtBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderSubtle}"" />
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextMuted}"" />
                            <Setter Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 8. TOOLTIP STYLE -->
    <Style TargetType=""{x:Type ToolTip}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgCard}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Padding"" Value=""8,4,8,4"" />
        <Setter Property=""FontSize"" Value=""12"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type ToolTip}"">
                    <Border Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""5""
                            Padding=""{TemplateBinding Padding}""
                            SnapsToDevicePixels=""True"">
                        <Border.Effect>
                            <DropShadowEffect BlurRadius=""8"" ShadowDepth=""2"" Direction=""270"" Opacity=""0.4"" Color=""#000000"" />
                        </Border.Effect>
                        <ContentPresenter Content=""{TemplateBinding Content}"" />
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 9. CONTEXTMENU & MENUITEM STYLE -->
    <Style TargetType=""{x:Type ContextMenu}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgCard}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Padding"" Value=""4"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type ContextMenu}"">
                    <Border Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""6""
                            Padding=""{TemplateBinding Padding}""
                            SnapsToDevicePixels=""True"">
                        <Border.Effect>
                            <DropShadowEffect BlurRadius=""10"" ShadowDepth=""3"" Direction=""270"" Opacity=""0.4"" Color=""#000000"" />
                        </Border.Effect>
                        <StackPanel IsItemsHost=""True"" KeyboardNavigation.DirectionalNavigation=""Cycle"" />
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType=""{x:Type MenuItem}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""FontSize"" Value=""12.5"" />
        <Setter Property=""Padding"" Value=""10,6,10,6"" />
        <Setter Property=""Cursor"" Value=""Hand"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type MenuItem}"">
                    <Border x:Name=""itemBorder""
                            Background=""Transparent""
                            CornerRadius=""4""
                            Padding=""{TemplateBinding Padding}""
                            SnapsToDevicePixels=""True"">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""Auto"" SharedSizeGroup=""Icon"" />
                                <ColumnDefinition Width=""*"" />
                                <ColumnDefinition Width=""Auto"" SharedSizeGroup=""Shortcut"" />
                            </Grid.ColumnDefinitions>
                            <ContentPresenter x:Name=""Icon""
                                              Grid.Column=""0""
                                              Content=""{TemplateBinding Icon}""
                                              Margin=""0,0,8,0""
                                              VerticalAlignment=""Center"" />
                            <ContentPresenter Grid.Column=""1""
                                              Content=""{TemplateBinding Header}""
                                              VerticalAlignment=""Center""
                                              RecognizesAccessKey=""True"" />
                            <TextBlock Grid.Column=""2""
                                       Text=""{TemplateBinding InputGestureText}""
                                       Foreground=""{DynamicResource ZeroUI.TextMuted}""
                                       Margin=""16,0,0,0""
                                       VerticalAlignment=""Center"" />
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsHighlighted"" Value=""True"">
                            <Setter TargetName=""itemBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgHover}"" />
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextMuted}"" />
                            <Setter Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 10. TAB CONTROL & TAB ITEM STYLE -->
    <Style TargetType=""{x:Type TabControl}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Background"" Value=""Transparent"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type TabControl}"">
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height=""Auto"" />
                            <RowDefinition Height=""*"" />
                        </Grid.RowDefinitions>
                        <Border Grid.Row=""0""
                                Background=""{DynamicResource ZeroUI.BgCard}""
                                BorderBrush=""{DynamicResource ZeroUI.BorderDefault}""
                                BorderThickness=""0,0,0,1""
                                Padding=""12,0,12,0"">
                            <TabPanel IsItemsHost=""True"" />
                        </Border>
                        <Border Grid.Row=""1""
                                Background=""{TemplateBinding Background}"">
                            <ContentPresenter ContentSource=""SelectedContent"" />
                        </Border>
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType=""{x:Type TabItem}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextSecondary}"" />
        <Setter Property=""FontSize"" Value=""13"" />
        <Setter Property=""FontWeight"" Value=""Normal"" />
        <Setter Property=""Cursor"" Value=""Hand"" />
        <Setter Property=""Padding"" Value=""16,10,16,10"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type TabItem}"">
                    <Border x:Name=""tabBorder""
                            Background=""Transparent""
                            BorderThickness=""0,0,0,2""
                            BorderBrush=""Transparent""
                            Padding=""{TemplateBinding Padding}""
                            SnapsToDevicePixels=""True"">
                        <ContentPresenter ContentSource=""Header""
                                          HorizontalAlignment=""Center""
                                          VerticalAlignment=""Center"" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""tabBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgHover}"" />
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
                        </Trigger>
                        <Trigger Property=""IsSelected"" Value=""True"">
                            <Setter TargetName=""tabBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                            <Setter Property=""FontWeight"" Value=""SemiBold"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
 
    <!-- 10. ZEROTEXTBOX STYLE -->
    <Style TargetType=""{x:Type edit:ZeroTextBox}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgInput}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Padding"" Value=""10,0,10,0"" />
        <Setter Property=""VerticalContentAlignment"" Value=""Center"" />
        <Setter Property=""FontSize"" Value=""12.5"" />
        <Setter Property=""CaretBrush"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
        <Setter Property=""SelectionBrush"" Value=""{DynamicResource ZeroUI.SelectionBackground}"" />
        <Setter Property=""SelectionOpacity"" Value=""0.6"" />
        <Setter Property=""Height"" Value=""32"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type edit:ZeroTextBox}"">
                    <Border x:Name=""txtBorder""
                            Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""{TemplateBinding CornerRadius}""
                            SnapsToDevicePixels=""True"">
                        <Grid Margin=""{TemplateBinding Padding}"">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""Auto"" />
                                <ColumnDefinition Width=""*"" />
                            </Grid.ColumnDefinitions>

                            <TextBlock x:Name=""PART_Leading""
                                       Grid.Column=""0""
                                       Text=""{TemplateBinding LeadingText}""
                                       Foreground=""{DynamicResource ZeroUI.TextSecondary}""
                                       VerticalAlignment=""Center""
                                       Margin=""0,0,6,0""
                                       Visibility=""Collapsed"" />

                            <Grid Grid.Column=""1"">
                                <TextBlock x:Name=""PART_Placeholder""
                                           Text=""{TemplateBinding Placeholder}""
                                           Foreground=""{DynamicResource ZeroUI.TextMuted}""
                                           VerticalAlignment=""Center""
                                           IsHitTestVisible=""False""
                                           Visibility=""Collapsed"" />

                                <ScrollViewer x:Name=""PART_ContentHost""
                                              Focusable=""False""
                                              HorizontalScrollBarVisibility=""Hidden""
                                              VerticalScrollBarVisibility=""Hidden""
                                              VerticalAlignment=""Center"" />
                            </Grid>
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""Text"" Value="""">
                            <Setter TargetName=""PART_Placeholder"" Property=""Visibility"" Value=""Visible"" />
                        </Trigger>
                        <Trigger Property=""LeadingText"" Value="""">
                            <Setter TargetName=""PART_Leading"" Property=""Visibility"" Value=""Collapsed"" />
                        </Trigger>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""txtBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderFocus}"" />
                        </Trigger>
                        <Trigger Property=""IsKeyboardFocused"" Value=""True"">
                            <Setter TargetName=""txtBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderFocus}"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter TargetName=""txtBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgDisabled}"" />
                            <Setter TargetName=""txtBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderSubtle}"" />
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextMuted}"" />
                            <Setter Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 11. ZERONUMERICBOX STYLE -->
    <Style TargetType=""{x:Type edit:ZeroNumericBox}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgInput}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""FontSize"" Value=""12.5"" />
        <Setter Property=""Height"" Value=""32"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type edit:ZeroNumericBox}"">
                    <Border x:Name=""boxBorder""
                            Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""5""
                            SnapsToDevicePixels=""True"">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""*"" />
                                <ColumnDefinition Width=""24"" />
                            </Grid.ColumnDefinitions>

                            <TextBox x:Name=""PART_TextBox""
                                     Grid.Column=""0""
                                     Background=""Transparent""
                                     BorderThickness=""0""
                                     Padding=""10,0,8,0""
                                     VerticalContentAlignment=""Center""
                                     TextAlignment=""{TemplateBinding TextAlignment}""
                                     Foreground=""{TemplateBinding Foreground}""
                                     FontSize=""{TemplateBinding FontSize}""
                                     CaretBrush=""{DynamicResource ZeroUI.PrimaryAccent}""
                                     IsReadOnly=""{TemplateBinding IsReadOnly}"" />

                            <Border Grid.Column=""1""
                                    BorderBrush=""{DynamicResource ZeroUI.BorderSubtle}""
                                    BorderThickness=""1,0,0,0"">
                                <Grid>
                                    <Grid.RowDefinitions>
                                        <RowDefinition Height=""*"" />
                                        <RowDefinition Height=""*"" />
                                    </Grid.RowDefinitions>

                                    <RepeatButton x:Name=""PART_UpButton""
                                                  Grid.Row=""0""
                                                  Interval=""60""
                                                  Delay=""250""
                                                  Focusable=""False""
                                                  Background=""Transparent""
                                                  BorderThickness=""0""
                                                  Cursor=""Hand"">
                                        <RepeatButton.Template>
                                            <ControlTemplate TargetType=""{x:Type RepeatButton}"">
                                                <Border x:Name=""upBorder"" Background=""{TemplateBinding Background}"">
                                                    <Path Data=""M 0 3 L 3 0 L 6 3 Z""
                                                          Fill=""{DynamicResource ZeroUI.TextSecondary}""
                                                          HorizontalAlignment=""Center""
                                                          VerticalAlignment=""Center"" />
                                                </Border>
                                                <ControlTemplate.Triggers>
                                                    <Trigger Property=""IsMouseOver"" Value=""True"">
                                                        <Setter TargetName=""upBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgHover}"" />
                                                    </Trigger>
                                                    <Trigger Property=""IsPressed"" Value=""True"">
                                                        <Setter TargetName=""upBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgActive}"" />
                                                    </Trigger>
                                                </ControlTemplate.Triggers>
                                            </ControlTemplate>
                                        </RepeatButton.Template>
                                    </RepeatButton>

                                    <RepeatButton x:Name=""PART_DownButton""
                                                  Grid.Row=""1""
                                                  Interval=""60""
                                                  Delay=""250""
                                                  Focusable=""False""
                                                  Background=""Transparent""
                                                  BorderThickness=""0""
                                                  Cursor=""Hand"">
                                        <RepeatButton.Template>
                                            <ControlTemplate TargetType=""{x:Type RepeatButton}"">
                                                <Border x:Name=""downBorder"" Background=""{TemplateBinding Background}"" BorderBrush=""{DynamicResource ZeroUI.BorderSubtle}"" BorderThickness=""0,1,0,0"">
                                                    <Path Data=""M 0 0 L 3 3 L 6 0 Z""
                                                          Fill=""{DynamicResource ZeroUI.TextSecondary}""
                                                          HorizontalAlignment=""Center""
                                                          VerticalAlignment=""Center"" />
                                                </Border>
                                                <ControlTemplate.Triggers>
                                                    <Trigger Property=""IsMouseOver"" Value=""True"">
                                                        <Setter TargetName=""downBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgHover}"" />
                                                    </Trigger>
                                                    <Trigger Property=""IsPressed"" Value=""True"">
                                                        <Setter TargetName=""downBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgActive}"" />
                                                    </Trigger>
                                                </ControlTemplate.Triggers>
                                            </ControlTemplate>
                                        </RepeatButton.Template>
                                    </RepeatButton>
                                </Grid>
                            </Border>
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""boxBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderFocus}"" />
                        </Trigger>
                        <Trigger Property=""IsKeyboardFocusWithin"" Value=""True"">
                            <Setter TargetName=""boxBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderFocus}"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter TargetName=""boxBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgDisabled}"" />
                            <Setter TargetName=""boxBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderSubtle}"" />
                            <Setter Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 15. ZGRIDCARD / GRIDCARD / ZEROCARD STYLE -->
    <Style x:Key=""{x:Type ind:ZGridCard}"" TargetType=""{x:Type ind:ZGridCard}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgCard}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Padding"" Value=""16"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type ind:ZGridCard}"">
                    <Border Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""8""
                            Padding=""{TemplateBinding Padding}"">
                        <DockPanel LastChildFill=""True"">
                            <StackPanel x:Name=""PART_HeaderPanel"" DockPanel.Dock=""Top"" Margin=""0,0,0,10"">
                                <TextBlock x:Name=""PART_HeaderText""
                                           Text=""{Binding HeaderText, RelativeSource={RelativeSource TemplatedParent}}""
                                           FontSize=""14"" FontWeight=""SemiBold""
                                           Foreground=""{DynamicResource ZeroUI.TextPrimary}"" />
                                <TextBlock x:Name=""PART_SubtitleText""
                                           Text=""{Binding SubtitleText, RelativeSource={RelativeSource TemplatedParent}}""
                                           FontSize=""11.5""
                                           Foreground=""{DynamicResource ZeroUI.TextSecondary}""
                                           Margin=""0,2,0,0"" />
                            </StackPanel>
                            <ContentPresenter />
                        </DockPanel>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""HeaderText"" Value="""">
                            <Setter TargetName=""PART_HeaderPanel"" Property=""Visibility"" Value=""Collapsed"" />
                        </Trigger>
                        <Trigger Property=""SubtitleText"" Value="""">
                            <Setter TargetName=""PART_SubtitleText"" Property=""Visibility"" Value=""Collapsed"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType=""{x:Type ind:GridCard}"" BasedOn=""{StaticResource {x:Type ind:ZGridCard}}"" />
    <Style TargetType=""{x:Type ind:ZeroGridCard}"" BasedOn=""{StaticResource {x:Type ind:ZGridCard}}"" />
    <Style TargetType=""{x:Type ind:ZeroCard}"" BasedOn=""{StaticResource {x:Type ind:ZGridCard}}"" />

    <!-- 16. PASSWORDBOX STYLE -->
    <Style TargetType=""{x:Type PasswordBox}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgInput}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Padding"" Value=""10,0,10,0"" />
        <Setter Property=""VerticalContentAlignment"" Value=""Center"" />
        <Setter Property=""FontSize"" Value=""12.5"" />
        <Setter Property=""CaretBrush"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
        <Setter Property=""SelectionBrush"" Value=""{DynamicResource ZeroUI.SelectionBackground}"" />
        <Setter Property=""SelectionOpacity"" Value=""0.6"" />
        <Setter Property=""PasswordChar"" Value=""●"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type PasswordBox}"">
                    <Border x:Name=""pwdBorder""
                            Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""5""
                            SnapsToDevicePixels=""True"">
                        <ScrollViewer x:Name=""PART_ContentHost""
                                      Focusable=""False""
                                      HorizontalScrollBarVisibility=""Hidden""
                                      VerticalScrollBarVisibility=""Hidden""
                                      VerticalAlignment=""Center""
                                      Margin=""{TemplateBinding Padding}"" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""pwdBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderFocus}"" />
                        </Trigger>
                        <Trigger Property=""IsKeyboardFocused"" Value=""True"">
                            <Setter TargetName=""pwdBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderFocus}"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter TargetName=""pwdBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgDisabled}"" />
                            <Setter TargetName=""pwdBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderSubtle}"" />
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextMuted}"" />
                            <Setter Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 17. LISTBOX & LISTBOXITEM STYLES -->
    <Style TargetType=""{x:Type ListBoxItem}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Background"" Value=""Transparent"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""Padding"" Value=""10,7,10,7"" />
        <Setter Property=""Margin"" Value=""0,1,0,1"" />
        <Setter Property=""HorizontalContentAlignment"" Value=""Left"" />
        <Setter Property=""VerticalContentAlignment"" Value=""Center"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type ListBoxItem}"">
                    <Border x:Name=""itemBorder""
                            Background=""{TemplateBinding Background}""
                            BorderThickness=""0""
                            CornerRadius=""4""
                            Margin=""{TemplateBinding Margin}""
                            Padding=""{TemplateBinding Padding}""
                            SnapsToDevicePixels=""True"">
                        <ContentPresenter HorizontalAlignment=""{TemplateBinding HorizontalContentAlignment}""
                                          VerticalAlignment=""{TemplateBinding VerticalContentAlignment}""
                                          SnapsToDevicePixels=""{TemplateBinding SnapsToDevicePixels}"" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""itemBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgHover}"" />
                        </Trigger>
                        <Trigger Property=""IsSelected"" Value=""True"">
                            <Setter TargetName=""itemBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgActive}"" />
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextMuted}"" />
                            <Setter Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType=""{x:Type ListBox}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgCard}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""ScrollViewer.HorizontalScrollBarVisibility"" Value=""Auto"" />
        <Setter Property=""ScrollViewer.VerticalScrollBarVisibility"" Value=""Auto"" />
        <Setter Property=""ScrollViewer.CanContentScroll"" Value=""True"" />
        <Setter Property=""Padding"" Value=""4"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type ListBox}"">
                    <Border Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""6""
                            SnapsToDevicePixels=""True"">
                        <ScrollViewer Focusable=""False"" Padding=""{TemplateBinding Padding}"">
                            <ItemsPresenter SnapsToDevicePixels=""{TemplateBinding SnapsToDevicePixels}"" />
                        </ScrollViewer>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 18. LISTVIEW, LISTVIEWITEM & GRIDVIEWCOLUMNHEADER STYLES -->
    <Style TargetType=""{x:Type GridViewColumnHeader}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgPrimary}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextSecondary}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderSubtle}"" />
        <Setter Property=""BorderThickness"" Value=""0,0,1,1"" />
        <Setter Property=""Padding"" Value=""10,8,10,8"" />
        <Setter Property=""FontWeight"" Value=""SemiBold"" />
        <Setter Property=""FontSize"" Value=""11.5"" />
        <Setter Property=""HorizontalContentAlignment"" Value=""Left"" />
        <Setter Property=""VerticalContentAlignment"" Value=""Center"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type GridViewColumnHeader}"">
                    <Grid SnapsToDevicePixels=""True"">
                        <Border x:Name=""headerBorder""
                                Background=""{TemplateBinding Background}""
                                BorderBrush=""{TemplateBinding BorderBrush}""
                                BorderThickness=""{TemplateBinding BorderThickness}""
                                Padding=""{TemplateBinding Padding}"">
                            <ContentPresenter HorizontalAlignment=""{TemplateBinding HorizontalContentAlignment}""
                                              VerticalAlignment=""{TemplateBinding VerticalContentAlignment}""
                                              SnapsToDevicePixels=""{TemplateBinding SnapsToDevicePixels}"" />
                        </Border>
                        <Thumb x:Name=""PART_HeaderGripper""
                               HorizontalAlignment=""Right""
                               Width=""8""
                               Margin=""0,0,-4,0""
                               Cursor=""SizeWE"">
                            <Thumb.Template>
                                <ControlTemplate TargetType=""{x:Type Thumb}"">
                                    <Border Background=""Transparent"" Width=""8"" />
                                </ControlTemplate>
                            </Thumb.Template>
                        </Thumb>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""headerBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgHover}"" />
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
                        </Trigger>
                        <Trigger Property=""IsPressed"" Value=""True"">
                            <Setter TargetName=""headerBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgActive}"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType=""{x:Type ListViewItem}"" BasedOn=""{StaticResource {x:Type ListBoxItem}}"" />

    <Style TargetType=""{x:Type ListView}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgCard}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""ScrollViewer.HorizontalScrollBarVisibility"" Value=""Auto"" />
        <Setter Property=""ScrollViewer.VerticalScrollBarVisibility"" Value=""Auto"" />
        <Setter Property=""ScrollViewer.CanContentScroll"" Value=""True"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type ListView}"">
                    <Border Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""6""
                            SnapsToDevicePixels=""True"">
                        <ScrollViewer Focusable=""False"">
                            <ItemsPresenter SnapsToDevicePixels=""{TemplateBinding SnapsToDevicePixels}"" />
                        </ScrollViewer>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 19. TREEVIEW & TREEVIEWITEM STYLES -->
    <ControlTemplate x:Key=""ZeroTreeExpanderButtonTemplate"" TargetType=""{x:Type ToggleButton}"">
        <Border Width=""16"" Height=""16"" Background=""Transparent"" SnapsToDevicePixels=""True"">
            <Path x:Name=""arrowPath""
                  Data=""M 1 1 L 5 5 L 1 9""
                  Stroke=""{DynamicResource ZeroUI.TextSecondary}""
                  StrokeThickness=""1.5""
                  HorizontalAlignment=""Center""
                  VerticalAlignment=""Center""
                  RenderTransformOrigin=""0.5,0.5"">
                <Path.RenderTransform>
                    <RotateTransform Angle=""0"" />
                </Path.RenderTransform>
            </Path>
        </Border>
        <ControlTemplate.Triggers>
            <Trigger Property=""IsChecked"" Value=""True"">
                <Setter TargetName=""arrowPath"" Property=""RenderTransform"">
                    <Setter.Value>
                        <RotateTransform Angle=""90"" />
                    </Setter.Value>
                </Setter>
                <Setter TargetName=""arrowPath"" Property=""Stroke"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
            </Trigger>
            <Trigger Property=""IsMouseOver"" Value=""True"">
                <Setter TargetName=""arrowPath"" Property=""Stroke"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
            </Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>

    <Style TargetType=""{x:Type TreeViewItem}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type TreeViewItem}"">
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height=""Auto"" />
                            <RowDefinition Height=""Auto"" />
                        </Grid.RowDefinitions>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width=""19"" />
                            <ColumnDefinition Width=""*"" />
                        </Grid.ColumnDefinitions>
                        <ToggleButton x:Name=""Expander""
                                      Template=""{StaticResource ZeroTreeExpanderButtonTemplate}""
                                      IsChecked=""{Binding IsExpanded, RelativeSource={RelativeSource TemplatedParent}}""
                                      ClickMode=""Press""
                                      VerticalAlignment=""Center"" />
                        <Border x:Name=""headerBorder""
                                Grid.Column=""1""
                                Background=""Transparent""
                                CornerRadius=""4""
                                Padding=""6,3,8,3""
                                Margin=""1,1,2,1"">
                            <ContentPresenter x:Name=""PART_Header""
                                              ContentSource=""Header""
                                              HorizontalAlignment=""Left""
                                              VerticalAlignment=""Center""
                                              SnapsToDevicePixels=""{TemplateBinding SnapsToDevicePixels}"" />
                        </Border>
                        <ItemsPresenter x:Name=""ItemsHost""
                                        Grid.Row=""1""
                                        Grid.Column=""1""
                                        Visibility=""Collapsed"" />
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsExpanded"" Value=""True"">
                            <Setter TargetName=""ItemsHost"" Property=""Visibility"" Value=""Visible"" />
                        </Trigger>
                        <Trigger Property=""HasItems"" Value=""False"">
                            <Setter TargetName=""Expander"" Property=""Visibility"" Value=""Hidden"" />
                        </Trigger>
                        <Trigger Property=""IsMouseOver"" SourceName=""headerBorder"" Value=""True"">
                            <Setter TargetName=""headerBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgHover}"" />
                        </Trigger>
                        <Trigger Property=""IsSelected"" Value=""True"">
                            <Setter TargetName=""headerBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgActive}"" />
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextMuted}"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType=""{x:Type TreeView}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgCard}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""Padding"" Value=""4"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type TreeView}"">
                    <Border Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""6""
                            SnapsToDevicePixels=""True"">
                        <ScrollViewer Focusable=""False"" Padding=""{TemplateBinding Padding}"">
                            <ItemsPresenter SnapsToDevicePixels=""{TemplateBinding SnapsToDevicePixels}"" />
                        </ScrollViewer>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 20. NATIVE PROGRESSBAR STYLE -->
    <Style TargetType=""{x:Type ProgressBar}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgInput}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderSubtle}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Height"" Value=""6"" />
        <Setter Property=""MinHeight"" Value=""4"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type ProgressBar}"">
                    <Border Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""3""
                            ClipToBounds=""True""
                            SnapsToDevicePixels=""True"">
                        <Grid x:Name=""TemplateRoot"">
                            <Border x:Name=""PART_Track"" />
                            <Border x:Name=""PART_Indicator""
                                    HorizontalAlignment=""Left""
                                    Background=""{TemplateBinding Foreground}""
                                    CornerRadius=""2"" />
                            <Border x:Name=""IndeterminateBar""
                                    Background=""{TemplateBinding Foreground}""
                                    Width=""80""
                                    HorizontalAlignment=""Left""
                                    CornerRadius=""2""
                                    Opacity=""0.85""
                                    Visibility=""Collapsed"" />
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""Orientation"" Value=""Vertical"">
                            <Setter TargetName=""PART_Indicator"" Property=""HorizontalAlignment"" Value=""Stretch"" />
                            <Setter TargetName=""PART_Indicator"" Property=""VerticalAlignment"" Value=""Bottom"" />
                        </Trigger>
                        <Trigger Property=""IsIndeterminate"" Value=""True"">
                            <Setter TargetName=""PART_Indicator"" Property=""Visibility"" Value=""Collapsed"" />
                            <Setter TargetName=""IndeterminateBar"" Property=""Visibility"" Value=""Visible"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 21. NATIVE SLIDER STYLE -->
    <ControlTemplate x:Key=""ZeroSliderTrackButtonTemplate"" TargetType=""{x:Type RepeatButton}"">
        <Border Background=""Transparent"" SnapsToDevicePixels=""True"" />
    </ControlTemplate>

    <ControlTemplate x:Key=""ZeroSliderHorizontalThumbTemplate"" TargetType=""{x:Type Thumb}"">
        <Grid Width=""16"" Height=""16"" SnapsToDevicePixels=""True"">
            <Ellipse x:Name=""thumbBg""
                     Width=""14"" Height=""14""
                     Fill=""{DynamicResource ZeroUI.PrimaryAccent}""
                     Stroke=""{DynamicResource ZeroUI.BgCard}""
                     StrokeThickness=""2"" />
        </Grid>
        <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
                <Setter TargetName=""thumbBg"" Property=""Fill"" Value=""{DynamicResource ZeroUI.PrimaryAccentDark}"" />
                <Setter TargetName=""thumbBg"" Property=""Width"" Value=""16"" />
                <Setter TargetName=""thumbBg"" Property=""Height"" Value=""16"" />
            </Trigger>
            <Trigger Property=""IsDragging"" Value=""True"">
                <Setter TargetName=""thumbBg"" Property=""Stroke"" Value=""{DynamicResource ZeroUI.BorderFocus}"" />
            </Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>

    <ControlTemplate x:Key=""ZeroSliderHorizontalTemplate"" TargetType=""{x:Type Slider}"">
        <Grid VerticalAlignment=""Center"" SnapsToDevicePixels=""True"">
            <Grid.RowDefinitions>
                <RowDefinition Height=""Auto"" />
                <RowDefinition Height=""Auto"" MinHeight=""{TemplateBinding MinHeight}"" />
                <RowDefinition Height=""Auto"" />
            </Grid.RowDefinitions>
            <Border Grid.Row=""1""
                    Height=""4""
                    CornerRadius=""2""
                    Background=""{DynamicResource ZeroUI.BorderSubtle}""
                    Margin=""5,0,5,0""
                    VerticalAlignment=""Center"" />
            <Track x:Name=""PART_Track"" Grid.Row=""1"">
                <Track.DecreaseRepeatButton>
                    <RepeatButton Command=""{x:Static Slider.DecreaseLarge}"" Template=""{StaticResource ZeroSliderTrackButtonTemplate}"" />
                </Track.DecreaseRepeatButton>
                <Track.IncreaseRepeatButton>
                    <RepeatButton Command=""{x:Static Slider.IncreaseLarge}"" Template=""{StaticResource ZeroSliderTrackButtonTemplate}"" />
                </Track.IncreaseRepeatButton>
                <Track.Thumb>
                    <Thumb x:Name=""Thumb"" Template=""{StaticResource ZeroSliderHorizontalThumbTemplate}"" VerticalAlignment=""Center"" />
                </Track.Thumb>
            </Track>
        </Grid>
    </ControlTemplate>

    <ControlTemplate x:Key=""ZeroSliderVerticalTemplate"" TargetType=""{x:Type Slider}"">
        <Grid HorizontalAlignment=""Center"" SnapsToDevicePixels=""True"">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width=""Auto"" />
                <ColumnDefinition Width=""Auto"" MinWidth=""{TemplateBinding MinWidth}"" />
                <ColumnDefinition Width=""Auto"" />
            </Grid.ColumnDefinitions>
            <Border Grid.Column=""1""
                    Width=""4""
                    CornerRadius=""2""
                    Background=""{DynamicResource ZeroUI.BorderSubtle}""
                    Margin=""0,5,0,5""
                    HorizontalAlignment=""Center"" />
            <Track x:Name=""PART_Track"" Grid.Column=""1"" IsDirectionReversed=""True"">
                <Track.DecreaseRepeatButton>
                    <RepeatButton Command=""{x:Static Slider.DecreaseLarge}"" Template=""{StaticResource ZeroSliderTrackButtonTemplate}"" />
                </Track.DecreaseRepeatButton>
                <Track.IncreaseRepeatButton>
                    <RepeatButton Command=""{x:Static Slider.IncreaseLarge}"" Template=""{StaticResource ZeroSliderTrackButtonTemplate}"" />
                </Track.IncreaseRepeatButton>
                <Track.Thumb>
                    <Thumb x:Name=""Thumb"" Template=""{StaticResource ZeroSliderHorizontalThumbTemplate}"" HorizontalAlignment=""Center"" />
                </Track.Thumb>
            </Track>
        </Grid>
    </ControlTemplate>

    <Style TargetType=""{x:Type Slider}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Focusable"" Value=""True"" />
        <Setter Property=""Template"" Value=""{StaticResource ZeroSliderHorizontalTemplate}"" />
        <Style.Triggers>
            <Trigger Property=""Orientation"" Value=""Vertical"">
                <Setter Property=""Template"" Value=""{StaticResource ZeroSliderVerticalTemplate}"" />
            </Trigger>
            <Trigger Property=""IsEnabled"" Value=""False"">
                <Setter Property=""Opacity"" Value=""0.5"" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <!-- 22. EXPANDER STYLE -->
    <ControlTemplate x:Key=""ZeroExpanderHeaderButtonTemplate"" TargetType=""{x:Type ToggleButton}"">
        <Border Background=""Transparent"" Padding=""12,10,12,10"" SnapsToDevicePixels=""True"">
            <DockPanel LastChildFill=""True"">
                <Border DockPanel.Dock=""Right"" Width=""20"" Height=""20"" Background=""Transparent"">
                    <Path x:Name=""chevron""
                          Data=""M 1 2 L 5 6 L 9 2""
                          Stroke=""{DynamicResource ZeroUI.TextSecondary}""
                          StrokeThickness=""1.5""
                          HorizontalAlignment=""Center""
                          VerticalAlignment=""Center""
                          RenderTransformOrigin=""0.5,0.5"">
                        <Path.RenderTransform>
                            <RotateTransform Angle=""0"" />
                        </Path.RenderTransform>
                    </Path>
                </Border>
                <ContentPresenter VerticalAlignment=""Center"" SnapsToDevicePixels=""{TemplateBinding SnapsToDevicePixels}"" />
            </DockPanel>
        </Border>
        <ControlTemplate.Triggers>
            <Trigger Property=""IsChecked"" Value=""True"">
                <Setter TargetName=""chevron"" Property=""RenderTransform"">
                    <Setter.Value>
                        <RotateTransform Angle=""180"" />
                    </Setter.Value>
                </Setter>
                <Setter TargetName=""chevron"" Property=""Stroke"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
            </Trigger>
            <Trigger Property=""IsMouseOver"" Value=""True"">
                <Setter TargetName=""chevron"" Property=""Stroke"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
            </Trigger>
        </ControlTemplate.Triggers>
    </ControlTemplate>

    <Style TargetType=""{x:Type Expander}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgCard}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type Expander}"">
                    <Border Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""6""
                            SnapsToDevicePixels=""True"">
                        <DockPanel>
                            <ToggleButton x:Name=""HeaderSite""
                                          DockPanel.Dock=""Top""
                                          Template=""{StaticResource ZeroExpanderHeaderButtonTemplate}""
                                          Content=""{TemplateBinding Header}""
                                          ContentTemplate=""{TemplateBinding HeaderTemplate}""
                                          IsChecked=""{Binding IsExpanded, RelativeSource={RelativeSource TemplatedParent}}"" />
                            <Border x:Name=""ExpandSite""
                                    Visibility=""Collapsed""
                                    BorderThickness=""0,1,0,0""
                                    BorderBrush=""{DynamicResource ZeroUI.BorderSubtle}""
                                    Padding=""12"">
                                <ContentPresenter Focusable=""False"" />
                            </Border>
                        </DockPanel>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsExpanded"" Value=""True"">
                            <Setter TargetName=""ExpandSite"" Property=""Visibility"" Value=""Visible"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 23. GROUPBOX STYLE -->
    <Style TargetType=""{x:Type GroupBox}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgCard}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""Padding"" Value=""12"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type GroupBox}"">
                    <Border Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""6""
                            SnapsToDevicePixels=""True"">
                        <Grid>
                            <Grid.RowDefinitions>
                                <RowDefinition Height=""Auto"" />
                                <RowDefinition Height=""Auto"" />
                                <RowDefinition Height=""*"" />
                            </Grid.RowDefinitions>
                            <ContentPresenter x:Name=""HeaderContent""
                                              ContentSource=""Header""
                                              ContentTemplate=""{TemplateBinding HeaderTemplate}""
                                              Margin=""12,10,12,6""
                                              TextElement.FontWeight=""SemiBold""
                                              TextElement.FontSize=""13""
                                              TextElement.Foreground=""{DynamicResource ZeroUI.TextPrimary}"" />
                            <Border x:Name=""HeaderDivider""
                                    Grid.Row=""1""
                                    Height=""1""
                                    Background=""{DynamicResource ZeroUI.BorderSubtle}""
                                    Margin=""0,0,0,8"" />
                            <ContentPresenter Grid.Row=""2"" Margin=""{TemplateBinding Padding}"" />
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""Header"" Value=""{x:Null}"">
                            <Setter TargetName=""HeaderContent"" Property=""Visibility"" Value=""Collapsed"" />
                            <Setter TargetName=""HeaderDivider"" Property=""Visibility"" Value=""Collapsed"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 24. CALENDAR & DATEPICKER STYLES -->
    <Style TargetType=""{x:Type CalendarDayButton}"">
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""Background"" Value=""Transparent"" />
        <Setter Property=""BorderThickness"" Value=""0"" />
        <Setter Property=""Padding"" Value=""2"" />
        <Setter Property=""MinWidth"" Value=""26"" />
        <Setter Property=""MinHeight"" Value=""24"" />
        <Setter Property=""HorizontalContentAlignment"" Value=""Center"" />
        <Setter Property=""VerticalContentAlignment"" Value=""Center"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type CalendarDayButton}"">
                    <Border x:Name=""dayBorder""
                            Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""3""
                            Margin=""1""
                            Padding=""{TemplateBinding Padding}""
                            SnapsToDevicePixels=""True"">
                        <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""dayBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgHover}"" />
                        </Trigger>
                        <Trigger Property=""IsSelected"" Value=""True"">
                            <Setter TargetName=""dayBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.SelectionForeground}"" />
                        </Trigger>
                        <Trigger Property=""IsToday"" Value=""True"">
                            <Setter TargetName=""dayBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                            <Setter TargetName=""dayBorder"" Property=""BorderThickness"" Value=""1"" />
                        </Trigger>
                        <Trigger Property=""IsInactive"" Value=""True"">
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextMuted}"" />
                            <Setter Property=""Opacity"" Value=""0.4"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType=""{x:Type CalendarButton}"">
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""Background"" Value=""Transparent"" />
        <Setter Property=""BorderThickness"" Value=""0"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type CalendarButton}"">
                    <Border x:Name=""monthBorder""
                            Background=""{TemplateBinding Background}""
                            CornerRadius=""4""
                            Margin=""2""
                            Padding=""4""
                            SnapsToDevicePixels=""True"">
                        <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""monthBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgHover}"" />
                        </Trigger>
                        <Trigger Property=""IsPressed"" Value=""True"">
                            <Setter TargetName=""monthBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.SelectionForeground}"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType=""{x:Type CalendarItem}"">
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgCard}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
    </Style>

    <Style TargetType=""{x:Type Calendar}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgCard}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""CalendarDayButtonStyle"" Value=""{DynamicResource {x:Type CalendarDayButton}}"" />
        <Setter Property=""CalendarButtonStyle"" Value=""{DynamicResource {x:Type CalendarButton}}"" />
    </Style>

    <Style TargetType=""{x:Type DatePickerTextBox}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""Background"" Value=""Transparent"" />
        <Setter Property=""BorderThickness"" Value=""0"" />
        <Setter Property=""VerticalContentAlignment"" Value=""Center"" />
        <Setter Property=""Padding"" Value=""4,0,4,0"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type DatePickerTextBox}"">
                    <ScrollViewer x:Name=""PART_ContentHost""
                                  Focusable=""False""
                                  HorizontalScrollBarVisibility=""Hidden""
                                  VerticalScrollBarVisibility=""Hidden""
                                  VerticalAlignment=""Center""
                                  Margin=""{TemplateBinding Padding}"" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType=""{x:Type DatePicker}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgInput}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Padding"" Value=""8,0,8,0"" />
        <Setter Property=""Height"" Value=""32"" />
        <Setter Property=""VerticalContentAlignment"" Value=""Center"" />
        <Setter Property=""CalendarStyle"" Value=""{DynamicResource {x:Type Calendar}}"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type DatePicker}"">
                    <Border x:Name=""dpBorder""
                            Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""5""
                            SnapsToDevicePixels=""True"">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""*"" />
                                <ColumnDefinition Width=""30"" />
                            </Grid.ColumnDefinitions>
                            <DatePickerTextBox x:Name=""PART_TextBox""
                                               Grid.Column=""0""
                                               Background=""Transparent""
                                               BorderThickness=""0""
                                               Foreground=""{TemplateBinding Foreground}""
                                               Padding=""{TemplateBinding Padding}""
                                               VerticalContentAlignment=""Center""
                                               Focusable=""{TemplateBinding Focusable}"" />
                            <Button x:Name=""PART_Button""
                                    Grid.Column=""1""
                                    Focusable=""False""
                                    Background=""Transparent""
                                    BorderThickness=""0""
                                    Cursor=""Hand"">
                                <Button.Template>
                                    <ControlTemplate TargetType=""{x:Type Button}"">
                                        <Border Background=""Transparent"" SnapsToDevicePixels=""True"">
                                            <Path Data=""M 2 4 L 14 4 L 14 14 L 2 14 Z M 4 2 L 4 5 M 12 2 L 12 5 M 2 7 L 14 7""
                                                  Stroke=""{DynamicResource ZeroUI.TextSecondary}""
                                                  StrokeThickness=""1.2""
                                                  HorizontalAlignment=""Center""
                                                  VerticalAlignment=""Center"" />
                                        </Border>
                                    </ControlTemplate>
                                </Button.Template>
                            </Button>
                            <Popup x:Name=""PART_Popup""
                                   AllowsTransparency=""True""
                                   Placement=""Bottom""
                                   PlacementTarget=""{Binding ElementName=dpBorder}""
                                   StaysOpen=""False"">
                                <Border Background=""{DynamicResource ZeroUI.BgCard}""
                                        BorderBrush=""{DynamicResource ZeroUI.BorderDefault}""
                                        BorderThickness=""1""
                                        CornerRadius=""6""
                                        Padding=""4""
                                        SnapsToDevicePixels=""True"">
                                    <Border.Effect>
                                        <DropShadowEffect BlurRadius=""12"" Direction=""270"" ShadowDepth=""3"" Opacity=""0.3"" Color=""#000000"" />
                                    </Border.Effect>
                                    <Calendar x:Name=""PART_Calendar"" />
                                </Border>
                            </Popup>
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""dpBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderFocus}"" />
                        </Trigger>
                        <Trigger Property=""IsKeyboardFocusWithin"" Value=""True"">
                            <Setter TargetName=""dpBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderFocus}"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter TargetName=""dpBorder"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgDisabled}"" />
                            <Setter Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 25. STATUSBAR & STATUSBARITEM STYLES -->
    <Style TargetType=""{x:Type StatusBarItem}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextSecondary}"" />
        <Setter Property=""Padding"" Value=""6,0,6,0"" />
        <Setter Property=""VerticalContentAlignment"" Value=""Center"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type StatusBarItem}"">
                    <Border Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            Padding=""{TemplateBinding Padding}"">
                        <ContentPresenter HorizontalAlignment=""{TemplateBinding HorizontalContentAlignment}""
                                          VerticalAlignment=""{TemplateBinding VerticalContentAlignment}""
                                          SnapsToDevicePixels=""{TemplateBinding SnapsToDevicePixels}"" />
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style TargetType=""{x:Type StatusBar}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgCard}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextSecondary}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""0,1,0,0"" />
        <Setter Property=""MinHeight"" Value=""26"" />
        <Setter Property=""Padding"" Value=""8,3,8,3"" />
        <Setter Property=""FontSize"" Value=""12"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type StatusBar}"">
                    <Border Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            Padding=""{TemplateBinding Padding}""
                            SnapsToDevicePixels=""True"">
                        <ItemsPresenter SnapsToDevicePixels=""{TemplateBinding SnapsToDevicePixels}"" />
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 26. SEPARATOR STYLE -->
    <Style TargetType=""{x:Type Separator}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BorderSubtle}"" />
        <Setter Property=""MinHeight"" Value=""1"" />
        <Setter Property=""Height"" Value=""1"" />
        <Setter Property=""Margin"" Value=""0,4,0,4"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type Separator}"">
                    <Border Background=""{TemplateBinding Background}""
                            Height=""1""
                            SnapsToDevicePixels=""True"" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 27. ZTITLEBAR STYLE -->
    <Style x:Key=""{x:Type layout:ZTitleBar}"" TargetType=""{x:Type layout:ZTitleBar}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgPrimary}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""0,0,0,1"" />
        <Setter Property=""Height"" Value=""36"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type layout:ZTitleBar}"">
                    <Border Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            SnapsToDevicePixels=""True"">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""Auto"" />
                                <ColumnDefinition Width=""*"" />
                                <ColumnDefinition Width=""Auto"" />
                            </Grid.ColumnDefinitions>
                            <!-- Title & Icon -->
                            <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"" Margin=""12,0,10,0"">
                                <Image Source=""{TemplateBinding Icon}"" Width=""16"" Height=""16"" Margin=""0,0,8,0"">
                                    <Image.Style>
                                        <Style TargetType=""Image"">
                                            <Style.Triggers>
                                                <Trigger Property=""Source"" Value=""{x:Null}"">
                                                    <Setter Property=""Visibility"" Value=""Collapsed"" />
                                                </Trigger>
                                            </Style.Triggers>
                                        </Style>
                                    </Image.Style>
                                </Image>
                                <TextBlock Text=""{TemplateBinding Title}""
                                           FontWeight=""SemiBold""
                                           FontSize=""12.5""
                                           Foreground=""{TemplateBinding Foreground}""
                                           VerticalAlignment=""Center"" />
                            </StackPanel>
                            <!-- Custom Title Content -->
                            <ContentPresenter x:Name=""PART_ContentHost""
                                              Grid.Column=""1""
                                              Content=""{TemplateBinding TitleContent}""
                                              VerticalAlignment=""Center""
                                              Margin=""8,0,8,0"" />
                            <!-- Window Action Buttons -->
                            <StackPanel Grid.Column=""2"" Orientation=""Horizontal"" VerticalAlignment=""Stretch"">
                                <Button x:Name=""PART_MinBtn"" Width=""44"" Background=""Transparent"" BorderThickness=""0"" Cursor=""Hand"">
                                    <TextBlock Text=""—"" FontSize=""11"" Foreground=""{DynamicResource ZeroUI.TextSecondary}"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" />
                                </Button>
                                <Button x:Name=""PART_MaxBtn"" Width=""44"" Background=""Transparent"" BorderThickness=""0"" Cursor=""Hand"">
                                    <TextBlock x:Name=""PART_MaxGlyph"" Text=""▢"" FontSize=""11"" Foreground=""{DynamicResource ZeroUI.TextSecondary}"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" />
                                </Button>
                                <Button x:Name=""PART_CloseBtn"" Width=""44"" Background=""Transparent"" BorderThickness=""0"" Cursor=""Hand"">
                                    <TextBlock Text=""✕"" FontSize=""11"" Foreground=""{DynamicResource ZeroUI.TextSecondary}"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" />
                                </Button>
                            </StackPanel>
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""ShowMinimizeButton"" Value=""False"">
                            <Setter TargetName=""PART_MinBtn"" Property=""Visibility"" Value=""Collapsed"" />
                        </Trigger>
                        <Trigger Property=""ShowMaximizeButton"" Value=""False"">
                            <Setter TargetName=""PART_MaxBtn"" Property=""Visibility"" Value=""Collapsed"" />
                        </Trigger>
                        <Trigger Property=""ShowCloseButton"" Value=""False"">
                            <Setter TargetName=""PART_CloseBtn"" Property=""Visibility"" Value=""Collapsed"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style TargetType=""{x:Type layout:TitleBar}"" BasedOn=""{StaticResource {x:Type layout:ZTitleBar}}"" />
    <Style TargetType=""{x:Type layout:ZeroTitleBar}"" BasedOn=""{StaticResource {x:Type layout:ZTitleBar}}"" />

    <!-- 28. ZWINDOW STYLE -->
    <Style x:Key=""{x:Type layout:ZWindow}"" TargetType=""{x:Type layout:ZWindow}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgPrimary}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type layout:ZWindow}"">
                    <Border Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            SnapsToDevicePixels=""True"">
                        <AdornerDecorator>
                            <ContentPresenter />
                        </AdornerDecorator>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style TargetType=""{x:Type layout:ChromeWindow}"" BasedOn=""{StaticResource {x:Type layout:ZWindow}}"" />
    <Style TargetType=""{x:Type layout:ZeroWindow}"" BasedOn=""{StaticResource {x:Type layout:ZWindow}}"" />

    <!-- 29. ZINFOBAR STYLE -->
    <Style x:Key=""{x:Type feed:ZInfoBar}"" TargetType=""{x:Type feed:ZInfoBar}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgCard}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Padding"" Value=""12,10,12,10"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type feed:ZInfoBar}"">
                    <Border x:Name=""infoBorder""
                            Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""6""
                            Padding=""{TemplateBinding Padding}""
                            SnapsToDevicePixels=""True"">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""Auto"" />
                                <ColumnDefinition Width=""*"" />
                                <ColumnDefinition Width=""Auto"" />
                                <ColumnDefinition Width=""Auto"" />
                            </Grid.ColumnDefinitions>
                            <!-- Severity Icon Badge -->
                            <Border x:Name=""iconBadge""
                                    Grid.Column=""0""
                                    Width=""24"" Height=""24""
                                    CornerRadius=""12""
                                    Background=""{DynamicResource ZeroUI.InfoAccent}""
                                    Margin=""0,0,12,0""
                                    VerticalAlignment=""Center"">
                                <TextBlock x:Name=""iconGlyph""
                                           Text=""ℹ""
                                           FontSize=""12""
                                           FontWeight=""Bold""
                                           Foreground=""{DynamicResource ZeroUI.SelectionForeground}""
                                           HorizontalAlignment=""Center""
                                           VerticalAlignment=""Center"" />
                            </Border>
                            <!-- Text Content -->
                            <StackPanel Grid.Column=""1"" VerticalAlignment=""Center"">
                                <TextBlock Text=""{TemplateBinding Title}""
                                           FontWeight=""SemiBold""
                                           FontSize=""13""
                                           Foreground=""{TemplateBinding Foreground}"" />
                                <TextBlock Text=""{TemplateBinding Message}""
                                           FontSize=""12""
                                           Foreground=""{DynamicResource ZeroUI.TextSecondary}""
                                           TextWrapping=""Wrap""
                                           Margin=""0,2,0,0"" />
                            </StackPanel>
                            <!-- Action Content -->
                            <ContentPresenter Grid.Column=""2""
                                              Content=""{TemplateBinding ActionContent}""
                                              VerticalAlignment=""Center""
                                              Margin=""12,0,12,0"" />
                            <!-- Close Button -->
                            <Button x:Name=""PART_CloseButton""
                                    Grid.Column=""3""
                                    Width=""24"" Height=""24""
                                    Background=""Transparent""
                                    BorderThickness=""0""
                                    Cursor=""Hand""
                                    VerticalAlignment=""Center"">
                                <TextBlock Text=""✕"" FontSize=""11"" Foreground=""{DynamicResource ZeroUI.TextSecondary}"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" />
                            </Button>
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsClosable"" Value=""False"">
                            <Setter TargetName=""PART_CloseButton"" Property=""Visibility"" Value=""Collapsed"" />
                        </Trigger>
                        <Trigger Property=""Severity"" Value=""Success"">
                            <Setter TargetName=""iconBadge"" Property=""Background"" Value=""{DynamicResource ZeroUI.SuccessAccent}"" />
                            <Setter TargetName=""iconGlyph"" Property=""Text"" Value=""✔"" />
                        </Trigger>
                        <Trigger Property=""Severity"" Value=""Warning"">
                            <Setter TargetName=""iconBadge"" Property=""Background"" Value=""{DynamicResource ZeroUI.WarningAccent}"" />
                            <Setter TargetName=""iconGlyph"" Property=""Text"" Value=""⚠"" />
                        </Trigger>
                        <Trigger Property=""Severity"" Value=""Error"">
                            <Setter TargetName=""iconBadge"" Property=""Background"" Value=""{DynamicResource ZeroUI.DangerAccent}"" />
                            <Setter TargetName=""iconGlyph"" Property=""Text"" Value=""✖"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style TargetType=""{x:Type feed:InfoBar}"" BasedOn=""{StaticResource {x:Type feed:ZInfoBar}}"" />
    <Style TargetType=""{x:Type feed:ZeroInfoBar}"" BasedOn=""{StaticResource {x:Type feed:ZInfoBar}}"" />

    <!-- 30. ZBADGE STYLE -->
    <Style x:Key=""{x:Type feed:ZBadge}"" TargetType=""{x:Type feed:ZBadge}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""HorizontalContentAlignment"" Value=""Stretch"" />
        <Setter Property=""VerticalContentAlignment"" Value=""Stretch"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type feed:ZBadge}"">
                    <Grid>
                        <!-- Wrapped Content -->
                        <ContentPresenter HorizontalAlignment=""{TemplateBinding HorizontalContentAlignment}""
                                          VerticalAlignment=""{TemplateBinding VerticalContentAlignment}""
                                          SnapsToDevicePixels=""{TemplateBinding SnapsToDevicePixels}"" />
                        <!-- Badge Adorner or Pill -->
                        <Border x:Name=""badgeBorder""
                                Background=""{TemplateBinding BadgeBrush}""
                                HorizontalAlignment=""Right""
                                VerticalAlignment=""Top""
                                Margin=""0,-6,-6,0""
                                Padding=""5,1,5,1""
                                MinWidth=""16"" Height=""16""
                                CornerRadius=""8""
                                SnapsToDevicePixels=""True"">
                            <TextBlock Text=""{TemplateBinding DisplayText}""
                                       FontSize=""10""
                                       FontWeight=""Bold""
                                       Foreground=""{DynamicResource ZeroUI.SelectionForeground}""
                                       HorizontalAlignment=""Center""
                                       VerticalAlignment=""Center"" />
                        </Border>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsDot"" Value=""True"">
                            <Setter TargetName=""badgeBorder"" Property=""MinWidth"" Value=""8"" />
                            <Setter TargetName=""badgeBorder"" Property=""Width"" Value=""8"" />
                            <Setter TargetName=""badgeBorder"" Property=""Height"" Value=""8"" />
                            <Setter TargetName=""badgeBorder"" Property=""CornerRadius"" Value=""4"" />
                            <Setter TargetName=""badgeBorder"" Property=""Padding"" Value=""0"" />
                            <Setter TargetName=""badgeBorder"" Property=""Margin"" Value=""0,-3,-3,0"" />
                        </Trigger>
                        <MultiTrigger>
                            <MultiTrigger.Conditions>
                                <Condition Property=""Content"" Value=""{x:Null}"" />
                                <Condition Property=""IsDot"" Value=""False"" />
                            </MultiTrigger.Conditions>
                            <Setter TargetName=""badgeBorder"" Property=""HorizontalAlignment"" Value=""Left"" />
                            <Setter TargetName=""badgeBorder"" Property=""VerticalAlignment"" Value=""Center"" />
                            <Setter TargetName=""badgeBorder"" Property=""Margin"" Value=""0"" />
                            <Setter TargetName=""badgeBorder"" Property=""Padding"" Value=""8,3,8,3"" />
                            <Setter TargetName=""badgeBorder"" Property=""Height"" Value=""22"" />
                            <Setter TargetName=""badgeBorder"" Property=""CornerRadius"" Value=""11"" />
                        </MultiTrigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style TargetType=""{x:Type feed:Badge}"" BasedOn=""{StaticResource {x:Type feed:ZBadge}}"" />
    <Style TargetType=""{x:Type feed:ZeroBadge}"" BasedOn=""{StaticResource {x:Type feed:ZBadge}}"" />

    <!-- 31. ZFLYOUT STYLE -->
    <Style x:Key=""{x:Type ovl:ZFlyout}"" TargetType=""{x:Type ovl:ZFlyout}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgCard}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type ovl:ZFlyout}"">
                    <Popup x:Name=""PART_Popup""
                           AllowsTransparency=""True""
                           IsOpen=""{Binding IsOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}""
                           StaysOpen=""{Binding StaysOpen, RelativeSource={RelativeSource TemplatedParent}}""
                           PlacementTarget=""{Binding Target, RelativeSource={RelativeSource TemplatedParent}}""
                           Placement=""{Binding Placement, RelativeSource={RelativeSource TemplatedParent}}""
                           PopupAnimation=""Fade"">
                        <Border Margin=""8""
                                Background=""{TemplateBinding Background}""
                                BorderBrush=""{TemplateBinding BorderBrush}""
                                BorderThickness=""{TemplateBinding BorderThickness}""
                                CornerRadius=""8""
                                Padding=""14""
                                MinWidth=""240""
                                SnapsToDevicePixels=""True"">
                            <Border.Effect>
                                <DropShadowEffect BlurRadius=""16"" Direction=""270"" ShadowDepth=""4"" Opacity=""0.35"" Color=""#000000"" />
                            </Border.Effect>
                            <StackPanel>
                                <DockPanel LastChildFill=""True"" Margin=""0,0,0,10"">
                                    <Button x:Name=""PART_CloseButton""
                                            DockPanel.Dock=""Right""
                                            Width=""20"" Height=""20""
                                            Background=""Transparent""
                                            BorderThickness=""0""
                                            Cursor=""Hand"">
                                        <TextBlock Text=""✕"" FontSize=""10"" Foreground=""{DynamicResource ZeroUI.TextSecondary}"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" />
                                    </Button>
                                    <TextBlock Text=""{TemplateBinding Title}""
                                               FontWeight=""SemiBold""
                                               FontSize=""13""
                                               Foreground=""{TemplateBinding Foreground}""
                                               VerticalAlignment=""Center"" />
                                </DockPanel>
                                <ContentPresenter Content=""{TemplateBinding FlyoutContent}"" Margin=""0,0,0,10"" />
                                <ContentPresenter Content=""{TemplateBinding Footer}"" />
                            </StackPanel>
                        </Border>
                    </Popup>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""ShowCloseButton"" Value=""False"">
                            <Setter TargetName=""PART_CloseButton"" Property=""Visibility"" Value=""Collapsed"" />
                        </Trigger>
                        <Trigger Property=""Title"" Value="""">
                            <Setter TargetName=""PART_CloseButton"" Property=""DockPanel.Dock"" Value=""Right"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
    <Style TargetType=""{x:Type ovl:FlyoutControl}"" BasedOn=""{StaticResource {x:Type ovl:ZFlyout}}"" />
    <Style TargetType=""{x:Type ovl:ZeroFlyout}"" BasedOn=""{StaticResource {x:Type ovl:ZFlyout}}"" />

    <!-- 32. EDITORBUTTON STYLE -->
    <Style TargetType=""{x:Type edit:EditorButton}"">
        <Setter Property=""Background"" Value=""Transparent"" />
        <Setter Property=""BorderThickness"" Value=""0"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextSecondary}"" />
        <Setter Property=""Width"" Value=""26"" />
        <Setter Property=""Height"" Value=""26"" />
        <Setter Property=""Margin"" Value=""2,0,2,0"" />
        <Setter Property=""Padding"" Value=""4"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type edit:EditorButton}"">
                    <Border x:Name=""bd"" Background=""{TemplateBinding Background}"" CornerRadius=""4"">
                        <Grid>
                            <Path x:Name=""glyphPath""
                                  Data=""{Binding GlyphData, RelativeSource={RelativeSource TemplatedParent}}""
                                  Fill=""{TemplateBinding Foreground}""
                                  Stretch=""Uniform""
                                  Width=""13"" Height=""13""
                                  HorizontalAlignment=""Center"" VerticalAlignment=""Center"" />
                            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" />
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""bd"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgHover}"" />
                            <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                        </Trigger>
                        <Trigger Property=""IsPressed"" Value=""True"">
                            <Setter TargetName=""bd"" Property=""Background"" Value=""{DynamicResource ZeroUI.BgCard}"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter Property=""Opacity"" Value=""0.4"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 33. BUTTONEDIT STYLE -->
    <Style TargetType=""{x:Type edit:ButtonEdit}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgInput}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Padding"" Value=""10,0,10,0"" />
        <Setter Property=""VerticalContentAlignment"" Value=""Center"" />
        <Setter Property=""FontSize"" Value=""12.5"" />
        <Setter Property=""CaretBrush"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
        <Setter Property=""SelectionBrush"" Value=""{DynamicResource ZeroUI.SelectionBackground}"" />
        <Setter Property=""SelectionOpacity"" Value=""0.6"" />
        <Setter Property=""Height"" Value=""32"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type edit:ButtonEdit}"">
                    <Border x:Name=""txtBorder""
                            Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""{TemplateBinding CornerRadius}""
                            SnapsToDevicePixels=""True"">
                        <Grid Margin=""{TemplateBinding Padding}"">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""Auto"" />
                                <ColumnDefinition Width=""Auto"" />
                                <ColumnDefinition Width=""*"" />
                                <ColumnDefinition Width=""Auto"" />
                            </Grid.ColumnDefinitions>

                            <!-- Left embedded buttons -->
                            <ItemsControl Grid.Column=""0"" ItemsSource=""{Binding LeftButtons, RelativeSource={RelativeSource TemplatedParent}}"" VerticalAlignment=""Center"">
                                <ItemsControl.ItemsPanel>
                                    <ItemsPanelTemplate>
                                        <StackPanel Orientation=""Horizontal"" />
                                    </ItemsPanelTemplate>
                                </ItemsControl.ItemsPanel>
                            </ItemsControl>

                            <!-- Leading Text -->
                            <TextBlock x:Name=""PART_Leading""
                                       Grid.Column=""1""
                                       Text=""{TemplateBinding LeadingText}""
                                       Foreground=""{DynamicResource ZeroUI.TextSecondary}""
                                       VerticalAlignment=""Center""
                                       Margin=""0,0,6,0""
                                       Visibility=""Collapsed"" />

                            <!-- Main Input & Placeholder -->
                            <Grid Grid.Column=""2"">
                                <TextBlock x:Name=""PART_Placeholder""
                                           Text=""{TemplateBinding Placeholder}""
                                           Foreground=""{DynamicResource ZeroUI.TextPlaceholder}""
                                           VerticalAlignment=""Center""
                                           IsHitTestVisible=""False""
                                           Visibility=""Collapsed"" />

                                <ScrollViewer x:Name=""PART_ContentHost""
                                              VerticalAlignment=""Center""
                                              VerticalContentAlignment=""Center""
                                              Focusable=""False""
                                              HorizontalScrollBarVisibility=""Hidden""
                                              VerticalScrollBarVisibility=""Hidden"" />
                            </Grid>

                            <!-- Right embedded buttons -->
                            <ItemsControl Grid.Column=""3"" ItemsSource=""{Binding RightButtons, RelativeSource={RelativeSource TemplatedParent}}"" VerticalAlignment=""Center"">
                                <ItemsControl.ItemsPanel>
                                    <ItemsPanelTemplate>
                                        <StackPanel Orientation=""Horizontal"" />
                                    </ItemsPanelTemplate>
                                </ItemsControl.ItemsPanel>
                            </ItemsControl>
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""Text"" Value="""">
                            <Setter TargetName=""PART_Placeholder"" Property=""Visibility"" Value=""Visible"" />
                        </Trigger>
                        <Trigger Property=""IsKeyboardFocused"" Value=""True"">
                            <Setter TargetName=""txtBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.FocusBorder}"" />
                        </Trigger>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""txtBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderHover}"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter TargetName=""txtBorder"" Property=""Opacity"" Value=""0.5"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 34. PICTUREEDIT STYLE -->
    <Style TargetType=""{x:Type edit:PictureEdit}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""Width"" Value=""64"" />
        <Setter Property=""Height"" Value=""64"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgCard}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""CornerRadius"" Value=""8"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type edit:PictureEdit}"">
                    <Grid>
                        <Border x:Name=""mainBorder""
                                Background=""{TemplateBinding Background}""
                                BorderBrush=""{TemplateBinding BorderBrush}""
                                BorderThickness=""{TemplateBinding BorderThickness}""
                                CornerRadius=""{TemplateBinding CornerRadius}""
                                ClipToBounds=""True"">
                            <Grid>
                                <!-- Fallback Initials -->
                                <Border x:Name=""fallbackBorder""
                                        Background=""{TemplateBinding FallbackBackground}""
                                        Visibility=""Collapsed"">
                                    <TextBlock Text=""{TemplateBinding FallbackText}""
                                               Foreground=""{DynamicResource ZeroUI.TextPrimary}""
                                               FontWeight=""Bold""
                                               FontSize=""16""
                                               HorizontalAlignment=""Center""
                                               VerticalAlignment=""Center"" />
                                </Border>

                                <!-- Actual Image -->
                                <Image x:Name=""partImage""
                                       Source=""{TemplateBinding ImageSource}""
                                       Stretch=""UniformToFill"" />
                            </Grid>
                        </Border>

                        <!-- Status Indicator Dot -->
                        <Border x:Name=""statusDot""
                                Width=""12"" Height=""12""
                                CornerRadius=""6""
                                BorderBrush=""{DynamicResource ZeroUI.BgDarker}""
                                BorderThickness=""2""
                                HorizontalAlignment=""Right""
                                VerticalAlignment=""Bottom""
                                Margin=""0,0,1,1""
                                Visibility=""Collapsed"" />
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""ImageSource"" Value=""{x:Null}"">
                            <Setter TargetName=""partImage"" Property=""Visibility"" Value=""Collapsed"" />
                            <Setter TargetName=""fallbackBorder"" Property=""Visibility"" Value=""Visible"" />
                        </Trigger>
                        <Trigger Property=""IsCircle"" Value=""True"">
                            <Setter TargetName=""mainBorder"" Property=""CornerRadius"" Value=""999"" />
                        </Trigger>
                        <Trigger Property=""Status"" Value=""Online"">
                            <Setter TargetName=""statusDot"" Property=""Visibility"" Value=""Visible"" />
                            <Setter TargetName=""statusDot"" Property=""Background"" Value=""#10B981"" />
                        </Trigger>
                        <Trigger Property=""Status"" Value=""Busy"">
                            <Setter TargetName=""statusDot"" Property=""Visibility"" Value=""Visible"" />
                            <Setter TargetName=""statusDot"" Property=""Background"" Value=""#EF4444"" />
                        </Trigger>
                        <Trigger Property=""Status"" Value=""Away"">
                            <Setter TargetName=""statusDot"" Property=""Visibility"" Value=""Visible"" />
                            <Setter TargetName=""statusDot"" Property=""Background"" Value=""#F59E0B"" />
                        </Trigger>
                        <Trigger Property=""Status"" Value=""Offline"">
                            <Setter TargetName=""statusDot"" Property=""Visibility"" Value=""Visible"" />
                            <Setter TargetName=""statusDot"" Property=""Background"" Value=""#64748B"" />
                        </Trigger>
                        <Trigger Property=""ScaleMode"" Value=""Contain"">
                            <Setter TargetName=""partImage"" Property=""Stretch"" Value=""Uniform"" />
                        </Trigger>
                        <Trigger Property=""ScaleMode"" Value=""Center"">
                            <Setter TargetName=""partImage"" Property=""Stretch"" Value=""None"" />
                        </Trigger>
                        <Trigger Property=""ScaleMode"" Value=""Stretch"">
                            <Setter TargetName=""partImage"" Property=""Stretch"" Value=""Fill"" />
                        </Trigger>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                            <Setter TargetName=""mainBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 35. IPADDRESSEDIT STYLE -->
    <Style TargetType=""{x:Type edit:IPAddressEdit}"">
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgInput}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""CornerRadius"" Value=""5"" />
        <Setter Property=""Height"" Value=""32"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""FontSize"" Value=""12.5"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type edit:IPAddressEdit}"">
                    <Border x:Name=""ipBorder""
                            Background=""{TemplateBinding Background}""
                            BorderBrush=""{TemplateBinding BorderBrush}""
                            BorderThickness=""{TemplateBinding BorderThickness}""
                            CornerRadius=""{TemplateBinding CornerRadius}"">
                        <Grid VerticalAlignment=""Center"" HorizontalAlignment=""Center"">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=""Auto"" />
                                <ColumnDefinition Width=""Auto"" />
                                <ColumnDefinition Width=""Auto"" />
                                <ColumnDefinition Width=""Auto"" />
                                <ColumnDefinition Width=""Auto"" />
                                <ColumnDefinition Width=""Auto"" />
                                <ColumnDefinition Width=""Auto"" />
                            </Grid.ColumnDefinitions>

                            <TextBox x:Name=""PART_Octet1"" Grid.Column=""0""
                                     Width=""34"" Height=""24""
                                     MaxLength=""3""
                                     TextAlignment=""Center""
                                     Background=""Transparent""
                                     BorderThickness=""0""
                                     Foreground=""{TemplateBinding Foreground}""
                                     FontSize=""{TemplateBinding FontSize}""
                                     VerticalContentAlignment=""Center""
                                     CaretBrush=""{DynamicResource ZeroUI.PrimaryAccent}"" />

                            <TextBlock Grid.Column=""1"" Text="".""
                                       Foreground=""{DynamicResource ZeroUI.TextSecondary}""
                                       FontWeight=""Bold""
                                       VerticalAlignment=""Center""
                                       Margin=""2,0,2,0"" />

                            <TextBox x:Name=""PART_Octet2"" Grid.Column=""2""
                                     Width=""34"" Height=""24""
                                     MaxLength=""3""
                                     TextAlignment=""Center""
                                     Background=""Transparent""
                                     BorderThickness=""0""
                                     Foreground=""{TemplateBinding Foreground}""
                                     FontSize=""{TemplateBinding FontSize}""
                                     VerticalContentAlignment=""Center""
                                     CaretBrush=""{DynamicResource ZeroUI.PrimaryAccent}"" />

                            <TextBlock Grid.Column=""3"" Text="".""
                                       Foreground=""{DynamicResource ZeroUI.TextSecondary}""
                                       FontWeight=""Bold""
                                       VerticalAlignment=""Center""
                                       Margin=""2,0,2,0"" />

                            <TextBox x:Name=""PART_Octet3"" Grid.Column=""4""
                                     Width=""34"" Height=""24""
                                     MaxLength=""3""
                                     TextAlignment=""Center""
                                     Background=""Transparent""
                                     BorderThickness=""0""
                                     Foreground=""{TemplateBinding Foreground}""
                                     FontSize=""{TemplateBinding FontSize}""
                                     VerticalContentAlignment=""Center""
                                     CaretBrush=""{DynamicResource ZeroUI.PrimaryAccent}"" />

                            <TextBlock Grid.Column=""5"" Text="".""
                                       Foreground=""{DynamicResource ZeroUI.TextSecondary}""
                                       FontWeight=""Bold""
                                       VerticalAlignment=""Center""
                                       Margin=""2,0,2,0"" />

                            <TextBox x:Name=""PART_Octet4"" Grid.Column=""6""
                                     Width=""34"" Height=""24""
                                     MaxLength=""3""
                                     TextAlignment=""Center""
                                     Background=""Transparent""
                                     BorderThickness=""0""
                                     Foreground=""{TemplateBinding Foreground}""
                                     FontSize=""{TemplateBinding FontSize}""
                                     VerticalContentAlignment=""Center""
                                     CaretBrush=""{DynamicResource ZeroUI.PrimaryAccent}"" />
                        </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsKeyboardFocusWithin"" Value=""True"">
                            <Setter TargetName=""ipBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.FocusBorder}"" />
                        </Trigger>
                        <Trigger Property=""IsEnabled"" Value=""False"">
                            <Setter TargetName=""ipBorder"" Property=""Opacity"" Value=""0.6"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 36. RANGESLIDER STYLE -->
    <Style TargetType=""{x:Type edit:RangeSlider}"">
        <Setter Property=""Height"" Value=""32"" />
        <Setter Property=""Focusable"" Value=""True"" />
        <Setter Property=""ActiveRangeBrush"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type edit:RangeSlider}"">
                    <Grid VerticalAlignment=""Center"">
                        <Grid.RowDefinitions>
                            <RowDefinition Height=""Auto"" />
                            <RowDefinition Height=""20"" />
                        </Grid.RowDefinitions>

                        <!-- Range Info Text -->
                        <TextBlock Grid.Row=""0""
                                   HorizontalAlignment=""Right""
                                   FontSize=""11""
                                   Foreground=""{DynamicResource ZeroUI.TextSecondary}""
                                   Margin=""0,0,0,4""
                                   Text=""{Binding FormattedRangeText, RelativeSource={RelativeSource TemplatedParent}}"" />

                        <!-- Slider Track & Thumbs Area -->
                        <Canvas x:Name=""PART_Track"" Grid.Row=""1"" Height=""20"" Background=""Transparent"">
                            <!-- Background Groove -->
                            <Border Canvas.Top=""8"" Height=""4"" Width=""{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=Canvas}}""
                                    Background=""{DynamicResource ZeroUI.BorderDefault}"" CornerRadius=""2"" />

                            <!-- Active Span Bar -->
                            <Border x:Name=""PART_ActiveRange"" Canvas.Top=""7"" Height=""6""
                                    Background=""{TemplateBinding ActiveRangeBrush}"" CornerRadius=""3""
                                    Cursor=""SizeWE"" />

                            <!-- Lower Thumb -->
                            <Thumb x:Name=""PART_LowerThumb"" Canvas.Top=""2"" Width=""16"" Height=""16"" Cursor=""Hand"">
                                <Thumb.Template>
                                    <ControlTemplate TargetType=""{x:Type Thumb}"">
                                        <Ellipse Fill=""{DynamicResource ZeroUI.BgCard}""
                                                 Stroke=""{DynamicResource ZeroUI.PrimaryAccent}""
                                                 StrokeThickness=""2.5"" />
                                    </ControlTemplate>
                                </Thumb.Template>
                            </Thumb>

                            <!-- Upper Thumb -->
                            <Thumb x:Name=""PART_UpperThumb"" Canvas.Top=""2"" Width=""16"" Height=""16"" Cursor=""Hand"">
                                <Thumb.Template>
                                    <ControlTemplate TargetType=""{x:Type Thumb}"">
                                        <Ellipse Fill=""{DynamicResource ZeroUI.BgCard}""
                                                 Stroke=""{DynamicResource ZeroUI.PrimaryAccent}""
                                                 StrokeThickness=""2.5"" />
                                    </ControlTemplate>
                                </Thumb.Template>
                            </Thumb>
                        </Canvas>
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 37. TOKENEDIT STYLE -->
    <Style TargetType=""{x:Type edit:TokenEdit}"">
        <Setter Property=""MinHeight"" Value=""32"" />
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgInput}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""BorderThickness"" Value=""1"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""FontSize"" Value=""12"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type edit:TokenEdit}"">
                    <Grid>
                        <Border x:Name=""tokenBorder""
                                Background=""{TemplateBinding Background}""
                                BorderBrush=""{TemplateBinding BorderBrush}""
                                BorderThickness=""{TemplateBinding BorderThickness}""
                                CornerRadius=""{TemplateBinding CornerRadius}""
                                Padding=""4,3,4,3""
                                SnapsToDevicePixels=""True"">
                            <WrapPanel Orientation=""Horizontal"">
                                <!-- Tokens List -->
                                <ItemsControl ItemsSource=""{Binding Tokens, RelativeSource={RelativeSource TemplatedParent}}"">
                                    <ItemsControl.ItemsPanel>
                                        <ItemsPanelTemplate>
                                            <WrapPanel Orientation=""Horizontal"" />
                                        </ItemsPanelTemplate>
                                    </ItemsControl.ItemsPanel>
                                    <ItemsControl.ItemTemplate>
                                        <DataTemplate>
                                            <Border Background=""{DynamicResource ZeroUI.BgCard}""
                                                    BorderBrush=""{DynamicResource ZeroUI.BorderDefault}""
                                                    BorderThickness=""1""
                                                    CornerRadius=""12""
                                                    Padding=""8,2,6,2""
                                                    Margin=""0,0,6,3"">
                                                <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"">
                                                    <TextBlock Text=""{Binding Text}""
                                                               Foreground=""{DynamicResource ZeroUI.TextPrimary}""
                                                               FontSize=""11.5""
                                                               VerticalAlignment=""Center"" />
                                                    <Button Content=""×""
                                                            CommandParameter=""{Binding}""
                                                            Margin=""5,-1,0,0""
                                                            Padding=""0""
                                                            Width=""14"" Height=""14""
                                                            FontSize=""12""
                                                            FontWeight=""Bold""
                                                            Foreground=""{DynamicResource ZeroUI.TextSecondary}""
                                                            Background=""Transparent""
                                                            BorderThickness=""0""
                                                            Cursor=""Hand"" />
                                                </StackPanel>
                                            </Border>
                                        </DataTemplate>
                                    </ItemsControl.ItemTemplate>
                                </ItemsControl>

                                <!-- Inline Input TextBox -->
                                <TextBox x:Name=""PART_Input""
                                         MinWidth=""80""
                                         Height=""24""
                                         Background=""Transparent""
                                         BorderThickness=""0""
                                         Foreground=""{TemplateBinding Foreground}""
                                         FontSize=""{TemplateBinding FontSize}""
                                         VerticalContentAlignment=""Center""
                                         CaretBrush=""{DynamicResource ZeroUI.PrimaryAccent}""
                                         Margin=""2,0,0,0"" />
                            </WrapPanel>
                        </Border>

                        <!-- Suggestions Popup -->
                        <Popup x:Name=""PART_Popup""
                               StaysOpen=""False""
                               AllowsTransparency=""True""
                               PopupAnimation=""Fade""
                               Placement=""Bottom"">
                            <Border Background=""{DynamicResource ZeroUI.BgCard}""
                                    BorderBrush=""{DynamicResource ZeroUI.BorderDefault}""
                                    BorderThickness=""1""
                                    CornerRadius=""6""
                                    Padding=""4""
                                    MinWidth=""160""
                                    MaxHeight=""180"">
                                <ListBox x:Name=""PART_SuggestionsList""
                                         Background=""Transparent""
                                         BorderThickness=""0"" />
                            </Border>
                        </Popup>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <Trigger Property=""IsKeyboardFocusWithin"" Value=""True"">
                            <Setter TargetName=""tokenBorder"" Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.FocusBorder}"" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 43. TIMESPANEDIT STYLE -->
    <Style TargetType=""{x:Type editors:TimeSpanEdit}"">
        <Setter Property=""Background"" Value=""{DynamicResource ZeroUI.BgInput}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource ZeroUI.BorderDefault}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.TextPrimary}"" />
        <Setter Property=""FontSize"" Value=""13"" />
        <Setter Property=""Height"" Value=""32"" />
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
    </Style>

    <!-- 44. HYPERLINKEDIT STYLE -->
    <Style TargetType=""{x:Type editors:HyperlinkEdit}"">
        <Setter Property=""Background"" Value=""Transparent"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource ZeroUI.PrimaryAccent}"" />
        <Setter Property=""FontSize"" Value=""13"" />
        <Setter Property=""Height"" Value=""32"" />
        <Setter Property=""SnapsToDevicePixels"" Value=""True"" />
    </Style>

</ResourceDictionary>";

            return (ResourceDictionary)XamlReader.Parse(xaml);
        }
    }
}
