using System;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Tools;
using ZeroAgent.Dialog.Engine;
using ZeroAgent.Tools;
using ZeroAgent.Tools.Data;
using ZeroAgent.Tools.Industrial;
using ZeroAgent.Tools.Safety;

namespace ZeroAgent.Dialog
{
    /// <summary>
    /// Fluent builder for constructing and configuring production-ready ZeroAgent dialogue engines.
    /// Provides 3-line initialization for WinForms, WPF, WebAPI, and headless microservices.
    /// </summary>
    public sealed class ZeroAgentBuilder
    {
        private int _dimension = 128;
        private HitlSafetyGate? _safetyGate;
        private AgentToolRegistry? _customRegistry;
        private bool _includeIndustrialTools = true;
        private bool _enableNeuralClassifier = true;
        private ILlmClient? _llmClient;
        private string? _reactRole;
        private string? _liveSqlConnStr;
        private string _liveSqlDbName = "LiveDB";
        private bool _useErpPreset = false;
        private Action<AgentToolRegistry>? _customToolConfig;

        public static ZeroAgentBuilder Create() => new ZeroAgentBuilder();

        public ZeroAgentBuilder WithErpPreset(bool enable = true)
        {
            _useErpPreset = enable;
            return this;
        }

        public ZeroAgentBuilder WithVectorDimension(int dimension)
        {
            _dimension = dimension > 0 ? dimension : 128;
            return this;
        }

        public ZeroAgentBuilder WithHitlSafetyGate(HitlSafetyGate gate)
        {
            _safetyGate = gate ?? throw new ArgumentNullException(nameof(gate));
            return this;
        }

        public ZeroAgentBuilder WithIndustrialTools(bool include = true)
        {
            _includeIndustrialTools = include;
            return this;
        }

        public ZeroAgentBuilder WithCustomTools(Action<AgentToolRegistry> configure)
        {
            _customToolConfig = configure ?? throw new ArgumentNullException(nameof(configure));
            return this;
        }

        public ZeroAgentBuilder WithToolRegistry(AgentToolRegistry registry)
        {
            _customRegistry = registry ?? throw new ArgumentNullException(nameof(registry));
            return this;
        }

        public ZeroAgentBuilder WithNeuralClassifier(bool enable = true)
        {
            _enableNeuralClassifier = enable;
            return this;
        }

        public ZeroAgentBuilder WithLiveSqlDatabase(string connectionString, string databaseName = "ProductionDB")
        {
            _liveSqlConnStr = connectionString;
            _liveSqlDbName = databaseName;
            return this;
        }

        /// <summary>
        /// Enables Two-Tier Cognitive Deliberation via ReActAgent when Tier 1 NLU is unresolved.
        /// </summary>
        public ZeroAgentBuilder WithCognitiveEscalation(ILlmClient llmClient, string role = "Industrial Deliberation Specialist")
        {
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
            _reactRole = role;
            return this;
        }

        public ZeroDialogEngine Build()
        {
            var safetyGate = _safetyGate ?? new HitlSafetyGate();
            var registry = _customRegistry ?? new AgentToolRegistry();

            if (_includeIndustrialTools)
            {
                registry.RegisterIndustrialToolkit(safetyGate);
            }

            if (!string.IsNullOrWhiteSpace(_liveSqlConnStr))
            {
                DynamicDatabaseQueryTool.RegisterLiveDatabase(_liveSqlConnStr!, _liveSqlDbName);
                DynamicDatabaseQueryTool.RegisterAll(registry);
            }

            _customToolConfig?.Invoke(registry);

            var engine = _useErpPreset
                ? ErpDialogFactory.CreateErpBot(registry, safetyGate, _dimension)
                : IndustrialDialogFactory.CreateIndustrialBot(safetyGate, registry, _enableNeuralClassifier, _dimension);

            if (_llmClient != null)
            {
                var reActAgent = new ReActAgent("ZeroPlatformReActAgent", _reactRole ?? "Industrial Deliberation Specialist", registry, _llmClient);
                var bridge = new CognitiveEscalationBridge(reActAgent);
                engine.SetCognitiveEscalationBridge(bridge);
            }

            return engine;
        }
    }
}
