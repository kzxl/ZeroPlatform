using System;
using System.Threading.Tasks;

namespace ZeroPlatform.Samples.ChatBot
{
    public static class ChatBotCli
    {
        public static async Task RunInteractiveAsync()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;

            var env = new ChatBotEnvironment();
            string sessionId = "cli-operator-session";

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine("       ZEROPLATFORM INDUSTRIAL AI COPILOT & CHATBOT CLI (TERMINAL MODE)");
            Console.WriteLine("================================================================================");
            Console.ResetColor();
            Console.WriteLine("Engine : Pure C# ZeroAgent Reflex NLU (Sub-5ms CPU)");
            Console.WriteLine("Memory : 4-Tier Agentic Memory (Working, Episodic, Semantic, Cache)");
            Console.WriteLine("Safety : Human-in-the-Loop (HITL) Safety Gate Active");
            Console.WriteLine("Data   : Columnar DataFrame & Dynamic SQL Analytics");
            Console.WriteLine("Type 'exit' or 'quit' to terminate. Type 'help' for sample queries.\n");

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\n[Operator]> ");
                Console.ResetColor();

                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) continue;

                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("Gợi ý các câu hỏi thử nghiệm:");
                    Console.WriteLine("  - Kiểm tra nhiệt độ máy CNC-01");
                    Console.WriteLine("  - Dừng máy CNC-01 (kích hoạt HITL Safety Gate)");
                    Console.WriteLine("  - Cho tôi xem bảng máy móc");
                    Console.WriteLine("  - Có cảnh báo gì không");
                    Console.WriteLine("  - Quy trình xử lý quá nhiệt Lò nung F-01");
                    Console.WriteLine("  - Nó có nóng không? (Anaphora resolution)");
                    Console.ResetColor();
                    continue;
                }

                var (response, latency, intent, confidence) = await env.SendMessageAsync(sessionId, input);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"[ZeroCopilot ({(int)latency.TotalMilliseconds}ms)]: ");
                Console.ResetColor();
                Console.WriteLine(response.Text);

                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"  [Telemetry] Intent: {intent ?? "NLU_REFLEX"} ({confidence * 100:F0}%) | State: {response.State}");
                Console.ResetColor();
            }
        }
    }
}
