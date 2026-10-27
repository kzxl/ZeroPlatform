using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;
using ZeroAgent.Dialog.Embedding;
using ZeroAgent.Dialog.Learning;
using ZeroAgent.Dialog.Memory;

namespace ZeroAgent.Tests
{
    public sealed class TeacherMockLlmClient : ILlmClient
    {
        private readonly Func<string, string> _handler;

        public TeacherMockLlmClient(Func<string, string> handler)
        {
            _handler = handler;
        }

        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_handler(prompt));
        }
    }

    public sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public HttpRequestMessage? LastRequest { get; private set; }

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_handler(request));
        }
    }

    public class TeacherKnowledgeDistillerTests
    {
        [Fact]
        public async Task ExternalApiLlmClient_ParsesOpenAiResponse_AndSendsAuthHeader()
        {
            var fakeHandler = new FakeHttpMessageHandler(req =>
            {
                Assert.NotNull(req.Headers.Authorization);
                Assert.Equal("Bearer", req.Headers.Authorization!.Scheme);
                Assert.Equal("sk-test-token-123", req.Headers.Authorization!.Parameter);

                var jsonResponse = @"{
                    ""choices"": [
                        {
                            ""message"": {
                                ""role"": ""assistant"",
                                ""content"": ""Hello from Teacher LLM!""
                            }
                        }
                    ]
                }";

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                };
            });

            using var httpClient = new HttpClient(fakeHandler);
            using var client = new ExternalApiLlmClient(
                endpoint: "https://api.openai.com/v1/chat/completions",
                model: "gpt-4o-mini",
                apiKey: "sk-test-token-123",
                httpClient: httpClient);

            string result = await client.CompleteAsync("Ping Teacher");
            Assert.Equal("Hello from Teacher LLM!", result);
        }

        [Fact]
        public async Task ExternalApiLlmClient_ParsesOllamaResponseFormat()
        {
            var fakeHandler = new FakeHttpMessageHandler(req =>
            {
                var jsonResponse = @"{ ""response"": ""Ollama response text"" }";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                };
            });

            using var httpClient = new HttpClient(fakeHandler);
            using var client = new ExternalApiLlmClient(
                endpoint: "http://localhost:11434/api/generate",
                model: "qwen2.5-coder",
                apiKey: null,
                httpClient: httpClient);

            string result = await client.CompleteAsync("Hi Ollama");
            Assert.Equal("Ollama response text", result);
        }

        [Fact]
        public async Task ExternalApiLlmClient_ThrowsOnHttpError()
        {
            var fakeHandler = new FakeHttpMessageHandler(req =>
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Server busy or quota exceeded", Encoding.UTF8, "text/plain")
                };
            });

            using var httpClient = new HttpClient(fakeHandler);
            using var client = new ExternalApiLlmClient("https://api.example.com", "model-1", httpClient: httpClient);

            await Assert.ThrowsAsync<HttpRequestException>(() => client.CompleteAsync("Hello"));
        }

        [Fact]
        public async Task DistillDocumentAsync_ExtractsAndInjectsKnowledgeIntoManagerAndMemory()
        {
            var teacherJson = @"
            ```json
            {
              ""aliases"": [
                { ""alias"": ""nguon to ong"", ""targetEntityCode"": ""PWR-24V-01"" }
              ],
              ""rules"": [
                { ""ruleKey"": ""MaxSpindleSpeed"", ""proposedValue"": ""12000"" }
              ],
              ""faqItems"": [
                { ""title"": ""Bao tri spindle"", ""content"": ""Kiem tra dau boi tron dinh ky moi 500 gio van hanh."" }
              ],
              ""incidentEpisodes"": [
                { ""issue"": ""E04 Overheat spindle"", ""resolution"": ""Ve sinh bo loc gio va thay nuoc lam mat truoc khi khoi dong lai."" }
              ]
            }
            ```";

            var mockLlm = new TeacherMockLlmClient(prompt => teacherJson);

            var validator = new TestMasterCatalogValidator();
            var arbiter = new VerifiedKnowledgeArbiter(validator);
            var manager = new AdminKnowledgeManager(arbiter);
            var memory = new AgenticMemoryEngine(dimension: 128);

            var distiller = new TeacherKnowledgeDistiller(mockLlm, manager, memory);

            string rawDoc = "Tai lieu huong dan van hanh va bao tri may CNC phay Spindle cao toc nam 2026.";
            var result = await distiller.DistillDocumentAsync(rawDoc, category: "IndustrialMachinery");

            Assert.True(result.Success);
            Assert.Single(result.Aliases);
            Assert.Equal("nguon to ong", result.Aliases[0].Alias);
            Assert.Equal("PWR-24V-01", result.Aliases[0].TargetEntityCode);

            Assert.Single(result.Rules);
            Assert.Equal("MaxSpindleSpeed", result.Rules[0].RuleKey);

            Assert.Single(result.FaqItems);
            Assert.Equal("Bao tri spindle", result.FaqItems[0].Title);

            Assert.Single(result.IncidentEpisodes);
            Assert.Equal("E04 Overheat spindle", result.IncidentEpisodes[0].Issue);

            // 1. Verify AdminKnowledgeManager has verified alias
            var aliasRes = manager.TestResolveAlias("nguon to ong", "IndustrialMachinery");
            Assert.True(aliasRes.IsResolved);
            Assert.Equal("PWR-24V-01", aliasRes.TargetCode);

            // 2. Verify Semantic Memory query
            Assert.Equal(1, memory.Semantic.Count);
            var queryEmb = memory.Embedder.Embed("Bao tri spindle");
            var semanticMatches = memory.Semantic.Query(queryEmb.AsSpan(), topK: 3, minScore: 0.2f);
            Assert.NotEmpty(semanticMatches);
            Assert.Equal("Bao tri spindle", semanticMatches[0].Item.Title);

            // 3. Verify Episodic Memory query
            var epEmb = memory.Embedder.Embed("E04 Overheat spindle");
            var epMatches = memory.Episodic.Recall(epEmb.AsSpan(), topK: 3);
            Assert.NotEmpty(epMatches);
            Assert.Contains("Ve sinh bo loc gio", epMatches[0].Episode.Resolution);
        }

        [Fact]
        public async Task AuditTrajectoryAsync_ApprovedTrajectory_StoresLessonInEpisodicMemory()
        {
            var auditJson = @"{
              ""score"": 0.92,
              ""isApproved"": true,
              ""critique"": ""Excellent step sequence, verified safety valves before executing high-torque tool."",
              ""distilledLesson"": ""Luon kiem tra ap suat khi nen truoc khi kick hoat spindle de tranh loi E04.""
            }";

            var mockLlm = new TeacherMockLlmClient(prompt => auditJson);
            var memory = new AgenticMemoryEngine(dimension: 128);
            var distiller = new TeacherKnowledgeDistiller(mockLlm, memoryEngine: memory);

            var context = new AgentContext("Khoi dong truc chinh CNC");
            var trace = new List<AgentMessage>
            {
                new AgentMessage(AgentRole.User, "Khoi dong truc chinh CNC"),
                new AgentMessage(AgentRole.Assistant, "Kiem tra ap suat khi nen"),
                new AgentMessage(AgentRole.Tool, "Valves OK, 6.5 bar", "SensorTool"),
                new AgentMessage(AgentRole.Assistant, "Kich hoat relay khoi dong spindle")
            };
            var response = AgentResponse.Succeeded("Spindle da khoi dong thanh cong.", 4, TimeSpan.FromSeconds(2), trace);

            var auditResult = await distiller.AuditTrajectoryAsync(context, response, minApprovalScore: 0.8f);

            Assert.True(auditResult.Success);
            Assert.True(auditResult.IsApproved);
            Assert.Equal(0.92f, auditResult.Score, precision: 2);
            Assert.True(auditResult.LessonIngestedToMemory);
            Assert.NotNull(auditResult.DistilledLesson);

            // Verify recall from Episodic Memory
            var queryEmb = memory.Embedder.Embed("Khoi dong truc chinh tranh loi");
            var matches = memory.Episodic.Recall(queryEmb.AsSpan(), topK: 1);
            Assert.NotEmpty(matches);
            Assert.Contains("Luon kiem tra ap suat khi nen", matches[0].Episode.Resolution);
        }

        [Fact]
        public async Task AuditTrajectoryAsync_DisapprovedTrajectory_DoesNotStoreLesson()
        {
            var auditJson = @"{
              ""score"": 0.45,
              ""isApproved"": false,
              ""critique"": ""Did not check safety interlocks before sending emergency command."",
              ""distilledLesson"": ""Never bypass safety interlocks.""
            }";

            var mockLlm = new TeacherMockLlmClient(prompt => auditJson);
            var memory = new AgenticMemoryEngine(dimension: 128);
            var distiller = new TeacherKnowledgeDistiller(mockLlm, memoryEngine: memory);

            var context = new AgentContext("Bypass switch");
            var response = AgentResponse.Succeeded("Done", 1, TimeSpan.FromSeconds(1), new List<AgentMessage>());

            var auditResult = await distiller.AuditTrajectoryAsync(context, response, minApprovalScore: 0.8f);

            Assert.True(auditResult.Success);
            Assert.False(auditResult.IsApproved);
            Assert.False(auditResult.LessonIngestedToMemory);
            Assert.Equal(0, memory.Episodic.Count);
        }

        [Fact]
        public async Task GenerateSyntheticKnowledgeAsync_GeneratesAndBootstraps()
        {
            var syntheticJson = @"{
              ""aliases"": [
                { ""alias"": ""cuc sac 24v"", ""targetEntityCode"": ""PWR-24V-01"" }
              ],
              ""faqItems"": [
                { ""title"": ""Huong dan nap nguon"", ""content"": ""Cam giac 24v vao cong nguon phu."" }
              ],
              ""incidentEpisodes"": [
                { ""issue"": ""Nguon khong vao dien"", ""resolution"": ""Kiem tra cau chi F1 tren bo mach."" }
              ]
            }";

            var mockLlm = new TeacherMockLlmClient(prompt => syntheticJson);
            var validator = new TestMasterCatalogValidator();
            var arbiter = new VerifiedKnowledgeArbiter(validator);
            var manager = new AdminKnowledgeManager(arbiter);
            var memory = new AgenticMemoryEngine(dimension: 128);

            var distiller = new TeacherKnowledgeDistiller(mockLlm, manager, memory);

            var result = await distiller.GenerateSyntheticKnowledgeAsync("PowerSupply", count: 1, category: "Product");

            Assert.True(result.Success);
            Assert.Equal(3, result.GeneratedCount);
            Assert.Single(result.Aliases);
            Assert.Single(result.FaqItems);
            Assert.Single(result.Incidents);

            // Test alias resolution
            var res = manager.TestResolveAlias("cuc sac 24v", "Product");
            Assert.True(res.IsResolved);
            Assert.Equal("PWR-24V-01", res.TargetCode);
        }
    }
}
