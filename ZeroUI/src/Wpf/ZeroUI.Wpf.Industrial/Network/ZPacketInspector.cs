using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    public class PacketRecord { public DateTime Timestamp { get; set; } public string Source { get; set; } = string.Empty; public string Dest { get; set; } = string.Empty; public string Protocol { get; set; } = string.Empty; public int Length { get; set; } public byte[] Data { get; set; } = new byte[0]; }
    /// <summary>
    /// ZPacketInspector WPF control.
    /// </summary>
    public class ZPacketInspector : FrameworkElement
    {        public static readonly DependencyProperty PacketsProperty = DependencyProperty.Register(nameof(Packets), typeof(System.Collections.Generic.List<PacketRecord>), typeof(ZPacketInspector));
        public System.Collections.Generic.List<PacketRecord> Packets { get => (System.Collections.Generic.List<PacketRecord>)GetValue(PacketsProperty); set => SetValue(PacketsProperty, value); }
        public static readonly DependencyProperty MaxPacketsProperty = DependencyProperty.Register(nameof(MaxPackets), typeof(int), typeof(ZPacketInspector));
        public int MaxPackets { get => (int)GetValue(MaxPacketsProperty); set => SetValue(MaxPacketsProperty, value); }
        public static readonly DependencyProperty AutoScrollProperty = DependencyProperty.Register(nameof(AutoScroll), typeof(bool), typeof(ZPacketInspector));
        public bool AutoScroll { get => (bool)GetValue(AutoScrollProperty); set => SetValue(AutoScrollProperty, value); }
        public static readonly DependencyProperty FilterProperty = DependencyProperty.Register(nameof(Filter), typeof(string), typeof(ZPacketInspector));
        public string Filter { get => (string)GetValue(FilterProperty); set => SetValue(FilterProperty, value); }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroPacketInspector is deprecated.")]
    public class ZeroPacketInspector : ZPacketInspector { }

    [Obsolete("PacketInspector is deprecated.")]
    public class PacketInspector : ZPacketInspector { }
}
