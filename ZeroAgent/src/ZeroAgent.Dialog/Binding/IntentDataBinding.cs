using System.Collections.Generic;

namespace ZeroAgent.Dialog.Binding
{
    /// <summary>
    /// Declarative configuration model for a slot requirement.
    /// </summary>
    public sealed class SlotBindingModel
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "string";
        public bool IsRequired { get; set; }
        public string? ClarificationPrompt { get; set; }
    }

    /// <summary>
    /// Declarative configuration model for target data source and query template.
    /// </summary>
    public sealed class DataSourceBindingModel
    {
        public string Provider { get; set; } = "SqlServer";
        public string ConnectionKey { get; set; } = "Default";
        public string QueryType { get; set; } = "ParameterizedSql";
        public string Query { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Declarative JSON configuration schema binding conversational intent to backend enterprise data sources.
    /// </summary>
    public sealed class IntentDataBindingModel
    {
        public string IntentId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? RequiredPermission { get; set; }
        public List<string> SampleUtterances { get; set; } = new List<string>();
        public List<SlotBindingModel> Slots { get; set; } = new List<SlotBindingModel>();
        public DataSourceBindingModel? DataSource { get; set; }
        public string ResponseTemplate { get; set; } = string.Empty;
    }
}
