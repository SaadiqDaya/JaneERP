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
        public bool IsRunning { get; private set; }

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

        private async Task PerformSyncAsync()
        {
            try
            {
                // perform a full fetch for now (could be optimized with updated_at_min)
                var orders = await _client.GetOrdersAsync(_store, _token).ConfigureAwait(false);
                await _db.UpsertOrdersAsync(orders, _store).ConfigureAwait(false);
                SyncCompleted?.Invoke(this, new SyncCompletedEventArgs(true, null, orders.Count));
            }
            catch (Exception ex)
            {
                SyncCompleted?.Invoke(this, new SyncCompletedEventArgs(false, ex, 0));
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
