using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    /// <summary>
    /// ZProtectionRelay WPF control.
    /// </summary>
    public class ZProtectionRelay : FrameworkElement
    {        public static readonly DependencyProperty RelayStateProperty = DependencyProperty.Register(nameof(RelayState), typeof(string), typeof(ZProtectionRelay));
        public string RelayState { get => (string)GetValue(RelayStateProperty); set => SetValue(RelayStateProperty, value); }
        public static readonly DependencyProperty TripCodeProperty = DependencyProperty.Register(nameof(TripCode), typeof(string), typeof(ZProtectionRelay));
        public string TripCode { get => (string)GetValue(TripCodeProperty); set => SetValue(TripCodeProperty, value); }
        public static readonly DependencyProperty ResetEnabledProperty = DependencyProperty.Register(nameof(ResetEnabled), typeof(bool), typeof(ZProtectionRelay));
        public bool ResetEnabled { get => (bool)GetValue(ResetEnabledProperty); set => SetValue(ResetEnabledProperty, value); }
        public static readonly RoutedEvent ResetClickedEvent = EventManager.RegisterRoutedEvent(nameof(ResetClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ZProtectionRelay));
        public event RoutedEventHandler ResetClicked { add { AddHandler(ResetClickedEvent, value); } remove { RemoveHandler(ResetClickedEvent, value); } }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroProtectionRelay is deprecated.")]
    public class ZeroProtectionRelay : ZProtectionRelay { }

    [Obsolete("ProtectionRelay is deprecated.")]
    public class ProtectionRelay : ZProtectionRelay { }
}
