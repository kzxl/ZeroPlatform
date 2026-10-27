using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroAgent.Dialog.DST;
using ZeroPrompt.Core.Templating;

namespace ZeroAgent.Dialog.Binding
{
    /// <summary>
    /// Loads declarative JSON intent definitions and binds them to pluggable database executors and response templates.
    /// Enables adding or updating enterprise business intents without recompilation or server restarts.
    /// </summary>
    public sealed class JsonIntentLoader
    {
        private readonly ConcurrentDictionary<string, IDataSourceExecutor> _executors
            = new ConcurrentDictionary<string, IDataSourceExecutor>(StringComparer.OrdinalIgnoreCase);

        public JsonIntentLoader()
        {
        }

        public JsonIntentLoader RegisterExecutor(IDataSourceExecutor executor)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            _executors[executor.ProviderName] = executor;
            return this;
        }

        public DialogueIntent LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON content cannot be empty", nameof(json));

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var model = JsonSerializer.Deserialize<IntentDataBindingModel>(json, options)
                ?? throw new InvalidOperationException("Failed to deserialize IntentDataBindingModel.");

            return CreateIntent(model);
        }

        public DialogueIntent LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("Intent configuration file not found.", filePath);
            string json = File.ReadAllText(filePath);
            return LoadFromJson(json);
        }

        public DialogueIntent CreateIntent(IntentDataBindingModel model)
        {
            if (string.IsNullOrWhiteSpace(model.IntentId))
            {
                throw new ArgumentException("IntentId must be specified.", nameof(model));
            }

            var intent = new DialogueIntent(model.IntentId, model.DisplayName ?? model.IntentId);
            if (!string.IsNullOrEmpty(model.RequiredPermission))
            {
                intent.RequiredPermission = model.RequiredPermission!;
            }

            if (model.SampleUtterances != null)
            {
                foreach (var sample in model.SampleUtterances)
                {
                    intent.AddSamples(sample);
                }
            }

            if (model.Slots != null)
            {
                foreach (var slot in model.Slots)
                {
                    if (slot.IsRequired)
                    {
                        intent.RequireSlot(slot.Name, slot.ClarificationPrompt ?? $"Vui lòng nhập {slot.Name}.");
                    }
                    else
                    {
                        intent.AddOptionalSlot(slot.Name);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(model.ResponseTemplate) && model.DataSource == null)
            {
                intent.AddTemplates(model.ResponseTemplate);
            }

            // Bind dynamic action execution
            if (model.DataSource != null)
            {
                intent.ActionHandler = async (session) =>
                {
                    if (!_executors.TryGetValue(model.DataSource.Provider, out var executor))
                    {
                        return $"Lỗi: Không tìm thấy bộ thực thi cơ sở dữ liệu '{model.DataSource.Provider}'.";
                    }

                    // Map session slots to query parameters
                    var queryParams = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    if (model.DataSource.Parameters != null)
                    {
                        foreach (var kvp in model.DataSource.Parameters)
                        {
                            string slotKey = kvp.Value
                                .Replace("{{slots.", string.Empty)
                                .Replace("}}", string.Empty)
                                .Trim();

                            if (session.Slots.TryGetValue(slotKey, out var slotVal))
                            {
                                queryParams[kvp.Key] = slotVal;
                            }
                            else
                            {
                                queryParams[kvp.Key] = null;
                            }
                        }
                    }

                    var queryResult = await executor.ExecuteAsync(
                        model.DataSource.ConnectionKey,
                        model.DataSource.Query,
                        queryParams).ConfigureAwait(false);

                    if (!queryResult.Success)
                    {
                        return $"Lỗi truy vấn dữ liệu: {queryResult.ErrorMessage}";
                    }

                    if (queryResult.Rows.Count == 0)
                    {
                        return "Không tìm thấy dữ liệu phù hợp với yêu cầu tra cứu của bạn.";
                    }

                    // Synchronize retrieved database columns back into session slots
                    var firstRow = queryResult.Rows[0];
                    foreach (var cell in firstRow)
                    {
                        session.SetSlot(cell.Key, cell.Value?.ToString() ?? string.Empty);
                    }

                    // Render with PromptTemplate
                    string templateString = !string.IsNullOrWhiteSpace(model.ResponseTemplate)
                        ? model.ResponseTemplate
                        : "Đã tìm thấy dữ liệu: " + string.Join(", ", firstRow.Keys);

                    var template = new PromptTemplate(templateString);
                    var ctx = new PromptContext();
                    foreach (var cell in firstRow)
                    {
                        ctx.Set(cell.Key, cell.Value?.ToString() ?? string.Empty);
                    }

                    return template.Render(ctx);
                };
            }

            return intent;
        }
    }
}
