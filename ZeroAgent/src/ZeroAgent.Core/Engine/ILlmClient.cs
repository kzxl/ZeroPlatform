using System.Threading;
using System.Threading.Tasks;

namespace ZeroAgent.Core.Engine
{
    /// <summary>
    /// LLM inference provider interface for generating completions.
    /// Can be backed by ZeroInference, an embedded model, or an external API gateway.
    /// </summary>
    public interface ILlmClient
    {
        Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default);
    }
}
