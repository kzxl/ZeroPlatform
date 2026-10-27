using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Engine;
using ZeroAgent.Dialog;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Engine;
using ZeroAgent.Dialog.Generator;

namespace ZeroAgent.Tests
{
    public class GenerativeNlgTests
    {
        private sealed class MockLlmClient : ILlmClient
        {
            private readonly Func<string, string> _handler;

            public string? LastPromptReceived { get; private set; }

            public MockLlmClient(Func<string, string> handler)
            {
                _handler = handler;
            }

            public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
            {
                LastPromptReceived = prompt;
                return Task.FromResult(_handler(prompt));
            }
        }

        [Fact]
        public async Task GenerativeNlgSynthesizer_GeneratesFluentResponse_WhenLlmSucceeds()
        {
            var mockLlm = new MockLlmClient(prompt =>
            {
                Assert.Contains("[DỮ LIỆU SỰ THẬT TỪ HỆ THỐNG]:", prompt);
                Assert.Contains("machine_id: CNC-01", prompt);
                Assert.Contains("temperature: 73.5°C", prompt);
                return "Dạ thưa anh, hệ thống ghi nhận máy CNC-01 đang hoạt động ổn định ở nhiệt độ 73.5°C ạ.";
            });

            var synthesizer = new GenerativeNlgSynthesizer(mockLlm);
            var facts = new Dictionary<string, string>
            {
                ["machine_id"] = "CNC-01",
                ["temperature"] = "73.5°C"
            };

            var context = new NlgContext(
                userQuery: "nhiệt độ máy CNC-01 hiện tại ra sao?",
                intentName: "CHECK_TEMPERATURE",
                facts: facts,
                fallbackTemplates: new[] { "Thiết bị {{machine_id}} nhiệt độ là {{temperature}}." },
                fallbackDefault: "73.5°C");

            string result = await synthesizer.SynthesizeAsync(context);

            Assert.Equal("Dạ thưa anh, hệ thống ghi nhận máy CNC-01 đang hoạt động ổn định ở nhiệt độ 73.5°C ạ.", result);
            Assert.NotNull(mockLlm.LastPromptReceived);
        }

        [Fact]
        public async Task GenerativeNlgSynthesizer_FallsBackToTemplate_WhenLlmThrowsOrFails()
        {
            var failingLlm = new MockLlmClient(_ => throw new InvalidOperationException("API Gateway timeout"));
            var synthesizer = new GenerativeNlgSynthesizer(failingLlm);

            var facts = new Dictionary<string, string>
            {
                ["machine_id"] = "PRESS-03",
                ["output"] = "84.5°C"
            };

            var context = new NlgContext(
                userQuery: "kiểm tra máy ép 3",
                intentName: "CHECK_TEMPERATURE",
                facts: facts,
                fallbackTemplates: new[] { "Thiết bị {{machine_id}} hiện có thông số là {{output}}." },
                fallbackDefault: "84.5°C");

            // Should not throw, should gracefully fall back to deterministic template
            string result = await synthesizer.SynthesizeAsync(context);

            Assert.Equal("Thiết bị PRESS-03 hiện có thông số là 84.5°C.", result);
        }

        [Fact]
        public async Task ZeroDialogEngine_WithGenerativeNlg_EndToEnd_ProducesNaturalResponse()
        {
            var bot = IndustrialDialogFactory.CreateIndustrialBot(enableNeuralClassifier: true);

            var mockLlm = new MockLlmClient(prompt =>
            {
                Assert.Contains("CNC-01", prompt);
                return "Chào anh, em vừa kiểm tra cảm biến của máy CNC-01 thì thấy nhiệt độ đang là 73.5°C, mọi thông số đều an toàn nhé anh.";
            });

            // Enable Generative NLG
            bot.UseGenerativeNlg(mockLlm);

            var response = await bot.ChatAsync("session_nlg_01", "kiểm tra nhiệt độ máy CNC-01");

            Assert.Equal(SessionState.Completed, response.State);
            Assert.Equal("CHECK_TEMPERATURE", response.IntentName);
            Assert.Equal("Chào anh, em vừa kiểm tra cảm biến của máy CNC-01 thì thấy nhiệt độ đang là 73.5°C, mọi thông số đều an toàn nhé anh.", response.Text);
        }

        [Fact]
        public async Task ZeroDialogEngine_DefaultMode_StillUsesDeterministicTemplates()
        {
            // Create default bot without generative NLG
            var bot = IndustrialDialogFactory.CreateIndustrialBot(enableNeuralClassifier: true);

            var response = await bot.ChatAsync("session_default_01", "kiểm tra nhiệt độ máy CNC-01");

            Assert.Equal(SessionState.Completed, response.State);
            Assert.Equal("CHECK_TEMPERATURE", response.IntentName);
            // Default template format
            Assert.Contains("CNC-01", response.Text);
            Assert.Contains("[BÌNH THƯỜNG]", response.Text);
        }

        [Fact]
        public async Task ZeroDialogEngine_LiveLocalGemma_SynthesizesNaturalResponse()
        {
            // Verify live endpoint reachability first
            using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            try
            {
                var ping = await httpClient.GetAsync("http://192.168.10.7:1234/v1/models");
                if (!ping.IsSuccessStatusCode) return; // Skip if local gateway is offline
            }
            catch
            {
                return; // Gateway offline in environment
            }

            var gemmaClient = new ExternalApiLlmClient(
                endpoint: "http://192.168.10.7:1234/v1/chat/completions",
                model: "google/gemma-4-e4b",
                apiKey: "ollama");

            var bot = IndustrialDialogFactory.CreateIndustrialBot(enableNeuralClassifier: true);
            bot.UseGenerativeNlg(gemmaClient);

            var response = await bot.ChatAsync("session_live_gemma_01", "kiểm tra nhiệt độ máy CNC-01");

            Assert.Equal(SessionState.Completed, response.State);
            Assert.Equal("CHECK_TEMPERATURE", response.IntentName);
            Assert.False(string.IsNullOrWhiteSpace(response.Text));
            Assert.Contains("73.5", response.Text);
        }
    }
}
