using System.Runtime.InteropServices;

namespace ZeroCharts.Rendering
{
    /// <summary>
    /// Direct3D 11 line/polygon vertex format for charts.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ChartVertex
    {
        public float X { get; }
        public float Y { get; }
        public uint ColorRgba { get; }

        public ChartVertex(float x, float y, uint colorRgba)
        {
            X = x;
            Y = y;
            ColorRgba = colorRgba;
        }
    }

    /// <summary>
    /// Hardware instancing quad descriptor for bars, candlesticks, and Gantt tasks.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct InstancedQuad
    {
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }
        public uint ColorRgba { get; }

        public InstancedQuad(float x, float y, float width, float height, uint colorRgba)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            ColorRgba = colorRgba;
        }
    }
}
