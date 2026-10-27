using System;
using System.ComponentModel;
using System.Windows;
using ZeroUI.Wpf.Charts;
using ZeroUI.Wpf.DataGrid;
using ZeroUI.Wpf.Editors;
using ZeroUI.Wpf.Industrial;
using ZeroUI.Wpf.Layout;
using ZeroUI.Wpf.Media;

namespace ZeroUI.Wpf.Design.Metadata
{
    /// <summary>
    /// Enterprise WPF Designer Metadata Registration Provider.
    /// Provides rich property category grouping, tooltips, and default values to Visual Studio XAML Designer.
    /// </summary>
    public static class ZeroWpfMetadataProvider
    {
        private static bool _isRegistered;
        private static readonly object _syncLock = new object();

        /// <summary>
        /// Registers design-time metadata attributes for all ZeroUI WPF components.
        /// </summary>
        public static void Register() => RegisterMetadata();

        /// <summary>
        /// Registers design-time metadata attributes for all ZeroUI WPF components.
        /// </summary>
        public static void RegisterMetadata()
        {
            if (_isRegistered) return;

            lock (_syncLock)
            {
                if (_isRegistered) return;

                // 1. ZGrid Metadata
                RegisterAttribute(typeof(ZGrid), new CategoryAttribute("ZeroUI - DataGrid"));
                RegisterAttribute(typeof(ZGrid), new DescriptionAttribute("Enterprise high-performance virtual WPF DataGrid"));

                // 2. ZChart Metadata
                RegisterAttribute(typeof(ZChart), new CategoryAttribute("ZeroUI - Charts & Analytics"));
                RegisterAttribute(typeof(ZChart), new DescriptionAttribute("High-performance zero-allocation universal WPF Chart control"));

                // 3. ZRadialGauge Metadata
                RegisterAttribute(typeof(ZRadialGauge), new CategoryAttribute("ZeroUI - Industrial & SCADA"));
                RegisterAttribute(typeof(ZRadialGauge), new DescriptionAttribute("High-precision circular industrial dial gauge for SCADA telemetry"));

                // 4. ZSevenSegment Metadata
                RegisterAttribute(typeof(ZSevenSegment), new CategoryAttribute("ZeroUI - Industrial & SCADA"));
                RegisterAttribute(typeof(ZSevenSegment), new DescriptionAttribute("Industrial 7-Segment Digital LED Display for SCADA & MES telemetry"));

                // 5. ZImageViewer Metadata
                RegisterAttribute(typeof(ZImageViewer), new CategoryAttribute("ZeroUI - Media"));
                RegisterAttribute(typeof(ZImageViewer), new DescriptionAttribute("Enterprise high-performance interactive image viewer control"));

                // 6. ZDateEdit Metadata
                RegisterAttribute(typeof(ZDateEdit), new CategoryAttribute("ZeroUI - Editors"));
                RegisterAttribute(typeof(ZDateEdit), new DescriptionAttribute("Modern date picker with custom popup calendar and quick-select presets"));

                // 7. Layout & Containers Metadata
                RegisterAttribute(typeof(ZFlowLayout), new CategoryAttribute("ZeroUI - Layout"));
                RegisterAttribute(typeof(ZCollapsibleToolCard), new CategoryAttribute("ZeroUI - Layout"));

                _isRegistered = true;
            }
        }

        private static void RegisterAttribute(Type type, Attribute attribute)
        {
            TypeDescriptor.AddAttributes(type, attribute);
        }
    }
}
