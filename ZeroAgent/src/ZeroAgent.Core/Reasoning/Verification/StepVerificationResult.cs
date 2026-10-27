using System;

namespace ZeroAgent.Core.Reasoning.Verification
{
    public enum StepVerificationStatus
    {
        Approved = 0,
        NeedsRevision = 1,
        Rejected = 2
    }

    public sealed class StepVerificationResult
    {
        public StepVerificationStatus Status { get; }
        public string? Reason { get; }
        public string? FeedbackForAgent { get; }

        public bool IsApproved => Status == StepVerificationStatus.Approved;

        private StepVerificationResult(StepVerificationStatus status, string? reason, string? feedbackForAgent)
        {
            Status = status;
            Reason = reason;
            FeedbackForAgent = feedbackForAgent;
        }

        public static StepVerificationResult Approve() =>
            new StepVerificationResult(StepVerificationStatus.Approved, null, null);

        public static StepVerificationResult Revise(string feedbackForAgent, string? reason = null) =>
            new StepVerificationResult(StepVerificationStatus.NeedsRevision, reason ?? "Step requires revision.", feedbackForAgent);

        public static StepVerificationResult Reject(string reason, string? feedbackForAgent = null) =>
            new StepVerificationResult(StepVerificationStatus.Rejected, reason, feedbackForAgent ?? reason);
    }
}
