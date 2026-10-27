using System;
using System.Collections.Generic;
using System.IO;

namespace ZeroAgent.Dialog.Learning
{
    /// <summary>
    /// Result returned when evaluating live entity or intent resolution against active knowledge.
    /// </summary>
    public sealed class KnowledgeResolutionResult
    {
        public bool IsResolved { get; }
        public string? TargetCode { get; }
        public VerificationStatus? Status { get; }
        public float Confidence { get; }
        public string? SourceCandidateId { get; }
        public string? Details { get; }

        public KnowledgeResolutionResult(
            bool isResolved,
            string? targetCode,
            VerificationStatus? status,
            float confidence,
            string? sourceCandidateId = null,
            string? details = null)
        {
            IsResolved = isResolved;
            TargetCode = targetCode;
            Status = status;
            Confidence = confidence;
            SourceCandidateId = sourceCandidateId;
            Details = details;
        }

        public static KnowledgeResolutionResult Unresolved(string query, string category) =>
            new(false, null, null, 0.0f, null, $"No active knowledge match for '{query}' in category '{category}'.");

        public static KnowledgeResolutionResult Resolved(
            string targetCode,
            VerificationStatus status,
            float confidence,
            string? candidateId = null,
            string? details = null) =>
            new(true, targetCode, status, confidence, candidateId, details);
    }

    /// <summary>
    /// Summary audit metrics of the agent's knowledge base.
    /// </summary>
    public sealed class KnowledgeAuditStatistics
    {
        public int TotalCandidates { get; set; }
        public int VerifiedCount { get; set; }
        public int StagedCount { get; set; }
        public int QuarantinedCount { get; set; }
        public int RevokedCount { get; set; }
        public int TotalEvidenceCount { get; set; }
    }

    /// <summary>
    /// Proactive Knowledge Administration Interface for ZeroAgent.
    /// Enables administrators, supervisors, and domain experts to actively curate, seed,
    /// review, approve, and audit the agent's learned knowledge base.
    /// </summary>
    public interface IAdminKnowledgeManager
    {
        /// <summary>
        /// Proactively teaches a verified alias mapping (e.g. colloquial name or slang to master SKU/Customer).
        /// Since this is invoked by an administrator, the alias is immediately promoted to Verified status.
        /// </summary>
        KnowledgeCandidate TeachAlias(
            string rawAlias,
            string targetEntityCode,
            string category,
            string adminUserId,
            string? notes = null);

        /// <summary>
        /// Batch-teaches multiple aliases mapped to the same target entity code.
        /// </summary>
        IReadOnlyList<KnowledgeCandidate> TeachAliasBatch(
            IEnumerable<string> rawAliases,
            string targetEntityCode,
            string category,
            string adminUserId);

        /// <summary>
        /// Proactively defines or updates a verified domain heuristic rule or operational constraint.
        /// </summary>
        KnowledgeCandidate TeachRule(
            string ruleKey,
            string proposedValue,
            string category,
            string adminUserId,
            string? notes = null);

        /// <summary>
        /// Retrieves the staging queue: candidates proposed during user sessions that require review or multi-observation consensus.
        /// </summary>
        IReadOnlyList<KnowledgeCandidate> GetStagingQueue();

        /// <summary>
        /// Retrieves the quarantined queue: candidates blocked due to domain invariant violations, conflict hijacking, or low confidence.
        /// </summary>
        IReadOnlyList<KnowledgeCandidate> GetQuarantinedQueue();

        /// <summary>
        /// Retrieves all currently verified knowledge items, optionally filtered by category.
        /// </summary>
        IReadOnlyList<KnowledgeCandidate> GetVerifiedKnowledge(string? category = null);

        /// <summary>
        /// Explicitly approves and promotes a staged or quarantined candidate to Verified status.
        /// </summary>
        bool ApproveCandidate(string candidateId, string adminUserId);

        /// <summary>
        /// Explicitly revokes and blocks an erroneous or invalid candidate.
        /// </summary>
        bool RevokeCandidate(string candidateId, string reason, string adminUserId);

        /// <summary>
        /// Emergency anti-poisoning rollback: purges all unverified candidates contributed by a specific rogue user ID.
        /// </summary>
        int PurgeUserContributions(string rogueUserId);

        /// <summary>
        /// Tests how the knowledge arbiter would resolve a given query in real time without altering state.
        /// </summary>
        KnowledgeResolutionResult TestResolveAlias(string query, string category);

        /// <summary>
        /// Computes comprehensive audit statistics across all knowledge categories.
        /// </summary>
        KnowledgeAuditStatistics GetStatistics();

        /// <summary>
        /// Exports the active knowledge base to a persistent binary stream.
        /// </summary>
        void ExportSnapshot(Stream stream);

        /// <summary>
        /// Imports and restores a knowledge base snapshot from a binary stream.
        /// </summary>
        void ImportSnapshot(Stream stream);
    }
}
