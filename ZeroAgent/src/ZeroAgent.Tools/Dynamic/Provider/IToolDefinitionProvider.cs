using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Tools.Dynamic.Model;

namespace ZeroAgent.Tools.Dynamic.Provider
{
    /// <summary>
    /// Contract for dynamic tool definition providers (JSON File, Database, Remote Service).
    /// </summary>
    public interface IToolDefinitionProvider : IDisposable
    {
        /// <summary>
        /// Source identifier (e.g. "JsonFile: configs/tools.json" or "Database: ERP_MDS").
        /// </summary>
        string ProviderSource { get; }

        /// <summary>
        /// Loads or reloads all active tool definitions from the source.
        /// </summary>
        Task<IReadOnlyList<ToolDefinitionRecord>> LoadDefinitionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Event fired when definitions change at the source (e.g. file modified, DB notify),
        /// enabling live hot-reloading without application restart.
        /// </summary>
        event EventHandler? DefinitionsChanged;
    }
}
