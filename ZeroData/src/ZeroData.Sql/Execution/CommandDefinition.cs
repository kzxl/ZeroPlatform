using System;
using System.Data;
using System.Threading;

namespace ZeroData.Sql
{
    /// <summary>
    /// Represents an executable SQL command definition including parameters, transaction, and timeout.
    /// Drop-in replacement for Dapper's CommandDefinition.
    /// </summary>
    public readonly struct CommandDefinition
    {
        public string CommandText { get; }
        public object Parameters { get; }
        public IDbTransaction Transaction { get; }
        public int? CommandTimeout { get; }
        public CommandType? CommandType { get; }
        public CommandFlags Flags { get; }
        public CancellationToken CancellationToken { get; }

        public CommandDefinition(
            string commandText,
            object parameters = null,
            IDbTransaction transaction = null,
            int? commandTimeout = null,
            CommandType? commandType = null,
            CommandFlags flags = CommandFlags.Buffered,
            CancellationToken cancellationToken = default)
        {
            CommandText = commandText;
            Parameters = parameters;
            Transaction = transaction;
            CommandTimeout = commandTimeout;
            CommandType = commandType;
            Flags = flags;
            CancellationToken = cancellationToken;
        }
    }

    [Flags]
    public enum CommandFlags
    {
        None = 0,
        Buffered = 1,
        Pipelined = 2,
        NoCache = 4
    }
}
