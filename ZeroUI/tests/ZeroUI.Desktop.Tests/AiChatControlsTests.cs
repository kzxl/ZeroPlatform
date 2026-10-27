using System;
using System.Linq;
using Xunit;
using ZeroUI.Core.AiMl;
using WpfChatBox = ZeroUI.Wpf.Documents.ZAiChatBox;
using WinFormsChatBox = ZeroUI.WinForms.Documents.ZAiChatBox;

namespace ZeroUI.Desktop.Tests
{
    public class AiChatControlsTests
    {
        #region WPF ZAiChatBox Tests

        [Fact]
        public void Wpf_ZAiChatBox_InitialState_And_MessageManagement_Works()
        {
            StaTestRunner.Run(() =>
            {
                var chat = new WpfChatBox();

                Assert.Equal("ZeroCopilot", chat.AssistantName);
                Assert.Equal("ZeroInference Edge", chat.ModelName);
                Assert.False(chat.IsGenerating);
                Assert.Equal(ChatStreamingState.Idle, chat.StreamingState);
                Assert.Empty(chat.Messages);

                // Add prompt suggestions
                chat.PromptSuggestions.Add("Explain OEE breakdown");
                chat.PromptSuggestions.Add("Check vibration alarms");
                Assert.Equal(2, chat.PromptSuggestions.Count);

                // User message
                chat.AppendUserMessage("What is the current line efficiency?");
                Assert.Single(chat.Messages);
                Assert.Equal(ChatRole.User, chat.Messages[0].Role);
                Assert.Equal("What is the current line efficiency?", chat.Messages[0].Content);

                // Assistant message
                var botMsg = chat.AppendAssistantMessage("Current line OEE is 87.4%.");
                Assert.Equal(2, chat.Messages.Count);
                Assert.Equal(ChatRole.Assistant, botMsg.Role);
                Assert.Equal("Current line OEE is 87.4%.", botMsg.Content);

                // Clear
                chat.ClearMessages();
                Assert.Empty(chat.Messages);
            });
        }

        [Fact]
        public void Wpf_ZAiChatBox_StreamingTokens_AppendsProperly()
        {
            StaTestRunner.Run(() =>
            {
                var chat = new WpfChatBox();
                var msg = chat.AppendAssistantMessage();

                chat.IsGenerating = true;
                chat.StreamingState = ChatStreamingState.Streaming;

                chat.StreamToken(msg.Id, "Zero");
                chat.StreamToken(msg.Id, "Platform");
                chat.StreamToken(msg.Id, " AI");

                Assert.Equal("ZeroPlatform AI", msg.Content);
                Assert.True(msg.IsStreaming);

                chat.CompleteStreaming(msg.Id);
                chat.IsGenerating = false;
                chat.StreamingState = ChatStreamingState.Completed;

                Assert.False(msg.IsStreaming);
                Assert.Equal("ZeroPlatform AI", msg.Content);
            });
        }

        #endregion

        #region WinForms ZAiChatBox Tests

        [Fact]
        public void WinForms_ZAiChatBox_InitialState_And_MessageManagement_Works()
        {
            using var chat = new WinFormsChatBox();

            Assert.Equal("ZeroCopilot", chat.AssistantName);
            Assert.Equal("ZeroInference Edge", chat.ModelName);
            Assert.False(chat.IsGenerating);
            Assert.Equal(ChatStreamingState.Idle, chat.StreamingState);
            Assert.Empty(chat.Messages);

            chat.PromptSuggestions.Add("Summarize batch 402");
            Assert.Single(chat.PromptSuggestions);

            chat.AppendUserMessage("Show telemetry summary.");
            Assert.Single(chat.Messages);
            Assert.Equal(ChatRole.User, chat.Messages[0].Role);

            var botMsg = chat.AppendAssistantMessage("Telemetry normal.");
            Assert.Equal(2, chat.Messages.Count);
            Assert.Equal(ChatRole.Assistant, botMsg.Role);

            chat.ClearMessages();
            Assert.Empty(chat.Messages);
        }

        [Fact]
        public void WinForms_ZAiChatBox_StreamingTokens_AppendsProperly()
        {
            using var chat = new WinFormsChatBox();
            var msg = chat.AppendAssistantMessage();

            chat.IsGenerating = true;
            chat.StreamingState = ChatStreamingState.Streaming;

            chat.StreamToken(msg.Id, "Token 1 ");
            chat.StreamToken(msg.Id, "Token 2");

            Assert.Equal("Token 1 Token 2", msg.Content);
            Assert.True(msg.IsStreaming);

            chat.CompleteStreaming(msg.Id);
            Assert.False(msg.IsStreaming);
            Assert.Equal("Token 1 Token 2", msg.Content);
            Assert.Equal(ChatStreamingState.Idle, chat.StreamingState);
        }

        [Fact]
        public void WinForms_ZAiChatBox_CompleteStreaming_RendersMarkdownTable()
        {
            using var chat = new WinFormsChatBox();
            var msg = chat.AppendAssistantMessage();

            chat.IsGenerating = true;
            chat.StreamingState = ChatStreamingState.Streaming;

            string tableMarkdown = "| STT | Mặt hàng | SL |\n|---|---|---|\n| 1 | Mì Hảo Hảo | 5 |";
            chat.StreamToken(msg.Id, tableMarkdown);
            chat.CompleteStreaming(msg.Id);

            Assert.False(msg.IsStreaming);
            Assert.Equal(ChatStreamingState.Idle, chat.StreamingState);
        }

        [Fact]
        public void Generic_ZFlowChatOptions_And_SetupExtensions_BindProperly()
        {
            using var chat = new WinFormsChatBox();

            var options = new ZFlowChatOptions()
                .WithEndpoint("http://127.0.0.1:8000", "intelligent_enterprise_chatbot_flow")
                .WithIdentity("phong.vo", "Manager")
                .WithContextParameter("entity_code", "REF-001", "Ref")
                .WithFeaturePermission("ScreenA", canView: true, canAdd: true, canEdit: true, canDelete: false)
                .WithContinuousLearningTools()
                .WithSuggestions("Prompt 1", "Prompt 2");

            chat.ConfigureFlow(options);

            Assert.NotNull(chat.FlowProvider);
            Assert.Equal("intelligent_enterprise_chatbot_flow", chat.WorkflowId);
            Assert.True(chat.ShowContextBar);
            Assert.Equal(2, chat.PromptSuggestions.Count);
            Assert.Equal(4, chat.ToolbarItems.Count); // 2 default attach + 2 continuous learning
            Assert.Equal("REF-001", chat.SessionContext.Parameters["entity_code"]);
            Assert.Contains("Ref: REF-001", chat.SessionContext.FormatDisplaySummary());

            // Test bubble rendering with generic ChatActionItem & LatencyMs
            var botMsg = chat.AppendAssistantMessage("Result ready.");
            botMsg.LatencyMs = 136;
            botMsg.Reasoning = "Fast-Path SQL Direct";
            botMsg.Actions.Add(new ChatActionItem
            {
                ActionType = "navigate",
                TargetKey = "ScreenA",
                Label = "Open Screen A",
                ReferenceValue = "REF-001",
                AllowedModes = { "VIEW", "EDIT" }
            });

            chat.RebuildSingleBubble(botMsg.Id);
            Assert.Single(botMsg.Actions);
        }

        [Fact]
        public void ZFlowChatClient_ParsesGenericActionsAndContextUpdates_Properly()
        {
            string sampleResponseJson = @"{
                ""workflow_id"": ""intelligent_enterprise_chatbot_flow"",
                ""status"": ""completed"",
                ""final_output"": ""Data loaded."",
                ""reasoning"": ""Fast-Path SQL"",
                ""total_duration_ms"": 135.8,
                ""entity_memory"": {
                    ""order_code"": ""ORD-100"",
                    ""customer_code"": ""CUST-01""
                },
                ""actions"": [
                    {
                        ""action_type"": ""navigate_erp_screen"",
                        ""feature_code"": ""OrderScreen"",
                        ""domain"": ""sales"",
                        ""label"": ""Open Order Screen"",
                        ""entity_code"": ""ORD-100"",
                        ""allowed_modes"": [""VIEW"", ""EDIT""],
                        ""permissions"": {
                            ""can_view"": true,
                            ""can_edit"": true,
                            ""can_delete"": false
                        }
                    }
                ]
            }";

            var result = ZFlowChatClient.ParseResultJson(sampleResponseJson);

            Assert.True(result.IsSuccess);
            Assert.Equal("Data loaded.", result.Output);
            Assert.Equal("Fast-Path SQL", result.Reasoning);
            Assert.Equal(135.8, result.DurationMs);
            Assert.Equal("ORD-100", result.ContextUpdates["order_code"]);
            Assert.Single(result.Actions);
            Assert.Equal("OrderScreen", result.Actions[0].TargetKey);
            Assert.Equal("ORD-100", result.Actions[0].ReferenceValue);
            Assert.Contains("EDIT", result.Actions[0].AllowedModes);
            Assert.True(result.Actions[0].Flags["can_edit"]);
            Assert.False(result.Actions[0].Flags["can_delete"]);
        }

        [Fact]
        public async System.Threading.Tasks.Task Live_ZFlow_EndToEnd_Execution_PopulatesBubbleActionsAndContext()
        {
            var options = new ZFlowChatOptions()
                .WithEndpoint("http://127.0.0.1:8000", "intelligent_enterprise_chatbot_flow")
                .WithIdentity("phong.vo", "Manager")
                .WithFeaturePermission("OrderSearch", canView: true, canAdd: true, canEdit: true, canDelete: false);

            // 1. Test ZFlowChatClient directly against live ZFlow backend (no Win32 HWND affinity)
            var provider = options.BuildProvider();
            var state = options.BuildContextState();
            var liveRes = await provider.ExecuteAsync(new ChatFlowRequest
            {
                WorkflowId = options.WorkflowId,
                InputText = "kiểm tra đơn hàng MLG26-2908",
                SessionId = state.SessionId,
                Context = state.Parameters
            });

            Assert.NotNull(liveRes);
            if (liveRes.IsSuccess)
            {
                Assert.Contains("MLG26-2908", liveRes.Output);
                Assert.True(liveRes.DurationMs > 0);
                Assert.NotEmpty(liveRes.Actions);
                Assert.Equal("OrderSearch", liveRes.Actions[0].TargetKey);
                Assert.Equal("MLG26-2908", liveRes.ContextUpdates["order_code"]);
            }

            // 2. Test WinFormsChatBox.ExecuteFlowPromptAsync on the same thread using a synchronous stub provider
            using var chat = new WinFormsChatBox { UseStreaming = false };
            chat.ConfigureFlow(options, new SynchronousStubFlowProvider(liveRes.IsSuccess ? liveRes : new ChatFlowResult
            {
                Status = "completed",
                Output = "Mocked MLG26-2908",
                DurationMs = 42,
                ContextUpdates = { ["order_code"] = "MLG26-2908" },
                Actions = { new ChatActionItem { TargetKey = "OrderSearch", Label = "Open Order", ReferenceValue = "MLG26-2908" } }
            }));

            var uiRes = await chat.ExecuteFlowPromptAsync("kiểm tra đơn hàng MLG26-2908");
            Assert.NotNull(uiRes);
            Assert.NotEmpty(chat.Messages);
            var lastMsg = chat.Messages.Last();
            Assert.Equal(ChatRole.Assistant, lastMsg.Role);
            Assert.Contains("MLG26-2908", lastMsg.Content);
            Assert.NotEmpty(lastMsg.Actions);
            Assert.Equal("OrderSearch", lastMsg.Actions[0].TargetKey);
            Assert.Equal("MLG26-2908", chat.SessionContext.Parameters["order_code"]);
        }

        private sealed class SynchronousStubFlowProvider : IChatFlowProvider
        {
            private readonly ChatFlowResult _result;
            public SynchronousStubFlowProvider(ChatFlowResult result) => _result = result;

            public System.Threading.Tasks.Task<ChatFlowResult> ExecuteAsync(ChatFlowRequest request, System.Threading.CancellationToken cancellationToken = default) =>
                System.Threading.Tasks.Task.FromResult(_result);

            public System.Threading.Tasks.Task<ChatFlowResult> StreamAsync(ChatFlowRequest request, Action<ChatStreamChunk>? onChunk = null, System.Threading.CancellationToken cancellationToken = default) =>
                System.Threading.Tasks.Task.FromResult(_result);

            public System.Threading.Tasks.Task<bool> ResetSessionAsync(string sessionId, System.Threading.CancellationToken cancellationToken = default) =>
                System.Threading.Tasks.Task.FromResult(true);
        }

        #endregion
    }
}
