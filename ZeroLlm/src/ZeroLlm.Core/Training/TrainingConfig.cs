using System;

namespace ZeroLlm.Core.Training
{
    public enum TrainingMode
    {
        /// <summary>
        /// Updates all layers, token embeddings, layer normalizations, and LM head.
        /// </summary>
        Full,

        /// <summary>
        /// Updates token embeddings, final layer normalization, and LM head (fast vocabulary/domain adaptation).
        /// </summary>
        HeadAndEmbeddings,

        /// <summary>
        /// Updates only the LM head output projection.
        /// </summary>
        HeadOnly
    }

    /// <summary>
    /// Hyperparameters for masked Causal LM training and AdamW optimization.
    /// </summary>
    public sealed class TrainingConfig
    {
        public float LearningRate { get; set; } = 1e-3f;
        public float WeightDecay { get; set; } = 0.01f;
        public float Beta1 { get; set; } = 0.9f;
        public float Beta2 { get; set; } = 0.999f;
        public float Epsilon { get; set; } = 1e-8f;
        public float MaxGradNorm { get; set; } = 1.0f;
        public TrainingMode Mode { get; set; } = TrainingMode.Full;

        /// <summary>
        /// Auxiliary load balancing loss weight for Sparse MoE router training (default: 0.01).
        /// Penalizes variance in routing probabilities to prevent expert collapse.
        /// </summary>
        public float AuxiliaryLossWeight { get; set; } = 0.01f;

        /// <summary>
        /// Number of sequence steps to accumulate gradients before executing an AdamW optimizer update (default: 1).
        /// Setting to > 1 stabilizes gradients and significantly boosts CPU training throughput.
        /// </summary>
        public int GradientAccumulationSteps { get; set; } = 1;
    }

    /// <summary>
    /// Metrics result from a single sequence training step.
    /// </summary>
    public readonly struct TrainStepResult
    {
        public float Loss { get; }
        public int TargetTokenCount { get; }
        public float Perplexity => (float)Math.Exp(Math.Min(Loss, 20.0f));
        public float GradNorm { get; }

        public TrainStepResult(float loss, int targetTokenCount, float gradNorm)
        {
            Loss = loss;
            TargetTokenCount = targetTokenCount;
            GradNorm = gradNorm;
        }

        public override string ToString() =>
            $"Loss: {Loss:F4} | PPL: {Perplexity:F2} | Tokens: {TargetTokenCount} | GradNorm: {GradNorm:F4}";
    }

    /// <summary>
    /// Metrics summary for a completed training epoch.
    /// </summary>
    public sealed class TrainEpochResult
    {
        public int Epoch { get; set; }
        public float AverageLoss { get; set; }
        public float AveragePerplexity => (float)Math.Exp(Math.Min(AverageLoss, 20.0f));
        public int TotalTrainedTokens { get; set; }
        public TimeSpan Elapsed { get; set; }

        public override string ToString() =>
            $"Epoch {Epoch} | AvgLoss: {AverageLoss:F4} | AvgPPL: {AveragePerplexity:F2} | Tokens: {TotalTrainedTokens} | Time: {Elapsed.TotalSeconds:F2}s";
    }

    /// <summary>
    /// Real-time progress callback payload emitted during epoch iterations.
    /// </summary>
    public readonly struct TrainProgress
    {
        public int CurrentStep { get; }
        public int TotalSteps { get; }
        public float StepLoss { get; }
        public float RunningLoss { get; }
        public float GradNorm { get; }

        public TrainProgress(int currentStep, int totalSteps, float stepLoss, float runningLoss, float gradNorm)
        {
            CurrentStep = currentStep;
            TotalSteps = totalSteps;
            StepLoss = stepLoss;
            RunningLoss = runningLoss;
            GradNorm = gradNorm;
        }
    }
}
