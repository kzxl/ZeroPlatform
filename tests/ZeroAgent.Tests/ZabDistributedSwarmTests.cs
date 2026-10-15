using System;
using System.IO;
using Xunit;
using ZeroAgent.Core.Database;
using ZeroAgent.Core.Reasoning.Cognitive;

namespace ZeroAgent.Tests
{
    public class ZabDistributedSwarmTests : IDisposable
    {
        private readonly string _tempDir;

        public ZabDistributedSwarmTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ZabSwarmTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch { }
        }

        [Fact]
        public void ZabWalReplication_LeaderToFollowerSync_PreservesAllMutations()
        {
            string leaderPath = Path.Combine(_tempDir, "leader.zab");
            string followerPath = Path.Combine(_tempDir, "follower.zab");

            using (var leader = ZabDatabase.CreateNew(leaderPath))
            using (var follower = ZabDatabase.CreateNew(followerPath))
            {
                // Leader commits several mutations to WAL
                leader.AddKnowledge("SWARM_PEER_COUNT", "16", "Cluster");
                leader.AddKnowledge("LEADER_ELECTION_EPOCH", "42", "Consensus");

                float[] vec = new float[64];
                vec[0] = 1.0f;
                leader.StoreVector("cluster_centroid", vec);

                leader.AddReflexion("QuorumLoss", "NetworkPartition", "MaintainSplitBrainGuard");

                // Extract incremental replication package from Leader's WAL
                var package = ZabWalReplicator.ExtractIncrementalPackage(leader.Wal.WalFilePath, 0, out long nextOffset);
                Assert.NotEmpty(package.SerializedFrames);
                Assert.True(nextOffset > 0);

                // Simulate network transport: Serialize -> Wire -> Deserialize with CRC32C check
                byte[] wireData = package.Serialize();
                var receivedPackage = ZabWalReplicationPackage.Deserialize(wireData);
                Assert.Equal(package.SerializedFrames.Count, receivedPackage.SerializedFrames.Count);

                // Ingest into Follower replica
                int applied = ZabWalReplicator.IngestReplicationPackage(follower, receivedPackage);
                Assert.Equal(package.SerializedFrames.Count, applied);

                // Verify Follower replica state matches Leader
                var k1 = follower.FindKnowledge("SWARM_PEER_COUNT");
                Assert.NotNull(k1);
                Assert.Equal("16", k1!.Value);

                var k2 = follower.FindKnowledge("LEADER_ELECTION_EPOCH");
                Assert.NotNull(k2);
                Assert.Equal("42", k2!.Value);

                Assert.Single(follower.Vectors);
                Assert.Equal("cluster_centroid", follower.Vectors[0].Label);

                var reflexions = follower.RecallReflexions("NetworkPartition");
                Assert.Single(reflexions);
                Assert.Equal("MaintainSplitBrainGuard", reflexions[0].Lesson);
            }
        }

        [Fact]
        public void ZabWalReplication_CorruptedPackageInTransit_ThrowsInvalidDataException()
        {
            var package = new ZabWalReplicationPackage
            {
                FromOffset = 0,
                ToOffset = 100,
                SerializedFrames = { new byte[] { 1, 2, 3, 4, 5 } }
            };

            byte[] wireData = package.Serialize();

            // Tamper 1 byte in payload to simulate wire bitflip
            wireData[10] ^= 0xFF;

            Assert.Throws<InvalidDataException>(() =>
            {
                ZabWalReplicationPackage.Deserialize(wireData);
            });
        }

        [Fact]
        public void ZabFederatedAveraging_AggregatesDecentralizedSwarmWeightsAccurately()
        {
            const int dim = 64;
            var classes = new[] { "ActionAlpha", "ActionBeta" };

            // Node 1 learns strong signal for ActionAlpha on feature 0
            var policy1 = new ZabNeuralPolicy("Node1Policy", dim, classes);
            policy1.WeightsInt8[0] = 100;  // Class 0, Dim 0
            policy1.WeightsInt8[dim] = -50; // Class 1, Dim 0
            policy1.Biases[0] = 0.5f;

            // Node 2 learns similar signal for ActionAlpha on feature 0
            var policy2 = new ZabNeuralPolicy("Node2Policy", dim, classes);
            policy2.WeightsInt8[0] = 80;   // Class 0, Dim 0
            policy2.WeightsInt8[dim] = -30; // Class 1, Dim 0
            policy2.Biases[0] = 0.3f;

            // Node 3 learns moderate signal
            var policy3 = new ZabNeuralPolicy("Node3Policy", dim, classes);
            policy3.WeightsInt8[0] = 60;   // Class 0, Dim 0
            policy3.WeightsInt8[dim] = -10; // Class 1, Dim 0
            policy3.Biases[0] = 0.4f;

            // Execute Federated Aggregation across 3 swarm nodes
            var swarmGlobal = ZabFederatedAveraging.Aggregate(
                new[] { policy1, policy2, policy3 },
                aggregatedModelName: "SwarmConsensusV1");

            Assert.Equal("SwarmConsensusV1", swarmGlobal.ModelName);
            Assert.Equal(dim, swarmGlobal.InputDim);
            Assert.Equal(2, swarmGlobal.OutputClasses.Count);

            // Verify averaged weights: (100 + 80 + 60) / 3 = 80
            Assert.Equal((sbyte)80, swarmGlobal.WeightsInt8[0]);

            // Verify averaged weights: (-50 + -30 + -10) / 3 = -30
            Assert.Equal((sbyte)(-30), swarmGlobal.WeightsInt8[dim]);

            // Verify averaged bias: (0.5 + 0.3 + 0.4) / 3 = 0.4
            Assert.Equal(0.4f, swarmGlobal.Biases[0], precision: 3);

            // Test decision inference on global policy
            float[] query = new float[dim];
            query[0] = 1.0f;
            string? choice = swarmGlobal.PredictChoice(query, out float confidence);

            Assert.Equal("ActionAlpha", choice);
            Assert.True(confidence > 0.70f);
        }

        [Fact]
        public void ZabFederatedAveraging_AggregateByzantineRobust_RejectsMaliciousPoisonedNodes()
        {
            const int dim = 32;
            var classes = new[] { "SafeRoute", "HazardousOverride" };

            // 4 honest nodes agree on weight +60 for SafeRoute and -40 for HazardousOverride
            var honestNodes = new ZabNeuralPolicy[4];
            for (int i = 0; i < 4; i++)
            {
                honestNodes[i] = new ZabNeuralPolicy($"HonestNode{i}", dim, classes) { Scale = 0.05f };
                honestNodes[i].WeightsInt8[0] = 60;
                honestNodes[i].WeightsInt8[dim] = -40;
                honestNodes[i].Biases[0] = 0.5f;
                honestNodes[i].Biases[1] = -0.2f;
            }

            // 1 compromised Byzantine node injects maximal inverted weights (-127 / +127) and huge bias
            var byzantineNode = new ZabNeuralPolicy("ByzantineAttacker", dim, classes) { Scale = 999.0f };
            byzantineNode.WeightsInt8[0] = -127;
            byzantineNode.WeightsInt8[dim] = 127;
            byzantineNode.Biases[0] = -500.0f;
            byzantineNode.Biases[1] = 500.0f;

            var allPolicies = new[]
            {
                honestNodes[0],
                honestNodes[1],
                byzantineNode,
                honestNodes[2],
                honestNodes[3]
            };

            var robustGlobal = ZabFederatedAveraging.AggregateByzantineRobust(allPolicies, trimFraction: 0.2f);

            // Trimmed mean over 5 nodes (trimCount = 1) discards the Byzantine outlier completely!
            Assert.Equal((sbyte)60, robustGlobal.WeightsInt8[0]);
            Assert.Equal((sbyte)(-40), robustGlobal.WeightsInt8[dim]);
            Assert.Equal(0.5f, robustGlobal.Biases[0], precision: 3);
            Assert.Equal(-0.2f, robustGlobal.Biases[1], precision: 3);
            Assert.Equal(0.05f, robustGlobal.Scale, precision: 4);

            float[] query = new float[dim];
            query[0] = 1.0f;
            string predicted = robustGlobal.PredictChoice(query, out float conf);
            Assert.Equal("SafeRoute", predicted);
            Assert.True(conf > 0.6f);
        }
    }
}
