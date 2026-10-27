using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace ZeroAgent.Core.Swarm
{
    /// <summary>
    /// Represents an entry recorded in the Swarm shared blackboard audit log.
    /// </summary>
    public sealed class BlackboardLogEntry
    {
        public DateTime Timestamp { get; }
        public string AgentName { get; }
        public string Action { get; }

        public BlackboardLogEntry(string agentName, string action)
        {
            Timestamp = DateTime.UtcNow;
            AgentName = agentName ?? "Unknown";
            Action = action ?? string.Empty;
        }

        public override string ToString() => $"[{Timestamp:HH:mm:ss}] [{AgentName}] {Action}";
    }

    /// <summary>
    /// Thread-safe in-memory Blackboard coordination mechanism for Multi-Agent Swarms.
    /// Enables peer agents and supervisors to share intermediate hypotheses, extracted metrics,
    /// environment state, and collaborative findings without tight coupling.
    /// </summary>
    public sealed class AgentBlackboard
    {
        private readonly ConcurrentDictionary<string, object> _data = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentQueue<BlackboardLogEntry> _logs = new ConcurrentQueue<BlackboardLogEntry>();

        public int Count => _data.Count;
        public int StateCount => _data.Count;
        public int LogCount => _logs.Count;

        /// <summary>
        /// Sets a shared state key-value pair on the blackboard with optional audit logging.
        /// </summary>
        public void Set<T>(string key, T value, string? agentName = null)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
            _data[key.Trim()] = value!;
            if (!string.IsNullOrEmpty(agentName))
            {
                PostFinding(agentName!, $"Updated '{key.Trim()}'");
            }
        }

        /// <summary>
        /// Checks if a shared state variable exists on the blackboard.
        /// </summary>
        public bool ContainsKey(string key) => !string.IsNullOrWhiteSpace(key) && _data.ContainsKey(key.Trim());

        /// <summary>
        /// Attempts to retrieve a shared state value by key.
        /// </summary>
        public bool TryGet<T>(string key, out T value)
        {
            if (!string.IsNullOrWhiteSpace(key) && _data.TryGetValue(key.Trim(), out var obj))
            {
                if (obj is T typed)
                {
                    value = typed;
                    return true;
                }
                try
                {
                    value = (T)Convert.ChangeType(obj, typeof(T));
                    return true;
                }
                catch
                {
                    // Type conversion failed
                }
            }
            value = default!;
            return false;
        }

        /// <summary>
        /// Appends an action observation or finding to the collaborative swarm log.
        /// </summary>
        public void PostFinding(string agentName, string finding)
        {
            _logs.Enqueue(new BlackboardLogEntry(agentName, finding));
        }

        /// <summary>
        /// Retrieves all logged collaboration actions.
        /// </summary>
        public IReadOnlyList<BlackboardLogEntry> GetLogs() => _logs.ToArray();

        /// <summary>
        /// Formats active blackboard state and recent findings into a concise markdown prompt section
        /// for injection into swarm agent reasoning contexts.
        /// </summary>
        public string ToPromptSummary(int maxRecentLogs = 5)
        {
            if (_data.IsEmpty && _logs.IsEmpty)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== Shared Swarm Blackboard Context ===");

            if (!_data.IsEmpty)
            {
                sb.AppendLine("Active Shared Variables:");
                foreach (var kvp in _data)
                {
                    sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
                }
            }

            if (!_logs.IsEmpty)
            {
                sb.AppendLine("Recent Swarm Findings:");
                var allLogs = _logs.ToArray();
                int start = Math.Max(0, allLogs.Length - maxRecentLogs);
                for (int i = start; i < allLogs.Length; i++)
                {
                    sb.AppendLine($"- {allLogs[i]}");
                }
            }

            sb.AppendLine("=======================================");
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Clears all shared blackboard data and activity logs.
        /// </summary>
        public void Clear()
        {
            _data.Clear();
            while (_logs.TryDequeue(out _)) { }
        }
    }
}
