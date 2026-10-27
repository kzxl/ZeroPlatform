using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using ZeroUI.Core.Localization;

namespace ZeroUI.Wpf.Localization
{
    /// <summary>
    /// Singleton proxy providing real-time data-binding notifications for WPF localization markup extensions.
    /// </summary>
    public sealed class LocalizationNotifier : INotifyPropertyChanged
    {
        public static LocalizationNotifier Instance { get; } = new LocalizationNotifier();

        public event PropertyChangedEventHandler? PropertyChanged;

        private LocalizationNotifier()
        {
            LocalizationManager.CultureChanged += (s, e) =>
            {
                // Invalidate all string indexers to notify WPF binding engine
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            };
        }

        public string this[string key] => LocalizationManager.Get(key);
    }

    /// <summary>
    /// Declarative XAML markup extension for zero-overhead real-time localization.
    /// Usage in XAML:
    ///   Text="{loc:Loc Common.Ok}"
    ///   Content="{loc:Loc Key=Archive.Create, Default='Create Archive'}"
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public class LocExtension : MarkupExtension
    {
        public string? Key { get; set; }
        public string? Default { get; set; }

        public LocExtension() { }

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrWhiteSpace(Key))
            {
                return Default ?? string.Empty;
            }

            // If resolving in Visual Studio / Blend design time or non-dependency object, return static text
            if (serviceProvider != null && serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget target)
            {
                if (target.TargetObject is DependencyObject && target.TargetProperty is DependencyProperty)
                {
                    // Create lightweight OneWay dynamic binding to the global indexer proxy
                    var binding = new Binding($"[{Key}]")
                    {
                        Source = LocalizationNotifier.Instance,
                        Mode = BindingMode.OneWay,
                        FallbackValue = Default ?? Key
                    };
                    return binding.ProvideValue(serviceProvider);
                }
            }

            return LocalizationManager.Get(Key, Default);
        }
    }
}
