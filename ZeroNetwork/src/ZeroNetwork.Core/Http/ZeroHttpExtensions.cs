using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.Http
{
    /// <summary>
    /// Fluent HTTP extensions for string URLs, providing zero-allocation stream-based API calls.
    /// Drop-in high-performance modern replacement for legacy HttpExtension patterns.
    /// </summary>
    public static class ZeroHttpExtensions
    {
        private static readonly Lazy<ZeroApiClient> _sharedClient = new Lazy<ZeroApiClient>(
            () => new ZeroApiClient(new ZeroApiClientOptions
            {
                Timeout = TimeSpan.FromSeconds(60),
                MaxRetryAttempts = 3
            }),
            LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Shared default <see cref="ZeroApiClient"/> instance used for fluent string extensions.
        /// </summary>
        public static ZeroApiClient Shared => _sharedClient.Value;

        /// <summary>
        /// Sends an HTTP GET request to the URL, streaming the response directly into <typeparamref name="T"/>.
        /// </summary>
        public static Task<T?> GetJsonAsync<T>(
            this string url,
            object? queryParams = null,
            string? bearerToken = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentNullException(nameof(url));
            return Shared.GetAsync<T>(url, queryParams, bearerToken, apiKey, cancellationToken);
        }

        /// <summary>
        /// Sends an HTTP POST request with a JSON payload to the URL, streaming the response directly into <typeparamref name="TResult"/>.
        /// </summary>
        public static Task<TResult?> PostJsonAsync<TResult>(
            this string url,
            object payload,
            string? bearerToken = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentNullException(nameof(url));
            return Shared.PostAsync<object, TResult>(url, payload, bearerToken, apiKey, cancellationToken);
        }

        /// <summary>
        /// Sends an HTTP PUT request with a JSON payload to the URL, streaming the response directly into <typeparamref name="TResult"/>.
        /// </summary>
        public static Task<TResult?> PutJsonAsync<TResult>(
            this string url,
            object payload,
            string? bearerToken = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentNullException(nameof(url));
            return Shared.PutAsync<object, TResult>(url, payload, bearerToken, apiKey, cancellationToken);
        }

        /// <summary>
        /// Sends an HTTP DELETE request to the URL, streaming the response directly into <typeparamref name="TResult"/>.
        /// </summary>
        public static Task<TResult?> DeleteJsonAsync<TResult>(
            this string url,
            string? bearerToken = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentNullException(nameof(url));
            return Shared.DeleteAsync<TResult>(url, bearerToken, apiKey, cancellationToken);
        }

        /// <summary>
        /// Downloads raw byte content from a URL directly using streaming memory copies.
        /// </summary>
        public static async Task<byte[]> DownloadBytesAsync(
            this string url,
            string? bearerToken = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentNullException(nameof(url));

            using (var stream = await Shared.GetStreamAsync(url, bearerToken, apiKey, cancellationToken).ConfigureAwait(false))
            using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms).ConfigureAwait(false);
                return ms.ToArray();
            }
        }
    }
}
