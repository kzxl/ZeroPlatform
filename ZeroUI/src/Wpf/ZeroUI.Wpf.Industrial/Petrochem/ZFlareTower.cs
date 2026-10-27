using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    /// <summary>
    /// ZFlareTower WPF control.
    /// </summary>
    public class ZFlareTower : FrameworkElement
    {        public static readonly DependencyProperty FlareIntensityProperty = DependencyProperty.Register(nameof(FlareIntensity), typeof(double), typeof(ZFlareTower));
        public double FlareIntensity { get => (double)GetValue(FlareIntensityProperty); set => SetValue(FlareIntensityProperty, value); }
        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(ZFlareTower));
        public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
        public static readonly DependencyProperty FlameColorProperty = DependencyProperty.Register(nameof(FlameColor), typeof(System.Windows.Media.Color), typeof(ZFlareTower));
        public System.Windows.Media.Color FlameColor { get => (System.Windows.Media.Color)GetValue(FlameColorProperty); set => SetValue(FlameColorProperty, value); }
        public static readonly DependencyProperty ShowSmokeProperty = DependencyProperty.Register(nameof(ShowSmoke), typeof(bool), typeof(ZFlareTower));
        public bool ShowSmoke { get => (bool)GetValue(ShowSmokeProperty); set => SetValue(ShowSmokeProperty, value); }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroFlareTower is deprecated.")]
    public class ZeroFlareTower : ZFlareTower { }

    [Obsolete("FlareTower is deprecated.")]
    public class FlareTower : ZFlareTower { }
}
