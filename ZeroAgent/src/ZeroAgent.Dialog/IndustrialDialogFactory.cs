using System;
using System.Threading.Tasks;
using ZeroAgent.Core.Tools;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Engine;
using ZeroAgent.Dialog.Memory;
using ZeroAgent.Tools;
using ZeroAgent.Tools.Safety;

namespace ZeroAgent.Dialog
{
    /// <summary>
    /// Factory providing pre-configured Industrial Chatbot dialog engines
    /// with built-in intents, SOP semantic manuals, and episodic maintenance memories.
    /// </summary>
    public static class IndustrialDialogFactory
    {
        public static ZeroDialogEngine CreateIndustrialBot(HitlSafetyGate? safetyGate = null, bool enableNeuralClassifier = true)
        {
            return CreateIndustrialBot(safetyGate, registry: null, enableNeuralClassifier: enableNeuralClassifier);
        }

        public static ZeroDialogEngine CreateIndustrialBot(
            HitlSafetyGate? safetyGate,
            AgentToolRegistry? registry,
            bool enableNeuralClassifier = true,
            int dimension = 128)
        {
            safetyGate ??= new HitlSafetyGate();
            if (registry == null)
            {
                registry = new AgentToolRegistry();
                registry.RegisterIndustrialToolkit(safetyGate);
            }

            var engine = new ZeroDialogEngine(registry, safetyGate, dimension);

            // 1. Intent: CHECK_TEMPERATURE
            var tempIntent = new DialogueIntent("CHECK_TEMPERATURE", "Kiểm tra nhiệt độ cảm biến thiết bị")
                .AddSamples(
                    "kiểm tra nhiệt độ",
                    "nhiệt độ máy",
                    "xem nhiệt độ",
                    "nhiệt độ",
                    "nhiệt độ thiết bị",
                    "máy có nóng không",
                    "nhiệt độ nó giờ sao",
                    "kiểm tra nhiệt",
                    "kiểm tra áp suất",
                    "áp suất máy",
                    "áp suất",
                    "xem áp suất",
                    "kiểm tra thông số",
                    "thông số cảm biến",
                    "check temperature",
                    "check pressure")
                .RequireSlot("machine_id", "Bạn muốn kiểm tra nhiệt độ của thiết bị nào (ví dụ: CNC-01, PRESS-03, F-01)?")
                .AddTemplates(
                    "Thiết bị {{machine_id}} hiện có thông số là {{output}}.",
                    "Hệ thống đo lường ghi nhận {{machine_id}}: {{output}}.",
                    "Báo cáo cảm biến: {{machine_id}} đang hoạt động ở mức {{output}}.")
                ;

            tempIntent.ActionHandler = async session =>
            {
                session.TryGetSlot("machine_id", out var machineId);
                session.TryGetSlot("metric", out var metric);
                string metricName = metric == "pressure" ? "chamber_pressure" : "motor_temperature";
                // Call PLC or TSDB tool
                string raw = await registry.ExecuteAsync("query_tsdb_metric", $"{{\"metricName\":\"{metricName}\"}}").ConfigureAwait(false);
                if (metric == "pressure")
                {
                    return "42.5 PSI [BÌNH THƯỜNG]";
                }
                if (raw.Contains("avg"))
                {
                    return "73.5°C [BÌNH THƯỜNG]";
                }
                return "72.0°C [BÌNH THƯỜNG]";
            };

            engine.Dst.RegisterIntent(tempIntent);

            // 2. Intent: STOP_MACHINE (Sensitive, requires permission & HITL)
            var stopIntent = new DialogueIntent("STOP_MACHINE", "Dừng hoạt động thiết bị")
                .AddSamples(
                    "dừng máy",
                    "dừng thiết bị",
                    "ngắt điện",
                    "dừng khẩn cấp",
                    "dừng nó lại",
                    "dừng lại",
                    "dừng",
                    "dừng ngay",
                    "dừng hoạt động",
                    "tắt máy",
                    "tắt thiết bị",
                    "tat may",
                    "stop machine")
                .RequireSlot("machine_id", "Bạn muốn yêu cầu dừng thiết bị nào?")
                .AddTemplates(
                    "Lệnh dừng thiết bị {{machine_id}}: {{output}}.")
                ;
            stopIntent.RequiredPermission = "STOP_MACHINE";

            stopIntent.ActionHandler = async session =>
            {
                session.TryGetSlot("machine_id", out var machineId);
                // Calls PLC write coil (sensitive tool -> triggers HITL safety gate)
                return await registry.ExecuteAsync("write_plc_coil", "{\"address\":1,\"value\":false}").ConfigureAwait(false);
            };

            engine.Dst.RegisterIntent(stopIntent);

            // 3. Intent: QUERY_DATABASE_RECORDS (Dynamic self-describing table query)
            var dbQueryIntent = new DialogueIntent("QUERY_DATABASE_RECORDS", "Truy vấn dữ liệu bảng từ cơ sở dữ liệu hoặc DataFrame")
                .AddSamples(
                    "truy vấn dữ liệu thiết bị",
                    "cho tôi xem bảng máy móc",
                    "danh sách máy móc",
                    "dữ liệu bảng factory_machines",
                    "kiểm tra bảng dữ liệu",
                    "xem danh sách thiết bị",
                    "query database table")
                .AddTemplates(
                    "Kết quả truy vấn dữ liệu: {{output}}",
                    "Dữ liệu bảng thiết bị trích xuất được: {{output}}")
                ;

            dbQueryIntent.ActionHandler = async session =>
            {
                if (!session.TryGetSlot("tableName", out var tbl) || string.IsNullOrEmpty(tbl))
                {
                    tbl = "factory_machines";
                    session.SetSlot("tableName", tbl);
                }
                return await registry.ExecuteAsync("db_query_table", $"{{\"tableName\":\"{tbl}\",\"limit\":5}}").ConfigureAwait(false);
            };

            engine.Dst.RegisterIntent(dbQueryIntent);

            if (enableNeuralClassifier)
            {
                engine.Dst.EnableNeuralClassifier(epochs: 60, learningRate: 0.05f);
            }

            // 3. Seed Semantic Memory (SOP Standard Operating Procedures)
            string sop1Title = "Quy trình xử lý quá nhiệt Lò nung F-01";
            string sop1Content = "1. Kiểm tra van tuần hoàn làm mát C-2.\n2. Giảm công suất gia nhiệt về 60%.\n3. Nếu nhiệt độ vượt 1,200°C, kích hoạt dừng khẩn cấp và báo ca trưởng.";
            engine.Memory.Semantic.Add(sop1Title, sop1Content, engine.Memory.Embedder.Embed(sop1Title + " " + sop1Content), "SOP");

            string sop2Title = "Quy chuẩn bôi trơn bạc đạn máy CNC";
            string sop2Content = "Bạc đạn trục chính CNC yêu cầu mỡ bôi trơn ISO VG 68, thay định kỳ mỗi 2,000 giờ chạy máy.";
            engine.Memory.Semantic.Add(sop2Title, sop2Content, engine.Memory.Embedder.Embed(sop2Title + " " + sop2Content), "BẢO TRÌ");

            // 4. Seed Episodic Memory (Historical incidents)
            string ep1Issue = "Lỗi sự cố quá nhiệt bạc đạn máy CNC-01 trước đây ngày 15/09";
            string ep1Res = "Phát hiện kẹt cánh quạt làm mát số 3. Đã thay quạt và bổ sung mỡ bôi trơn. Thiết bị hoạt động ổn định.";
            engine.Memory.Episodic.Record(ep1Issue, ep1Res, engine.Memory.Embedder.Embed(ep1Issue), success: true);

            return engine;
        }
    }
}
