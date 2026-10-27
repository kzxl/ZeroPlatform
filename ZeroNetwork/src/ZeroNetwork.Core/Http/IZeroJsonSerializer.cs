using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#if NET8_0_OR_GREATER
using System.Text.Json;
#endif

namespace ZeroNetwork.Http
{
    /// <summary>
    /// Contract for high-performance, stream-based JSON serialization and deserialization.
    /// Operates directly on streams to eliminate intermediate string allocations and Large Object Heap (LOH) fragmentation.
    /// </summary>
    public interface IZeroJsonSerializer
    {
        /// <summary>
        /// Deserializes an object of type <typeparamref name="T"/> directly from a stream.
        /// </summary>
        Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Serializes an object of type <typeparamref name="T"/> directly to a stream.
        /// </summary>
        Task SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default);
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// High-performance default JSON serializer implementation for .NET 8.0+ using System.Text.Json stream pipelines.
    /// </summary>
    public class SystemTextJsonStreamSerializer : IZeroJsonSerializer
    {
        private readonly JsonSerializerOptions _options;

        public SystemTextJsonStreamSerializer(JsonSerializerOptions? options = null)
        {
            _options = options ?? new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream == null) return default;
            return await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken).ConfigureAwait(false);
        }

        public async Task SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default)
        {
            if (stream == null) return;
            await JsonSerializer.SerializeAsync(stream, value, _options, cancellationToken).ConfigureAwait(false);
        }
    }
#else
    /// <summary>
    /// Default lightweight string/stream serializer for legacy runtimes (.NET Standard 2.0 / .NET Framework 4.6.2).
    /// Can be overridden by plugging in NewtonsoftJsonStreamSerializer from consuming projects.
    /// </summary>
    public class FallbackStreamSerializer : IZeroJsonSerializer
    {
        public async Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream == null) return default;

            if (typeof(T) == typeof(string))
            {
                using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
                {
                    string text = await reader.ReadToEndAsync().ConfigureAwait(false);
                    return (T)(object)text;
                }
            }

            if (typeof(T) == typeof(byte[]))
            {
                using (var ms = new MemoryStream())
                {
                    await stream.CopyToAsync(ms).ConfigureAwait(false);
                    return (T)(object)ms.ToArray();
                }
            }

            throw new NotSupportedException(
                $"Direct deserialization to '{typeof(T).FullName}' on .NET Standard 2.0/4.6.2 requires configuring a JSON serializer. " +
                "Please set ZeroApiClient.DefaultSerializer = new YourJsonSerializer(); (e.g. wrapping Newtonsoft.Json or System.Text.Json).");
        }

        public async Task SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default)
        {
            if (stream == null || value == null) return;

            if (value is string str)
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(str);
                await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (value is byte[] rawBytes)
            {
                await stream.WriteAsync(rawBytes, 0, rawBytes.Length, cancellationToken).ConfigureAwait(false);
                return;
            }

            throw new NotSupportedException(
                $"Serialization of '{typeof(T).FullName}' on .NET Standard 2.0/4.6.2 requires configuring a JSON serializer. " +
                "Please set ZeroApiClient.DefaultSerializer = new YourJsonSerializer();");
        }
    }
#endif
}
