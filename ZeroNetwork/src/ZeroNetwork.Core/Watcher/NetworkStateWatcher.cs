using System;
using System.Net.NetworkInformation;
using System.Threading;

namespace ZeroNetwork.Watcher
{
    /// <summary>
    /// Monitors network availability and address configuration changes with built-in debounce protection.
    /// </summary>
    public class NetworkStateWatcher : IDisposable
    {
        private readonly int _debounceMs;
        private Timer? _debounceTimer;
        private readonly object _lock = new object();
        private bool _disposed;

        public event EventHandler<bool>? NetworkAvailabilityChanged;
        public event EventHandler? NetworkAddressChanged;

        public bool IsNetworkAvailable => NetworkInterface.GetIsNetworkAvailable();

        public NetworkStateWatcher(int debounceMs = 300)
        {
            _debounceMs = Math.Max(50, debounceMs);

            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        }

        private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
        {
            Debounce(() =>
            {
                NetworkAvailabilityChanged?.Invoke(this, e.IsAvailable);
            });
        }

        private void OnNetworkAddressChanged(object? sender, EventArgs e)
        {
            Debounce(() =>
            {
                NetworkAddressChanged?.Invoke(this, EventArgs.Empty);
            });
        }

        private void Debounce(Action action)
        {
            lock (_lock)
            {
                if (_disposed) return;

                _debounceTimer?.Dispose();
                _debounceTimer = new Timer(_ =>
                {
                    try
                    {
                        action();
                    }
                    catch
                    {
                        // Suppress event listener exceptions
                    }
                }, null, _debounceMs, Timeout.Infinite);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;

                NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
                NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;

                _debounceTimer?.Dispose();
                _debounceTimer = null;
            }
        }
    }
}
