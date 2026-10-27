using System;
using System.IO;
using Xunit;
using ZeroAgent.Dialog.Learning;

namespace ZeroAgent.Tests
{
    public class AdminKnowledgeManagerTests
    {
        [Fact]
        public void AdminCanProactivelyTeachAliasBatch()
        {
            var validator = new TestMasterCatalogValidator();
            var arbiter = new VerifiedKnowledgeArbiter(validator);
            var manager = new AdminKnowledgeManager(arbiter);

            var aliases = new[] { "nguon to ong", "bo nguon led", "adapter 24v" };
            var results = manager.TeachAliasBatch(aliases, "PWR-24V-01", "Product", "Admin01");

            Assert.Equal(3, results.Count);
            foreach (var r in results)
            {
                Assert.Equal(VerificationStatus.Verified, r.Status);
                Assert.Equal(1.0f, r.Confidence);
            }

            // Test resolution
            var testRes = manager.TestResolveAlias("nguon to ong", "Product");
            Assert.True(testRes.IsResolved);
            Assert.Equal("PWR-24V-01", testRes.TargetCode);
            Assert.Equal(VerificationStatus.Verified, testRes.Status);
        }

        [Fact]
        public void AdminCanApproveAndPromoteStagedCandidate()
        {
            var validator = new TestMasterCatalogValidator();
            var arbiter = new VerifiedKnowledgeArbiter(validator);
            var manager = new AdminKnowledgeManager(arbiter);

            // Operator creates staged candidate
            var staged = arbiter.ProposeAlias("day lan 5m", "CBL-ETH-05M", "Product", "Operator01", "Operator");
            Assert.Equal(VerificationStatus.Staged, staged.Status);

            var stagingQueue = manager.GetStagingQueue();
            Assert.Single(stagingQueue);
            Assert.Equal(staged.Id, stagingQueue[0].Id);

            // Admin approves candidate
            bool approved = manager.ApproveCandidate(staged.Id, "AdminBoss");
            Assert.True(approved);

            Assert.Empty(manager.GetStagingQueue());
            var verified = manager.GetVerifiedKnowledge("Product");
            Assert.Single(verified);
            Assert.Equal(VerificationStatus.Verified, verified[0].Status);
            Assert.Equal("AdminBoss", verified[0].VerifiedByUserId);
        }

        [Fact]
        public void AdminCanRevokeInvalidCandidate()
        {
            var validator = new TestMasterCatalogValidator();
            var arbiter = new VerifiedKnowledgeArbiter(validator);
            var manager = new AdminKnowledgeManager(arbiter);

            var candidate = manager.TeachAlias("chuot quang cu", "PWR-24V-01", "Product", "Admin01");
            Assert.Equal(VerificationStatus.Verified, candidate.Status);

            bool revoked = manager.RevokeCandidate(candidate.Id, "Wrong classification", "Admin02");
            Assert.True(revoked);

            var testRes = manager.TestResolveAlias("chuot quang cu", "Product");
            Assert.False(testRes.IsResolved);
        }

        [Fact]
        public void ComputesAccurateKnowledgeStatistics()
        {
            var validator = new TestMasterCatalogValidator();
            var arbiter = new VerifiedKnowledgeArbiter(validator);
            var manager = new AdminKnowledgeManager(arbiter);

            manager.TeachAlias("alias1", "PWR-24V-01", "Product", "Admin01");
            manager.TeachAlias("alias2", "PLC-FX5U", "Product", "Admin01");
            arbiter.ProposeAlias("staged1", "CBL-ETH-05M", "Product", "User1", "Operator");
            arbiter.ProposeAlias("bad_ghost", "NON-EXISTENT", "Product", "User2", "Operator");

            var stats = manager.GetStatistics();
            Assert.Equal(4, stats.TotalCandidates);
            Assert.Equal(2, stats.VerifiedCount);
            Assert.Equal(1, stats.StagedCount);
            Assert.Equal(1, stats.QuarantinedCount);
            Assert.Equal(0, stats.RevokedCount);
            Assert.True(stats.TotalEvidenceCount >= 4);
        }

        [Fact]
        public void ExportAndImportSnapshotPreservesEntireKnowledgeBase()
        {
            var validator = new TestMasterCatalogValidator();
            var arbiter = new VerifiedKnowledgeArbiter(validator);
            var manager = new AdminKnowledgeManager(arbiter);

            manager.TeachAlias("nguon to ong", "PWR-24V-01", "Product", "Admin01");
            manager.TeachRule("DefaultWarehouse", "Kho 01", "Logistics", "Admin01");

            using var ms = new MemoryStream();
            manager.ExportSnapshot(ms);

            ms.Position = 0;
            var reloadedArbiter = new VerifiedKnowledgeArbiter(validator);
            var reloadedManager = new AdminKnowledgeManager(reloadedArbiter);
            reloadedManager.ImportSnapshot(ms);

            var stats = reloadedManager.GetStatistics();
            Assert.Equal(2, stats.VerifiedCount);

            var res = reloadedManager.TestResolveAlias("nguon to ong", "Product");
            Assert.True(res.IsResolved);
            Assert.Equal("PWR-24V-01", res.TargetCode);
        }
    }
}
