using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Swarm;
using ZeroAgent.Core.Swarm.Dag;
using ZeroAgent.Core.Tools;

namespace ZeroAgent.Tests
{
    public class DagPlannerTests
    {
        [Fact]
        public void TaskDag_DetectsCircularDependencies_ThrowsException()
        {
            var dag = new TaskDag();
            dag.AddNode(new DagTaskNode("T1", "AgentA", "Goal 1", new[] { "T2" }));
            dag.AddNode(new DagTaskNode("T2", "AgentB", "Goal 2", new[] { "T1" }));

            var ex = Assert.Throws<InvalidOperationException>(() => dag.ValidateAcyclic());
            Assert.Contains("Cycle detected", ex.Message);
        }

        [Fact]
        public void TaskDag_GetReadyNodes_FollowsTopologicalOrder()
        {
            var dag = new TaskDag();
            var node1 = new DagTaskNode("T1", "AgentA", "Goal 1");
            var node2 = new DagTaskNode("T2", "AgentB", "Goal 2", new[] { "T1" });
            var node3 = new DagTaskNode("T3", "AgentC", "Goal 3", new[] { "T2" });

            dag.AddNode(node1);
            dag.AddNode(node2);
            dag.AddNode(node3);

            dag.ValidateAcyclic();

            // At start, only T1 is ready
            var ready1 = dag.GetReadyNodes();
            Assert.Single(ready1);
            Assert.Equal("T1", ready1[0].Id);

            // Complete T1
            node1.Status = DagTaskStatus.Completed;

            // Now T2 is ready
            var ready2 = dag.GetReadyNodes();
            Assert.Single(ready2);
            Assert.Equal("T2", ready2[0].Id);

            // Complete T2
            node2.Status = DagTaskStatus.Completed;

            // Now T3 is ready
            var ready3 = dag.GetReadyNodes();
            Assert.Single(ready3);
            Assert.Equal("T3", ready3[0].Id);
        }

        [Fact]
        public async Task DynamicDagExecutionEngine_ExecutesParallelAndDependentNodes_WithBlackboardSync()
        {
            var swarm = new AgentSwarm();

            var llmA = new MockLlmClient();
            llmA.Enqueue("Final Answer: Telemetry is normal at 50 Hz.");
            var agentA = new ReActAgent("SensorAgent", "Sensor telemetry", new AgentToolRegistry(), llmA);
            swarm.RegisterAgent(agentA);

            var llmB = new MockLlmClient();
            llmB.Enqueue("Final Answer: Database shows 10,000 batches processed.");
            var agentB = new ReActAgent("DbAgent", "Database analyst", new AgentToolRegistry(), llmB);
            swarm.RegisterAgent(agentB);

            var llmC = new MockLlmClient();
            llmC.Enqueue("Final Answer: Overall diagnosis: Equipment healthy.");
            var agentC = new ReActAgent("DiagnosisAgent", "System doctor", new AgentToolRegistry(), llmC);
            swarm.RegisterAgent(agentC);

            // T1 and T2 run in parallel; T3 depends on both T1 and T2
            var dag = new TaskDag();
            dag.AddNode(new DagTaskNode("T1", "SensorAgent", "Read telemetry"));
            dag.AddNode(new DagTaskNode("T2", "DbAgent", "Query batch counts"));
            dag.AddNode(new DagTaskNode("T3", "DiagnosisAgent", "Perform diagnosis", new[] { "T1", "T2" }));

            var engine = new DynamicDagExecutionEngine { MaxDegreeOfParallelism = 2 };
            var result = await engine.ExecuteAsync(dag, swarm);

            Assert.Equal(3, result.Findings.Count);
            Assert.True(dag.IsFinished());

            // Check blackboard sync
            Assert.True(swarm.Blackboard.ContainsKey("task_T1_output"));
            Assert.True(swarm.Blackboard.ContainsKey("task_T2_output"));
            Assert.True(swarm.Blackboard.ContainsKey("task_T3_output"));
        }

        [Fact]
        public async Task SupervisorAgent_ExecutesDagTaskPlanning_EndToEnd()
        {
            var swarm = new AgentSwarm();

            var llmSpecialist = new MockLlmClient();
            llmSpecialist.Enqueue("Final Answer: Line 1 vibration is 0.5 mm/s.");
            var agent = new ReActAgent("VibrationAgent", "Vibration expert", new AgentToolRegistry(), llmSpecialist);
            swarm.RegisterAgent(agent);

            var llmSupervisor = new MockLlmClient();
            // Step 1: Decomposition with DAG syntax
            llmSupervisor.Enqueue("Delegate: [VibrationAgent] | Id: T1 | DependsOn: [] | Task: Check Line 1 vibration levels");
            // Step 2: Final synthesis
            llmSupervisor.Enqueue("Authoritative Synthesis: Line 1 vibration is well within normal safe thresholds.");

            var supervisor = new SupervisorAgent("ChiefSupervisor", "Plant Director", swarm, llmSupervisor)
            {
                UseDagPlanning = true
            };

            var response = await supervisor.ExecuteHierarchicalTaskAsync("Check Line 1 vibration status.");

            Assert.True(response.Success);
            Assert.Contains("Authoritative Synthesis", response.Output);
        }
    }
}
