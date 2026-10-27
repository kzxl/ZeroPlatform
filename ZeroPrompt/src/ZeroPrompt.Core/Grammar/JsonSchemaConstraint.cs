using System;
using System.Collections.Generic;

namespace ZeroPrompt.Core.Grammar
{
    public enum SchemaPropertyType
    {
        String,
        Number,
        Boolean,
        Array,
        Object
    }

    /// <summary>
    /// Schema definition defining required fields and structural constraints for tool calling.
    /// </summary>
    public sealed class JsonSchemaConstraint
    {
        public string Title { get; }
        public Dictionary<string, SchemaPropertyType> Properties { get; } = new Dictionary<string, SchemaPropertyType>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> RequiredProperties { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public JsonSchemaConstraint(string title)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
        }

        public JsonSchemaConstraint AddProperty(string name, SchemaPropertyType type, bool required = true)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            Properties[name] = type;
            if (required)
            {
                RequiredProperties.Add(name);
            }
            return this;
        }
    }
}
