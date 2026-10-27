using System;

namespace ZeroPrompt.Core.FewShot
{
    /// <summary>
    /// A single few-shot prompt demonstration exemplar.
    /// </summary>
    public sealed class Exemplar
    {
        public string Id { get; }
        public string Input { get; }
        public string Thought { get; }
        public string Action { get; }
        public string Output { get; }
        public float[] Embedding { get; }

        public Exemplar(string id, string input, string thought, string action, string output, float[] embedding)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Input = input ?? string.Empty;
            Thought = thought ?? string.Empty;
            Action = action ?? string.Empty;
            Output = output ?? string.Empty;
            Embedding = embedding ?? Array.Empty<float>();
        }

        public string Format()
        {
            return $"User: {Input}\nThought: {Thought}\nAction: {Action}\nObservation: Success\nOutput: {Output}\n";
        }
    }
}
