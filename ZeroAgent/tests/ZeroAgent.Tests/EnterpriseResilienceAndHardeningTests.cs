using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Tools;
using ZeroAgent.Dialog;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Embedding;
using ZeroAgent.Dialog.Engine;
using ZeroAgent.Dialog.Memory;
using ZeroAgent.Tools;
using ZeroAgent.Tools.Mcp;
using ZeroAgent.Tools.Safety;

namespace ZeroAgent.Tests
{
    public class EnterpriseResilienceAndHardeningTests
    {
        [Fact]
        public void SemanticMemory_BiTemporalFiltering_HidesExpiredSopManuals()
        {
            var memory = new SemanticMemory(dimension: 128);
            var embedder = new LexicalSemanticEmbedder(dimension: 128);

            // Active SOP
            string activeTitle = "Quy trình bảo trì máy CNC mới 2026";
            memory.Add(
                activeTitle,
                "Bảo dưỡng định kỳ mỡ ISO VG 68",
                embedder.Embed(activeTitle),
                "SOP",
                validFromUtc: DateTime.UtcNow.AddDays(-10),
                validUntilUtc: DateTime.UtcNow.AddDays(365));

            // Obsolete SOP (expired 5 days ago)
            string obsoleteTitle = "Quy trình bảo trì máy CNC cũ 2020";
            memory.Add(
                obsoleteTitle,
                "Dùng dầu công nghiệp cũ",
                embedder.Embed(obsoleteTitle),
                "SOP",
                validFromUtc: DateTime.UtcNow.AddDays(-500),
                validUntilUtc: DateTime.UtcNow.AddDays(-5));

            var queryVec = embedder.Embed("Quy trình bảo trì máy CNC");

            // Query by default filters out expired items
            var matches = memory.Query(queryVec, topK: 5, minScore: 0.10f);
            Assert.Single(matches);
            Assert.Equal("Quy trình bảo trì máy CNC mới 2026", matches[0].Item.Title);

            // Historical audit query includes expired documents
            var auditMatches = memory.Query(queryVec, topK: 5, minScore: 0.10f, includeExpired: true);
            Assert.Equal(2, auditMatches.Count);
        }

        [Fact]
        public void ProfileMemory_PrunesStaleGuestProfiles_PreservesEnterpriseUsers()
        {
            var memory = new ProfileMemory();

            // Create registered enterprise user
            var employee = new UserProfile("emp_101", "Worker Bob", UserRole.Operator);
            memory.SaveProfile(employee);

            // Create 3 guest profiles
            var g1 = memory.GetOrCreateGuest("guest_temp_1");
            var g2 = memory.GetOrCreateGuest("guest_temp_2");
            var g3 = memory.GetOrCreateGuest("guest_temp_3");

            Assert.Equal(4, memory.Count);

            // Prune profiles older than negative timespan (simulating age)
            int evicted = memory.PruneStaleGuestProfiles(TimeSpan.FromSeconds(-1));

            // All 3 guests should be evicted, enterprise user must remain intact
            Assert.Equal(3, evicted);
            Assert.Equal(1, memory.Count);
            Assert.True(memory.TryGetProfile("emp_101", out _));
            Assert.False(memory.TryGetProfile("guest_temp_1", out _));
        }

        [Fact]
        public async Task FileCheckpointerSessionStore_PersistsStateAcrossProcessRestarts()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"zeroagent_checkpoints_{Guid.NewGuid():N}");
            try
            {
                var store1 = new FileCheckpointerSessionStore(tempDir);
                var bot1 = new ZeroDialogEngine(sessionStore: store1);

                string sessionId = "shift_session_persist_01";
                var session1 = bot1.GetOrCreateSession(sessionId);
                session1.State = SessionState.CollectingSlots;
                session1.PendingRequiredSlot = "machine_id";
                session1.SetSlot("department", "Workshop-B");
                store1.Save(session1);

                // Simulate new engine instance in separate process loading from disk
                var store2 = new FileCheckpointerSessionStore(tempDir);
                var bot2 = new ZeroDialogEngine(sessionStore: store2);

                var session2 = bot2.GetOrCreateSession(sessionId);
                Assert.Equal(SessionState.CollectingSlots, session2.State);
                Assert.Equal("machine_id", session2.PendingRequiredSlot);
                Assert.Equal("Workshop-B", session2.Slots["department"]);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task SemanticCache_VolatileTelemetryHardening_DoesNotStoreStaleLongTermReadings()
        {
            var safetyGate = new HitlSafetyGate { AutoApprove = true };
            var bot = IndustrialDialogFactory.CreateIndustrialBot(safetyGate);
            string sessionId = "test_volatile_cache_01";

            // Turn 1: Check temperature
            var r1 = await bot.ChatAsync(sessionId, "Kiểm tra nhiệt độ máy CNC-01");
            Assert.Equal(SessionState.Completed, r1.State);
            Assert.Contains("CNC-01", r1.Text);

            // Turn 2: State mutating command STOP_MACHINE must never be cached
            var rStop = await bot.ChatAsync(sessionId, "Dừng máy CNC-01", new UserProfile("admin_01", "Admin", UserRole.Supervisor));
            Assert.Equal(SessionState.Completed, rStop.State);

            float[] stopVec = bot.Memory.Embedder.Embed("Dừng máy CNC-01");
            Assert.False(bot.Memory.ResponseCache.TryGet(stopVec, "Dừng máy CNC-01", 0.95f, out _));
        }

        [Fact]
        public void McpToolExporter_ExportsAllIndustrialTools_ToStandardMcpFormat()
        {
            var registry = new AgentToolRegistry();
            registry.RegisterIndustrialToolkit(new HitlSafetyGate());

            var descriptors = McpToolExporter.ExportTools(registry);
            Assert.NotEmpty(descriptors);

            string mcpJson = McpToolExporter.ToMcpJson(registry);
            Assert.Contains("\"tools\":", mcpJson);
            Assert.Contains("query_tsdb_metric", mcpJson);
            Assert.Contains("write_plc_coil", mcpJson);
            Assert.Contains("db_query_table", mcpJson);
        }
    }
}
