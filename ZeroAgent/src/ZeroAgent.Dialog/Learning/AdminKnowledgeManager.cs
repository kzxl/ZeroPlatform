using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ZeroAgent.Dialog.Learning
{
    /// <summary>
    /// Thread-safe enterprise implementation of IAdminKnowledgeManager.
    /// Operates over VerifiedKnowledgeArbiter to provide structured administration,
    /// batch curation, sandbox resolution testing, and audit reporting.
    /// </summary>
    public sealed class AdminKnowledgeManager : IAdminKnowledgeManager
    {
        private readonly VerifiedKnowledgeArbiter _arbiter;

        public VerifiedKnowledgeArbiter Arbiter => _arbiter;

        public AdminKnowledgeManager(VerifiedKnowledgeArbiter arbiter)
        {
            _arbiter = arbiter ?? throw new ArgumentNullException(nameof(arbiter));
        }

        public KnowledgeCandidate TeachAlias(
            string rawAlias,
            string targetEntityCode,
            string category,
            string adminUserId,
            string? notes = null)
        {
            if (string.IsNullOrWhiteSpace(rawAlias)) throw new ArgumentNullException(nameof(rawAlias));
            if (string.IsNullOrWhiteSpace(targetEntityCode)) throw new ArgumentNullException(nameof(targetEntityCode));

            string author = string.IsNullOrWhiteSpace(adminUserId) ? "Admin" : adminUserId.Trim();
            var candidate = _arbiter.ProposeAlias(rawAlias, targetEntityCode, category, author, operatorRole: "Admin", initialConfidence: 1.0f);

            if (!string.IsNullOrWhiteSpace(notes) && candidate.Status == VerificationStatus.Verified)
            {
                candidate.ConflictDetails = notes;
            }

            return candidate;
        }

        public IReadOnlyList<KnowledgeCandidate> TeachAliasBatch(
            IEnumerable<string> rawAliases,
            string targetEntityCode,
            string category,
            string adminUserId)
        {
            if (rawAliases == null) throw new ArgumentNullException(nameof(rawAliases));
            if (string.IsNullOrWhiteSpace(targetEntityCode)) throw new ArgumentNullException(nameof(targetEntityCode));

            var results = new List<KnowledgeCandidate>();
            foreach (var alias in rawAliases)
            {
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    results.Add(TeachAlias(alias, targetEntityCode, category, adminUserId));
                }
            }
            return results;
        }

        public KnowledgeCandidate TeachRule(
            string ruleKey,
            string proposedValue,
            string category,
            string adminUserId,
            string? notes = null)
        {
            if (string.IsNullOrWhiteSpace(ruleKey)) throw new ArgumentNullException(nameof(ruleKey));
            if (string.IsNullOrWhiteSpace(proposedValue)) throw new ArgumentNullException(nameof(proposedValue));

            string author = string.IsNullOrWhiteSpace(adminUserId) ? "Admin" : adminUserId.Trim();
            var candidate = _arbiter.ProposeRule(ruleKey, proposedValue, category, author, operatorRole: "Admin", initialConfidence: 1.0f);

            if (!string.IsNullOrWhiteSpace(notes) && candidate.Status == VerificationStatus.Verified)
            {
                candidate.ConflictDetails = notes;
            }

            return candidate;
        }

        public IReadOnlyList<KnowledgeCandidate> GetStagingQueue()
        {
            return _arbiter.GetCandidates(VerificationStatus.Staged);
        }

        public IReadOnlyList<KnowledgeCandidate> GetQuarantinedQueue()
        {
            return _arbiter.GetCandidates(VerificationStatus.Quarantined);
        }

        public IReadOnlyList<KnowledgeCandidate> GetVerifiedKnowledge(string? category = null)
        {
            var verified = _arbiter.GetCandidates(VerificationStatus.Verified);
            if (!string.IsNullOrWhiteSpace(category))
            {
                return verified.Where(c => c.Category.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            }
            return verified;
        }

        public bool ApproveCandidate(string candidateId, string adminUserId)
        {
            string author = string.IsNullOrWhiteSpace(adminUserId) ? "Admin" : adminUserId.Trim();
            return _arbiter.ApproveCandidate(candidateId, author);
        }

        public bool RevokeCandidate(string candidateId, string reason, string adminUserId)
        {
            string formattedReason = $"Revoked by {adminUserId}: {reason}";
            return _arbiter.RevokeCandidate(candidateId, formattedReason);
        }

        public int PurgeUserContributions(string rogueUserId)
        {
            return _arbiter.PurgeUserLearnedData(rogueUserId);
        }

        public KnowledgeResolutionResult TestResolveAlias(string query, string category)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return KnowledgeResolutionResult.Unresolved(string.Empty, category);
            }

            string clean = query.Trim();
            if (_arbiter.TryResolveAlias(clean, category, out var targetCode) && !string.IsNullOrEmpty(targetCode))
            {
                var candidate = _arbiter.GetCandidates()
                    .FirstOrDefault(c => c.Type == CandidateType.Alias &&
                                         c.Key.Equals(clean, StringComparison.OrdinalIgnoreCase) &&
                                         c.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

                return KnowledgeResolutionResult.Resolved(
                    targetCode!,
                    candidate?.Status ?? VerificationStatus.Verified,
                    candidate?.Confidence ?? 1.0f,
                    candidate?.Id,
                    $"Successfully resolved to '{targetCode}' via {candidate?.Status} alias.");
            }

            return KnowledgeResolutionResult.Unresolved(clean, category);
        }

        public KnowledgeAuditStatistics GetStatistics()
        {
            var all = _arbiter.GetCandidates();
            return new KnowledgeAuditStatistics
            {
                TotalCandidates = all.Count,
                VerifiedCount = all.Count(c => c.Status == VerificationStatus.Verified),
                StagedCount = all.Count(c => c.Status == VerificationStatus.Staged),
                QuarantinedCount = all.Count(c => c.Status == VerificationStatus.Quarantined),
                RevokedCount = all.Count(c => c.Status == VerificationStatus.Revoked),
                TotalEvidenceCount = all.Sum(c => c.EvidenceCount)
            };
        }

        public void ExportSnapshot(Stream stream)
        {
            _arbiter.Save(stream);
        }

        public void ImportSnapshot(Stream stream)
        {
            _arbiter.Load(stream);
        }
    }
}
