using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Core.Tools;

namespace ZeroAgent.Tools.Safety
{
    public sealed class HitlApprovalRequest
    {
        public string RequestId { get; } = Guid.NewGuid().ToString("N");
        public string ToolName { get; }
        public string Argument { get; }
        public DateTime TimestampUtc { get; } = DateTime.UtcNow;
        public TaskCompletionSource<bool> CompletionSource { get; } = new TaskCompletionSource<bool>();

        public HitlApprovalRequest(string toolName, string argument)
        {
            ToolName = toolName;
            Argument = argument;
        }
    }

    public sealed class HitlAuditRecord
    {
        public string RequestId { get; }
        public string ToolName { get; }
        public string Argument { get; }
        public bool Approved { get; }
        public string OperatorId { get; }
        public DateTime TimestampUtc { get; }

        public HitlAuditRecord(string requestId, string toolName, string argument, bool approved, string operatorId)
        {
            RequestId = requestId;
            ToolName = toolName;
            Argument = argument;
            Approved = approved;
            OperatorId = operatorId;
            TimestampUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Human-in-the-Loop (HITL) Safety Gate that intercepts sensitive tool invocations
    /// and halts execution until an authorized human operator grants explicit approval.
    /// </summary>
    public sealed class HitlSafetyGate
    {
        private readonly ConcurrentDictionary<string, HitlApprovalRequest> _pendingRequests = new ConcurrentDictionary<string, HitlApprovalRequest>();
        private readonly List<HitlAuditRecord> _auditLog = new List<HitlAuditRecord>();
        private readonly object _lock = new object();

        public IReadOnlyList<HitlAuditRecord> AuditLog
        {
            get
            {
                lock (_lock)
                {
                    return _auditLog.ToArray();
                }
            }
        }

        public IEnumerable<HitlApprovalRequest> PendingRequests => _pendingRequests.Values;

        /// <summary>
        /// When true, immediately approves all sensitive tool calls without blocking for human input.
        /// Useful for automated integration testing and headless environments.
        /// </summary>
        public bool AutoApprove { get; set; }

        /// <summary>
        /// Asynchronously awaits operator approval or timeout for a sensitive tool call.
        /// </summary>
        public async Task<bool> InterceptAsync(IAgentTool tool, string argument, TimeSpan? timeout = null)
        {
            if (AutoApprove)
            {
                lock (_lock)
                {
                    _auditLog.Add(new HitlAuditRecord(Guid.NewGuid().ToString("N"), tool.Name, argument, true, "AutoApproved"));
                }
                return true;
            }

            var req = new HitlApprovalRequest(tool.Name, argument);
            _pendingRequests[req.RequestId] = req;

            using (var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(30)))
            {
                using (cts.Token.Register(() =>
                {
                    if (_pendingRequests.TryRemove(req.RequestId, out _))
                    {
                        lock (_lock)
                        {
                            _auditLog.Add(new HitlAuditRecord(req.RequestId, tool.Name, argument, false, "SystemTimeout"));
                        }
                        req.CompletionSource.TrySetResult(false);
                    }
                }))
                {
                    return await req.CompletionSource.Task.ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Grants approval for a pending request ID by a human operator.
        /// </summary>
        public bool Approve(string requestId, string operatorId = "Operator")
        {
            if (_pendingRequests.TryRemove(requestId, out var req))
            {
                lock (_lock)
                {
                    _auditLog.Add(new HitlAuditRecord(requestId, req.ToolName, req.Argument, true, operatorId));
                }
                return req.CompletionSource.TrySetResult(true);
            }
            return false;
        }

        /// <summary>
        /// Denies a pending request ID by a human operator.
        /// </summary>
        public bool Reject(string requestId, string operatorId = "Operator")
        {
            if (_pendingRequests.TryRemove(requestId, out var req))
            {
                lock (_lock)
                {
                    _auditLog.Add(new HitlAuditRecord(requestId, req.ToolName, req.Argument, false, operatorId));
                }
                return req.CompletionSource.TrySetResult(false);
            }
            return false;
        }
    }
}
