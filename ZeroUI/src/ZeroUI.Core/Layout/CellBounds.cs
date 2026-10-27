using System.Runtime.CompilerServices;

namespace ZeroUI.Core.Layout
{
    /// <summary>
    /// Immutable value type representing spatial bounding rectangle for a cell or region.
    /// </summary>
    public readonly struct CellBounds
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CellBounds(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int Right => X + Width;
        public int Bottom => Y + Height;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(int px, int py)
        {
            return px >= X && px < X + Width && py >= Y && py < Y + Height;
        }
    }
}
