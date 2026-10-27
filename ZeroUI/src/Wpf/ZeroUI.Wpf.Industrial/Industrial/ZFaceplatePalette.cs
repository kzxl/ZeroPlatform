using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    public class FaceplateTemplate { public string Name { get; set; } = string.Empty; public string Glyph { get; set; } = string.Empty; public string Type { get; set; } = string.Empty; }
    /// <summary>
    /// ZFaceplatePalette WPF control.
    /// </summary>
    public class ZFaceplatePalette : FrameworkElement
    {        public static readonly DependencyProperty TemplatesProperty = DependencyProperty.Register(nameof(Templates), typeof(System.Collections.Generic.List<FaceplateTemplate>), typeof(ZFaceplatePalette));
        public System.Collections.Generic.List<FaceplateTemplate> Templates { get => (System.Collections.Generic.List<FaceplateTemplate>)GetValue(TemplatesProperty); set => SetValue(TemplatesProperty, value); }
        public static readonly RoutedEvent TemplateSelectedEvent = EventManager.RegisterRoutedEvent(nameof(TemplateSelected), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ZFaceplatePalette));
        public event RoutedEventHandler TemplateSelected { add { AddHandler(TemplateSelectedEvent, value); } remove { RemoveHandler(TemplateSelectedEvent, value); } }
        public static readonly RoutedEvent TemplateDragStartedEvent = EventManager.RegisterRoutedEvent(nameof(TemplateDragStarted), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ZFaceplatePalette));
        public event RoutedEventHandler TemplateDragStarted { add { AddHandler(TemplateDragStartedEvent, value); } remove { RemoveHandler(TemplateDragStartedEvent, value); } }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroFaceplatePalette is deprecated.")]
    public class ZeroFaceplatePalette : ZFaceplatePalette { }

    [Obsolete("FaceplatePalette is deprecated.")]
    public class FaceplatePalette : ZFaceplatePalette { }
}
