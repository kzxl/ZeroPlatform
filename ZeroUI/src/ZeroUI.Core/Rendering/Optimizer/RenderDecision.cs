namespace ZeroUI.Core.Rendering.Optimizer
{
    /// <summary>
    /// Result produced by <see cref="ZeroRenderAnalyzer"/> indicating the optimal rendering pipeline and rationale.
    /// </summary>
    public readonly struct RenderDecision
    {
        public readonly RenderPipelineTarget Pipeline;
        public readonly double EstimatedCpuCostUs;
        public readonly double EstimatedGpuCostUs;
        public readonly double EstimatedSpeedupFactor;
        public readonly string RecommendedShader;
        public readonly bool UseAtlas;
        public readonly string Reason;

        public RenderDecision(
            RenderPipelineTarget pipeline,
            double estimatedCpuCostUs,
            double estimatedGpuCostUs,
            string recommendedShader,
            bool useAtlas,
            string reason)
        {
            Pipeline = pipeline;
            EstimatedCpuCostUs = estimatedCpuCostUs;
            EstimatedGpuCostUs = estimatedGpuCostUs;
            EstimatedSpeedupFactor = estimatedGpuCostUs > 0.001
                ? System.Math.Round(estimatedCpuCostUs / estimatedGpuCostUs, 1)
                : 1.0;
            RecommendedShader = recommendedShader;
            UseAtlas = useAtlas;
            Reason = reason;
        }

        public override string ToString() =>
            $"[{Pipeline}] Shader: {RecommendedShader}, Speedup: {EstimatedSpeedupFactor:F1}x (CPU: {EstimatedCpuCostUs:F1}µs vs GPU: {EstimatedGpuCostUs:F1}µs) - {Reason}";
    }
}
