using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using JaneERP.Data;
using JaneERP.Logging;
using JaneERP.Models;
using JaneERP.Security;
using JaneERP.Services;

namespace JaneERP
{
    public partial class FormSalesDash : Form
    {
        private string _store;
        private string _token;
        private ShopifyStore? _currentStore;   // null when "All Stores" is selected
        private List<ShopifyStore> _allStores;
        private List<Order> _fullOrders = new List<Order>();
        private BindingList<Order> _currentOrders = new BindingList<Order>();
        private SyncService? _syncService;
        private CancellationTokenSource? _syncCts;

        // Persisted across form instances within the same session
        private static DateTime _lastFromDate = new DateTime(2026, 4, 1);
        private static DateTime _lastToDate   = DateTime.Today;

        /// <summary>Opens directly to "All Stores" mode — no Shopify store pre-selected.</summary>
        public FormSalesDash(List<ShopifyStore> allStores) : this(null, allStores) { }

        public FormSalesDash(ShopifyStore? initialStore, List<ShopifyStore> allStores)
        {
            _currentStore = initialStore;
            _store        = initialStore?.StoreDomain ?? "";
            _token        = initialStore?.Token ?? "";
            _allStores    = allStores;

            InitializeComponent();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);

            // Default date range: remembered from last session (or Apr 1 2026 on first open)
            dtpFrom.Value = _lastFromDate;
            dtpTo.Value   = _lastToDate;

            // Save date range whenever it changes
            dtpFrom.ValueChanged += (_, _) => _lastFromDate = dtpFrom.Value;
            dtpTo.ValueChanged   += (_, _) => _lastToDate   = dtpTo.Value;

            InitializeGridColumns();
            PopulateStoreFilter();
            LoadCachedOrders();

            // Wire real-time filter events
            dtpFrom.ValueChanged    += (_, _) => ApplyFilters();
            dtpTo.ValueChanged      += (_, _) => ApplyFilters();
            txtMinAmount.TextChanged += (_, _) => ApplyFilters();
            txtMaxAmount.TextChanged += (_, _) => ApplyFilters();

            // Permission: only Editors/Admins can sync to ERP
            btnSyncToERP.Enabled = Security.PermissionHelper.CanEdit("SalesOrders");

            Shown += FormSalesDash_Shown;
        }

        private void FormSalesDash_Shown(object? sender, EventArgs e)
        {
            try
            {
                if (_currentStore != null && !string.IsNullOrEmpty(_store) && !string.IsNullOrEmpty(_token))
                    StartBackgroundSync(_store, _token);
            }
            catch (Exception ex) { AppLogger.Info($"[FormSalesDash.FormSalesDash_Shown]: {ex.Message}"); }
        }

        private void PopulateStoreFilter()
        {
            cboStoreFilter.Items.Clear();
            cboStoreFilter.Items.Add("All Stores");
            cboStoreFilter.Items.Add("Manual Orders");
            cboStoreFilter.Items.Add("All ERP Orders");
            foreach (var s in _allStores)
                cboStoreFilter.Items.Add(s);

            // Select the initial store
            var idx = _allStores.FindIndex(s => s.StoreID == _currentStore?.StoreID);
            cboStoreFilter.SelectedIndex = idx >= 0 ? idx + 3 : 0; // +3 for the 3 fixed entries
        }

        // Returns true if the current filter is an ERP-direct view (not Shopify cache)
        private bool IsErpView => cboStoreFilter.SelectedIndex == 1 || cboStoreFilter.SelectedIndex == 2;

        private void CboStoreFilter_SelectedIndexChanged(object? sender, EventArgs e)
        {
            switch (cboStoreFilter.SelectedIndex)
            {
                case 0: // All Stores (Shopify cache)
                    _currentStore = null; _store = ""; _token = "";
                    _syncService?.Stop();
                    btnFetch.Enabled     = false;
                    btnSyncToERP.Enabled = false;
                    Text = "Shopify Orders — All Stores";
                    LoadCachedOrders();
                    break;

                case 1: // Non-Shopify Orders
                    _currentStore = null; _store = ""; _token = "";
                    btnFetch.Enabled     = false;
                    btnSyncToERP.Enabled = false;
                    Text = "ERP Orders — Non-Shopify";
                    LoadErpOrders(null, nonShopifyOnly: true);
                    break;

                case 2: // All ERP Orders
                    _currentStore = null; _store = ""; _token = "";
                    btnFetch.Enabled     = false;
                    btnSyncToERP.Enabled = false;
                    Text = "ERP Orders — All";
                    LoadErpOrders(null);
                    break;

                default: // specific Shopify store
                    var selected = (ShopifyStore)cboStoreFilter.SelectedItem!;
                    _currentStore = selected;
                    _store        = selected.StoreDomain;
                    _token        = selected.Token ?? "";
                    btnFetch.Enabled     = true;
                    btnSyncToERP.Enabled = Security.PermissionHelper.CanEdit("SalesOrders");
                    Text = $"Shopify Orders — {selected.StoreName}";
                    if (!string.IsNullOrEmpty(_store) && !string.IsNullOrEmpty(_token))
                        StartBackgroundSync(_store, _token);
                    LoadCachedOrders();
                    break;
            }
        }

        private async void LoadErpOrders(string? orderType, bool nonShopifyOnly = false)
        {
            lblStatus.Text = "Loading ERP orders...";
            try
            {
                var orders = await Task.Run(() =>
                    new Services.ShopifySyncService().GetErpOrders(orderType, nonShopifyOnly));
                _fullOrders = orders;
                ApplyFilters();
                lblStatus.Text = $"{orders.Count} ERP order(s)";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "ERP load failed";
                AppLogger.Audit("system", "ErpOrderLoadFailed", ex.Message);
            }
        }

        private void InitializeGridColumns()
        {
            // Prevent automatic columns from the data source
            dgvOrders.AutoGenerateColumns = false;
            dgvOrders.Columns.Clear();

            // Make the grid editable so checkbox can be changed, but make data columns read-only individually
            dgvOrders.ReadOnly = false;
            dgvOrders.AllowUserToAddRows = false;
            dgvOrders.AllowUserToDeleteRows = false;
            dgvOrders.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvOrders.MultiSelect = true;

            // Checkbox column bound to Order.Selected
            var chk = new DataGridViewCheckBoxColumn
            {
                Name = "colSelected",
                HeaderText = "",
                DataPropertyName = "Selected",
                Width = 30,
                ReadOnly = false,
                TrueValue = true,
                FalseValue = false
            };
            dgvOrders.Columns.Add(chk);

            // Order number
            dgvOrders.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colOrderNumber",
                HeaderText = "Order #",
                DataPropertyName = "OrderNumber",
                ReadOnly = true,
                Width = 90
            });

            // Store
            dgvOrders.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colStore",
                HeaderText = "Store",
                DataPropertyName = "StoreName",
                ReadOnly = true,
                Width = 140
            });

            // Sync status
            dgvOrders.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colSyncStatus",
                HeaderText = "ERP Sync",
                DataPropertyName = "SyncStatus",
                ReadOnly = true,
                Width = 80
            });

            // ERP order status (Draft / Live / WIP / Complete)
            dgvOrders.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colErpStatus",
                HeaderText       = "Status",
                DataPropertyName = "ErpStatus",
                ReadOnly         = true,
                Width            = 80
            });

            // Customer / Name (fills remaining space)
            dgvOrders.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colName",
                HeaderText = "Name",
                DataPropertyName = "Name",
                ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            // Created at
            dgvOrders.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colCreatedAt",
                HeaderText = "Created",
                DataPropertyName = "CreatedAt",
                ReadOnly = true,
                Width = 140
            });

            // Total price (currency)
            var currencyStyle = new DataGridViewCellStyle { Format = "C2" };
            dgvOrders.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colTotalPrice",
                HeaderText = "Total",
                DataPropertyName = "TotalPrice",
                ReadOnly = true,
                Width = 100,
                DefaultCellStyle = currencyStyle
            });

            // Ensure checkbox edits commit immediately
            dgvOrders.CurrentCellDirtyStateChanged -= DgvOrders_CurrentCellDirtyStateChanged;
            dgvOrders.CurrentCellDirtyStateChanged += DgvOrders_CurrentCellDirtyStateChanged;

            // Wire double-click for single-row action
            dgvOrders.CellDoubleClick -= DgvOrders_CellDoubleClick;
            dgvOrders.CellDoubleClick += DgvOrders_CellDoubleClick;
        }

        private void LoadCachedOrders()
        {
            try
            {
                using var db = new AppDbContext();
                var cached = db.GetCachedOrders(_currentStore?.StoreDomain);

                var domainToName = _allStores.ToDictionary(
                    s => s.StoreDomain, s => s.StoreName,
                    StringComparer.OrdinalIgnoreCase);

                HashSet<long> syncedIds;
                Dictionary<long, (int id, string status)> erpInfo;
                try
                {
                    var svc = new ShopifySyncService();
                    syncedIds = svc.GetSyncedOrderIds();
                    // Get ERP status for synced orders
                    var erpOrders = svc.GetErpOrders("Shopify");
                    erpInfo = erpOrders
                        .Where(o => o.Id != 0 && o.ErpSalesOrderID.HasValue)
                        .ToDictionary(o => o.Id,
                                      o => (o.ErpSalesOrderID!.Value, o.ErpStatus ?? ""));
                }
                catch { syncedIds = new HashSet<long>(); erpInfo = new(); }

                foreach (var order in cached)
                {
                    order.StoreName  = domainToName.TryGetValue(order.StoreDomain ?? "", out var n) ? n : order.StoreDomain;
                    order.SyncStatus = syncedIds.Contains(order.Id) ? "Synced" : "Not Synced";
                    if (erpInfo.TryGetValue(order.Id, out var erp))
                    {
                        order.ErpSalesOrderID = erp.id;
                        order.ErpStatus       = erp.status;
                    }
                }

                _fullOrders = cached;
                ApplyFilters();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Cache load failed";
                AppLogger.Audit("system", "CacheLoadFailed", ex.Message);
            }
        }

        private void ApplyFilters()
        {
            var from = dtpFrom.Value.Date;
            var to   = dtpTo.Value.Date.AddDays(1).AddTicks(-1);

            decimal.TryParse(txtMinAmount.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var min);
            var max = decimal.MaxValue;
            if (decimal.TryParse(txtMaxAmount.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedMax))
                max = parsedMax;

            var filtered = _fullOrders
                // Normalize UTC dates from Shopify to local time before comparing against picker values
                .Where(o => {
                    var local = o.CreatedAt.Kind == DateTimeKind.Utc ? o.CreatedAt.ToLocalTime() : o.CreatedAt;
                    return local >= from && local <= to;
                })
                .Where(o => o.TotalPrice >= min && o.TotalPrice <= max)
                .ToList();

            _currentOrders       = new BindingList<Order>(filtered);
            dgvOrders.DataSource = _currentOrders;
            lblStatus.Text       = $"{filtered.Count} order(s) shown";
        }

        private void StartBackgroundSync(string store, string token)
        {
            _syncService?.Dispose();
            _syncService = new SyncService(store, token, TimeSpan.FromMinutes(5));
            _syncService.SyncCompleted += SyncService_SyncCompleted;
            _syncService.Start();
            AppLogger.Audit(store, "BackgroundSyncStarted", $"Interval=5m");
        }

        private void SyncService_SyncCompleted(object? sender, SyncCompletedEventArgs e)
        {
            // Form may not be visible yet (race on startup sync) — bail safely
            if (!IsHandleCreated || IsDisposed) return;

            if (e.Success)
            {
                Invoke(LoadCachedOrders);
                Invoke(() => lblStatus.Text = $"Background sync: {e.Count} orders");
            }
            else
            {
                Invoke(() => lblStatus.Text = $"Background sync failed: {e.Error?.Message}");
                AppLogger.Audit("system", "BackgroundSyncFailed", e.Error?.ToString() ?? "unknown");
            }
        }

        // Ensure checkbox edits are committed to the data source immediately
        private void DgvOrders_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            if (dgvOrders.IsCurrentCellDirty)
            {
                // commit edit so the checkbox change is written to the Order.Selected property
                dgvOrders.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        private async void DgvOrders_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _currentOrders.Count) return;

            // ERP-view mode: no Shopify API available — show basic ERP details
            if (IsErpView)
            {
                var o = _currentOrders[e.RowIndex];
                var details = new System.Text.StringBuilder();
                details.AppendLine($"Order #:    {o.OrderNumber}");
                details.AppendLine($"Customer:   {o.Name}");
                details.AppendLine($"Date:       {o.CreatedAt:yyyy-MM-dd}");
                details.AppendLine($"Total:      {o.TotalPrice:C2} {o.Currency}");
                if (o.DiscountAmount > 0)
                {
                    details.AppendLine($"Discount:   {o.DiscountType ?? "Discount"}" +
                        (o.DiscountPercent > 0 ? $" ({o.DiscountPercent:N2}%)" : "") +
                        $" → -{o.DiscountAmount:C2}");
                }
                details.AppendLine($"Status:     {o.ErpStatus ?? "—"}");
                details.AppendLine($"Type:       {o.StoreName}");

                MessageBox.Show(this, details.ToString().TrimEnd(),
                    "ERP Order Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // check how many orders are currently checked (use underlying model)
            var checkedOrders = _currentOrders.Where(o => o.Selected).ToList();
            if (checkedOrders.Count > 1)
            {
                await ShowMultiOrderDetailsForOrdersAsync(checkedOrders).ConfigureAwait(false);
                return;
            }

            // if no checkbox or only one checked, fall back to single-row double-click behavior
            var selected = _currentOrders[e.RowIndex];
            await ShowSingleOrderDetailsAsync(selected).ConfigureAwait(false);
        }

        private List<Order> GetCheckedOrdersFromGrid()
        {
            // Prefer checkbox-selected orders; fall back to grid-highlighted rows
            var checked_ = _currentOrders.Where(o => o.Selected).ToList();
            if (checked_.Count > 0) return checked_;

            // Use highlighted (selected) rows when no checkboxes are ticked
            return dgvOrders.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as Order)
                .Where(o => o != null)
                .Select(o => o!)
                .ToList();
        }

        private async void btnViewMultiple_Click(object? sender, EventArgs e)
        {
            var checkedOrders = GetCheckedOrdersFromGrid();
            if (checkedOrders.Count == 0)
            {
                MessageBox.Show(this, "No orders selected. Please check the boxes for the orders you want to view.", "No selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            await ShowMultiOrderDetailsForOrdersAsync(checkedOrders).ConfigureAwait(false);
        }

        // update fetch button to also upsert into DB and audit
        private async void btnFetch_Click(object? sender, EventArgs e)
        {
            AppLogger.Audit(_store, "FetchClicked", $"From={dtpFrom.Value:yyyy-MM-dd} To={dtpTo.Value:yyyy-MM-dd}");
            btnFetch.Enabled = false;
            btnSave.Enabled = false;
            lblStatus.Text = "Fetching...";
            try
            {
                var store = NormalizeStoreInput(_store);
                var token = _token;
                DateTime? from = dtpFrom.Value.Date;
                DateTime? to = dtpTo.Value.Date.AddDays(1).AddTicks(-1);

                decimal? minAmount = null;
                decimal? maxAmount = null;
                if (decimal.TryParse(txtMinAmount.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var ma)) minAmount = ma;
                if (decimal.TryParse(txtMaxAmount.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var Mb)) maxAmount = Mb;

                using var httpClient = new HttpClient();
                var client = new ShopifyClient(httpClient);

                var progress = new Progress<string>(s => lblStatus.Text = s);

                var orders = await client.GetOrdersAsync(store, token, from, to, minAmount, maxAmount, progress).ConfigureAwait(false);

                // Tag orders with the current store before caching
                foreach (var o in orders) o.StoreDomain = store;

                // persist to DB
                using (var db = new AppDbContext())
                {
                    await db.UpsertOrdersAsync(orders, store).ConfigureAwait(false);
                }

                Invoke(new Action(() =>
                {
                    // Populate StoreName and SyncStatus before displaying
                    var domainToName = _allStores.ToDictionary(
                        s => s.StoreDomain, s => s.StoreName, StringComparer.OrdinalIgnoreCase);
                    HashSet<long> syncedIds;
                    try { syncedIds = new ShopifySyncService().GetSyncedOrderIds(); }
                    catch { syncedIds = new HashSet<long>(); }

                    foreach (var o in orders)
                    {
                        o.StoreName  = domainToName.TryGetValue(o.StoreDomain ?? "", out var n) ? n : o.StoreDomain;
                        o.SyncStatus = syncedIds.Contains(o.Id) ? "Synced" : "Not Synced";
                    }

                    _fullOrders = orders.OrderByDescending(o => o.CreatedAt).ToList();
                    ApplyFilters();
                    lblStatus.Text  = $"Fetched {_fullOrders.Count} orders";
                    btnSave.Enabled = _fullOrders.Count > 0;
                }));

                AppLogger.Audit(_store, "FetchCompleted", $"FetchedCount={orders.Count}");
            }
            catch (Exception ex)
            {
                Invoke(new Action(() =>
                {
                    lblStatus.Text = $"Error: {ex.Message}";
                    MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
                AppLogger.Audit("system", "FetchFailed", ex.ToString());
            }
            finally
            {
                Invoke(new Action(() => btnFetch.Enabled = true));
            }
        }

        // Load order details — try SQLite cache first, only fall back to Shopify API if not cached
        private async Task ShowSingleOrderDetailsAsync(Order selected)
        {
            // Try cache first (avoids Shopify API calls for already-fetched orders)
            try
            {
                using var dbCtx = new AppDbContext();
                var entity = await dbCtx.Orders.FindAsync(selected.Id).ConfigureAwait(false);
                if (entity?.RawJson != null)
                {
                    var opts    = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var cached  = System.Text.Json.JsonSerializer.Deserialize<OrderDetails>(entity.RawJson, opts);
                    if (cached?.LineItems?.Count > 0)
                    {
                        Invoke(() =>
                        {
                            using var f = new OrderDetailsForm(cached);
                            f.ShowDialog(this);
                            lblStatus.Text = "Ready (cached)";
                        });
                        AppLogger.Audit(_store, "ViewOrderCached", $"OrderId={selected.Id}");
                        return;
                    }
                }
            }
            catch (Exception ex) { AppLogger.Info($"[ShowSingleOrderDetailsAsync cache miss]: {ex.Message}"); }

            // Not in cache or no line items — show basic info if no store/token available
            if (string.IsNullOrEmpty(_store) || string.IsNullOrEmpty(_token))
            {
                Invoke(() =>
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"Order #:    {selected.OrderNumber}");
                    sb.AppendLine($"Customer:   {selected.Name}");
                    sb.AppendLine($"Date:       {(selected.CreatedAt.Kind == DateTimeKind.Utc ? selected.CreatedAt.ToLocalTime() : selected.CreatedAt):yyyy-MM-dd HH:mm}");
                    sb.AppendLine($"Total:      {selected.TotalPrice:C2}");
                    sb.AppendLine($"Store:      {selected.StoreName}");
                    sb.AppendLine($"ERP Sync:   {selected.SyncStatus}");
                    if (!string.IsNullOrEmpty(selected.ErpStatus))
                        sb.AppendLine($"Status:     {selected.ErpStatus}");
                    sb.AppendLine();
                    sb.AppendLine("Line item details are available after fetching from a specific store.");
                    MessageBox.Show(this, sb.ToString().TrimEnd(),
                        $"Order {selected.OrderNumber}", MessageBoxButtons.OK, MessageBoxIcon.Information);
                });
                return;
            }

            var store = NormalizeStoreInput(_store);
            var token = _token;
            using var httpClient = new HttpClient();
            var client   = new ShopifyClient(httpClient);
            var progress = new Progress<string>(s => lblStatus.Text = s);

            try
            {
                lblStatus.Text = "Loading order details from Shopify...";
                var details = await client.GetOrderAsync(store, token, selected.Id, progress).ConfigureAwait(false);

                // Persist so next click uses cache
                using (var db = new AppDbContext())
                    await db.UpsertOrderDetailsAsync(details).ConfigureAwait(false);

                Invoke(() =>
                {
                    using var detailsForm = new OrderDetailsForm(details);
                    detailsForm.ShowDialog(this);
                    lblStatus.Text = "Ready";
                });
                AppLogger.Audit(store, "ViewOrder", $"OrderId={selected.Id}");
            }
            catch (Exception ex)
            {
                Invoke(() =>
                {
                    lblStatus.Text = $"Error: {ex.Message}";
                    MessageBox.Show(this, ex.ToString(), "Order details error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                });
                AppLogger.Audit("system", "GetOrderFailed", ex.ToString());
            }
        }

        private async Task ShowMultiOrderDetailsForOrdersAsync(IEnumerable<Order> orders)
        {
            var orderList = orders?.ToList() ?? new List<Order>();
            if (!orderList.Any()) return;

            var jsonOpts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            try
            {
                Invoke(() => lblStatus.Text = "Loading multiple order details...");

                var detailsList    = new List<OrderDetails>();
                var uncachedOrders = new List<Order>();

                // Try SQLite cache first for all orders (avoids API calls and works in All Stores mode)
                using (var dbCtx = new AppDbContext())
                {
                    foreach (var order in orderList)
                    {
                        try
                        {
                            var entity = await dbCtx.Orders.FindAsync(order.Id).ConfigureAwait(false);
                            if (entity?.RawJson != null)
                            {
                                var cached = System.Text.Json.JsonSerializer.Deserialize<OrderDetails>(entity.RawJson, jsonOpts);
                                if (cached?.LineItems?.Count > 0) { detailsList.Add(cached); continue; }
                            }
                        }
                        catch { }
                        uncachedOrders.Add(order);
                    }
                }

                // For uncached orders, fall back to Shopify API if we have store+token
                if (uncachedOrders.Count > 0)
                {
                    if (!string.IsNullOrEmpty(_store) && !string.IsNullOrEmpty(_token))
                    {
                        var store    = NormalizeStoreInput(_store);
                        var progress = new Progress<string>(s => lblStatus.Text = s);
                        using var httpClient = new HttpClient();
                        var client = new ShopifyClient(httpClient);
                        var fetchTasks = uncachedOrders
                            .Select(o => client.GetOrderAsync(store, _token, o.Id, progress))
                            .ToArray();
                        var fetched = await Task.WhenAll(fetchTasks).ConfigureAwait(false);
                        detailsList.AddRange(fetched);
                    }
                    else if (detailsList.Count == 0)
                    {
                        // Nothing from cache AND no API access — nothing to show
                        Invoke(() => MessageBox.Show(this,
                            "Line item details are not available.\nFetch orders from a specific store first.",
                            "Not Available", MessageBoxButtons.OK, MessageBoxIcon.Information));
                        return;
                    }
                    else if (uncachedOrders.Count > 0)
                    {
                        // Partial: some orders had cached details, some did not
                        Invoke(() => MessageBox.Show(this,
                            $"Note: {uncachedOrders.Count} order(s) had no cached line items and were skipped.",
                            "Partial Results", MessageBoxButtons.OK, MessageBoxIcon.Warning));
                    }
                }

                if (!detailsList.Any()) return;

                var aggregated = new List<Models.AggregatedLineItem>();
                foreach (var details in detailsList)
                {
                    var orderNumberLabel = details.Name ?? details.OrderNumber.ToString();
                    foreach (var li in details.LineItems)
                    {
                        aggregated.Add(new Models.AggregatedLineItem
                        {
                            OrderNumber = orderNumberLabel,
                            PartNumber  = li.Sku,
                            Title       = li.Title,
                            Quantity    = li.Quantity,
                            Price       = li.Price,
                        });
                    }
                }

                Invoke(new Action(() =>
                {
                    using var multi = new MultiOrderDetailsForm(aggregated);
                    multi.ShowDialog(this);
                    lblStatus.Text = "Ready";
                }));
            }
            catch (Exception ex)
            {
                Invoke(new Action(() =>
                {
                    lblStatus.Text = $"Error: {ex.Message}";
                    MessageBox.Show(this, ex.ToString(), "Multi-order details error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
            }
        }

        private static string NormalizeStoreInput(string storeInput)
            => Data.StoreRepository.NormalizeStoreDomain(storeInput);

        private async void btnSave_Click(object? sender, EventArgs e)
        {
            if (_currentOrders == null || !_currentOrders.Any())
            {
                MessageBox.Show(this, "No orders to save.", "Empty", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = $"orders_{DateTime.UtcNow:yyyyMMddHHmmss}.json"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                await ShopifyClient.SaveOrdersToFileAsync(_currentOrders.ToList(), dlg.FileName).ConfigureAwait(false);
                Invoke(new Action(() => lblStatus.Text = $"Saved {_currentOrders.Count} orders to {Path.GetFileName(dlg.FileName)}"));
            }
            catch (Exception ex)
            {
                Invoke(new Action(() =>
                {
                    MessageBox.Show(this, ex.Message, "Save error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = "Save failed";
                }));
            }
        }

        private async void btnSyncToERP_Click(object? sender, EventArgs e)
        {
            // Respect current selection — only sync checked/highlighted orders
            var orders = GetCheckedOrdersFromGrid();
            if (orders.Count == 0)
            {
                // Fall back to all displayed orders only if truly nothing is selected
                if (_currentOrders.Count == 0)
                {
                    MessageBox.Show(this, "Fetch orders first before syncing.", "No Orders",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                var answer = MessageBox.Show(this,
                    $"No orders are selected.\n\nSync all {_currentOrders.Count} displayed order(s)?",
                    "Sync All?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;
                orders = _currentOrders.ToList();
            }

            _syncCts              = new CancellationTokenSource();
            var token             = _syncCts.Token;
            btnSyncToERP.Enabled  = false;
            btnCancelSync.Enabled = true;
            btnFetch.Enabled      = false;
            lblStatus.Text        = $"Syncing 0/{orders.Count}...";

            var syncService = new ShopifySyncService();
            var store       = NormalizeStoreInput(_store);

            // Max 3 concurrent to avoid Shopify rate limits (only needed for cache misses)
            var sem          = new SemaphoreSlim(3, 3);
            var counts       = new int[3]; // [0]=saved  [1]=skipped  [2]=failed
            var savedNumbers = new System.Collections.Concurrent.ConcurrentBag<string>();
            bool cancelled   = false;

            var jsonOpts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            try
            {
                var tasks = orders.Select(async order =>
                {
                    bool acquired = false;
                    try
                    {
                        await sem.WaitAsync(token);
                        acquired = true;
                        if (token.IsCancellationRequested) return;

                        // Try SQLite cache first — avoids hitting Shopify rate limits
                        OrderDetails? details = null;
                        try
                        {
                            using var dbCtx = new AppDbContext();
                            var entity = await dbCtx.Orders.FindAsync(order.Id).ConfigureAwait(false);
                            if (entity?.RawJson != null)
                            {
                                var cached = System.Text.Json.JsonSerializer.Deserialize<OrderDetails>(entity.RawJson, jsonOpts);
                                if (cached?.LineItems?.Count > 0) details = cached;
                            }
                        }
                        catch { /* fall through to API */ }

                        // Fall back to Shopify API only if not cached
                        if (details == null)
                        {
                            using var http = new HttpClient();
                            var client2    = new ShopifyClient(http);
                            details = await client2.GetOrderAsync(store, _token, order.Id).ConfigureAwait(false);
                        }

                        var wasSaved = syncService.ProcessShopifyOrder(details, _currentStore?.StoreID);
                        if (wasSaved)
                        {
                            Interlocked.Increment(ref counts[0]);
                            savedNumbers.Add($"#{order.OrderNumber}");
                        }
                        else Interlocked.Increment(ref counts[1]);

                        int done = counts[0] + counts[1] + counts[2];
                        if (!IsDisposed && IsHandleCreated)
                            Invoke(() => lblStatus.Text = $"Syncing {done}/{orders.Count}...");
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref counts[2]);
                        AppLogger.Audit("sync", "ShopifySyncOrderFailed",
                            $"OrderId={order.Id} OrderNumber={order.OrderNumber} Error={ex.Message}");
                    }
                    finally { if (acquired) sem.Release(); }
                }).ToArray();

                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            finally
            {
                _syncCts.Dispose();
                _syncCts = null;
                sem.Dispose();
            }

            int saved = counts[0], skipped = counts[1], failed = counts[2];
            var savedList = savedNumbers.OrderBy(n => n).ToList();

            btnSyncToERP.Enabled  = Security.PermissionHelper.CanEdit("SalesOrders");
            btnCancelSync.Enabled = false;
            btnFetch.Enabled      = true;

            // Refresh the grid so ERP Sync column updates immediately
            LoadCachedOrders();

            if (cancelled)
            {
                lblStatus.Text = $"Sync cancelled — {saved} saved, {skipped} skipped, {failed} failed";
                AppLogger.Audit(_store, "ShopifySyncCancelled", $"Saved={saved} Skipped={skipped} Failed={failed}");
                return;
            }

            lblStatus.Text = $"Sync done — {saved} saved, {skipped} skipped, {failed} failed";
            AppLogger.Audit(_store, "ShopifySyncCompleted", $"Saved={saved} Skipped={skipped} Failed={failed}");

            var ordersSummary = savedList.Count > 0
                ? "\n\nOrders synced:\n" + string.Join(", ", savedList)
                : "";

            if (failed > 0)
                MessageBox.Show(this,
                    $"{saved} order(s) saved to ERP\n{skipped} already existed (skipped)\n{failed} failed — check log for details.{ordersSummary}",
                    "Sync Completed With Errors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                MessageBox.Show(this,
                    $"{saved} order(s) saved to ERP\n{skipped} already existed (skipped){ordersSummary}",
                    "Sync Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F5:
                    if (btnFetch.Enabled) btnFetch_Click(this, EventArgs.Empty);
                    return true;
                case Keys.Escape:
                    Close();
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void btnCancelSync_Click(object? sender, EventArgs e)
        {
            _syncCts?.Cancel();
            btnCancelSync.Enabled = false;
            lblStatus.Text        = "Cancelling...";
        }

        private void btnCreateOrder_Click(object? sender, EventArgs e)
        {
            using var frm = new FormCreateOrder();
            if (frm.ShowDialog(this) == DialogResult.OK)
            {
                if (IsErpView) LoadErpOrders(null, cboStoreFilter.SelectedIndex == 1);
                else LoadCachedOrders();
                lblStatus.Text = "Manual order created.";
            }
        }

        private void btnMarkStatus_Click(object? sender, EventArgs e)
        {
            // Collect checked orders that have an ERP SalesOrderID
            var targets = _currentOrders
                .Where(o => o.Selected && o.ErpSalesOrderID.HasValue)
                .ToList();

            // Fall back to all highlighted (selected) rows if nothing checked
            if (targets.Count == 0)
            {
                targets = dgvOrders.SelectedRows
                    .Cast<DataGridViewRow>()
                    .Select(r => r.DataBoundItem as Order)
                    .Where(o => o?.ErpSalesOrderID.HasValue == true)
                    .Select(o => o!)
                    .ToList();
            }

            if (targets.Count == 0)
            {
                MessageBox.Show(this,
                    "No ERP-synced orders selected.\n\n" +
                    "Highlight or check orders that have been synced to ERP (shown as 'Synced').\n" +
                    "Shopify orders must be synced to ERP first via 'Sync to ERP'.",
                    "No ERP Orders Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Show status picker
            using var picker = new FormStatusPicker(targets[0].ErpStatus ?? "Draft");
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            var newStatus = picker.ChosenStatus;

            var svc   = new Services.ShopifySyncService();
            int done  = 0, failed = 0;
            foreach (var o in targets)
            {
                try { if (svc.UpdateOrderStatus(o.ErpSalesOrderID!.Value, newStatus)) done++; else failed++; }
                catch { failed++; }
            }

            lblStatus.Text = $"Status → {newStatus}: {done} updated{(failed > 0 ? $", {failed} failed" : "")}";

            if (IsErpView) LoadErpOrders(null, cboStoreFilter.SelectedIndex == 1);
            else LoadCachedOrders();
        }

        private void btnStoreSettings_Click(object? sender, EventArgs e)
        {
            using var frm = new FormStoreDashboard();
            frm.ShowDialog(this);
            // Reload stores in case settings changed
            _allStores = new Data.StoreRepository().GetAll().ToList();
            PopulateStoreFilter();
        }
    }
}