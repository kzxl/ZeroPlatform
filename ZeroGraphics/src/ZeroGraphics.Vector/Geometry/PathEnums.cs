namespace ZeroGraphics.Vector.Geometry
{
    /// <summary>
    /// Represents the operation verb for a segment within a vector path.
    /// </summary>
    public enum PathVerb : byte
    {
        MoveTo = 0,
        LineTo = 1,
        QuadTo = 2,
        CubicTo = 3,
        Close = 4
    }

    /// <summary>
    /// Determines how the interior of a closed vector path is calculated for filling.
    /// </summary>
    public enum FillRule : byte
    {
        NonZero = 0,
        EvenOdd = 1
    }

    /// <summary>
    /// Shape of the endpoints for stroked lines.
    /// </summary>
    public enum LineCap : byte
    {
        Butt = 0,
        Round = 1,
        Square = 2
    }

    /// <summary>
    /// Shape of the corner joints between connecting stroked line segments.
    /// </summary>
    public enum LineJoin : byte
    {
        Miter = 0,
        Bevel = 1,
        Round = 2
    }
}
