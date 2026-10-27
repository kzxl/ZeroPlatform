using System;
using ZeroAgent.Core.Tools;
using ZeroAgent.Tools.Data;
using ZeroAgent.Tools.Erp;
using ZeroAgent.Tools.Industrial;
using ZeroAgent.Tools.Safety;
using ZeroAgent.Tools.Storage;
using ZeroAgent.Tools.System;

namespace ZeroAgent.Tools
{
    /// <summary>
    /// Master entry point for registering the complete industrial operational toolset into ZeroAgent.
    /// Wires PLC Modbus, TSDB analytics, Host telemetry, DataFrame queries, and HITL safety gates.
    /// </summary>
    public static class IndustrialAgentToolkit
    {
        /// <summary>
        /// Registers all industrial and operational tools into the agent registry.
        /// If a <see cref="HitlSafetyGate"/> is supplied, it is automatically wired as the approval handler.
        /// </summary>
        public static AgentToolRegistry RegisterIndustrialToolkit(this AgentToolRegistry registry, HitlSafetyGate? safetyGate = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            // Wire HITL safety gate if supplied
            if (safetyGate != null)
            {
                registry.ApprovalHandler = (tool, arg) => safetyGate.InterceptAsync(tool, arg);
            }

            // Register all tool modules
            PlcModbusTool.RegisterAll(registry);
            TsdbQueryTool.RegisterAll(registry);
            HostTelemetryTool.RegisterAll(registry);
            DataFrameQueryTool.RegisterAll(registry);
            DynamicDatabaseQueryTool.RegisterAll(registry);
            ErpEnterpriseToolkit.RegisterAll(registry);

            return registry;
        }
    }
}
