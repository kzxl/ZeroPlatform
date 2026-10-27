using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;

namespace ZeroAgent.Core.Swarm
{
    /// <summary>
    /// Multi-Agent Swarm coordinator with shared blackboard state.
    /// Manages a collective of specialized agents, orchestrates goal delegation, and handles collaborative handoffs.
    /// </summary>
    public sealed class AgentSwarm
    {
        private readonly Dictionary<string, ReActAgent> _agents = new Dictionary<string, ReActAgent>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the shared in-memory blackboard utilized by all agents in this swarm.
        /// </summary>
        public AgentBlackboard Blackboard { get; } = new AgentBlackboard();

        /// <summary>
        /// Gets the total number of registered agents.
        /// </summary>
        public int AgentCount => _agents.Count;

        /// <summary>
        /// Gets the collection of registered agents.
        /// </summary>
        public IEnumerable<ReActAgent> Agents => _agents.Values;

        /// <summary>
        /// Registers a specialist ReAct agent into the swarm.
        /// </summary>
        public void RegisterAgent(ReActAgent agent)
        {
            if (agent == null) throw new ArgumentNullException(nameof(agent));
            _agents[agent.Name] = agent;
        }

        /// <summary>
        /// Attempts to locate a registered agent by name.
        /// </summary>
        public ReActAgent? FindAgent(string agentName)
        {
            return _agents.TryGetValue(agentName, out var agent) ? agent : null;
        }

        /// <summary>
        /// Delegates execution of an AgentContext to a specialized agent within the swarm.
        /// Automatically injects the current Blackboard state into the agent's context.
        /// </summary>
        public async Task<AgentResponse> DelegateAsync(string agentName, AgentContext context, CancellationToken cancellationToken = default)
        {
            if (!_agents.TryGetValue(agentName, out var agent))
            {
                return AgentResponse.Failed($"Agent '{agentName}' is not registered in this swarm.", 0, TimeSpan.Zero, context.History);
            }

            // Inject shared blackboard context into system history if present
            string blackboardSummary = Blackboard.ToPromptSummary();
            if (!string.IsNullOrWhiteSpace(blackboardSummary))
            {
                context.AddMessage(AgentRole.System, blackboardSummary);
            }

            var response = await agent.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);

            if (response.Success)
            {
                Blackboard.PostFinding(agentName, $"Completed task '{context.Goal}': {response.Output}");
            }
            else
            {
                Blackboard.PostFinding(agentName, $"Failed task '{context.Goal}': {response.ErrorMessage}");
            }

            return response;
        }

        /// <summary>
        /// Orchestrates a collaborative task handoff from one specialist agent to another.
        /// </summary>
        public async Task<AgentResponse> HandoffAsync(
            string fromAgentName,
            string toAgentName,
            string taskGoal,
            string? transferData = null,
            CancellationToken cancellationToken = default)
        {
            Blackboard.PostFinding(fromAgentName, $"Handoff initiated to [{toAgentName}] for goal: {taskGoal}");
            if (!string.IsNullOrEmpty(transferData))
            {
                Blackboard.Set($"handoff_{fromAgentName}_to_{toAgentName}", transferData);
            }

            var context = new AgentContext(taskGoal, maxSteps: 6);
            if (!string.IsNullOrEmpty(transferData))
            {
                context.AddMessage(AgentRole.System, $"Handoff context from {fromAgentName}: {transferData}");
            }

            return await DelegateAsync(toAgentName, context, cancellationToken).ConfigureAwait(false);
        }
    }
}
