using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroUI.Core.Communication
{
    /// <summary>
    /// Connection lifecycle states for industrial protocol adapters.
    /// </summary>
    public enum AdapterConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Faulted
    }

    /// <summary>
    /// Supported data types for field register to engineering tag mapping.
    /// </summary>
    public enum TagDataType
    {
        Boolean,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Float32,
        Double64,
        StringAscii
    }

    /// <summary>
    /// Definition of a mapped field tag to a SCADA engineering path.
    /// </summary>
    public sealed class AdapterTagDefinition
    {
        public string TagPath { get; }
        public string FieldAddress { get; }
        public TagDataType DataType { get; }
        public double Scale { get; }
        public double Offset { get; }

        public AdapterTagDefinition(
            string tagPath,
            string fieldAddress,
            TagDataType dataType = TagDataType.Float32,
            double scale = 1.0,
            double offset = 0.0)
        {
            TagPath = tagPath ?? throw new ArgumentNullException(nameof(tagPath));
            FieldAddress = fieldAddress ?? throw new ArgumentNullException(nameof(fieldAddress));
            DataType = dataType;
            Scale = scale;
            Offset = offset;
        }
    }

    /// <summary>
    /// Standardized contract for all industrial communication protocol adapters.
    /// </summary>
    public interface IProtocolAdapter : IDisposable
    {
        string AdapterId { get; }
        string Endpoint { get; }
        AdapterConnectionState State { get; }
        TimeSpan Latency { get; }

        event Action<IProtocolAdapter, AdapterConnectionState>? StateChanged;

        void RegisterTag(AdapterTagDefinition tagDef);
        IReadOnlyCollection<AdapterTagDefinition> GetRegisteredTags();

        Task ConnectAsync(CancellationToken cancellationToken = default);
        Task DisconnectAsync(CancellationToken cancellationToken = default);
        Task PollOnceAsync(CancellationToken cancellationToken = default);
        Task<bool> WriteTagAsync(string tagPath, object value, CancellationToken cancellationToken = default);
    }
}
