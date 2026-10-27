using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ZeroAgent.Dialog.Learning
{
    /// <summary>
    /// Four-Tier Verified Continual Learning Arbiter.
    /// Defends the agent against data poisoning, conflicting assertions, and adversarial feedback.
    /// Manages candidate staging, consensus promotion, domain invariant enforcement, and audit rollbacks.
    /// </summary>
    public sealed class VerifiedKnowledgeArbiter
    {
        private readonly IKnowledgeValidator? _validator;
        private readonly ConcurrentDictionary<string, KnowledgeCandidate> _candidates = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _syncLock = new();

        /// <summary>
        /// Minimum confidence threshold for a staged candidate to be auto-promoted to Verified status.
        /// </summary>
        public float ConsensusConfidenceThreshold { get; set; } = 0.80f;

        /// <summary>
        /// Minimum independent observations required before auto-promoting a staged candidate.
        /// </summary>
        public int MinEvidenceCount { get; set; } = 3;

        /// <summary>
        /// Minimum number of distinct operator user IDs required before auto-promoting a staged candidate via consensus.
        /// Default is 1 for backward compatibility; set &gt;= 2 or 3 for strict Sybil-resistant multi-operator consensus.
        /// </summary>
        public int MinDistinctUsersForConsensus { get; set; } = 1;

        public int TotalCandidates => _candidates.Count;
        public int VerifiedCount => _candidates.Values.Count(c => c.Status == VerificationStatus.Verified);
        public int StagedCount => _candidates.Values.Count(c => c.Status == VerificationStatus.Staged);
        public int QuarantinedCount => _candidates.Values.Count(c => c.Status == VerificationStatus.Quarantined);

        public VerifiedKnowledgeArbiter(IKnowledgeValidator? validator = null)
        {
            _validator = validator;
        }

        private static string BuildKey(CandidateType type, string category, string key)
        {
            return $"{type}:{category.Trim().ToLowerInvariant()}:{key.Trim().ToLowerInvariant()}";
        }

        /// <summary>
        /// Proposes an alias mapping between colloquial text and an official entity code.
        /// Enforces domain existence invariants, conflict detection, and consensus staging.
        /// </summary>
        public KnowledgeCandidate ProposeAlias(
            string rawAlias,
            string targetEntityCode,
            string entityCategory,
            string userId,
            string operatorRole = "Operator",
            float initialConfidence = 0.40f)
        {
            if (string.IsNullOrWhiteSpace(rawAlias)) throw new ArgumentNullException(nameof(rawAlias));
            if (string.IsNullOrWhiteSpace(targetEntityCode)) throw new ArgumentNullException(nameof(targetEntityCode));

            string cleanAlias = rawAlias.Trim();
            string cleanCode = targetEntityCode.Trim();
            string category = string.IsNullOrWhiteSpace(entityCategory) ? "General" : entityCategory.Trim();
            string uniqueKey = BuildKey(CandidateType.Alias, category, cleanAlias);

            // Layer 1: Domain Invariant Check
            if (_validator != null && !_validator.ValidateEntityExists(category, cleanCode))
            {
                var invalidCandidate = new KnowledgeCandidate(
                    Guid.NewGuid().ToString("N"),
                    CandidateType.Alias,
                    cleanAlias,
                    cleanCode,
                    category,
                    userId,
                    operatorRole,
                    initialConfidence: 0.0f,
                    initialStatus: VerificationStatus.Quarantined)
                {
                    ConflictDetails = $"Entity code '{cleanCode}' does not exist in master catalog for category '{category}'."
                };
                _candidates[uniqueKey] = invalidCandidate;
                return invalidCandidate;
            }

            lock (_syncLock)
            {
                // Layer 2: Conflict Detection against existing knowledge
                if (_candidates.TryGetValue(uniqueKey, out var existing))
                {
                    // Case A: Reinforcing the same target code
                    if (existing.Value.Equals(cleanCode, StringComparison.OrdinalIgnoreCase))
                    {
                        if (existing.Status != VerificationStatus.Quarantined && existing.Status != VerificationStatus.Revoked)
                        {
                            existing.Reinforce(0.20f, userId);
                            CheckAutoPromotion(existing);
                            return existing;
                        }
                    }
                    else
                    {
                        // Case B: Conflict - Attempting to map same alias to a DIFFERENT target code
                        bool canOverride = _validator != null ? _validator.CanCommitDirectly(operatorRole) : IsDefaultPrivilegedRole(operatorRole);
                        if (!canOverride && existing.Status == VerificationStatus.Verified)
                        {
                            // Reject unauthorized override of verified knowledge
                            var conflicted = new KnowledgeCandidate(
                                Guid.NewGuid().ToString("N"),
                                CandidateType.Alias,
                                cleanAlias,
                                cleanCode,
                                category,
                                userId,
                                operatorRole,
                                initialConfidence: 0.10f,
                                initialStatus: VerificationStatus.Quarantined)
                            {
                                ConflictDetails = $"Conflict: Alias '{cleanAlias}' is already verified as mapping to '{existing.Value}'."
                            };
                            return conflicted;
                        }
                    }
                }

                // Layer 3: Author Role & Staging Determination
                bool isPrivileged = _validator != null ? _validator.CanCommitDirectly(operatorRole) : IsDefaultPrivilegedRole(operatorRole);
                var status = isPrivileged ? VerificationStatus.Verified : VerificationStatus.Staged;
                float confidence = isPrivileged ? 1.0f : initialConfidence;

                var candidate = new KnowledgeCandidate(
                    Guid.NewGuid().ToString("N"),
                    CandidateType.Alias,
                    cleanAlias,
                    cleanCode,
                    category,
                    userId,
                    operatorRole,
                    confidence,
                    status);

                if (isPrivileged)
                {
                    candidate.VerifiedByUserId = userId;
                }

                _candidates[uniqueKey] = candidate;
                return candidate;
            }
        }

        /// <summary>
        /// Proposes a domain heuristic rule or operational constraint.
        /// </summary>
        public KnowledgeCandidate ProposeRule(
            string ruleKey,
            string proposedValue,
            string category,
            string userId,
            string operatorRole = "Operator",
            float initialConfidence = 0.50f)
        {
            if (string.IsNullOrWhiteSpace(ruleKey)) throw new ArgumentNullException(nameof(ruleKey));
            if (string.IsNullOrWhiteSpace(proposedValue)) throw new ArgumentNullException(nameof(proposedValue));

            string cleanKey = ruleKey.Trim();
            string cleanVal = proposedValue.Trim();
            string cleanCat = string.IsNullOrWhiteSpace(category) ? "Policy" : category.Trim();
            string uniqueKey = BuildKey(CandidateType.HeuristicRule, cleanCat, cleanKey);

            // Layer 1: Boundary & Safety Validation
            if (_validator != null && !_validator.ValidateSafetyBounds(cleanCat, cleanKey, cleanVal, out string? violation))
            {
                var quarantined = new KnowledgeCandidate(
                    Guid.NewGuid().ToString("N"),
                    CandidateType.HeuristicRule,
                    cleanKey,
                    cleanVal,
                    cleanCat,
                    userId,
                    operatorRole,
                    initialConfidence: 0.0f,
                    initialStatus: VerificationStatus.Quarantined)
                {
                    ConflictDetails = violation ?? "Violates domain safety boundary."
                };
                _candidates[uniqueKey] = quarantined;
                return quarantined;
            }

            lock (_syncLock)
            {
                bool isPrivileged = _validator != null ? _validator.CanCommitDirectly(operatorRole) : IsDefaultPrivilegedRole(operatorRole);
                var status = isPrivileged ? VerificationStatus.Verified : VerificationStatus.Staged;
                float confidence = isPrivileged ? 1.0f : initialConfidence;

                if (_candidates.TryGetValue(uniqueKey, out var existing))
                {
                    if (existing.Value.Equals(cleanVal, StringComparison.OrdinalIgnoreCase))
                    {
                        existing.Reinforce(0.20f, userId);
                        CheckAutoPromotion(existing);
                        return existing;
                    }
                }

                var candidate = new KnowledgeCandidate(
                    Guid.NewGuid().ToString("N"),
                    CandidateType.HeuristicRule,
                    cleanKey,
                    cleanVal,
                    cleanCat,
                    userId,
                    operatorRole,
                    confidence,
                    status);

                if (isPrivileged)
                {
                    candidate.VerifiedByUserId = userId;
                }

                _candidates[uniqueKey] = candidate;
                return candidate;
            }
        }

        private void CheckAutoPromotion(KnowledgeCandidate candidate)
        {
            int requiredDistinctUsers = Math.Max(1, MinDistinctUsersForConsensus);
            if (candidate.Status == VerificationStatus.Staged &&
                candidate.Confidence >= ConsensusConfidenceThreshold &&
                candidate.EvidenceCount >= MinEvidenceCount &&
                candidate.DistinctContributorCount >= requiredDistinctUsers)
            {
                candidate.Status = VerificationStatus.Verified;
                candidate.VerifiedByUserId = "SYSTEM_CONSENSUS";
                candidate.LastUpdatedUtc = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Causal Counterfactual Demotion: Evaluates observed downstream causal reward for a learned candidate
        /// and automatically demotes staged or consensus-promoted rules to Quarantined when reward falls at or below threshold.
        /// </summary>
        public bool ReportCausalOutcome(
            string domain,
            string rawKey,
            float observedReward,
            float quarantineThreshold = -0.50f)
        {
            if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(rawKey))
                return false;

            string cleanDomain = domain.Trim();
            string cleanKey = rawKey.Trim();

            lock (_syncLock)
            {
                KnowledgeCandidate? candidate = null;
                string ruleLookup = BuildKey(CandidateType.HeuristicRule, cleanDomain, cleanKey);
                string aliasLookup = BuildKey(CandidateType.Alias, cleanDomain, cleanKey);

                if (!_candidates.TryGetValue(ruleLookup, out candidate) &&
                    !_candidates.TryGetValue(aliasLookup, out candidate))
                {
                    candidate = _candidates.Values.FirstOrDefault(c =>
                        c.Category.Equals(cleanDomain, StringComparison.OrdinalIgnoreCase) &&
                        c.Key.Equals(cleanKey, StringComparison.OrdinalIgnoreCase));
                }

                if (candidate == null)
                    return false;

                candidate.Confidence = Math.Max(0.0f, Math.Min(1.0f, candidate.Confidence + observedReward * 0.50f));
                candidate.LastUpdatedUtc = DateTime.UtcNow;

                bool isEligibleForAutoDemotion =
                    candidate.Status == VerificationStatus.Staged ||
                    (candidate.Status == VerificationStatus.Verified &&
                     string.Equals(candidate.VerifiedByUserId, "SYSTEM_CONSENSUS", StringComparison.OrdinalIgnoreCase));

                if (isEligibleForAutoDemotion && (observedReward <= quarantineThreshold || candidate.Confidence <= 0.15f))
                {
                    candidate.Status = VerificationStatus.Quarantined;
                    candidate.ConflictDetails =
                        $"Causal counterfactual demotion: observedReward={observedReward:F2} (threshold={quarantineThreshold:F2}).";
                }
                else if (candidate.Status == VerificationStatus.Staged && observedReward > 0f)
                {
                    CheckAutoPromotion(candidate);
                }

                return true;
            }
        }

        /// <summary>
        /// Attempts to resolve an alias to its target entity code.
        /// Returns true if a verified mapping exists, or if a staged mapping matches current session author with sufficient confidence.
        /// </summary>
        public bool TryResolveAlias(string rawAlias, string entityCategory, out string? resolvedCode, string? sessionUserId = null, float minConfidence = 0.50f)
        {
            resolvedCode = null;
            if (string.IsNullOrWhiteSpace(rawAlias)) return false;

            string uniqueKey = BuildKey(CandidateType.Alias, entityCategory, rawAlias);
            if (_candidates.TryGetValue(uniqueKey, out var candidate))
            {
                if (candidate.Status == VerificationStatus.Verified)
                {
                    resolvedCode = candidate.Value;
                    return true;
                }

                // Session-scoped lookup for staged candidates if authored by same user
                if (candidate.Status == VerificationStatus.Staged &&
                    candidate.Confidence >= minConfidence &&
                    sessionUserId != null &&
                    candidate.LearnedByUserId.Equals(sessionUserId, StringComparison.OrdinalIgnoreCase))
                {
                    resolvedCode = candidate.Value;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Explicit supervisor verification and sign-off.
        /// Promotes any staged or disputed candidate to permanent Verified status.
        /// </summary>
        public bool ApproveCandidate(string candidateId, string supervisorUserId)
        {
            if (string.IsNullOrWhiteSpace(candidateId)) return false;

            lock (_syncLock)
            {
                var match = _candidates.Values.FirstOrDefault(c => c.Id.Equals(candidateId, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    match.Status = VerificationStatus.Verified;
                    match.Confidence = 1.0f;
                    match.VerifiedByUserId = supervisorUserId ?? "Supervisor";
                    match.ConflictDetails = null;
                    match.LastUpdatedUtc = DateTime.UtcNow;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Revokes and quarantines an erroneous or poisoned candidate.
        /// </summary>
        public bool RevokeCandidate(string candidateId, string reason)
        {
            if (string.IsNullOrWhiteSpace(candidateId)) return false;

            lock (_syncLock)
            {
                var match = _candidates.Values.FirstOrDefault(c => c.Id.Equals(candidateId, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    match.Status = VerificationStatus.Revoked;
                    match.Confidence = 0.0f;
                    match.ConflictDetails = reason ?? "Revoked by supervisor.";
                    match.LastUpdatedUtc = DateTime.UtcNow;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Layer 4: Emergency Rollback / Anti-Poisoning Purge.
        /// Removes all unverified or staged knowledge introduced by a specific user ID.
        /// </summary>
        public int PurgeUserLearnedData(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return 0;

            int purged = 0;
            lock (_syncLock)
            {
                var toPurge = _candidates.Where(kvp =>
                    kvp.Value.LearnedByUserId.Equals(userId, StringComparison.OrdinalIgnoreCase) &&
                    kvp.Value.Status != VerificationStatus.Verified).ToList();

                foreach (var item in toPurge)
                {
                    if (_candidates.TryRemove(item.Key, out _))
                    {
                        purged++;
                    }
                }
            }
            return purged;
        }

        /// <summary>
        /// Retrieves all candidates filtered by status.
        /// </summary>
        public IReadOnlyList<KnowledgeCandidate> GetCandidates(VerificationStatus? status = null)
        {
            if (status.HasValue)
            {
                return _candidates.Values.Where(c => c.Status == status.Value).ToList();
            }
            return _candidates.Values.ToList();
        }

        /// <summary>
        /// Serializes verified and staged knowledge candidates to a binary stream.
        /// </summary>
        public void Save(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write("ZVKN"); // Magic header (Zero Verified Knowledge)
                writer.Write((int)1);  // Version
                var list = _candidates.Values.ToList();
                writer.Write(list.Count);

                foreach (var c in list)
                {
                    writer.Write(c.Id);
                    writer.Write((int)c.Type);
                    writer.Write(c.Key);
                    writer.Write(c.Value);
                    writer.Write(c.Category);
                    writer.Write(c.Confidence);
                    writer.Write(c.EvidenceCount);
                    writer.Write((int)c.Status);
                    writer.Write(c.LearnedByUserId);
                    writer.Write(c.OperatorRole);
                    writer.Write(c.TimestampUtc.ToBinary());
                    writer.Write(c.LastUpdatedUtc.ToBinary());
                    writer.Write(c.ConflictDetails ?? string.Empty);
                    writer.Write(c.VerifiedByUserId ?? string.Empty);
                }
            }
        }

        /// <summary>
        /// Deserializes knowledge candidates from a binary stream.
        /// </summary>
        public void Load(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                string magic = reader.ReadString();
                if (magic != "ZVKN") throw new InvalidDataException("Invalid knowledge snapshot format.");

                int version = reader.ReadInt32();
                int count = reader.ReadInt32();

                lock (_syncLock)
                {
                    _candidates.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        string id = reader.ReadString();
                        var type = (CandidateType)reader.ReadInt32();
                        string key = reader.ReadString();
                        string val = reader.ReadString();
                        string cat = reader.ReadString();
                        float conf = reader.ReadSingle();
                        int evCount = reader.ReadInt32();
                        var status = (VerificationStatus)reader.ReadInt32();
                        string author = reader.ReadString();
                        string role = reader.ReadString();
                        var ts = DateTime.FromBinary(reader.ReadInt64());
                        var lastUp = DateTime.FromBinary(reader.ReadInt64());
                        string conflict = reader.ReadString();
                        string verifier = reader.ReadString();

                        var candidate = new KnowledgeCandidate(id, type, key, val, cat, author, role, conf, status)
                        {
                            EvidenceCount = evCount,
                            LastUpdatedUtc = lastUp,
                            ConflictDetails = string.IsNullOrEmpty(conflict) ? null : conflict,
                            VerifiedByUserId = string.IsNullOrEmpty(verifier) ? null : verifier
                        };

                        string uniqueKey = BuildKey(type, cat, key);
                        _candidates[uniqueKey] = candidate;
                    }
                }
            }
        }

        private static bool IsDefaultPrivilegedRole(string operatorRole)
        {
            if (string.IsNullOrWhiteSpace(operatorRole)) return false;
            return operatorRole.Equals("Supervisor", StringComparison.OrdinalIgnoreCase) ||
                   operatorRole.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
