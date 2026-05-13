using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using JaneERP.Services;
using JaneERP.Data;
using System.Collections.Generic;

namespace JaneERP.Services
{
    // Background sync worker that raises an event when sync completes.
    public class SyncService : IDisposable
    {
        private readonly TimeSpan _interval;
        private readonly System.Threading.Timer? _timer;
        private readonly string _store;
        private readonly string _token;
        private readonly HttpClient _httpClient;
        private readonly ShopifyClient _client;
        private readonly AppDbContext _db;

        public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;
        public event EventHandler? SyncStarted;

        public bool IsRunning  { get; private set; }
        public bool IsSyncing  { get; private set; }
        public bool LastSyncFailed { get; private set; }
        public DateTime? LastSyncAt { get; private set; }

        public SyncService(string store, string token, TimeSpan? interval = null)
        {
            _interval = interval ?? TimeSpan.FromMinutes(5);
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _token = token ?? throw new ArgumentNullException(nameof(token));
            _httpClient = new HttpClient();
            _client = new ShopifyClient(_httpClient);
            _db = new AppDbContext();
            _timer = new System.Threading.Timer(async _ => await PerformSyncAsync(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public void Start()
        {
            if (IsRunning) return;
            IsRunning = true;
            _timer?.Change(TimeSpan.Zero, _interval);
        }

        public void Stop()
        {
            if (!IsRunning) return;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            IsRunning = false;
        }

        /// <summary>Triggers an immediate sync without waiting for the next scheduled interval.
        /// No-op if a sync is already in progress.</summary>
        public void TriggerNow()
        {
            if (IsSyncing) return;
            _ = Task.Run(PerformSyncAsync);
        }

        private DateTime? _lastSyncAt;

        private async Task PerformSyncAsync()
        {
            if (IsSyncing) return;
            IsSyncing = true;
            SyncStarted?.Invoke(this, EventArgs.Empty);
            try
            {
                // Delta sync: only fetch orders updated since the last successful sync.
                // On first run _lastSyncAt is null, so all orders are fetched.
                var since = _lastSyncAt;
                var orders = await _client.GetOrdersAsync(_store, _token, updatedAtMin: since)
                                          .ConfigureAwait(false);
                await _db.UpsertOrdersAsync(orders, _store).ConfigureAwait(false);
                _lastSyncAt = DateTime.UtcNow;
                LastSyncAt     = DateTime.Now;
                LastSyncFailed = false;
                SyncCompleted?.Invoke(this, new SyncCompletedEventArgs(true, null, orders.Count));
            }
            catch (Exception ex)
            {
                LastSyncAt     = DateTime.Now;
                LastSyncFailed = true;
                SyncCompleted?.Invoke(this, new SyncCompletedEventArgs(false, ex, 0));
            }
            finally
            {
                IsSyncing = false;
            }
        }

        public void Dispose()
        {
            Stop();
            _timer?.Dispose();
            _httpClient.Dispose();
            _db.Dispose();
        }
    }

    public class SyncCompletedEventArgs : EventArgs
    {
        public bool Success { get; }
        public Exception? Error { get; }
        public int Count { get; }

        public SyncCompletedEventArgs(bool success, Exception? error, int count)
        {
            Success = success;
            Error = error;
            Count = count;
        }
    }
}
