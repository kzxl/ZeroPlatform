using System;
using Xunit;
using ZeroUI.Core.Rendering.Optimizer;

namespace ZeroUI.Core.Tests
{
    public class RenderOptimizerTests
    {
        [Fact]
        public void RenderAnalyzer_TextAndFlatFill_RoutesToCpu()
        {
            // Pure text and flat buttons should stay on CPU to preserve ClearType and avoid GPU context-switch
            var textProfile = RenderOperationProfile.ForText(300, 40);
            var decision = ZeroRenderAnalyzer.Evaluate(textProfile, RenderFidelityTier.Ultra);

            Assert.Equal(RenderPipelineTarget.Cpu, decision.Pipeline);
            Assert.False(decision.UseAtlas);
            Assert.Contains("ClearType", decision.Reason);
            Assert.True(decision.EstimatedCpuCostUs < decision.EstimatedGpuCostUs);
        }

        [Fact]
        public void RenderAnalyzer_DropShadowAndGlowWithText_RoutesToHybrid()
        {
            // A card with drop shadow, neon glow, and inner text should route heavy convolutions to GPU and text to CPU
            var cardProfile = RenderOperationProfile.ForCard(
                width: 400,
                height: 250,
                elevation: 8f,
                cornerRadius: 10f,
                blurRadius: 16f,
                glowIntensity: 1.0f,
                hasText: true);

            var decision = ZeroRenderAnalyzer.Evaluate(cardProfile, RenderFidelityTier.Ultra);

            Assert.Equal(RenderPipelineTarget.Hybrid, decision.Pipeline);
            Assert.Contains("Glow", decision.RecommendedShader);
            Assert.True(decision.EstimatedSpeedupFactor > 1.0);
            Assert.True(decision.EstimatedCpuCostUs > decision.EstimatedGpuCostUs);
        }

        [Fact]
        public void RenderAnalyzer_BatchedCards_RoutesToNineSliceAtlas()
        {
            // When rendering multiple cards with recurring elevation/shadow, it should route to 9-slice atlas
            var batchedProfile = RenderOperationProfile.ForCard(
                width: 320,
                height: 200,
                elevation: 6f,
                cornerRadius: 8f,
                blurRadius: 12f,
                hasText: true,
                batchCount: 16);

            var decision = ZeroRenderAnalyzer.Evaluate(batchedProfile, RenderFidelityTier.Balanced);

            Assert.Equal(RenderPipelineTarget.GpuAtlas, decision.Pipeline);
            Assert.True(decision.UseAtlas);
            Assert.Equal("SdfBoxShadowAtlas", decision.RecommendedShader);
            Assert.Contains("9-slice", decision.Reason);
        }

        [Fact]
        public void RenderAnalyzer_BudgetStrained_DegradesToAtlas()
        {
            // Frame budget violation (> 16.6ms) should trigger PowerSaver / Atlas fallback
            var profile = RenderOperationProfile.ForCard(
                width: 500,
                height: 400,
                elevation: 12f,
                blurRadius: 20f,
                batchCount: 2);

            var decision = ZeroRenderAnalyzer.Evaluate(
                profile,
                fidelity: RenderFidelityTier.PowerSaver,
                currentFrameTimeMs: 22.5);

            Assert.Equal(RenderPipelineTarget.GpuAtlas, decision.Pipeline);
            Assert.True(decision.UseAtlas);
            Assert.Contains("budget strained", decision.Reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShaderRegistry_PreRegisteredShaders_Available()
        {
            Assert.True(ZeroShaderRegistry.TryGet("AnalyticalSdfBoxShadow", out var sdfDesc));
            Assert.NotNull(sdfDesc);
            Assert.Equal("Shadow", sdfDesc!.Category);

            Assert.True(ZeroShaderRegistry.TryGet("SdfBoxShadowAtlas", out var atlasDesc));
            Assert.NotNull(atlasDesc);

            Assert.True(ZeroShaderRegistry.TryGet("DualPassGaussianBlur", out var blurDesc));
            Assert.NotNull(blurDesc);

            Assert.True(ZeroShaderRegistry.TryGet("NeonGlowSdf", out var glowDesc));
            Assert.NotNull(glowDesc);
        }

        [Fact]
        public void ShaderRegistry_EvaluateBoxSdf_AccurateGeometry()
        {
            // Center is (0, 0), half-dimensions are (50, 25), cornerRadius = 5
            // At center (0, 0), dist must be negative (inside)
            float distCenter = ZeroShaderRegistry.EvaluateBoxSdf(0, 0, 50, 25, 5);
            Assert.True(distCenter < 0);

            // Far outside (100, 0), dist must be positive
            float distOutside = ZeroShaderRegistry.EvaluateBoxSdf(100, 0, 50, 25, 5);
            Assert.True(distOutside > 0);

            // Shadow Alpha evaluation
            float alphaInside = ZeroShaderRegistry.EvaluateBoxShadowAlpha(0, 0, 50, 25, 5, 10);
            Assert.Equal(1.0f, alphaInside);

            float alphaFarAway = ZeroShaderRegistry.EvaluateBoxShadowAlpha(100, 100, 50, 25, 5, 5);
            Assert.Equal(0.0f, alphaFarAway);
        }

        [Fact]
        public void ShaderRegistry_ComputeGaussianKernel_NormalizedAndSymmetric()
        {
            Span<float> weights = stackalloc float[7]; // radius = 3 -> kernel size = 7
            ZeroShaderRegistry.ComputeGaussianKernel(3, weights);

            float sum = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                sum += weights[i];
            }

            // Sum of weights must equal 1.0
            Assert.InRange(sum, 0.999f, 1.001f);

            // Center weight must be peak
            Assert.True(weights[3] > weights[2]);
            Assert.True(weights[3] > weights[4]);

            // Symmetric
            Assert.Equal(weights[0], weights[6], 4);
            Assert.Equal(weights[1], weights[5], 4);
            Assert.Equal(weights[2], weights[4], 4);
        }

        [Fact]
        public void ShadowAtlas_GetOrCreatePatch_CachesInstances()
        {
            ZeroShadowAtlas.Clear();

            var patch1 = ZeroShadowAtlas.GetOrCreatePatch(8f, 6f, 12f);
            Assert.NotNull(patch1);
            Assert.True(patch1.PatchWidth > 0);
            Assert.True(patch1.AlphaMask.Length > 0);

            long hitsBefore = ZeroShadowAtlas.CacheHits;
            var patch2 = ZeroShadowAtlas.GetOrCreatePatch(8f, 6f, 12f);

            Assert.Same(patch1, patch2);
            Assert.Equal(hitsBefore + 1, ZeroShadowAtlas.CacheHits);
            Assert.True(ZeroShadowAtlas.HitRatePercentage > 0.0);
        }

        [Fact]
        public void ShadowAtlas_ComputeDestinationSlices_ValidPartitions()
        {
            var patch = ZeroShadowAtlas.GetOrCreatePatch(10f, 4f, 8f);
            patch.ComputeDestinationSlices(
                targetWidth: 300,
                targetHeight: 150,
                out var tl, out var tc, out var tr,
                out var ml, out var mc, out var mr,
                out var bl, out var bc, out var br);

            // Corner sizes must equal border margin
            Assert.Equal(patch.BorderMargin, tl.w);
            Assert.Equal(patch.BorderMargin, tl.h);
            Assert.Equal(patch.BorderMargin, tr.w);
            Assert.Equal(patch.BorderMargin, br.w);

            // Middle center width must be targetWidth - 2 * margin
            Assert.Equal(300 - 2 * patch.BorderMargin, mc.w);
            Assert.Equal(150 - 2 * patch.BorderMargin, mc.h);
        }

        [Fact]
        public void AdaptiveRenderMonitor_FidelityDegradesAndPromotes()
        {
            var monitor = new ZeroAdaptiveRenderMonitor(targetFps: 60.0); // 16.67ms budget
            Assert.Equal(RenderFidelityTier.Ultra, monitor.CurrentFidelity);

            // Record 10 frame spikes (> 16.67ms)
            for (int i = 0; i < 10; i++)
            {
                monitor.RecordFrameTime(25.0);
            }

            // Should have degraded to Balanced
            Assert.Equal(RenderFidelityTier.Balanced, monitor.CurrentFidelity);

            // Record 10 more frame spikes
            for (int i = 0; i < 10; i++)
            {
                monitor.RecordFrameTime(28.0);
            }

            // Should have degraded to PowerSaver
            Assert.Equal(RenderFidelityTier.PowerSaver, monitor.CurrentFidelity);
            Assert.True(monitor.ViolationRatePercentage > 0.0);

            // Now provide consistent healthy headroom (< 70% of 16.67ms = < 11.6ms)
            for (int i = 0; i < 50; i++)
            {
                monitor.RecordFrameTime(6.0); // Very fast frame time
            }

            // Should promote back to Balanced
            Assert.Equal(RenderFidelityTier.Balanced, monitor.CurrentFidelity);

            // Another 50 healthy frames
            for (int i = 0; i < 50; i++)
            {
                monitor.RecordFrameTime(5.0);
            }

            // Should promote back to Ultra
            Assert.Equal(RenderFidelityTier.Ultra, monitor.CurrentFidelity);
        }

        [Fact]
        public void RenderOperationProfile_GlowAndElevation_AccurateCostEvaluation()
        {
            var profile = RenderOperationProfile.ForCard(
                width: 500,
                height: 300,
                elevation: 10f,
                cornerRadius: 8f,
                blurRadius: 16f,
                glowIntensity: 1.2f,
                hasText: false);

            var decision = ZeroRenderAnalyzer.Evaluate(profile, RenderFidelityTier.Ultra);

            // Without interactive text, complex glow + shadow should route directly to GPU Shader
            Assert.Equal(RenderPipelineTarget.GpuShader, decision.Pipeline);
            Assert.Equal("NeonGlowSdf", decision.RecommendedShader);
            Assert.True(decision.EstimatedGpuCostUs > 0);
            Assert.True(decision.EstimatedCpuCostUs > decision.EstimatedGpuCostUs);
        }

        [Fact]
        public void RenderDecision_Formatting_ContainsSpeedupAndPipeline()
        {
            var decision = new RenderDecision(
                RenderPipelineTarget.Hybrid,
                estimatedCpuCostUs: 120.0,
                estimatedGpuCostUs: 10.0,
                recommendedShader: "AnalyticalSdfBoxShadow",
                useAtlas: false,
                reason: "Hybrid test execution");

            string str = decision.ToString();
            Assert.Contains("[Hybrid]", str);
            Assert.Contains("AnalyticalSdfBoxShadow", str);
            Assert.Contains("12.0x", str);
            Assert.Contains("Hybrid test execution", str);
        }

        [Fact]
        public void ShadowPatchKey_ZeroAndNegativeValues_ClampedCleanly()
        {
            var key1 = new ShadowPatchKey(cornerRadius: 8.4f, elevation: 6.2f, blurRadius: 12.1f, spread: 0f);
            var key2 = new ShadowPatchKey(cornerRadius: 8f, elevation: 6f, blurRadius: 12f, spread: 0f);

            Assert.Equal(key1, key2);
            Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
        }

        [Fact]
        public void ShadowNineSlice_TilingDimensions_ConsistentSum()
        {
            var patch = ZeroShadowAtlas.GetOrCreatePatch(8f, 4f, 8f);
            int targetW = 400;
            int targetH = 200;

            patch.ComputeDestinationSlices(
                targetW, targetH,
                out var tl, out var tc, out var tr,
                out var ml, out var mc, out var mr,
                out var bl, out var bc, out var br);

            // Top row total width = tl.w + tc.w + tr.w
            int topRowWidth = tl.w + tc.w + tr.w;
            Assert.Equal(targetW, topRowWidth);

            // Left column total height = tl.h + ml.h + bl.h
            int leftColHeight = tl.h + ml.h + bl.h;
            Assert.Equal(targetH, leftColHeight);
        }

        [Fact]
        public void GpuCapabilities_ConfigureAndReset_MaintainsTelemetry()
        {
            ZeroGpuCapabilities.Reset();
            Assert.Equal(HardwareGpuTier.Tier2_Discrete, ZeroGpuCapabilities.CurrentTier);
            Assert.True(ZeroGpuCapabilities.IsHardwareAccelerated);

            ZeroGpuCapabilities.Configure("Intel Iris Xe Graphics", HardwareGpuTier.Tier1_Integrated, 128.0, 4096.0, 0x8086);
            Assert.Equal("Intel Iris Xe Graphics", ZeroGpuCapabilities.AdapterName);
            Assert.Equal(HardwareGpuTier.Tier1_Integrated, ZeroGpuCapabilities.CurrentTier);
            Assert.Equal(128.0, ZeroGpuCapabilities.DedicatedVramMb);
            Assert.Equal(0x8086u, ZeroGpuCapabilities.VendorId);

            ZeroGpuCapabilities.Reset();
            Assert.Equal(HardwareGpuTier.Tier2_Discrete, ZeroGpuCapabilities.CurrentTier);
        }

        [Fact]
        public void RenderAnalyzer_Tier0Software_LowersAtlasThreshold()
        {
            // On Tier 0 (WARP software rasterizer), batch size of 2 already routes to Atlas to avoid software pixel shaders
            var profile = RenderOperationProfile.ForCard(350, 200, elevation: 8f, blurRadius: 14f, batchCount: 2);
            var decision = ZeroRenderAnalyzer.Evaluate(profile, RenderFidelityTier.Balanced, overrideGpuTier: HardwareGpuTier.Tier0_Software);

            Assert.Equal(RenderPipelineTarget.GpuAtlas, decision.Pipeline);
            Assert.True(decision.UseAtlas);
        }

        [Fact]
        public void RenderAnalyzer_Tier2Discrete_LowerGpuCostThanTier0()
        {
            var profile = RenderOperationProfile.ForCard(800, 600, elevation: 16f, blurRadius: 24f, batchCount: 1);
            var decisionTier0 = ZeroRenderAnalyzer.Evaluate(profile, RenderFidelityTier.Ultra, overrideGpuTier: HardwareGpuTier.Tier0_Software);
            var decisionTier2 = ZeroRenderAnalyzer.Evaluate(profile, RenderFidelityTier.Ultra, overrideGpuTier: HardwareGpuTier.Tier2_Discrete);

            Assert.True(decisionTier2.EstimatedGpuCostUs < decisionTier0.EstimatedGpuCostUs);
            Assert.True(decisionTier2.EstimatedSpeedupFactor > decisionTier0.EstimatedSpeedupFactor);
        }

        [Fact]
        public void RenderAnalyzer_BatchOf32Cards_RoutesToAtlasWithMassiveSpeedup()
        {
            var profile = RenderOperationProfile.ForCard(260, 140, elevation: 8f, cornerRadius: 10f, blurRadius: 14f, batchCount: 32);
            var decision = ZeroRenderAnalyzer.Evaluate(profile, RenderFidelityTier.Balanced);

            Assert.Equal(RenderPipelineTarget.GpuAtlas, decision.Pipeline);
            Assert.True(decision.UseAtlas);
            Assert.Equal("SdfBoxShadowAtlas", decision.RecommendedShader);
            Assert.True(decision.EstimatedSpeedupFactor >= 10.0);
        }
    }
}
