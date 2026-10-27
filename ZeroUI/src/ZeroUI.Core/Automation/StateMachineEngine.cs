using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Automation
{
    public enum MachineStateStatus
    {
        Idle,
        Active,
        Completed,
        Faulted
    }

    public class MachineStateNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public MachineStateStatus Status { get; set; } = MachineStateStatus.Idle;
        public double DurationSeconds { get; set; } = 5.0;
        public double ElapsedSeconds { get; set; } = 0.0;
        public double X { get; set; } = 0;
        public double Y { get; set; } = 0;
        public double Radius { get; set; } = 40;
        public uint ColorArgb { get; set; } = 0xFF3B82F6;

        public MachineStateNode(string id, string name, double x, double y, uint color = 0xFF3B82F6, double duration = 5.0)
        {
            Id = id;
            Name = name;
            X = x;
            Y = y;
            ColorArgb = color;
            DurationSeconds = duration;
        }

        public double Progress => DurationSeconds > 0 ? Math.Min(1.0, ElapsedSeconds / DurationSeconds) : 1.0;
    }

    public class StateTransitionEdge
    {
        public string Id { get; set; }
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public string ConditionText { get; set; } = string.Empty;

        public StateTransitionEdge(string id, string sourceId, string targetId, string condition = "")
        {
            Id = id;
            SourceId = sourceId;
            TargetId = targetId;
            ConditionText = condition;
        }
    }

    public class ExecutionPulse
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string TransitionId { get; set; }
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public float Progress { get; set; } = 0.0f; // 0.0 to 1.0
        public float Speed { get; set; } = 1.5f; // Completion speed factor
        public uint ColorArgb { get; set; } = 0xFFF59E0B; // Luminous amber

        public ExecutionPulse(string transitionId, string sourceId, string targetId, float speed = 1.5f)
        {
            TransitionId = transitionId;
            SourceId = sourceId;
            TargetId = targetId;
            Speed = speed;
        }
    }

    /// <summary>
    /// Live execution state machine engine for industrial automation sequences,
    /// tracking active state timeouts and flowing pulse transitions.
    /// </summary>
    public class StateMachineEngine
    {
        public List<MachineStateNode> Nodes { get; } = new List<MachineStateNode>();
        public List<StateTransitionEdge> Transitions { get; } = new List<StateTransitionEdge>();
        public List<ExecutionPulse> ActivePulses { get; } = new List<ExecutionPulse>();

        public string? ActiveStateId { get; private set; }
        public bool IsRunning { get; set; } = true;

        public event EventHandler<string>? StateEntered;
        public event EventHandler<string>? StateExited;

        public void SetInitialState(string stateId)
        {
            ActiveStateId = stateId;
            foreach (var node in Nodes)
            {
                if (node.Id == stateId)
                {
                    node.Status = MachineStateStatus.Active;
                    node.ElapsedSeconds = 0;
                    StateEntered?.Invoke(this, stateId);
                }
                else
                {
                    node.Status = MachineStateStatus.Idle;
                    node.ElapsedSeconds = 0;
                }
            }
        }

        public bool TriggerTransition(string targetStateId)
        {
            if (string.IsNullOrEmpty(ActiveStateId)) return false;

            // Find matching transition
            StateTransitionEdge? edge = null;
            for (int i = 0; i < Transitions.Count; i++)
            {
                if (Transitions[i].SourceId == ActiveStateId && Transitions[i].TargetId == targetStateId)
                {
                    edge = Transitions[i];
                    break;
                }
            }

            if (edge == null) return false;

            // Launch execution pulse
            var pulse = new ExecutionPulse(edge.Id, edge.SourceId, edge.TargetId);
            ActivePulses.Add(pulse);
            return true;
        }

        /// <summary>
        /// Updates the state machine progression by delta time in seconds.
        /// </summary>
        public void Update(double deltaSeconds)
        {
            if (!IsRunning || deltaSeconds <= 0) return;

            // 1. Advance Active State Timer
            if (!string.IsNullOrEmpty(ActiveStateId))
            {
                var currNode = Nodes.Find(n => n.Id == ActiveStateId);
                if (currNode != null && currNode.Status == MachineStateStatus.Active)
                {
                    currNode.ElapsedSeconds += deltaSeconds;

                    // If duration elapsed and automatic transition available, trigger it
                    if (currNode.ElapsedSeconds >= currNode.DurationSeconds)
                    {
                        var autoEdge = Transitions.Find(t => t.SourceId == ActiveStateId);
                        if (autoEdge != null && ActivePulses.Count == 0)
                        {
                            TriggerTransition(autoEdge.TargetId);
                        }
                    }
                }
            }

            // 2. Advance Pulses
            for (int i = ActivePulses.Count - 1; i >= 0; i--)
            {
                var p = ActivePulses[i];
                p.Progress += (float)(deltaSeconds * p.Speed);

                if (p.Progress >= 1.0f)
                {
                    // Pulse arrived at target state!
                    ActivePulses.RemoveAt(i);

                    // Switch states
                    if (ActiveStateId != null)
                    {
                        var prevNode = Nodes.Find(n => n.Id == ActiveStateId);
                        if (prevNode != null)
                        {
                            prevNode.Status = MachineStateStatus.Completed;
                            StateExited?.Invoke(this, prevNode.Id);
                        }
                    }

                    ActiveStateId = p.TargetId;
                    var nextNode = Nodes.Find(n => n.Id == p.TargetId);
                    if (nextNode != null)
                    {
                        nextNode.Status = MachineStateStatus.Active;
                        nextNode.ElapsedSeconds = 0.0;
                        StateEntered?.Invoke(this, nextNode.Id);
                    }
                }
            }
        }
    }
}
