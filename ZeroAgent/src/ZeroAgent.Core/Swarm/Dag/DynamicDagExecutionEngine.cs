using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Core.Context;

namespace ZeroAgent.Core.Swarm.Dag
{
    public sealed class DynamicDagExecutionEngine
    {
        public int MaxDegreeOfParallelism { get; set; } = 4;

        /// <summary>
        /// Executes a task DAG collaboratively across the swarm.
        /// Independent tasks are executed concurrently. Dependent tasks wait for prerequisites.
        /// Results are synchronized to the swarm's shared blackboard.
        /// </summary>
        public async Task<DagExecutionResult> ExecuteAsync(
            TaskDag dag,
            AgentSwarm swarm,
            CancellationToken cancellationToken = default)
        {
            if (dag == null) throw new ArgumentNullException(nameof(dag));
            if (swarm == null) throw new ArgumentNullException(nameof(swarm));

            dag.ValidateAcyclic();

            int totalSteps = 0;
            var findings = new List<string>();

            using (var semaphore = new SemaphoreSlim(Math.Max(1, MaxDegreeOfParallelism)))
            {
                while (!dag.IsFinished())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var readyNodes = dag.GetReadyNodes();
                    if (readyNodes.Count == 0)
                    {
                        // Deadlock or unresolvable failure: mark remaining pending nodes as skipped
                        foreach (var node in dag.Nodes.Where(n => n.Status == DagTaskStatus.Pending))
                        {
                            node.Status = DagTaskStatus.Skipped;
                            node.ErrorMessage = "Prerequisite dependency failed or unresolvable.";
                            findings.Add($"[{node.AgentName}] Task '{node.Goal}' skipped due to dependency failure.");
                        }
                        break;
                    }

                    // Execute ready batch in parallel
                    var tasks = readyNodes.Select(async node =>
                    {
                        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                        try
                        {
                            node.Status = DagTaskStatus.Running;

                            // Build context with outputs of completed dependencies and blackboard state
                            var subContext = new AgentContext(node.Goal, maxSteps: 6);

                            // Inject blackboard context
                            if (swarm.Blackboard.Count > 0)
                            {
                                subContext.AddMessage(AgentRole.System, swarm.Blackboard.ToPromptSummary());
                            }

                            // Inject completed dependency findings
                            if (node.Dependencies.Count > 0)
                            {
                                var depSummary = new StringBuilder("Prerequisite findings:\n");
                                foreach (var depId in node.Dependencies)
                                {
                                    var depNode = dag.GetNode(depId);
                                    if (depNode != null && !string.IsNullOrEmpty(depNode.Output))
                                    {
                                        depSummary.AppendLine($"- {depNode.AgentName} ({depNode.Id}): {depNode.Output}");
                                    }
                                }
                                subContext.AddMessage(AgentRole.System, depSummary.ToString().TrimEnd());
                            }

                            var subResponse = await swarm.DelegateAsync(node.AgentName, subContext, cancellationToken).ConfigureAwait(false);

                            node.TotalSteps = subResponse.TotalSteps;
                            if (subResponse.Success)
                            {
                                node.Status = DagTaskStatus.Completed;
                                node.Output = subResponse.Output;
                                swarm.Blackboard.Set($"task_{node.Id}_output", subResponse.Output, node.AgentName);
                            }
                            else
                            {
                                node.Status = DagTaskStatus.Failed;
                                node.ErrorMessage = subResponse.ErrorMessage;
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }

            // Gather findings
            foreach (var node in dag.Nodes)
            {
                totalSteps += node.TotalSteps;
                if (node.Status == DagTaskStatus.Completed)
                {
                    findings.Add($"[{node.AgentName}] (Node {node.Id}) '{node.Goal}': {node.Output}");
                }
                else if (node.Status == DagTaskStatus.Failed)
                {
                    findings.Add($"[{node.AgentName}] (Node {node.Id}) '{node.Goal}' (Failed): {node.ErrorMessage}");
                }
            }

            return new DagExecutionResult(findings, totalSteps, dag);
        }
    }

    public sealed class DagExecutionResult
    {
        public IReadOnlyList<string> Findings { get; }
        public int TotalSteps { get; }
        public TaskDag Dag { get; }

        public DagExecutionResult(IReadOnlyList<string> findings, int totalSteps, TaskDag dag)
        {
            Findings = findings;
            TotalSteps = totalSteps;
            Dag = dag;
        }
    }
}
