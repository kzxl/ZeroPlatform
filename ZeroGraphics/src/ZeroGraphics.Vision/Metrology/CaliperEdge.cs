namespace ZeroGraphics.Vision.Metrology
{
    /// <summary>
    /// Direction of grayscale intensity transition across an edge.
    /// </summary>
    public enum EdgePolarity
    {
        /// <summary>
        /// Transition from darker to lighter intensity.
        /// </summary>
        DarkToLight,

        /// <summary>
        /// Transition from lighter to darker intensity.
        /// </summary>
        LightToDark,

        /// <summary>
        /// Detect any transition regardless of polarity.
        /// </summary>
        Any
    }

    /// <summary>
    /// Represents a sub-pixel edge point detected by a 1D Caliper rake.
    /// </summary>
    public readonly struct CaliperEdge
    {
        /// <summary>
        /// X coordinate of the edge with sub-pixel precision.
        /// </summary>
        public double X { get; }

        /// <summary>
        /// Y coordinate of the edge with sub-pixel precision.
        /// </summary>
        public double Y { get; }

        /// <summary>
        /// Distance along the caliper scan line from start point.
        /// </summary>
        public double Distance { get; }

        /// <summary>
        /// First derivative gradient magnitude (steepness of transition).
        /// </summary>
        public double Magnitude { get; }

        /// <summary>
        /// Polarity of the transition (Dark-to-Light or Light-to-Dark).
        /// </summary>
        public EdgePolarity Polarity { get; }

        public CaliperEdge(double x, double y, double distance, double magnitude, EdgePolarity polarity)
        {
            X = x;
            Y = y;
            Distance = distance;
            Magnitude = magnitude;
            Polarity = polarity;
        }

        public override string ToString()
            => $"[Edge] ({X:F3}, {Y:F3}), Dist={Distance:F2}, Mag={Magnitude:F1}, Pol={Polarity}";
    }
}
