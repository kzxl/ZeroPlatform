using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroUI.Core.Security
{
    /// <summary>
    /// Severity level of an audit trail operation log entry.
    /// </summary>
    public enum OperationLogLevel
    {
        Info = 0,
        Notice = 1,
        Warning = 2,
        SecurityAudit = 3,
        Error = 4,
        Critical = 5
    }

    /// <summary>
    /// Represents an immutable audit trail record for compliance (ISA-88, 21 CFR Part 11).
    /// </summary>
    public class OperationLogEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string? UserId { get; set; }
        public string? Username { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public string? TargetResource { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public OperationLogLevel Level { get; set; } = OperationLogLevel.Info;
        public string? Details { get; set; }
        public string? Workstation { get; set; }

        public OperationLogEntry() { }

        public OperationLogEntry(
            string action,
            string? username = null,
            string? targetResource = null,
            string? oldValue = null,
            string? newValue = null,
            OperationLogLevel level = OperationLogLevel.Info,
            string category = "General",
            string? details = null)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            Username = username;
            TargetResource = targetResource;
            OldValue = oldValue;
            NewValue = newValue;
            Level = level;
            Category = category;
            Details = details;
        }
    }

    /// <summary>
    /// Filter parameters for querying historical audit logs.
    /// </summary>
    public class OperationLogFilter
    {
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public string? Username { get; set; }
        public string? Action { get; set; }
        public string? Category { get; set; }
        public OperationLogLevel? MinLevel { get; set; }
        public int MaxResults { get; set; } = 1000;
    }

    /// <summary>
    /// Decoupled interface for capturing and retrieving audit trail and operation logs.
    /// </summary>
    public interface IOperationLogger
    {
        /// <summary>
        /// Records an operation log entry.
        /// </summary>
        void Log(OperationLogEntry entry);

        /// <summary>
        /// Asynchronously queries historical operation log entries based on filter criteria.
        /// </summary>
        Task<IReadOnlyList<OperationLogEntry>> QueryLogsAsync(OperationLogFilter? filter = null, CancellationToken cancellationToken = default);
    }
}
