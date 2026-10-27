using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Gateway;
using ZeroAgent.Dialog.Learning;

namespace ZeroAgent.Tests
{
    public class NineRouterGatewayTests
    {
        [Theory]
        [InlineData("http://192.168.1.100:20128", "http://192.168.1.100:20128/v1/chat/completions")]
        [InlineData("http://192.168.1.100:20128/v1", "http://192.168.1.100:20128/v1/chat/completions")]
        [InlineData("http://192.168.1.100:20128/v1/", "http://192.168.1.100:20128/v1/chat/completions")]
        [InlineData("http://192.168.1.100:20128/v1/chat/completions", "http://192.168.1.100:20128/v1/chat/completions")]
        public void NineRouterOptions_NormalizesEndpointsCorrectly(string input, string expected)
        {
            var options = new NineRouterOptions { BaseUrl = input };
            Assert.Equal(expected, options.GetNormalizedChatEndpoint());
        }

        [Fact]
        public void NineRouterGateway_ExposesSpecializedClientsForThreeRoles()
        {
            var options = new NineRouterOptions
            {
                BaseUrl = "http://localhost:20128/v1",
                ApiKey = "nr-key-test-123",
                ChatModel = "qwen2.5:3b",
                FeedbackModel = "qwen2.5:7b",
                TeacherModel = "deepseek-chat"
            };

            using (var gateway = new NineRouterGateway(options))
            {
                Assert.NotNull(gateway.ChatClient);
                Assert.NotNull(gateway.FeedbackClient);
                Assert.NotNull(gateway.TeacherClient);

                var chatClient = Assert.IsType<ExternalApiLlmClient>(gateway.ChatClient);
                Assert.Equal("qwen2.5:3b", chatClient.Options.Model);
                Assert.Equal("http://localhost:20128/v1/chat/completions", chatClient.Options.Endpoint);

                var feedbackClient = Assert.IsType<ExternalApiLlmClient>(gateway.FeedbackClient);
                Assert.Equal("qwen2.5:7b", feedbackClient.Options.Model);

                var teacherClient = Assert.IsType<ExternalApiLlmClient>(gateway.TeacherClient);
                Assert.Equal("deepseek-chat", teacherClient.Options.Model);

                Assert.Same(gateway.ChatClient, gateway.GetClientForRole(NineRouterRole.Chat));
                Assert.Same(gateway.FeedbackClient, gateway.GetClientForRole(NineRouterRole.Feedback));
                Assert.Same(gateway.TeacherClient, gateway.GetClientForRole(NineRouterRole.Teacher));
            }
        }

        [Fact]
        public async Task NineRouterGateway_Role1_Chat_ExecutesCompletionSuccessfully()
        {
            string? capturedModel = null;
            string? capturedAuth = null;

            var fakeHandler = new FakeHttpMessageHandler(req =>
            {
                capturedAuth = req.Headers.Authorization?.Parameter;
                string body = req.Content!.ReadAsStringAsync().Result;
                using (var doc = JsonDocument.Parse(body))
                {
                    capturedModel = doc.RootElement.GetProperty("model").GetString();
                }

                string responseJson = @"{
                    ""choices"": [
                        {
                            ""message"": {
                                ""role"": ""assistant"",
                                ""content"": ""Chào bạn, đơn MLG26-00123 đã được duyệt Active!""
                            }
                        }
                    ]
                }";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
                };
            });

            using (var httpClient = new HttpClient(fakeHandler))
            using (var gateway = new NineRouterGateway(new NineRouterOptions
            {
                BaseUrl = "http://linux-server:20128/v1",
                ApiKey = "nr-secret-key",
                ChatModel = "qwen2.5:3b"
            }, httpClient))
            {
                string answer = await gateway.ChatClient.CompleteAsync("Đơn MLG26-00123 đang ở trạng thái nào?");

                Assert.Contains("MLG26-00123", answer);
                Assert.Equal("qwen2.5:3b", capturedModel);
                Assert.Equal("nr-secret-key", capturedAuth);
            }
        }

        [Fact]
        public async Task NineRouterGateway_Role2_Feedback_AuditsTrajectoryViaTeacher()
        {
            var fakeHandler = new FakeHttpMessageHandler(req =>
            {
                string critiqueResponse = @"{
                    ""score"": 0.95,
                    ""isApproved"": true,
                    ""critique"": ""The agent properly verified that the order is finalized and refused inline edits."",
                    ""distilledLesson"": ""Always verify finalization before attempting mutation.""
                }";

                string responseJson = @"{
                    ""choices"": [
                        {
                            ""message"": {
                                ""role"": ""assistant"",
                                ""content"": " + JsonSerializer.Serialize(critiqueResponse) + @"
                            }
                        }
                    ]
                }";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
                };
            });

            using (var httpClient = new HttpClient(fakeHandler))
            using (var gateway = new NineRouterGateway(new NineRouterOptions
            {
                BaseUrl = "http://linux-server:20128/v1",
                FeedbackModel = "qwen2.5:7b"
            }, httpClient))
            {
                var distiller = new TeacherKnowledgeDistiller(gateway.FeedbackClient);
                var context = new AgentContext("Fix order MLG26-001");
                var trace = new System.Collections.Generic.List<AgentMessage>
                {
                    new AgentMessage(AgentRole.User, "Fix order MLG26-001"),
                    new AgentMessage(AgentRole.Assistant, "Order is finalized, suggested Re-Draft")
                };
                var response = AgentResponse.Succeeded("Order is finalized, suggested Re-Draft", 2, TimeSpan.FromSeconds(1), trace);

                var auditResult = await distiller.AuditTrajectoryAsync(context, response, minApprovalScore: 0.8f);

                Assert.True(auditResult.Success);
                Assert.True(auditResult.IsApproved);
                Assert.True(auditResult.Score >= 0.9f);
                Assert.Contains("finalized", auditResult.Critique);
            }
        }

        [Fact]
        public async Task NineRouterGateway_Role3_Teacher_DistillsDocumentDirectly()
        {
            var fakeHandler = new FakeHttpMessageHandler(req =>
            {
                string distillationPayload = @"{
                    ""aliases"": [
                        { ""alias"": ""MLG"", ""targetEntityCode"": ""MylanGroup"" }
                    ],
                    ""rules"": [
                        { ""ruleKey"": ""so_cancel_suffix"", ""proposedValue"": ""Appends -Cancel upon cancellation"" }
                    ],
                    ""faqItems"": [
                        { ""title"": ""Why cannot edit"", ""content"": ""Order is finalized."" }
                    ],
                    ""incidentEpisodes"": [
                        { ""issue"": ""Cannot edit"", ""resolution"": ""Request manager Re-Draft"" }
                    ]
                }";

                string responseJson = @"{
                    ""choices"": [
                        {
                            ""message"": {
                                ""role"": ""assistant"",
                                ""content"": " + JsonSerializer.Serialize(distillationPayload) + @"
                            }
                        }
                    ]
                }";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
                };
            });

            using (var httpClient = new HttpClient(fakeHandler))
            using (var gateway = new NineRouterGateway(new NineRouterOptions
            {
                BaseUrl = "http://linux-server:20128/v1",
                TeacherModel = "deepseek-chat"
            }, httpClient))
            {
                var distiller = new TeacherKnowledgeDistiller(gateway.TeacherClient);
                var result = await distiller.DistillDocumentAsync("Document content about MLG and cancellation...");

                Assert.True(result.Success);
                Assert.Single(result.Aliases);
                Assert.Equal("MLG", result.Aliases[0].Alias);
                Assert.Single(result.Rules);
                Assert.Equal("so_cancel_suffix", result.Rules[0].RuleKey);
            }
        }
    }
}
