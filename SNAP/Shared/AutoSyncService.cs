using System.Net.NetworkInformation;
using SNAP.infrastructure.config;
using SNAP.infrastructure.services;

namespace SNAP
{
    /// <summary>
    /// Pushes rows saved while offline to MonsterASP automatically once the connection is back,
    /// instead of leaving them waiting for the next unrelated save or a manual "Sync Now".
    ///
    /// Two triggers, both landing on the same TenantSyncService.PushPendingIfIdle:
    ///  - Windows' NetworkAvailabilityChanged, the moment it reports a network is available.
    ///  - A timer every 5 seconds - Windows can report "connected" without real internet
    ///    behind it (or miss the change entirely), so this is the backstop, and it keeps the
    ///    remote close to real-time even when the save-triggered push didn't make it.
    /// Neither one decides whether the internet is "really" up: the push attempt itself is the
    /// test. If MonsterASP can't be reached, the rows simply stay pending until the next try.
    ///
    /// Each tick first checks the local outbox and does nothing (no network traffic) when it's
    /// empty, and PushPendingIfIdle skips outright if a push for this tenant is already running.
    /// Started once the dashboard opens for a signed-in tenant; stopped on logout and app close.
    /// </summary>
    internal static class AutoSyncService
    {
        private static readonly TimeSpan FirstTry = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);

        private static readonly object Sync = new();
        private static System.Threading.Timer? _timer;
        private static string? _localConnectionString;

        public static void Start(string? localConnectionString)
        {
            lock (Sync)
            {
                StopLocked();
                if (string.IsNullOrWhiteSpace(localConnectionString)) return;

                _localConnectionString = localConnectionString;
                NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
                _timer = new System.Threading.Timer(_ => TryPush(), null, FirstTry, RetryInterval);
            }
        }

        public static void Stop()
        {
            lock (Sync)
            {
                StopLocked();
            }
        }

        private static void StopLocked()
        {
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            _timer?.Dispose();
            _timer = null;
            _localConnectionString = null;
        }

        private static void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
        {
            if (e.IsAvailable)
            {
                Task.Run(TryPush);
            }
        }

        private static void TryPush()
        {
            var local = _localConnectionString;
            if (local is null) return;

            try
            {
                if (!TenantSyncService.HasPending(local)) return;

                var remote = ConnectionStrings.TryResolveRemoteForLocal(local);
                TenantSyncService.PushPendingIfIdle(local, remote);
            }
            catch
            {
                // Best-effort background work on a timer thread - an exception here would
                // otherwise take the whole app down. Rows stay pending and the next tick retries.
            }
        }
    }
}
