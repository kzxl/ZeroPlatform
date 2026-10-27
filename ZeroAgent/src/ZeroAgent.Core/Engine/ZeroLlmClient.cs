using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroLlm.Core.Engine;

namespace ZeroAgent.Core.Engine
{
    /// <summary>
    /// Pure C# LLM provider client backed directly by an in-process ZeroLlm engine.
    /// Eliminates all external HTTP/Python/unmanaged runtime dependencies.
    /// </summary>
    public sealed class ZeroLlmClient : ILlmClient
    {
        private readonly ILlmEngine _engine;

        public ZeroLlmClient(ILlmEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
        {
            return _engine.CompleteAsync(prompt, cancellationToken);
        }
    }
}
