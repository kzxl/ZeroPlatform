using System;
using System.IO;
using Xunit;
using ZeroAgent.Dialog.Learning;

namespace ZeroAgent.Tests
{
    public sealed class TestMasterCatalogValidator : IKnowledgeValidator
    {
        public bool ValidateEntityExists(string entityCategory, string entityCode)
        {
            if (entityCategory.Equals("Product", StringComparison.OrdinalIgnoreCase))
            {
                return entityCode == "PWR-24V-01" || entityCode == "PLC-FX5U" || entityCode == "CBL-ETH-05M";
            }
            if (entityCategory.Equals("Customer", StringComparison.OrdinalIgnoreCase))
            {
                return entityCode == "KH001" || entityCode == "KH002";
            }
            return true;
        }

        public bool ValidateSafetyBounds(string category, string ruleKey, string proposedValue, out string? violationReason)
        {
            violationReason = null;
            if (ruleKey.Equals("MaxDiscount", StringComparison.OrdinalIgnoreCase))
            {
                if (decimal.TryParse(proposedValue, out decimal d))
                {
                    if (d < 0 || d > 0.30m)
                    {
                        violationReason = "Discount exceeds corporate boundary [0.0, 0.30].";
                        return false;
                    }
                }
            }
            return true;
        }

        public bool CanCommitDirectly(string operatorRole)
        {
            return operatorRole.Equals("Supervisor", StringComparison.OrdinalIgnoreCase) ||
                   operatorRole.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class VerifiedKnowledgeArbiterTests
    {
        [Fact]
        public void RejectsNonExistentMasterEntityCode()
        {
            var validator = new TestMasterCatalogValidator();
            var arbiter = new VerifiedKnowledgeArbiter(validator);

            var candidate = arbiter.ProposeAlias("bộ nguồn ma", "NON-EXISTENT-SKU", "Product", "User01", "Operator");

            Assert.Equal(VerificationStatus.Quarantined, candidate.Status);
            Assert.Contains("does not exist", candidate.ConflictDetails);
            Assert.False(arbiter.TryResolveAlias("bộ nguồn ma", "Product", out _));
        }

        [Fact]
        public void RejectsViolatingSafetyBoundRule()
        {
            var validator = new TestMasterCatalogValidator();
            var arbiter = new VerifiedKnowledgeArbiter(validator);

            // Attempting to inject 90% discount (malicious / erroneous)
            var rule = arbiter.ProposeRule("MaxDiscount", "0.90", "Pricing", "RogueUser", "Operator");

            Assert.Equal(VerificationStatus.Quarantined, rule.Status);
            Assert.Contains("exceeds corporate boundary", rule.ConflictDetails);
        }

        [Fact]
        public void PrivilegedRoleDirectlyCommitsVerifiedKnowledge()
        {
            var validator = new TestMasterCatalogValidator();
            var arbiter = new VerifiedKnowledgeArbiter(validator);

            var candidate = arbiter.ProposeAlias("cục sạc nguồn", "PWR-24V-01", "Product", "BossUser", "Supervisor");

            Assert.Equal(VerificationStatus.Verified, candidate.Status);
            Assert.Equal(1.0f, candidate.Confidence);
            Assert.True(arbiter.TryResolveAlias("cục sạc nguồn", "Product", out var resolvedCode));
            Assert.Equal("PWR-24V-01", resolvedCode);
        }

        [Fact]
        public void UnprivilegedRoleStagesKnowledgeAndRequiresConsensus()
        {
            var validator = new TestMasterCatalogValidator();
            var arbiter = new VerifiedKnowledgeArbiter(validator);

            // Turn 1: Low confidence, staged
            var c1 = arbiter.ProposeAlias("bộ nguồn 24v", "PWR-24V-01", "Product", "Op1", "Operator", initialConfidence: 0.40f);
            Assert.Equal(VerificationStatus.Staged, c1.Status);
            Assert.Equal(1, c1.EvidenceCount);

            // Global resolution fails for generic query
            Assert.False(arbiter.TryResolveAlias("bộ nguồn 24v", "Product", out _));

            // But author's own session can see it if confidence matches
            Assert.True(arbiter.TryResolveAlias("bộ nguồn 24v", "Product", out var ownCode, sessionUserId: "Op1", minConfidence: 0.35f));
            Assert.Equal("PWR-24V-01", ownCode);

            // Turn 2 & 3: Independent corroborations reinforce candidate
            arbiter.ProposeAlias("bộ nguồn 24v", "PWR-24V-01", "Product", "Op2", "Operator");
            arbiter.ProposeAlias("bộ nguồn 24v", "PWR-24V-01", "Product", "Op3", "Operator");

            // Auto-promoted to Verified by consensus!
            Assert.Equal(VerificationStatus.Verified, c1.Status);
            Assert.True(c1.Confidence >= 0.80f);
            Assert.True(arbiter.TryResolveAlias("bộ nguồn 24v", "Product", out var globalResolved));
            Assert.Equal("PWR-24V-01", globalResolved);
        }

        [Fact]
        public void DetectsAndQuarantinesConflictingAliasHijacking()
        {
            var validator = new TestMasterCatalogValidator();
            var arbiter = new VerifiedKnowledgeArbiter(validator);

            // Supervisor establishes verified alias: "bộ điều khiển" -> "PLC-FX5U"
            arbiter.ProposeAlias("bộ điều khiển", "PLC-FX5U", "Product", "Boss", "Supervisor");

            // Operator attempts to hijack same alias to map to power supply
            var conflict = arbiter.ProposeAlias("bộ điều khiển", "PWR-24V-01", "Product", "UserX", "Operator");

            Assert.Equal(VerificationStatus.Quarantined, conflict.Status);
            Assert.Contains("Conflict", conflict.ConflictDetails);

            // Ground truth remains intact
            Assert.True(arbiter.TryResolveAlias("bộ điều khiển", "Product", out var resolvedCode));
            Assert.Equal("PLC-FX5U", resolvedCode);
        }

        [Fact]
        public void PurgeUserLearnedDataRollsBackPoisonedStagedEntries()
        {
            var validator = new TestMasterCatalogValidator();
            var arbiter = new VerifiedKnowledgeArbiter(validator);

            arbiter.ProposeAlias("alias1", "PWR-24V-01", "Product", "RogueUser", "Operator");
            arbiter.ProposeAlias("alias2", "PLC-FX5U", "Product", "RogueUser", "Operator");
            arbiter.ProposeAlias("legit1", "CBL-ETH-05M", "Product", "GoodUser", "Operator");

            Assert.Equal(3, arbiter.TotalCandidates);

            int purged = arbiter.PurgeUserLearnedData("RogueUser");
            Assert.Equal(2, purged);
            Assert.Equal(1, arbiter.TotalCandidates);
            Assert.False(arbiter.TryResolveAlias("alias1", "Product", out _));
        }

        [Fact]
        public void SaveAndLoadRoundtripPreservesVerifiedAndStagedCandidates()
        {
            var arbiter = new VerifiedKnowledgeArbiter();
            arbiter.ProposeAlias("day mang", "CBL-ETH-05M", "Product", "AdminUser", "Supervisor");
            arbiter.ProposeRule("DeliveryCutoff", "16:00", "Logistics", "Supervisor1", "Supervisor");

            using var ms = new MemoryStream();
            arbiter.Save(ms);

            ms.Position = 0;
            var reloadedArbiter = new VerifiedKnowledgeArbiter();
            reloadedArbiter.Load(ms);

            Assert.Equal(2, reloadedArbiter.VerifiedCount);
            Assert.True(reloadedArbiter.TryResolveAlias("day mang", "Product", out var code));
            Assert.Equal("CBL-ETH-05M", code);
        }

        [Fact]
        public void SybilResistance_BlocksSingleOperatorSpamAndPromotesOnlyOnMultiUserQuorum()
        {
            var validator = new TestMasterCatalogValidator();
            var arbiter = new VerifiedKnowledgeArbiter(validator)
            {
                MinEvidenceCount = 3,
                MinDistinctUsersForConsensus = 3,
                ConsensusConfidenceThreshold = 0.80f
            };

            // Single rogue user spams the same alias 5 times
            KnowledgeCandidate candidate = null!;
            for (int i = 0; i < 5; i++)
            {
                candidate = arbiter.ProposeAlias("nguồn công nghiệp", "PWR-24V-01", "Product", "SybilUser1", "Operator", initialConfidence: 0.50f);
            }

            Assert.Equal(5, candidate.EvidenceCount);
            Assert.Equal(1, candidate.DistinctContributorCount);
            // Must remain Staged because DistinctContributorCount (1) < MinDistinctUsersForConsensus (3)
            Assert.Equal(VerificationStatus.Staged, candidate.Status);

            // Second distinct user corroborates -> still 2 < 3
            arbiter.ProposeAlias("nguồn công nghiệp", "PWR-24V-01", "Product", "OperatorTwo", "Operator");
            Assert.Equal(2, candidate.DistinctContributorCount);
            Assert.Equal(VerificationStatus.Staged, candidate.Status);

            // Third distinct user corroborates -> reaches quorum of 3 distinct operators!
            arbiter.ProposeAlias("nguồn công nghiệp", "PWR-24V-01", "Product", "OperatorThree", "Operator");
            Assert.Equal(3, candidate.DistinctContributorCount);
            Assert.Equal(VerificationStatus.Verified, candidate.Status);
            Assert.Equal("SYSTEM_CONSENSUS", candidate.VerifiedByUserId);
        }

        [Fact]
        public void ReportCausalOutcome_DemotesConsensusRuleOnNegativeCounterfactualReward()
        {
            var validator = new TestMasterCatalogValidator();
            var arbiter = new VerifiedKnowledgeArbiter(validator)
            {
                MinEvidenceCount = 3,
                MinDistinctUsersForConsensus = 2
            };

            arbiter.ProposeRule("SafetyStockBuffer", "15", "Inventory", "Op1", "Operator", initialConfidence: 0.50f);
            arbiter.ProposeRule("SafetyStockBuffer", "15", "Inventory", "Op2", "Operator");
            var promoted = arbiter.ProposeRule("SafetyStockBuffer", "15", "Inventory", "Op3", "Operator");

            Assert.Equal(VerificationStatus.Verified, promoted.Status);
            Assert.Equal("SYSTEM_CONSENSUS", promoted.VerifiedByUserId);

            // Report severe negative causal outcome (e.g. stockout anomaly triggered downstream)
            bool updated = arbiter.ReportCausalOutcome("Inventory", "SafetyStockBuffer", observedReward: -0.75f, quarantineThreshold: -0.50f);

            Assert.True(updated);
            Assert.Equal(VerificationStatus.Quarantined, promoted.Status);
            Assert.Contains("Causal counterfactual demotion", promoted.ConflictDetails);
        }
    }
}
