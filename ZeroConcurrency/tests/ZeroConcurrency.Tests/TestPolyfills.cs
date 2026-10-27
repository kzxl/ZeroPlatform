#if !NETCOREAPP && !NET8_0_OR_GREATER
using System;
using System.Text;
using System.Threading.Tasks;

namespace ZeroConcurrency.Tests
{
    internal static class TestPolyfills
    {
        public static unsafe string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
        {
            if (bytes.IsEmpty) return string.Empty;
            fixed (byte* p = bytes)
            {
                return encoding.GetString(p, bytes.Length);
            }
        }

        public static string GetString(this Encoding encoding, Span<byte> bytes)
            => encoding.GetString((ReadOnlySpan<byte>)bytes);

        public static async Task WaitAsync(this Task task, TimeSpan timeout)
        {
            if (task == await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false))
            {
                await task.ConfigureAwait(false);
            }
            else
            {
                throw new TimeoutException($"Task timed out after {timeout}");
            }
        }
    }
}
#endif
