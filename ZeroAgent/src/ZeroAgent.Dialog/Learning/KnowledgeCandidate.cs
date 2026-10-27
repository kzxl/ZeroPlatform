using System;

namespace ZeroAgent.Dialog.Learning
{
    /// <summary>
    /// Categorizes the type of knowledge acquired during agent operation.
    /// </summary>
    public enum CandidateType
    {
        /// <summary>
        /// A lexical or colloquial alias mapped to a master entity code (e.g. "bộ nguồn" -> "PWR-24V").
        /// </summary>
        Alias = 0,

        /// <summary>
        /// A domain heuristic rule or default preference (e.g. Customer X -> DefaultWarehouse Y).
        /// </summary>
        HeuristicRule = 1,

        /// <summary>
        /// A contrastive semantic exemplar pair (pull closer or push apart).
        /// </summary>
        ContrastiveExemplar = 2
    }

    /// <summary>
    /// Lifecycle verification status of candidate knowledge to protect against data poisoning.
    /// </summary>
    public enum VerificationStatus
    {
        /// <summary>
        /// Proposed by user/operator, awaiting multi-session consensus or supervisor review.
        /// Low confidence, local/session scope only.
        /// </summary>
        Staged = 0,

        /// <summary>
        /// Confirmed by domain invariants, consensus threshold, or supervisor sign-off.
        /// Active across all sessions.
        /// </summary>
        Verified = 1,

        /// <summary>
        /// Conflicted with existing verified master data or invariant boundaries.
        /// Blocked from affecting runtime behavior.
        /// </summary>
        Quarantined = 2,

        /// <summary>
        /// Manually revoked or purged due to data cleanliness or security review.
        /// </summary>
        Revoked = 3
    }

    /// <summary>
    /// An immutable provenance record for a unit of learned knowledge.
    /// Tracks author, role, confidence score, evidence count, and verification lifecycle.
    /// </summary>
    public sealed class KnowledgeCandidate
    {
        private readonly System.Collections.Generic.HashSet<string> _contributors =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public string Id { get; }
        public CandidateType Type { get; }
        public string Key { get; }
        public string Value { get; set; }
        public string Category { get; }
        public float Confidence { get; set; }
        public int EvidenceCount { get; set; }
        public VerificationStatus Status { get; set; }
        public string LearnedByUserId { get; }
        public string OperatorRole { get; }
        public DateTime TimestampUtc { get; }
        public DateTime LastUpdatedUtc { get; set; }
        public string? ConflictDetails { get; set; }
        public string? VerifiedByUserId { get; set; }

        /// <summary>
        /// Number of distinct operator user IDs that have independently proposed or corroborated this candidate.
        /// </summary>
        public int DistinctContributorCount => _contributors.Count;

        /// <summary>
        /// Distinct operator user IDs that contributed to this candidate.
        /// </summary>
        public System.Collections.Generic.IReadOnlyCollection<string> Contributors => _contributors;

        public KnowledgeCandidate(
            string id,
            CandidateType type,
            string key,
            string value,
            string category,
            string learnedByUserId,
            string operatorRole = "Operator",
            float initialConfidence = 0.35f,
            VerificationStatus initialStatus = VerificationStatus.Staged)
        {
            Id = id ?? Guid.NewGuid().ToString("N");
            Type = type;
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Category = category ?? "General";
            LearnedByUserId = string.IsNullOrWhiteSpace(learnedByUserId) ? "Anonymous" : learnedByUserId.Trim();
            OperatorRole = string.IsNullOrWhiteSpace(operatorRole) ? "Operator" : operatorRole.Trim();
            Confidence = Math.Max(0.0f, Math.Min(1.0f, initialConfidence));
            EvidenceCount = 1;
            Status = initialStatus;
            TimestampUtc = DateTime.UtcNow;
            LastUpdatedUtc = TimestampUtc;
            _contributors.Add(LearnedByUserId);
        }

        /// <summary>
        /// Reinforces the candidate with new corroborating observation from another turn/session.
        /// Increases evidence count, tracks distinct contributor identity, and boosts confidence.
        /// </summary>
        public void Reinforce(float boost = 0.20f, string? contributorUserId = null)
        {
            EvidenceCount++;
            if (!string.IsNullOrWhiteSpace(contributorUserId))
            {
                _contributors.Add(contributorUserId!.Trim());
            }
            Confidence = Math.Min(1.0f, Confidence + boost);
            LastUpdatedUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Penalizes the candidate when conflicting with user feedback or ground truth.
        /// </summary>
        public void Penalize(float penalty = 0.30f)
        {
            Confidence = Math.Max(0.0f, Confidence - penalty);
            LastUpdatedUtc = DateTime.UtcNow;
            if (Confidence <= 0.15f && Status == VerificationStatus.Staged)
            {
                Status = VerificationStatus.Quarantined;
                ConflictDetails = "Confidence dropped below minimum viability threshold.";
            }
        }
    }
}
