using JaneERP.Models;
using JaneERP.Security;
using JaneERP.Services;

namespace JaneERP
{
    /// <summary>
    /// Inventory Picking Dashboard.
    /// Shows Live orders waiting to be picked and Picking orders in progress.
    /// Operators select an order, mark individual items as picked, then advance
    /// the order to Packing when everything is collected.
    /// </summary>
    internal class FormPickingDash : Form
    {
        private readonly ShopifySyncService  _svc     = new();
        private readonly Panel               _pnlHeader = new();
        private readonly SplitContainer      _split   = new();
        private readonly DataGridView        _dgvOrders = new();
        private readonly DataGridView        _dgvItems  = new();
        private readonly Label               _lblOrderInfo = new();

        private readonly Button _btnRefresh      = new() { Text = "↺  Refresh" };
        private readonly Button _btnStartPicking = new() { Text = "▶  Start Picking" };
        private readonly Button _btnPickAll      = new() { Text = "✓  Pick All" };
        private readonly Button _btnSave         = new() { Text = "💾  Save" };
        private readonly Button _btnComplete     = new() { Text = "✓✓  Complete → Packing" };

        private List<FulfillmentOrder> _orders  = [];
        private List<SalesOrderItem>   _items   = [];
        private FulfillmentOrder?      _current;

        public FormPickingDash()
        {
            Text            = "Picking Dashboard";
            ClientSize      = new Size(1120, 660);
            MinimumSize     = new Size(860, 500);
            StartPosition   = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;

            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            Theme.MakeDraggable(this, _pnlHeader);

            Load += (_, _) => RefreshOrders();
        }

        // ── Layout ────────────────────────────────────────────────────────────────

        private void BuildUI()
        {
            // Header bar
            _pnlHeader.Dock      = DockStyle.Top;
            _pnlHeader.Height    = 52;
            _pnlHeader.BackColor = Theme.Header;
            _pnlHeader.Controls.Add(new Label
            {
                Text      = "📦  Picking Dashboard",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                Location  = new Point(14, 13),
                AutoSize  = true
            });

            // Bottom action bar
            var pnlActions = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 54,
                BackColor = Theme.Header
            };
            StyleBtn(_btnRefresh,      Theme.StyleSecondaryButton, 90);
            StyleBtn(_btnStartPicking, Theme.StyleButton,          126);
            StyleBtn(_btnPickAll,      Theme.StyleSecondaryButton, 110);
            StyleBtn(_btnSave,         Theme.StyleSecondaryButton, 86);
            StyleBtn(_btnComplete,     Theme.StyleButton,          184);

            _btnRefresh.Click      += (_, _) => RefreshOrders();
            _btnStartPicking.Click += BtnStartPicking_Click;
            _btnPickAll.Click      += BtnPickAll_Click;
            _btnSave.Click         += BtnSave_Click;
            _btnComplete.Click     += BtnComplete_Click;

            int bx = 12;
            foreach (var btn in new Button[] { _btnRefresh, _btnStartPicking, _btnPickAll, _btnSave, _btnComplete })
            {
                btn.Location = new Point(bx, 11);
                pnlActions.Controls.Add(btn);
                bx += btn.Width + 8;
            }

            // Split container
            _split.Dock            = DockStyle.Fill;
            _split.Orientation     = Orientation.Vertical;
            _split.SplitterDistance = 420;
            _split.SplitterWidth   = 6;

            // Left: order list
            BuildOrdersPanel();

            // Right: item list
            BuildItemsPanel();

            Controls.Add(_split);
            Controls.Add(pnlActions);
            Controls.Add(_pnlHeader);

            UpdateButtons();
        }

        private void BuildOrdersPanel()
        {
            _dgvOrders.Dock                 = DockStyle.Fill;
            _dgvOrders.AutoGenerateColumns  = false;
            _dgvOrders.AllowUserToAddRows   = false;
            _dgvOrders.AllowUserToDeleteRows = false;
            _dgvOrders.ReadOnly             = true;
            _dgvOrders.RowHeadersVisible    = false;
            _dgvOrders.SelectionMode        = DataGridViewSelectionMode.FullRowSelect;
            _dgvOrders.MultiSelect          = false;
            _dgvOrders.Columns.AddRange(
                new DataGridViewTextBoxColumn { Name = "colNo",       HeaderText = "Order #",  Width = 72  },
                new DataGridViewTextBoxColumn { Name = "colCustomer", HeaderText = "Customer", Width = 158 },
                new DataGridViewTextBoxColumn { Name = "colStatus",   HeaderText = "Status",   Width = 72  },
                new DataGridViewTextBoxColumn { Name = "colProgress", HeaderText = "Progress", Width = 80  }
            );
            _dgvOrders.SelectionChanged += DgvOrders_SelectionChanged;
            _dgvOrders.CellFormatting   += DgvOrders_CellFormatting;

            var lbl = MakeSectionLabel("Live & Picking Orders");
            var pnl = new Panel { Dock = DockStyle.Fill };
            pnl.Controls.Add(_dgvOrders);
            pnl.Controls.Add(lbl);
            _split.Panel1.Controls.Add(pnl);
        }

        private void BuildItemsPanel()
        {
            _dgvItems.Dock                 = DockStyle.Fill;
            _dgvItems.AutoGenerateColumns  = false;
            _dgvItems.AllowUserToAddRows   = false;
            _dgvItems.AllowUserToDeleteRows = false;
            _dgvItems.RowHeadersVisible    = false;
            _dgvItems.SelectionMode        = DataGridViewSelectionMode.FullRowSelect;
            _dgvItems.EditMode             = DataGridViewEditMode.EditOnKeystrokeOrF2;
            _dgvItems.Columns.AddRange(
                new DataGridViewTextBoxColumn { Name = "colSku",      HeaderText = "SKU",       Width = 100, ReadOnly = true  },
                new DataGridViewTextBoxColumn { Name = "colTitle",    HeaderText = "Item",       Width = 210, ReadOnly = true  },
                new DataGridViewTextBoxColumn { Name = "colRequired", HeaderText = "Required",  Width = 72,  ReadOnly = true  },
                new DataGridViewTextBoxColumn { Name = "colPicked",   HeaderText = "Picked ✏",  Width = 72,  ReadOnly = false },
                new DataGridViewTextBoxColumn { Name = "colLeft",     HeaderText = "Remaining", Width = 76,  ReadOnly = true  },
                new DataGridViewTextBoxColumn { Name = "colLoc",      HeaderText = "Pick From", Width = 150, ReadOnly = true  }
            );
            _dgvItems.CellValidating += DgvItems_CellValidating;
            _dgvItems.CellEndEdit    += DgvItems_CellEndEdit;
            _dgvItems.CellFormatting += DgvItems_CellFormatting;

            _lblOrderInfo.Dock      = DockStyle.Top;
            _lblOrderInfo.Height    = 26;
            _lblOrderInfo.ForeColor = Theme.TextSecondary;
            _lblOrderInfo.Font      = new Font("Segoe UI", 8.5F);
            _lblOrderInfo.Padding   = new Padding(4, 5, 0, 0);
            _lblOrderInfo.Text      = "Select an order to see items.";

            var lbl = MakeSectionLabel("Items to Pick");
            var pnl = new Panel { Dock = DockStyle.Fill };
            pnl.Controls.Add(_dgvItems);
            pnl.Controls.Add(_lblOrderInfo);
            pnl.Controls.Add(lbl);
            _split.Panel2.Controls.Add(pnl);
        }

        // ── Data loading ──────────────────────────────────────────────────────────

        private void RefreshOrders()
        {
            int? prevId = _current?.SalesOrderID;
            _orders = _svc.GetFulfillmentOrders("Live", "Picking");

            _dgvOrders.SuspendLayout();
            _dgvOrders.Rows.Clear();
            foreach (var o in _orders)
            {
                int i = _dgvOrders.Rows.Add(
                    $"#{o.OrderNumber}",
                    o.CustomerName,
                    o.Status,
                    o.ProgressText
                );
                _dgvOrders.Rows[i].Tag = o;
            }
            _dgvOrders.ResumeLayout();

            // Restore selection
            if (prevId.HasValue)
            {
                foreach (DataGridViewRow row in _dgvOrders.Rows)
                {
                    if (row.Tag is FulfillmentOrder fo && fo.SalesOrderID == prevId)
                    {
                        row.Selected = true;
                        return;
                    }
                }
            }
            ClearItems();
            UpdateButtons();
        }

        private void LoadItems(int salesOrderId)
        {
            _items = _svc.GetOrderItemsWithPicking(salesOrderId);
            _dgvItems.SuspendLayout();
            _dgvItems.Rows.Clear();
            foreach (var item in _items)
            {
                int remaining = Math.Max(0, item.Quantity - item.PickedQty);
                int i = _dgvItems.Rows.Add(
                    item.SKU,
                    item.Title,
                    item.Quantity,
                    item.PickedQty,
                    remaining,
                    item.PickLocation ?? "—"
                );
                _dgvItems.Rows[i].Tag = item;
            }
            _dgvItems.ResumeLayout();
        }

        private void ClearItems()
        {
            _current = null;
            _items.Clear();
            _dgvItems.Rows.Clear();
            _lblOrderInfo.Text = "Select an order to see items.";
        }

        // ── Grid events ───────────────────────────────────────────────────────────

        private void DgvOrders_SelectionChanged(object? sender, EventArgs e)
        {
            if (_dgvOrders.SelectedRows.Count == 0) { ClearItems(); UpdateButtons(); return; }
            _current = _dgvOrders.SelectedRows[0].Tag as FulfillmentOrder;
            if (_current != null)
            {
                _lblOrderInfo.Text =
                    $"Order #{_current.OrderNumber}  ·  {_current.CustomerName}  ·  {_current.TotalPrice:C2}";
                LoadItems(_current.SalesOrderID);
            }
            UpdateButtons();
        }

        private void DgvOrders_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || _dgvOrders.Rows[e.RowIndex].Tag is not FulfillmentOrder fo) return;
            if (_dgvOrders.Columns[e.ColumnIndex].Name == "colStatus")
            {
                e.CellStyle.ForeColor = fo.Status switch
                {
                    "Live"    => Color.FromArgb(80,  200, 255),
                    "Picking" => Color.FromArgb(255, 195,  60),
                    _         => Theme.TextPrimary
                };
                e.FormattingApplied = true;
            }
        }

        private void DgvItems_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || _dgvItems.Rows[e.RowIndex].Tag is not SalesOrderItem item) return;
            var style = _dgvItems.Rows[e.RowIndex].DefaultCellStyle;
            if (item.PickedQty >= item.Quantity)
                style.ForeColor = Color.FromArgb(100, 210, 100);   // green — done
            else if (item.PickedQty > 0)
                style.ForeColor = Color.FromArgb(255, 195,  60);   // amber — partial
            else
                style.ForeColor = Theme.TextPrimary;
        }

        private void DgvItems_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
        {
            if (_dgvItems.Columns[e.ColumnIndex].Name != "colPicked") return;
            if (_dgvItems.Rows[e.RowIndex].Tag is not SalesOrderItem item) return;
            if (!int.TryParse(e.FormattedValue?.ToString(), out int val) || val < 0 || val > item.Quantity)
            {
                e.Cancel = true;
                _dgvItems.Rows[e.RowIndex].ErrorText = $"Enter a number from 0 to {item.Quantity}.";
            }
        }

        private void DgvItems_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            _dgvItems.Rows[e.RowIndex].ErrorText = "";
            if (_dgvItems.Columns[e.ColumnIndex].Name != "colPicked") return;
            if (_dgvItems.Rows[e.RowIndex].Tag is not SalesOrderItem item) return;
            if (int.TryParse(_dgvItems.Rows[e.RowIndex].Cells["colPicked"].Value?.ToString(), out int val))
            {
                item.PickedQty = Math.Clamp(val, 0, item.Quantity);
                _dgvItems.Rows[e.RowIndex].Cells["colPicked"].Value = item.PickedQty;
                _dgvItems.Rows[e.RowIndex].Cells["colLeft"].Value   = item.Quantity - item.PickedQty;
            }
            _dgvItems.Invalidate();
            UpdateButtons();
        }

        // ── Button actions ────────────────────────────────────────────────────────

        private void BtnStartPicking_Click(object? sender, EventArgs e)
        {
            if (_current == null || _current.Status != "Live") return;
            _svc.UpdateOrderStatus(_current.SalesOrderID, "Picking");
            Logging.AppLogger.Audit(AppSession.CurrentUser?.Username ?? "system",
                "StartPicking", $"OrderID={_current.SalesOrderID} #{_current.OrderNumber}");
            RefreshOrders();
        }

        private void BtnPickAll_Click(object? sender, EventArgs e)
        {
            if (_current == null || _current.Status != "Picking") return;
            string picker = AppSession.CurrentUser?.Username ?? "system";
            foreach (var item in _items)
            {
                item.PickedQty = item.Quantity;
                _svc.UpdatePickedQty(item.SalesOrderItemID, item.Quantity, picker);
            }
            LoadItems(_current.SalesOrderID);
            RefreshOrders();
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (_current == null) return;
            CommitAndSave();
            MessageBox.Show(this, "Pick progress saved.", "Saved",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadItems(_current.SalesOrderID);
            RefreshOrders();
        }

        private void BtnComplete_Click(object? sender, EventArgs e)
        {
            if (_current == null || _current.Status != "Picking") return;
            if (!_items.All(i => i.PickedQty >= i.Quantity))
            {
                MessageBox.Show(this,
                    "All items must be fully picked before completing.\nSave partial progress first.",
                    "Incomplete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            CommitAndSave();
            _svc.UpdateOrderStatus(_current.SalesOrderID, "Packing");
            Logging.AppLogger.Audit(AppSession.CurrentUser?.Username ?? "system",
                "PickingComplete", $"OrderID={_current.SalesOrderID} #{_current.OrderNumber}");
            RefreshOrders();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void CommitAndSave()
        {
            _dgvItems.CommitEdit(DataGridViewDataErrorContexts.Commit);
            _dgvItems.EndEdit();
            string picker = AppSession.CurrentUser?.Username ?? "system";
            foreach (var item in _items)
                _svc.UpdatePickedQty(item.SalesOrderItemID, item.PickedQty, picker);
        }

        private void UpdateButtons()
        {
            bool hasOrder  = _current != null;
            bool isLive    = _current?.Status == "Live";
            bool isPicking = _current?.Status == "Picking";
            bool allPicked = _items.Count > 0 && _items.All(i => i.PickedQty >= i.Quantity);

            _btnStartPicking.Enabled = hasOrder && isLive;
            _btnPickAll.Enabled      = hasOrder && isPicking;
            _btnSave.Enabled         = hasOrder && isPicking;
            _btnComplete.Enabled     = hasOrder && isPicking && allPicked;
        }

        private static void StyleBtn(Button btn, Action<Button> style, int width)
        {
            btn.Size = new Size(width, 32);
            style(btn);
        }

        private static Label MakeSectionLabel(string text) => new()
        {
            Text      = text,
            Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Theme.Gold,
            Dock      = DockStyle.Top,
            Height    = 26,
            Padding   = new Padding(4, 6, 0, 0)
        };
    }
}
