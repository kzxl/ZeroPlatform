using System;
using System.IO;
using Xunit;
using ZeroStorage.Core.Sync;

namespace ZeroStorage.Tests
{
    public class StoreAndForwardSyncTests : IDisposable
    {
        private readonly string _testDir;

        public StoreAndForwardSyncTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "zs_sync_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                    Directory.Delete(_testDir, true);
            }
            catch { }
        }

        [Fact]
        public void StoreAndForward_InitialState_DefaultsToZero()
        {
            var sync = new StoreAndForwardSync(_testDir);
            var status = sync.GetStatus(0);

            Assert.Equal(0, status.PendingSegmentCount);
            Assert.Equal(0, status.SyncedSegmentCount);
            Assert.Equal(0, status.PendingWalBytes);
            Assert.Equal(0, status.LastWalAckOffset);
            Assert.Null(status.LastSyncTimeUtc);
        }

        [Fact]
        public void StoreAndForward_PendingSegments_DiscoversUnacknowledgedFiles()
        {
            string segDir = Path.Combine(_testDir, "segments");
            Directory.CreateDirectory(segDir);

            // Create fake segments
            File.WriteAllBytes(Path.Combine(segDir, "segment_000001.zts"), new byte[100]);
            File.WriteAllBytes(Path.Combine(segDir, "segment_000002.zts"), new byte[200]);
            File.WriteAllBytes(Path.Combine(segDir, "segment_000003.zts"), new byte[300]);

            var sync = new StoreAndForwardSync(_testDir);
            var pending = sync.GetPendingSegments();

            Assert.Equal(3, pending.Count);
            Assert.EndsWith("segment_000001.zts", pending[0]);
            Assert.EndsWith("segment_000002.zts", pending[1]);
            Assert.EndsWith("segment_000003.zts", pending[2]);

            // Acknowledge the first two segments
            sync.AcknowledgeSegment("segment_000001.zts");
            sync.AcknowledgeSegment(pending[1]); // pass full path

            var remainingPending = sync.GetPendingSegments();
            Assert.Single(remainingPending);
            Assert.EndsWith("segment_000003.zts", remainingPending[0]);

            var status = sync.GetStatus(0);
            Assert.Equal(1, status.PendingSegmentCount);
            Assert.Equal(2, status.SyncedSegmentCount);
            Assert.NotNull(status.LastSyncTimeUtc);
        }

        [Fact]
        public void StoreAndForward_WalRangeTracking_ComputesDeltaAndAdvances()
        {
            var sync = new StoreAndForwardSync(_testDir);

            // WAL file is at 1024 bytes
            bool hasPending = sync.TryGetPendingWalRange(1024, out long fromOffset, out long length);
            Assert.True(hasPending);
            Assert.Equal(0, fromOffset);
            Assert.Equal(1024, length);

            // Acknowledge first 512 bytes
            sync.AcknowledgeWalOffset(512);
            Assert.Equal(512, sync.LastWalAckOffset);

            // WAL grew to 2048 bytes
            hasPending = sync.TryGetPendingWalRange(2048, out fromOffset, out length);
            Assert.True(hasPending);
            Assert.Equal(512, fromOffset);
            Assert.Equal(1536, length);

            // Acknowledge to 2048
            sync.AcknowledgeWalOffset(2048);

            // No pending bytes
            hasPending = sync.TryGetPendingWalRange(2048, out fromOffset, out length);
            Assert.False(hasPending);
            Assert.Equal(2048, fromOffset);
            Assert.Equal(0, length);
        }

        [Fact]
        public void StoreAndForward_PersistenceAndRestart_RecoversStateWithCrcValidation()
        {
            string segDir = Path.Combine(_testDir, "segments");
            Directory.CreateDirectory(segDir);
            File.WriteAllBytes(Path.Combine(segDir, "segment_000001.zts"), new byte[100]);
            File.WriteAllBytes(Path.Combine(segDir, "segment_000002.zts"), new byte[100]);

            // Phase 1: Initialize and set state
            {
                var sync1 = new StoreAndForwardSync(_testDir);
                sync1.AcknowledgeSegment("segment_000001.zts");
                sync1.AcknowledgeWalOffset(4096);
            }

            // Phase 2: Simulate process crash/restart by creating a brand new instance on same dir
            {
                var sync2 = new StoreAndForwardSync(_testDir);
                Assert.Equal(4096, sync2.LastWalAckOffset);
                Assert.NotNull(sync2.LastSyncTimeUtc);

                var pending = sync2.GetPendingSegments();
                Assert.Single(pending);
                Assert.EndsWith("segment_000002.zts", pending[0]);

                var status = sync2.GetStatus(5000);
                Assert.Equal(1, status.PendingSegmentCount);
                Assert.Equal(1, status.SyncedSegmentCount);
                Assert.Equal(904, status.PendingWalBytes); // 5000 - 4096 = 904
            }
        }
    }
}
