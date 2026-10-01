using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ZeroAgent.Core.Tools;
using ZeroAgent.Dialog;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Engine;
using ZeroAgent.Dialog.Memory;
using ZeroAgent.Tools;
using ZeroAgent.Tools.Data;
using ZeroAgent.Tools.Industrial;
using ZeroAgent.Tools.Safety;

namespace ZeroPlatform.Samples.ChatBot
{
    public sealed class ToolInvocationLog
    {
        public DateTime Timestamp { get; } = DateTime.Now;
        public string ToolName { get; }
        public string Arguments { get; }
        public string Result { get; }
        public bool IsHazardous { get; }

        public ToolInvocationLog(string toolName, string arguments, string result, bool isHazardous = false)
        {
            ToolName = toolName;
            Arguments = arguments;
            Result = result;
            IsHazardous = isHazardous;
        }
    }

    /// <summary>
    /// Encapsulates the runtime environment for the ZeroPlatform Chatbot Demo,
    /// unifying Dialog Engine, HITL Safety Gate, Agentic Memory, and Tool Calling.
    /// </summary>
    public sealed class ChatBotEnvironment
    {
        public ZeroDialogEngine Engine { get; }
        public HitlSafetyGate SafetyGate { get; }
        public AgentToolRegistry Registry { get; }
        public List<ToolInvocationLog> ToolLogs { get; } = new List<ToolInvocationLog>();

        public event EventHandler<ToolInvocationLog>? ToolExecuted;
        public event EventHandler<HitlApprovalRequest>? HitlIntercepted;

        public ChatBotEnvironment()
        {
            SafetyGate = new HitlSafetyGate();
            Registry = new AgentToolRegistry();

            // 1. Register Core Industrial Toolkit & Safety Interception
            Registry.RegisterIndustrialToolkit(SafetyGate);

            // 2. Register Dynamic Self-Describing DataFrames & SQL
            DynamicDatabaseQueryTool.RegisterAll(Registry);

            // 3. Register Custom Observable Tool Wrapper for Logging
            WrapToolsForTelemetry();

            // 4. Initialize Industrial Dialogue Engine
            Engine = IndustrialDialogFactory.CreateIndustrialBot(SafetyGate, Registry, enableNeuralClassifier: true);

            // 5. Enrich with additional Production Intents
            EnrichDialogueIntents();

            // 6. Enrich Multi-Tier Agentic Memory (SOPs & Past Incidents)
            SeedAgenticMemory();
        }

        private void WrapToolsForTelemetry()
        {
            // Register an explicit NL-to-SQL dynamic builder tool
            Registry.Register(new AgentTool("execute_nl_sql_analytics",
                "Phân tích dữ liệu tự động bằng câu hỏi tự nhiên sang SQL.",
                "query: string",
                async arg =>
                {
                    var list = await Registry.ExecuteAsync("db_list_tables", "").ConfigureAwait(false);
                    var records = await Registry.ExecuteAsync("db_query_table", "{\"tableName\":\"factory_machines\",\"limit\":5}").ConfigureAwait(false);
                    return $"[SQL Analytics Engine]: Danh mục bảng: {list}\n\nTop 5 thiết bị: {records}";
                }));
        }

        private void EnrichDialogueIntents()
        {
            // Intent: QUERY_ALARMS
            var alarmIntent = new DialogueIntent("QUERY_ALARMS", "Tra cứu cảnh báo hệ thống SCADA")
                .AddSamples(
                    "có cảnh báo gì không",
                    "cảnh báo hệ thống",
                    "kiểm tra cảnh báo",
                    "danh sách cảnh báo",
                    "báo động máy móc",
                    "có lỗi gì không",
                    "active alarms")
                .AddTemplates(
                    "Danh sách cảnh báo SCADA ghi nhận: {{output}}",
                    "Hệ thống kiểm tra cảnh báo: {{output}}")
                ;

            alarmIntent.ActionHandler = async session =>
            {
                var result = await Registry.ExecuteAsync("db_query_table", "{\"tableName\":\"factory_machines\",\"whereColumn\":\"status\",\"whereValue\":\"CRITICAL_ERROR\"}").ConfigureAwait(false);
                return result;
            };

            Engine.Dst.RegisterIntent(alarmIntent);

            // Intent: NL_SQL_ANALYTICS
            var sqlIntent = new DialogueIntent("NL_SQL_ANALYTICS", "Phân tích số liệu và truy vấn SQL tự động")
                .AddSamples(
                    "phân tích dữ liệu",
                    "truy vấn sql",
                    "thống kê thiết bị theo trạng thái",
                    "có bao nhiêu máy đang chạy",
                    "liệt kê thiết bị",
                    "máy nào có điểm sức khỏe dưới 80",
                    "run sql query")
                .AddTemplates(
                    "Phân tích cơ sở dữ liệu hoàn tất:\n{{output}}")
                ;

            sqlIntent.ActionHandler = async session =>
            {
                return await Registry.ExecuteAsync("execute_nl_sql_analytics", "factory_machines").ConfigureAwait(false);
            };

            Engine.Dst.RegisterIntent(sqlIntent);
        }

        private void SeedAgenticMemory()
        {
            // Semantic Memory: SOPs
            string sopAlarm = "Quy trình xử lý Cảnh báo Áp suất Thủy lực Máy PRESS-03";
            string sopAlarmContent = "1. Kiểm tra van an toàn thủy lực SV-101.\n2. Quan sát áp suất đồng hồ thứ cấp.\n3. Nếu vượt quá 250 Bar, ấn nút xả tải và ngắt bơm chính P-1.";
            Engine.Memory.Semantic.Add(sopAlarm, sopAlarmContent, Engine.Memory.Embedder.Embed(sopAlarm + " " + sopAlarmContent), "SOP-SCADA");

            // Episodic Memory: Past incident resolutions
            string epPress = "Sự cố tụt áp thủy lực máy PRESS-03 ca đêm ngày 20/09";
            string epPressRes = "Gioăng phớt xi lanh số 2 bị mòn gây rò rỉ dầu. Đã thay gioăng Viton chịu nhiệt và châm thêm 20L dầu thủy lực ISO 46.";
            Engine.Memory.Episodic.Record(epPress, epPressRes, Engine.Memory.Embedder.Embed(epPress), success: true);
        }

        public UserProfile CurrentUser { get; set; } = new UserProfile("op_supervisor_01", "Ca trưởng Nguyễn", UserRole.Supervisor);

        public async Task<(DialogResponse Response, TimeSpan Latency, string? RecognizedIntent, double Confidence)> SendMessageAsync(string sessionId, string message)
        {
            var sw = Stopwatch.StartNew();
            var response = await Engine.ChatAsync(sessionId, message, CurrentUser).ConfigureAwait(false);
            sw.Stop();

            var session = Engine.GetOrCreateSession(sessionId);
            string? intent = response.IntentName ?? session.CurrentIntent?.Name;
            double confidence = response.Confidence > 0 ? response.Confidence : 0.95;

            // Check if there are pending HITL requests
            var pending = SafetyGate.PendingRequests.FirstOrDefault();
            if (pending != null)
            {
                HitlIntercepted?.Invoke(this, pending);
            }

            return (response, sw.Elapsed, intent, confidence);
        }

        public void LogToolCall(string toolName, string args, string result, bool isHazardous = false)
        {
            var log = new ToolInvocationLog(toolName, args, result, isHazardous);
            lock (ToolLogs)
            {
                ToolLogs.Add(log);
            }
            ToolExecuted?.Invoke(this, log);
        }
    }
}
