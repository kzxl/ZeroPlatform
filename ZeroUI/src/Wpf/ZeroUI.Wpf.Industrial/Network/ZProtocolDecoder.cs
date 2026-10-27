using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    /// <summary>
    /// ZProtocolDecoder WPF control.
    /// </summary>
    public class ZProtocolDecoder : FrameworkElement
    {        public static readonly DependencyProperty RawBytesProperty = DependencyProperty.Register(nameof(RawBytes), typeof(byte[]), typeof(ZProtocolDecoder));
        public byte[] RawBytes { get => (byte[])GetValue(RawBytesProperty); set => SetValue(RawBytesProperty, value); }
        public static readonly DependencyProperty ProtocolProperty = DependencyProperty.Register(nameof(Protocol), typeof(string), typeof(ZProtocolDecoder));
        public string Protocol { get => (string)GetValue(ProtocolProperty); set => SetValue(ProtocolProperty, value); }
        public static readonly DependencyProperty DecodedFieldsProperty = DependencyProperty.Register(nameof(DecodedFields), typeof(System.Collections.Generic.Dictionary<string, string>), typeof(ZProtocolDecoder));
        public System.Collections.Generic.Dictionary<string, string> DecodedFields { get => (System.Collections.Generic.Dictionary<string, string>)GetValue(DecodedFieldsProperty); set => SetValue(DecodedFieldsProperty, value); }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroProtocolDecoder is deprecated.")]
    public class ZeroProtocolDecoder : ZProtocolDecoder { }

    [Obsolete("ProtocolDecoder is deprecated.")]
    public class ProtocolDecoder : ZProtocolDecoder { }
}
