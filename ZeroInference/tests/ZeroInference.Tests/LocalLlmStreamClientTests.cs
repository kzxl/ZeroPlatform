using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroInference.Core.Clients;

namespace ZeroInference.Tests
{
    public class LocalLlmStreamClientTests
    {
        [Fact]
        public void ExtractTokenFromStreamLine_OllamaResponse_ReturnsToken()
        {
            string line = "{\"model\":\"llama3\",\"response\":\"Hello \",\"done\":false}";
            string? token = LocalLlmStreamClient.ExtractTokenFromStreamLine(line, LlmServerProtocol.Ollama);
            Assert.Equal("Hello ", token);
        }

        [Fact]
        public void ExtractTokenFromStreamLine_OllamaChatMessage_ReturnsToken()
        {
            string line = "{\"model\":\"llama3\",\"message\":{\"role\":\"assistant\",\"content\":\"world\"},\"done\":false}";
            string? token = LocalLlmStreamClient.ExtractTokenFromStreamLine(line, LlmServerProtocol.Ollama);
            Assert.Equal("world", token);
        }

        [Fact]
        public void ExtractTokenFromStreamLine_OpenAiSseDelta_ReturnsToken()
        {
            string line = "data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"!\"},\"finish_reason\":null}]}";
            string? token = LocalLlmStreamClient.ExtractTokenFromStreamLine(line, LlmServerProtocol.OpenAiCompatible);
            Assert.Equal("!", token);
        }

        [Fact]
        public void ExtractTokenFromStreamLine_OpenAiDone_ReturnsNull()
        {
            string line = "data: [DONE]";
            string? token = LocalLlmStreamClient.ExtractTokenFromStreamLine(line, LlmServerProtocol.OpenAiCompatible);
            Assert.Null(token);
        }

        [Fact]
        public void BuildRequestPayload_Ollama_ContainsExpectedProperties()
        {
            using var client = new LocalLlmStreamClient(
                endpoint: "http://localhost:11434/api/generate",
                modelName: "qwen2.5:latest",
                protocol: LlmServerProtocol.Ollama);

            string payload = client.BuildRequestPayload("What is zero?", "You are concise.", stream: true);

            Assert.Contains("\"model\":\"qwen2.5:latest\"", payload);
            Assert.Contains("\"prompt\":\"What is zero?\"", payload);
            Assert.Contains("\"system\":\"You are concise.\"", payload);
            Assert.Contains("\"stream\":true", payload);
        }

        [Fact]
        public void BuildRequestPayload_OpenAi_ContainsMessages()
        {
            using var client = new LocalLlmStreamClient(
                endpoint: "http://localhost:1234/v1/chat/completions",
                modelName: "local-model",
                protocol: LlmServerProtocol.OpenAiCompatible);

            string payload = client.BuildRequestPayload("Hi", null, stream: true);

            Assert.Contains("\"model\":\"local-model\"", payload);
            Assert.Contains("\"messages\":", payload);
            Assert.Contains("\"role\":\"user\"", payload);
            Assert.Contains("\"content\":\"Hi\"", payload);
        }

        [Fact]
        public async Task StreamGenerateAsync_WithMockHandler_StreamsCorrectTokens()
        {
            var sseContent =
                "data: {\"choices\":[{\"delta\":{\"content\":\"Zero\"}}]}\n\n" +
                "data: {\"choices\":[{\"delta\":{\"content\":\"Platform\"}}]}\n\n" +
                "data: [DONE]\n\n";

            var handler = new MockHttpMessageHandler(sseContent);
            var httpClient = new HttpClient(handler);

            using var client = new LocalLlmStreamClient(
                endpoint: "http://localhost:1234/v1/chat/completions",
                modelName: "local-model",
                protocol: LlmServerProtocol.OpenAiCompatible,
                httpClient: httpClient);

            var sb = new StringBuilder();
            await client.StreamGenerateAsync("Test", token => sb.Append(token));

            Assert.Equal("ZeroPlatform", sb.ToString());
        }

        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly string _responseContent;

            public MockHttpMessageHandler(string responseContent)
            {
                _responseContent = responseContent;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_responseContent, Encoding.UTF8, "text/event-stream")
                };
                return Task.FromResult(response);
            }
        }
    }
}
