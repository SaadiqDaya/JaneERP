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
using JaneERP.Infrastructure;
using JaneERP.Interfaces;
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
        private System.Windows.Forms.Timer? _syncRefreshTimer;

        // Product setup notification bar (shown when unverified auto-created products exist)
        private Panel  _pnlSetupNotice = new();
        private Label  _lblSetupMsg    = new();
        private Button _btnReviewProds = new();

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
            dgvOrders.SelectionChanged += DgvOrders_SelectionChanged;

            // Disable Sync if the user lacks permission — PopulateStoreFilter may further restrict it
            // based on whether a store is selected, so evaluate permission first.
            if (!Security.PermissionHelper.CanEdit("SalesOrders"))
                btnSyncToERP.Enabled = false;

            BuildSetupNotice();
            PopulateStoreFilter();   // sets initial store state and may disable Sync/Fetch buttons
            LoadCachedOrders();

            // Wire real-time filter events
            dtpFrom.ValueChanged    += (_, _) => ApplyFilters();
            dtpTo.ValueChanged      += (_, _) => ApplyFilters();
            txtMinAmount.TextChanged += (_, _) => ApplyFilters();
            txtMaxAmount.TextChanged += (_, _) => ApplyFilters();

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
                    _syncRefreshTimer?.Stop();
                    btnFetch.Enabled     = false;
                    btnSyncToERP.Enabled = Security.PermissionHelper.CanEdit("SalesOrders");
                    Text = "Shopify Orders — All Stores";
                    UpdateSyncLabel();
                    LoadCachedOrders();
                    break;

                case 1: // Non-Shopify Orders
                    _currentStore = null; _store = ""; _token = "";
                    _syncService?.Stop();
                    _syncRefreshTimer?.Stop();
                    btnFetch.Enabled     = false;
                    btnSyncToERP.Enabled = Security.PermissionHelper.CanEdit("SalesOrders");
                    Text = "ERP Orders — Non-Shopify";
                    UpdateSyncLabel();
                    LoadErpOrders(null, nonShopifyOnly: true);
                    break;

                case 2: // All ERP Orders
                    _currentStore = null; _store = ""; _token = "";
                    _syncService?.Stop();
                    _syncRefreshTimer?.Stop();
                    btnFetch.Enabled     = false;
                    btnSyncToERP.Enabled = Security.PermissionHelper.CanEdit("SalesOrders");
                    Text = "ERP Orders — All";
                    UpdateSyncLabel();
                    LoadErpOrders(null);
                    break;

                default: // specific Shopify store
                    var selected = (ShopifyStore)cboStoreFilter.SelectedItem!;
                    _currentStore = selected;
                    _store        = selected.StoreDomain;
                    _token        = selected.Token ?? "";
                    btnFetch.Enabled     = true;
                    btnSyncToERP.Enabled = !string.IsNullOrEmpty(_store) &&
                                          Security.PermissionHelper.CanEdit("SalesOrders");
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
                var svcLocal = AppServices.Get<IShopifySyncService>();
                var orders = await Task.Run(() =>
                    svcLocal.GetErpOrders(orderType, nonShopifyOnly));
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

            // Payment status
            dgvOrders.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colPayment",
                HeaderText       = "Payment",
                DataPropertyName = "PaymentGateway",
                ReadOnly         = true,
                Width            = 110
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
            // Keep the "to" date ceiling current so newly-arrived orders (synced today) are not filtered out
            if (_lastToDate.Date < DateTime.Today)
            {
                _lastToDate    = DateTime.Today;
                dtpTo.Value    = DateTime.Today;
            }

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
                    var svc = AppServices.Get<IShopifySyncService>();
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
                RefreshSetupNotice();
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

        // ── Product setup notification ────────────────────────────────────────────

        private void BuildSetupNotice()
        {
            _pnlSetupNotice.Dock      = DockStyle.Top;
            _pnlSetupNotice.Height    = 38;
            _pnlSetupNotice.BackColor = Color.FromArgb(255, 190, 0);
            _pnlSetupNotice.Visible   = false;

            _lblSetupMsg.AutoSize  = false;
            _lblSetupMsg.Dock      = DockStyle.Fill;
            _lblSetupMsg.TextAlign = ContentAlignment.MiddleLeft;
            _lblSetupMsg.Padding   = new Padding(10, 0, 0, 0);
            _lblSetupMsg.ForeColor = Color.FromArgb(50, 30, 0);
            _lblSetupMsg.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            _pnlSetupNotice.Controls.Add(_lblSetupMsg);

            _btnReviewProds.Text      = "Set Up Products →";
            _btnReviewProds.Size      = new Size(148, 26);
            _btnReviewProds.Anchor    = AnchorStyles.Right | AnchorStyles.Top;
            _btnReviewProds.Top       = 6;
            _btnReviewProds.FlatStyle = FlatStyle.Flat;
            _btnReviewProds.BackColor = Color.FromArgb(220, 150, 0);
            _btnReviewProds.ForeColor = Color.FromArgb(50, 30, 0);
            _btnReviewProds.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            _btnReviewProds.FlatAppearance.BorderColor = Color.FromArgb(180, 110, 0);
            _btnReviewProds.Click += (_, _) =>
            {
                using var frm = new FormUnverifiedItems();
                frm.ShowDialog(this);
                RefreshSetupNotice();   // re-check after user finishes setup
            };
            _pnlSetupNotice.Controls.Add(_btnReviewProds);

            // Position button from right on resize
            _pnlSetupNotice.SizeChanged += (_, _) =>
                _btnReviewProds.Left = _pnlSetupNotice.ClientSize.Width - _btnReviewProds.Width - 10;

            // Insert the notice bar below the filter row and above the grid.
            // The Fill-docked splitMain is at Controls index 0; we use SetChildIndex to
            // place this Top panel between the filter separator and the split container.
            Controls.Add(_pnlSetupNotice);
            Controls.SetChildIndex(_pnlSetupNotice, 1);
        }

        /// <summary>Checks the unverified product count and shows / hides the notice bar.</summary>
        private void RefreshSetupNotice()
        {
            if (IsDisposed) return;
            try
            {
                int count = AppServices.Get<IShopifySyncService>().GetUnverifiedProductCount();
                if (count > 0)
                {
                    _lblSetupMsg.Text       = $"⚠  {count} product{(count == 1 ? "" : "s")} from orders need setup before they can be used in fulfilment.";
                    _pnlSetupNotice.Visible = true;
                }
                else
                {
                    _pnlSetupNotice.Visible = false;
                }
            }
            catch { _pnlSetupNotice.Visible = false; }
        }

        private void StartBackgroundSync(string store, string token)
        {
            _syncService?.Dispose();
            _syncRefreshTimer?.Stop();
            _syncRefreshTimer?.Dispose();

            _syncService = new SyncService(store, token, TimeSpan.FromMinutes(5));
            _syncService.SyncCompleted += SyncService_SyncCompleted;
            _syncService.SyncStarted   += (_, _) =>
            {
                if (!IsHandleCreated || IsDisposed) return;
                Invoke(UpdateSyncLabel);
            };
            _syncService.Start();
            AppLogger.Audit(store, "BackgroundSyncStarted", "Interval=5m");

            // Refresh the "X min ago" label every minute on the UI thread
            _syncRefreshTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
            _syncRefreshTimer.Tick += (_, _) => UpdateSyncLabel();
            _syncRefreshTimer.Start();

            UpdateSyncLabel();
        }

        /// <summary>Updates lblLastSync with the current sync state. Must be called on the UI thread.</summary>
        private void UpdateSyncLabel()
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (_syncService == null || !_syncService.IsRunning)
            {
                lblLastSync.Text  = "";
                btnSyncNow.Enabled = false;
                return;
            }

            btnSyncNow.Enabled = !_syncService.IsSyncing;

            if (_syncService.IsSyncing)
            {
                lblLastSync.Text      = "Syncing\u2026";
                lblLastSync.ForeColor = Theme.TextSecondary;
                return;
            }

            var synced = _syncService.LastSyncAt;
            if (synced == null)
            {
                lblLastSync.Text      = "Last synced: never";
                lblLastSync.ForeColor = Theme.TextSecondary;
                return;
            }

            var ago     = DateTime.Now - synced.Value;
            string agoText = ago.TotalMinutes < 1  ? "just now"
                           : ago.TotalMinutes < 60 ? $"{(int)ago.TotalMinutes} min ago"
                           :                         $"{(int)ago.TotalHours}h ago";

            if (_syncService.LastSyncFailed)
            {
                lblLastSync.Text      = $"Sync error \u00b7 {agoText}";
                lblLastSync.ForeColor = Theme.Danger;
            }
            else
            {
                lblLastSync.Text      = $"Cached: {agoText}";
                lblLastSync.ForeColor = Theme.Teal;
            }
        }

        private void BtnSyncNow_Click(object? sender, EventArgs e)
        {
            if (_syncService == null || !_syncService.IsRunning) return;
            _syncService.TriggerNow();
            lblStatus.Text = "Manual refresh triggered\u2026";
            UpdateSyncLabel();
        }

        private void SyncService_SyncCompleted(object? sender, SyncCompletedEventArgs e)
        {
            // Form may not be visible yet (race on startup sync) — bail safely
            if (!IsHandleCreated || IsDisposed) return;

            try
            {
                if (e.Success)
                {
                    if (IsErpView)
                        Invoke(() => LoadErpOrders(null, cboStoreFilter.SelectedIndex == 1));
                    else
                        Invoke(LoadCachedOrders);
                    Invoke(() => { lblStatus.Text = $"Background sync: {e.Count} orders"; UpdateSyncLabel(); RefreshSetupNotice(); });
                }
                else
                {
                    AppLogger.Audit("system", "BackgroundSyncFailed", e.Error?.ToString() ?? "unknown");
                    Invoke(() => { lblStatus.Text = $"Background sync failed: {e.Error?.Message}"; UpdateSyncLabel(); });
                }
            }
            catch (ObjectDisposedException) { /* form closed during in-flight sync — safe to ignore */ }
            catch (InvalidOperationException) { /* handle not yet created — safe to ignore */ }
        }

        private void DgvOrders_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvOrders.SelectedRows.Count == 0 ||
                dgvOrders.SelectedRows[0].DataBoundItem is not Order o)
            {
                rtbOrderDetail.Text = "Select an order to view details.";
                return;
            }
            PopulateOrderDetail(o);
        }

        private void PopulateOrderDetail(Order o)
        {
            var local = o.CreatedAt.Kind == DateTimeKind.Utc ? o.CreatedAt.ToLocalTime() : o.CreatedAt;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Order {o.Name ?? $"#{o.OrderNumber}"}");
            sb.AppendLine(new string('─', 26));
            sb.AppendLine();
            sb.AppendLine($"Date:        {local:yyyy-MM-dd HH:mm}");
            if (!string.IsNullOrEmpty(o.ContactEmail))
                sb.AppendLine($"Email:       {o.ContactEmail}");
            sb.AppendLine($"Store:       {o.StoreName ?? o.StoreDomain ?? "—"}");
            sb.AppendLine();
            sb.AppendLine($"Total:       {o.TotalPrice:C2}{(!string.IsNullOrEmpty(o.Currency) ? $" {o.Currency}" : "")}");
            if (o.ShippingCost > 0)
                sb.AppendLine($"Shipping:    {o.ShippingCost:C2}");
            if (!string.IsNullOrEmpty(o.ShippingMethod))
                sb.AppendLine($"Method:      {o.ShippingMethod}");
            sb.AppendLine();
            sb.AppendLine($"Payment:     {(o.IsPaid ? "✓ Paid" : "Unpaid")}");
            if (!string.IsNullOrEmpty(o.PaymentGateway))
                sb.AppendLine($"Pay Method:  {o.PaymentGateway}");
            if (o.PaidAt.HasValue)
                sb.AppendLine($"Paid At:     {o.PaidAt.Value:yyyy-MM-dd HH:mm}");
            sb.AppendLine();
            sb.AppendLine($"ERP Sync:    {o.SyncStatus ?? "Not Synced"}");
            if (!string.IsNullOrEmpty(o.ErpStatus))
                sb.AppendLine($"ERP Status:  {o.ErpStatus}");
            if (o.ErpSalesOrderID.HasValue)
                sb.AppendLine($"ERP ID:      #{o.ErpSalesOrderID}");

            rtbOrderDetail.Text = sb.ToString();
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

            // ERP-view mode: open detail form with pricing breakdown and status change
            if (IsErpView)
            {
                var o = _currentOrders[e.RowIndex];
                using var frm = new FormErpOrderDetail(o);
                frm.ShowDialog(this);
                if (frm.StatusWasChanged)
                {
                    if (IsErpView) LoadErpOrders(null, cboStoreFilter.SelectedIndex == 1);
                    else LoadCachedOrders();
                }
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

                var orders = await client.GetOrdersAsync(
                    store, token,
                    createdAtMin: from, createdAtMax: to,
                    updatedAtMin: null,
                    amountMin: minAmount, amountMax: maxAmount,
                    progress: progress).ConfigureAwait(false);

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
                    try { syncedIds = AppServices.Get<IShopifySyncService>().GetSyncedOrderIds(); }
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

            var syncService  = AppServices.Get<IShopifySyncService>();
            var store        = NormalizeStoreInput(_store);
            bool allStoresMode = string.IsNullOrEmpty(_store);

            // Sequential (1 at a time) for API calls to respect Shopify's leaky-bucket rate limit.
            // Cache hits are instant so the semaphore effectively throttles only API requests.
            var sem          = new SemaphoreSlim(1, 1);
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

                        // When syncing all stores, resolve per-order store/token
                        string orderStore   = store;
                        string orderToken   = _token;
                        int?   orderStoreId = _currentStore?.StoreID;
                        if (allStoresMode)
                        {
                            if (!string.IsNullOrEmpty(order.StoreDomain))
                            {
                                var ms = _allStores.FirstOrDefault(s =>
                                    string.Equals(s.StoreDomain, order.StoreDomain, StringComparison.OrdinalIgnoreCase));
                                if (ms != null)
                                {
                                    orderStore   = NormalizeStoreInput(ms.StoreDomain);
                                    orderToken   = ms.Token ?? "";
                                    orderStoreId = ms.StoreID;
                                }
                                else
                                {
                                    // StoreDomain present but no matching configured store — skip
                                    AppLogger.Info($"[SyncToERP] Skipping order #{order.OrderNumber}: store '{order.StoreDomain}' not configured.");
                                    return;
                                }
                            }
                            else
                            {
                                // No StoreDomain on order and in all-stores mode — skip to avoid blank credentials
                                AppLogger.Info($"[SyncToERP] Skipping order #{order.OrderNumber}: no store domain and all-stores mode active.");
                                return;
                            }
                        }

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
                            details = await client2.GetOrderAsync(orderStore, orderToken, order.Id).ConfigureAwait(false);
                            // Brief pause after each live API call to stay under Shopify's rate limit
                            await Task.Delay(250, token).ConfigureAwait(false);
                        }

                        var wasSaved = syncService.ProcessShopifyOrder(details, orderStoreId);
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

            if (IsDisposed || !IsHandleCreated) return;

            btnSyncToERP.Enabled  = Security.PermissionHelper.CanEdit("SalesOrders");
            btnCancelSync.Enabled = false;
            btnFetch.Enabled      = _currentStore != null;

            // Refresh the grid so ERP Sync column updates immediately
            if (IsErpView) LoadErpOrders(null, cboStoreFilter.SelectedIndex == 1);
            else LoadCachedOrders();

            if (cancelled)
            {
                lblStatus.Text = $"Sync cancelled — {saved} saved, {skipped} skipped, {failed} failed";
                AppLogger.Audit(_store, "ShopifySyncCancelled", $"Saved={saved} Skipped={skipped} Failed={failed}");
                return;
            }

            lblStatus.Text = $"Sync done — {saved} saved, {skipped} skipped, {failed} failed";
            AppLogger.Audit(_store, "ShopifySyncCompleted", $"Saved={saved} Skipped={skipped} Failed={failed}");
            RefreshSetupNotice();

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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Cancel any in-flight sync before disposing — prevents continuation callbacks
            // from trying to update disposed controls after the form closes.
            _syncCts?.Cancel();
            _syncRefreshTimer?.Stop();
            _syncRefreshTimer?.Dispose();
            if (_syncService != null)
            {
                _syncService.SyncCompleted -= SyncService_SyncCompleted;
                _syncService.Dispose();
                _syncService = null;
            }
            base.OnFormClosed(e);
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

            var svc   = AppServices.Get<IShopifySyncService>();
            int done  = 0, failed = 0;
            foreach (var o in targets)
            {
                try
                {
                    if (svc.UpdateOrderStatus(o.ErpSalesOrderID!.Value, newStatus)) done++;
                    else failed++;
                }
                catch { failed++; }
            }

            lblStatus.Text = $"Status → {newStatus}: {done} updated{(failed > 0 ? $", {failed} failed" : "")}";

            if (IsErpView) LoadErpOrders(null, cboStoreFilter.SelectedIndex == 1);
            else LoadCachedOrders();
        }

        private void BtnQuickFulfil_Click(object? sender, EventArgs e)
        {
            // Resolve the selected order — works in both Shopify-cache and ERP-direct views
            Order? selected = null;
            if (dgvOrders.SelectedRows.Count > 0)
                selected = dgvOrders.SelectedRows[0].DataBoundItem as Order;

            if (selected == null || !selected.ErpSalesOrderID.HasValue)
            {
                MessageBox.Show(this,
                    "Select a synced ERP order to quick-fulfil.\n\n" +
                    "Shopify orders must be synced to ERP first (Sync to ERP button).",
                    "No ERP Order Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var status = selected.ErpStatus ?? "Draft";
            if (status is "Shipped" or "Complete")
            {
                MessageBox.Show(this,
                    $"Order #{selected.OrderNumber} is already {status} — nothing to fulfil.",
                    "Already Fulfilled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var svc = AppServices.Get<IShopifySyncService>();
            using var dlg = new FormQuickFulfil(
                selected.ErpSalesOrderID.Value,
                selected.OrderNumber.ToString(),
                selected.Name ?? "—",
                status,
                svc);

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                lblStatus.Text = $"Order #{selected.OrderNumber} quick-fulfilled.";
                if (IsErpView) LoadErpOrders(null, cboStoreFilter.SelectedIndex == 1);
                else LoadCachedOrders();
            }
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

    /// <summary>
    /// Full-screen ERP order detail view: line items, pricing breakdown (subtotal → discount →
    /// shipping → total), and status-change workflow.
    /// </summary>
    internal class FormErpOrderDetail : Form
    {
        private readonly Order _order;

        /// <summary>True if the user changed the order's status at least once during this session.</summary>
        public bool StatusWasChanged { get; private set; }

        // UI controls that need refreshing after a status change
        private Label        lblStatusValue = new();
        private Label        lblPaidValue   = new();
        private Button       btnMarkPaid    = new();
        private DataGridView dgvItems       = new();
        private Label        lblSubtotal    = new();
        private Label        lblDiscount    = new();
        private Label        lblShipping    = new();
        private Label        lblTotal       = new();

        public FormErpOrderDetail(Order order)
        {
            _order = order;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.MakeResizable(this);
            Theme.AddCloseButton(this);
            LoadItems();
        }

        private void BuildUI()
        {
            Text          = $"ERP Order #{_order.OrderNumber}";
            ClientSize    = new Size(720, 580);
            MinimumSize   = new Size(540, 460);
            StartPosition = FormStartPosition.CenterParent;

            // ── Header ───────────────────────────────────────────────────────────────
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Theme.Header };
            pnlHeader.Controls.Add(new Label
            {
                Text      = $"Sales Order  #{_order.OrderNumber}",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 8),
                AutoSize  = true
            });
            Theme.MakeDraggable(this, pnlHeader);
            Controls.Add(pnlHeader);

            // ── Info rows ─────────────────────────────────────────────────────────────
            int y = 48, lx = 16;
            AddInfoPair("Customer:",  _order.Name          ?? "—", lx,  y,
                        "Date:",      _order.CreatedAt.ToString("yyyy-MM-dd"), 360, y);
            y += 24;
            AddInfoPair("Email:",     _order.ContactEmail  ?? "—", lx,  y,
                        "Type:",      _order.StoreName     ?? "—", 360, y);
            y += 24;
            AddCaption("Status:", lx, y + 2);
            lblStatusValue.Text      = _order.ErpStatus ?? "—";
            lblStatusValue.Location  = new Point(lx + 96, y);
            lblStatusValue.AutoSize  = true;
            lblStatusValue.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblStatusValue.ForeColor = StatusColor(_order.ErpStatus);
            Controls.Add(lblStatusValue);
            y += 24;

            AddCaption("Payment:", lx, y + 2);
            lblPaidValue.Location  = new Point(lx + 96, y);
            lblPaidValue.AutoSize  = true;
            lblPaidValue.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            RefreshPaidLabel();
            Controls.Add(lblPaidValue);

            btnMarkPaid.Text     = "Mark as Paid";
            btnMarkPaid.Size     = new Size(110, 24);
            btnMarkPaid.Location = new Point(lx + 240, y - 1);
            btnMarkPaid.Enabled  = !_order.IsPaid && _order.ErpSalesOrderID.HasValue;
            btnMarkPaid.Click   += BtnMarkPaid_Click;
            Theme.StyleSecondaryButton(btnMarkPaid);
            Controls.Add(btnMarkPaid);
            y += 30;

            // ── Grid ─────────────────────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = "Line Items",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(lx, y),
                AutoSize  = true
            });
            y += 20;

            dgvItems.AutoGenerateColumns   = false;
            dgvItems.ReadOnly              = true;
            dgvItems.AllowUserToAddRows    = false;
            dgvItems.AllowUserToDeleteRows = false;
            dgvItems.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvItems.Location              = new Point(lx, y);
            dgvItems.Anchor                = AnchorStyles.Top | AnchorStyles.Bottom
                                           | AnchorStyles.Left | AnchorStyles.Right;
            dgvItems.Size                  = new Size(ClientSize.Width - lx * 2, ClientSize.Height - y - 152);
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSKU",   HeaderText = "SKU",        Width = 120 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",  HeaderText = "Product",    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cQty",   HeaderText = "Qty",        Width = 55  });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPrice", HeaderText = "Unit Price", Width = 96  });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cLine",  HeaderText = "Line Total", Width = 96  });
            Theme.StyleGrid(dgvItems);
            Controls.Add(dgvItems);

            // ── Pricing summary (anchored bottom-left) ────────────────────────────────
            lblSubtotal.Font      = new Font("Segoe UI", 9F);
            lblSubtotal.ForeColor = Theme.TextSecondary;
            lblSubtotal.Anchor    = AnchorStyles.Bottom | AnchorStyles.Left;
            lblSubtotal.Location  = new Point(lx, ClientSize.Height - 136);
            lblSubtotal.AutoSize  = true;
            Controls.Add(lblSubtotal);

            lblDiscount.Font      = new Font("Segoe UI", 9F);
            lblDiscount.ForeColor = Theme.Teal;
            lblDiscount.Anchor    = AnchorStyles.Bottom | AnchorStyles.Left;
            lblDiscount.Location  = new Point(lx, ClientSize.Height - 114);
            lblDiscount.AutoSize  = true;
            Controls.Add(lblDiscount);

            lblShipping.Font      = new Font("Segoe UI", 9F);
            lblShipping.ForeColor = Theme.TextSecondary;
            lblShipping.Anchor    = AnchorStyles.Bottom | AnchorStyles.Left;
            lblShipping.Location  = new Point(lx, ClientSize.Height - 92);
            lblShipping.AutoSize  = true;
            Controls.Add(lblShipping);

            lblTotal.Font      = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblTotal.ForeColor = Theme.Gold;
            lblTotal.Anchor    = AnchorStyles.Bottom | AnchorStyles.Left;
            lblTotal.Location  = new Point(lx, ClientSize.Height - 68);
            lblTotal.AutoSize  = true;
            Controls.Add(lblTotal);

            // ── Action buttons (anchored bottom-right) ────────────────────────────────
            var btnChangeStatus = new Button
            {
                Text     = "Change Status",
                Size     = new Size(130, 30),
                Anchor   = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point(ClientSize.Width - 286, ClientSize.Height - 46)
            };
            btnChangeStatus.Click += BtnChangeStatus_Click;
            Theme.StyleSecondaryButton(btnChangeStatus);
            Controls.Add(btnChangeStatus);

            var btnClose = new Button
            {
                Text     = "Close",
                Size     = new Size(80, 30),
                Anchor   = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point(ClientSize.Width - 146, ClientSize.Height - 46)
            };
            btnClose.Click += (_, _) => Close();
            Theme.StyleButton(btnClose);
            Controls.Add(btnClose);
        }

        private void AddInfoPair(string cap1, string val1, int x1, int y,
                                  string cap2, string val2, int x2, int y2)
        {
            AddCaption(cap1, x1, y + 2);
            Controls.Add(new Label { Text = val1, Location = new Point(x1 + 96, y + 2), AutoSize = true });
            AddCaption(cap2, x2, y2 + 2);
            Controls.Add(new Label { Text = val2, Location = new Point(x2 + 76, y2 + 2), AutoSize = true });
        }

        private void AddCaption(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                Text      = text,
                Location  = new Point(x, y),
                AutoSize  = true,
                ForeColor = Theme.TextSecondary,
                Font      = new Font("Segoe UI", 8F)
            });
        }

        private static Color StatusColor(string? status) => status switch
        {
            "Complete" => Color.FromArgb(100, 210, 100),
            "Live"     => Color.FromArgb(0, 180, 180),
            "WIP"      => Color.FromArgb(210, 160, 50),
            _          => Color.FromArgb(160, 160, 170)  // Draft / unknown
        };

        private void LoadItems()
        {
            if (!_order.ErpSalesOrderID.HasValue) return;
            try
            {
                var items = AppServices.Get<IShopifySyncService>().GetOrderItems(_order.ErpSalesOrderID.Value);
                decimal subtotal = 0;
                foreach (var item in items)
                {
                    decimal lineTotal = item.UnitPrice * item.Quantity;
                    subtotal += lineTotal;
                    int r   = dgvItems.Rows.Add();
                    var row = dgvItems.Rows[r];
                    row.Cells["cSKU"].Value   = item.SKU   ?? "";
                    row.Cells["cName"].Value  = item.Title ?? "";
                    row.Cells["cQty"].Value   = item.Quantity.ToString();
                    row.Cells["cPrice"].Value = $"${item.UnitPrice:N2}";
                    row.Cells["cLine"].Value  = $"${lineTotal:N2}";
                }

                string cur = _order.Currency ?? "CAD";
                lblSubtotal.Text = $"Subtotal:   {subtotal:N2} {cur}";

                lblDiscount.Text = _order.DiscountAmount > 0
                    ? $"Discount:   -{_order.DiscountAmount:N2} {cur}" +
                      (_order.DiscountType != null ? $"  ({_order.DiscountType})" : "")
                    : "";

                lblShipping.Text = _order.ShippingCost > 0
                    ? $"Shipping:   +{_order.ShippingCost:N2} {cur}"
                    : "";

                lblTotal.Text = $"Total:   {_order.TotalPrice:N2} {cur}";
            }
            catch { /* non-fatal — form still useful for status changes */ }
        }

        private void BtnChangeStatus_Click(object? sender, EventArgs e)
        {
            if (!_order.ErpSalesOrderID.HasValue) return;
            using var picker = new FormStatusPicker(_order.ErpStatus ?? "Draft");
            if (picker.ShowDialog(this) != DialogResult.OK) return;

            var svc = AppServices.Get<IShopifySyncService>();
            try
            {
                svc.UpdateOrderStatus(_order.ErpSalesOrderID.Value, picker.ChosenStatus);
                _order.ErpStatus        = picker.ChosenStatus;
                lblStatusValue.Text      = picker.ChosenStatus;
                lblStatusValue.ForeColor = StatusColor(picker.ChosenStatus);
                StatusWasChanged         = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Status update failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshPaidLabel()
        {
            if (_order.IsPaid)
            {
                string date = _order.PaidAt.HasValue ? _order.PaidAt.Value.ToString("yyyy-MM-dd") : "";
                lblPaidValue.Text      = $"PAID{(date.Length > 0 ? "  " + date : "")}";
                lblPaidValue.ForeColor = Color.FromArgb(80, 210, 100);
            }
            else
            {
                lblPaidValue.Text      = "Unpaid";
                lblPaidValue.ForeColor = Color.FromArgb(160, 160, 170);
            }
        }

        private void BtnMarkPaid_Click(object? sender, EventArgs e)
        {
            if (!_order.ErpSalesOrderID.HasValue) return;
            using var dlg = new FormMarkPaid(_order.TotalPrice, _order.Currency);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                AppServices.Get<IShopifySyncService>().MarkAsPaid(
                    _order.ErpSalesOrderID.Value, dlg.PaymentMethod, dlg.Notes);
                _order.IsPaid  = true;
                _order.PaidAt  = DateTime.Now;
                RefreshPaidLabel();
                btnMarkPaid.Enabled = false;
                StatusWasChanged    = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Mark as Paid failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Mark-as-Paid entry dialog
    // ─────────────────────────────────────────────────────────────────────────────

    internal sealed class FormMarkPaid : Form
    {
        public string? PaymentMethod { get; private set; }
        public string? Notes         { get; private set; }

        private ComboBox cboMethod = new();
        private TextBox  txtNotes  = new();

        public FormMarkPaid(decimal amount, string? currency)
        {
            BuildUI(amount, currency);
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
        }

        private void BuildUI(decimal amount, string? currency)
        {
            Text          = "Mark as Paid";
            ClientSize    = new Size(360, 210);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label
            {
                Text      = $"Mark order as paid — {amount:N2} {currency ?? "CAD"}",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(14, 14),
                Size      = new Size(330, 22)
            });

            Controls.Add(new Label { Text = "Payment Method:", Location = new Point(14, 50), AutoSize = true, ForeColor = Theme.TextSecondary });
            cboMethod.Location      = new Point(14, 70);
            cboMethod.Size          = new Size(160, 24);
            cboMethod.DropDownStyle = ComboBoxStyle.DropDownList;
            cboMethod.Items.AddRange(new object[] { "Cash", "Credit/Debit Card", "EFT / Bank Transfer", "Cheque", "Other" });
            cboMethod.SelectedIndex = 0;
            Controls.Add(cboMethod);

            Controls.Add(new Label { Text = "Notes (optional):", Location = new Point(14, 104), AutoSize = true, ForeColor = Theme.TextSecondary });
            txtNotes.Location   = new Point(14, 124);
            txtNotes.Size       = new Size(330, 23);
            Controls.Add(txtNotes);

            var btnOk = new Button { Text = "Confirm", Size = new Size(90, 28), Location = new Point(166, 166), UseVisualStyleBackColor = true };
            btnOk.Click += (_, _) =>
            {
                PaymentMethod = cboMethod.SelectedItem?.ToString();
                Notes         = string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim();
                DialogResult  = DialogResult.OK;
                Close();
            };
            Controls.Add(btnOk);

            var btnCancel = new Button { Text = "Cancel", Size = new Size(80, 28), Location = new Point(264, 166), UseVisualStyleBackColor = true };
            btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);
        }
    }
}