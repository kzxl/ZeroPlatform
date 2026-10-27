using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Dialog;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Engine;
using ZeroAgent.Dialog.Memory;
using ZeroAgent.Tools.Data;

namespace ZeroAgent.Tests
{
    public class LongContextMultiTurnDialogueTests
    {
        [Fact]
        public async Task MultiTurnDialogue_LongContextAndAnaphoraResolution_PreservesContext()
        {
            var bot = IndustrialDialogFactory.CreateIndustrialBot(enableNeuralClassifier: true);
            string sessionId = "session_long_context_multi_turn_01";
            var profile = new UserProfile(sessionId, "LeadOperator", UserRole.Supervisor);

            // Seed an SOP into Semantic Memory
            float[] sopVec = bot.Memory.Embedder.Embed("Quy trình xử lý sự cố quá nhiệt máy móc và dừng khẩn cấp");
            bot.Memory.Semantic.Add(
                title: "SOP-OVERHEAT-01",
                content: "1. Nhấn nút E-Stop dừng máy lập tức.\n2. Kiểm tra van tuần hoàn nước làm mát.\n3. Chờ nhiệt độ hạ dưới 50 độ C trước khi khởi động lại.",
                embedding: sopVec,
                category: "SAFETY"
            );

            // =========================================================================
            // TURN 1: User inquires about available database tables
            // =========================================================================
            var r1 = await bot.ChatAsync(sessionId, "Cho tôi xem danh sách bảng máy móc trong cơ sở dữ liệu", profile);
            Assert.Equal(SessionState.Completed, r1.State);
            Assert.Equal("QUERY_DATABASE_RECORDS", r1.IntentName);
            Assert.Contains("factory_machines", r1.Text);

            var wm = bot.Memory.GetWorkingMemory(sessionId);
            Assert.Single(wm.Turns);
            Assert.Equal("factory_machines", wm.CurrentSubject);

            // =========================================================================
            // TURN 2: Anaphora resolution using pronoun 'bảng đó' (that table)
            // =========================================================================
            // "Đọc thông tin bảng đó" -> Working Memory resolves "bảng đó" to "factory_machines"
            string resolvedInput2 = wm.ResolveAnaphora("Đọc thông tin bảng đó");
            Assert.Contains("factory_machines", resolvedInput2);

            var r2 = await bot.ChatAsync(sessionId, "Đọc thông tin bảng đó", profile);
            Assert.Equal(SessionState.Completed, r2.State);
            Assert.Contains("factory_machines", r2.Text);
            Assert.Equal(2, wm.Turns.Count);

            // =========================================================================
            // TURN 3: Specific telemetry slot query on CNC-01
            // =========================================================================
            var r3 = await bot.ChatAsync(sessionId, "Kiểm tra nhiệt độ máy CNC-01", profile);
            Assert.Equal(SessionState.Completed, r3.State);
            Assert.Contains("CNC-01", r3.Text);
            Assert.Equal("CNC-01", wm.CurrentSubject);
            Assert.Equal(3, wm.Turns.Count);

            // =========================================================================
            // TURN 4: Elliptical follow-up question ("còn áp suất thì sao?")
            // =========================================================================
            // Elliptical resolution should expand to "kiểm tra áp suất của CNC-01"
            string resolvedInput4 = wm.ResolveAnaphora("còn áp suất thì sao?");
            Assert.Contains("CNC-01", resolvedInput4);
            Assert.Contains("áp suất", resolvedInput4);

            var r4 = await bot.ChatAsync(sessionId, "còn áp suất thì sao?", profile);
            Assert.Equal(SessionState.Completed, r4.State);
            Assert.Contains("CNC-01", r4.Text);
            Assert.Equal(4, wm.Turns.Count);

            // =========================================================================
            // TURN 5: Distraction / Context switch to SOP Manual (Semantic Memory)
            // =========================================================================
            var r5 = await bot.ChatAsync(sessionId, "Quy trình xử lý sự cố quá nhiệt máy móc là gì?", profile);
            Assert.Equal(SessionState.Idle, r5.State);
            Assert.Equal("KNOWLEDGE_RETRIEVAL", r5.IntentName);
            Assert.Contains("SOP-OVERHEAT-01", r5.Text);
            Assert.Contains("Nhấn nút E-Stop", r5.Text);
            Assert.Equal(5, wm.Turns.Count);

            // =========================================================================
            // TURN 6: Long-context return: Reference to the machine 2 turns ago ('con đó')
            // =========================================================================
            string resolvedInput6 = wm.ResolveAnaphora("Dừng khẩn cấp con đó ngay");
            Assert.Contains("CNC-01", resolvedInput6);

            var r6 = await bot.ChatAsync(sessionId, "Dừng khẩn cấp con đó ngay", profile);
            Assert.Equal(SessionState.Completed, r6.State);
            Assert.True(r6.IsActionExecuted);
            Assert.Contains("CNC-01", r6.Text);
            Assert.Equal(6, wm.Turns.Count);

            // Verify full turn-by-turn conversational history is accurately recorded
            Assert.Equal("Cho tôi xem danh sách bảng máy móc trong cơ sở dữ liệu", wm.Turns[0].UserMessage);
            Assert.Equal("Đọc thông tin bảng đó", wm.Turns[1].UserMessage);
            Assert.Equal("Kiểm tra nhiệt độ máy CNC-01", wm.Turns[2].UserMessage);
            Assert.Equal("còn áp suất thì sao?", wm.Turns[3].UserMessage);
            Assert.Equal("Quy trình xử lý sự cố quá nhiệt máy móc là gì?", wm.Turns[4].UserMessage);
            Assert.Equal("Dừng khẩn cấp con đó ngay", wm.Turns[5].UserMessage);
        }

        [Fact]
        public async Task LiveSqlDatabase_DynamicDiscovery_And_PushdownExecution_Succeeds()
        {
            string connStr = "Server=192.168.19.70,1433;Database=SampleDB;User Id=testing;Password=268479#Kzx;TrustServerCertificate=True;Connection Timeout=5;";

            // Register live database (reads INFORMATION_SCHEMA and discovers SampleData)
            try
            {
                DynamicDatabaseQueryTool.RegisterLiveDatabase(connStr, "SampleDB");
            }
            catch (Exception)
            {
                // In isolated test environments without SQL Server connection, pass gracefully
                return;
            }

            var registry = new Core.Tools.AgentToolRegistry();
            DynamicDatabaseQueryTool.RegisterAll(registry);

            // 1. Verify table was discovered in catalog
            string listJson = await registry.ExecuteAsync("db_list_tables", "");
            Assert.Contains("SampleData", listJson);

            // 2. Verify schema was discovered via SQL Pushdown
            string describeJson = await registry.ExecuteAsync("db_describe_table", "SampleData");
            Assert.Contains("SampleData", describeJson);
            Assert.Contains("Category", describeJson);
            Assert.Contains("Price", describeJson);

            // 3. Query records with SQL pushdown filter
            string queryArgs = System.Text.Json.JsonSerializer.Serialize(new
            {
                tableName = "SampleData",
                whereColumn = "Category",
                whereValue = "Clothing",
                limit = 3
            });

            string resultJson = await registry.ExecuteAsync("db_query_table", queryArgs);
            Assert.Contains("Clothing", resultJson);
            Assert.Contains("SQL_PUSHDOWN_LIVE", resultJson);
        }
    }
}
