namespace ZeroGraphics.DirectX.Interception
{
    /// <summary>
    /// Runtime performance telemetry and render pipeline metrics captured during frame presentation.
    /// </summary>
    public readonly struct FrameTelemetry
    {
        /// <summary>
        /// Total consecutive frames presented since interception commenced.
        /// </summary>
        public ulong FrameIndex { get; }

        /// <summary>
        /// Frame duration in milliseconds.
        /// </summary>
        public double DeltaTimeMs { get; }

        /// <summary>
        /// Instantaneous frames per second (FPS).
        /// </summary>
        public double Fps { get; }

        /// <summary>
        /// Total Draw and DrawIndexed calls executed during this frame.
        /// </summary>
        public int DrawCalls { get; }

        /// <summary>
        /// Total non-indexed vertices processed during this frame.
        /// </summary>
        public long VertexCount { get; }

        /// <summary>
        /// Total indexed indices/primitives processed during this frame.
        /// </summary>
        public long IndexCount { get; }

        public FrameTelemetry(ulong frameIndex, double deltaTimeMs, double fps, int drawCalls, long vertexCount, long indexCount)
        {
            FrameIndex = frameIndex;
            DeltaTimeMs = deltaTimeMs;
            Fps = fps;
            DrawCalls = drawCalls;
            VertexCount = vertexCount;
            IndexCount = indexCount;
        }

        public override string ToString() =>
            $"Frame #{FrameIndex}: {Fps:F1} FPS ({DeltaTimeMs:F2} ms) | Draws: {DrawCalls} | Verts: {VertexCount} | Indices: {IndexCount}";
    }
}
