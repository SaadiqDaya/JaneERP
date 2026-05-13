using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;
using JaneERP.Security;
using JaneERP.Services;

namespace JaneERP
{
    /// <summary>
    /// Packing and Shipping Dashboard.
    /// Shows Packing orders (all items picked, ready to pack and ship) and
    /// Shipped orders (for tracking reference / final completion).
    /// Operators enter tracking details, mark as Shipped, then mark Complete.
    /// </summary>
    internal class FormPackingDash : Form
    {
        private readonly IShopifySyncService _svc = AppServices.Get<IShopifySyncService>();
        private readonly Panel              _pnlHeader  = new();
        private readonly SplitContainer     _split      = new();
        private readonly DataGridView       _dgvOrders  = new();

        // Right-panel controls
        private readonly Panel     _pnlDetail      = new();
        private readonly Label     _lblDetailHeader = new();
        private readonly Label     _lblCustomer     = new();
        private readonly Label     _lblStatus       = new();
        private readonly Label     _lblNotes        = new();
        private readonly DataGridView _dgvItems     = new();
        private readonly Label     _lblTracking     = new();
        private readonly TextBox   _txtTracking     = new();
        private readonly Label     _lblCarrier      = new();
        private readonly TextBox   _txtCarrier      = new();

        // Action buttons
        private readonly Button _btnRefresh  = new() { Text = "↺  Refresh"       };
        private readonly Button _btnShip     = new() { Text = "🚚  Ship Order"    };
        private readonly Button _btnComplete = new() { Text = "☑  Mark Complete"  };
        private readonly CheckBox _chkShowShipped = new() { Text = "Show Shipped" };

        private List<FulfillmentOrder> _orders  = [];
        private FulfillmentOrder?      _current;

        public FormPackingDash()
        {
            Text            = "Packing & Shipping Dashboard";
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
            // Header
            _pnlHeader.Dock      = DockStyle.Top;
            _pnlHeader.Height    = 52;
            _pnlHeader.BackColor = Theme.Header;
            _pnlHeader.Controls.Add(new Label
            {
                Text      = "📬  Packing & Shipping Dashboard",
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

            Theme.StyleSecondaryButton(_btnRefresh);
            _btnRefresh.Size  = new Size(96, 32);
            _btnRefresh.Click += (_, _) => RefreshOrders();

            Theme.StyleButton(_btnShip);
            _btnShip.Size    = new Size(130, 32);
            _btnShip.Enabled = false;
            _btnShip.Click  += BtnShip_Click;

            Theme.StyleButton(_btnComplete);
            _btnComplete.Size    = new Size(140, 32);
            _btnComplete.Enabled = false;
            _btnComplete.Click  += BtnComplete_Click;

            _chkShowShipped.ForeColor    = Theme.TextSecondary;
            _chkShowShipped.Font         = new Font("Segoe UI", 9F);
            _chkShowShipped.AutoSize     = true;
            _chkShowShipped.CheckedChanged += (_, _) => RefreshOrders();

            int bx = 12;
            foreach (Control c in new Control[] { _btnRefresh, _btnShip, _btnComplete })
            {
                c.Location = new Point(bx, 11);
                pnlActions.Controls.Add(c);
                bx += ((Button)c).Width + 8;
            }
            _chkShowShipped.Location = new Point(bx + 8, 16);
            pnlActions.Controls.Add(_chkShowShipped);

            // Split
            _split.Dock            = DockStyle.Fill;
            _split.Orientation     = Orientation.Vertical;
            _split.SplitterDistance = 400;
            _split.SplitterWidth   = 6;

            BuildOrdersPanel();
            BuildDetailPanel();

            Controls.Add(_split);
            Controls.Add(pnlActions);
            Controls.Add(_pnlHeader);
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
                new DataGridViewTextBoxColumn { Name = "colStatus",   HeaderText = "Status",   Width = 76  },
                new DataGridViewTextBoxColumn { Name = "colPacked",   HeaderText = "Packed At", Width = 100 }
            );
            _dgvOrders.SelectionChanged += DgvOrders_SelectionChanged;
            _dgvOrders.CellFormatting   += DgvOrders_CellFormatting;

            var lbl = MakeSectionLabel("Orders Ready to Pack / Ship");
            var pnl = new Panel { Dock = DockStyle.Fill };
            pnl.Controls.Add(_dgvOrders);
            pnl.Controls.Add(lbl);
            _split.Panel1.Controls.Add(pnl);
        }

        private void BuildDetailPanel()
        {
            // Items grid (fills most of the detail panel)
            _dgvItems.Dock                 = DockStyle.Fill;
            _dgvItems.AutoGenerateColumns  = false;
            _dgvItems.AllowUserToAddRows   = false;
            _dgvItems.AllowUserToDeleteRows = false;
            _dgvItems.ReadOnly             = true;
            _dgvItems.RowHeadersVisible    = false;
            _dgvItems.SelectionMode        = DataGridViewSelectionMode.FullRowSelect;
            _dgvItems.Columns.AddRange(
                new DataGridViewTextBoxColumn { Name = "colSku",   HeaderText = "SKU",      Width = 100 },
                new DataGridViewTextBoxColumn { Name = "colTitle", HeaderText = "Item",     Width = 250 },
                new DataGridViewTextBoxColumn { Name = "colQty",   HeaderText = "Qty",      Width = 60  },
                new DataGridViewTextBoxColumn { Name = "colPrice", HeaderText = "Unit $",   Width = 80  }
            );

            // Tracking / carrier inputs
            _lblTracking.Text      = "Tracking #:";
            _lblTracking.AutoSize  = true;
            _lblTracking.ForeColor = Theme.TextSecondary;
            _lblTracking.Font      = new Font("Segoe UI", 9F);

            _txtTracking.Font      = new Font("Segoe UI", 10F);
            _txtTracking.Width     = 280;
            _txtTracking.BackColor = Color.FromArgb(22, 20, 40);
            _txtTracking.ForeColor = Theme.TextPrimary;
            _txtTracking.BorderStyle = BorderStyle.FixedSingle;

            _lblCarrier.Text      = "Carrier:";
            _lblCarrier.AutoSize  = true;
            _lblCarrier.ForeColor = Theme.TextSecondary;
            _lblCarrier.Font      = new Font("Segoe UI", 9F);

            _txtCarrier.Font      = new Font("Segoe UI", 10F);
            _txtCarrier.Width     = 200;
            _txtCarrier.BackColor = Color.FromArgb(22, 20, 40);
            _txtCarrier.ForeColor = Theme.TextPrimary;
            _txtCarrier.BorderStyle = BorderStyle.FixedSingle;

            // Shipping fields panel (docked bottom of detail panel)
            var pnlShipping = new Panel { Dock = DockStyle.Bottom, Height = 70 };
            pnlShipping.Paint += (s, e) =>
            {
                // top separator line
                e.Graphics.DrawLine(new Pen(Theme.Border), 0, 0, pnlShipping.Width, 0);
            };

            // row 1: Tracking #
            int lx = 12, ly = 12;
            _lblTracking.Location = new Point(lx, ly + 2);
            _txtTracking.Location = new Point(lx + 90, ly);
            // row 2: Carrier
            ly += 32;
            _lblCarrier.Location  = new Point(lx, ly + 2);
            _txtCarrier.Location  = new Point(lx + 90, ly);

            pnlShipping.Controls.AddRange(new Control[]
                { _lblTracking, _txtTracking, _lblCarrier, _txtCarrier });

            // Order info header (docked top of detail panel)
            _lblDetailHeader.Dock      = DockStyle.Top;
            _lblDetailHeader.Height    = 30;
            _lblDetailHeader.Font      = new Font("Segoe UI", 10F, FontStyle.Bold);
            _lblDetailHeader.ForeColor = Theme.TextPrimary;
            _lblDetailHeader.Padding   = new Padding(6, 7, 0, 0);
            _lblDetailHeader.Text      = "Select an order";

            _lblCustomer.Dock      = DockStyle.Top;
            _lblCustomer.Height    = 22;
            _lblCustomer.Font      = new Font("Segoe UI", 8.5F);
            _lblCustomer.ForeColor = Theme.TextSecondary;
            _lblCustomer.Padding   = new Padding(6, 2, 0, 0);

            _lblStatus.Dock        = DockStyle.Top;
            _lblStatus.Height      = 22;
            _lblStatus.Font        = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _lblStatus.Padding     = new Padding(6, 2, 0, 0);

            _lblNotes.Dock         = DockStyle.Top;
            _lblNotes.Height       = 20;
            _lblNotes.Font         = new Font("Segoe UI", 8F);
            _lblNotes.ForeColor    = Theme.TextSecondary;
            _lblNotes.Padding      = new Padding(6, 0, 0, 0);

            var itemsLbl = MakeSectionLabel("Packing Slip — Line Items");

            _pnlDetail.Dock = DockStyle.Fill;
            _pnlDetail.Controls.Add(_dgvItems);
            _pnlDetail.Controls.Add(pnlShipping);
            _pnlDetail.Controls.Add(itemsLbl);
            _pnlDetail.Controls.Add(_lblNotes);
            _pnlDetail.Controls.Add(_lblStatus);
            _pnlDetail.Controls.Add(_lblCustomer);
            _pnlDetail.Controls.Add(_lblDetailHeader);

            _split.Panel2.Controls.Add(_pnlDetail);
        }

        // ── Data loading ──────────────────────────────────────────────────────────

        private void RefreshOrders()
        {
            int? prevId = _current?.SalesOrderID;
            var statuses = _chkShowShipped.Checked
                ? new[] { "Packing", "Shipped" }
                : new[] { "Packing" };
            _orders = _svc.GetFulfillmentOrders(statuses);

            _dgvOrders.SuspendLayout();
            _dgvOrders.Rows.Clear();
            foreach (var o in _orders)
            {
                int i = _dgvOrders.Rows.Add(
                    $"#{o.OrderNumber}",
                    o.CustomerName,
                    o.Status,
                    o.PackedAt.HasValue ? o.PackedAt.Value.ToString("MMM d, HH:mm") : "—"
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
            ClearDetail();
        }

        private void LoadDetail(FulfillmentOrder o)
        {
            _lblDetailHeader.Text = $"Order #{o.OrderNumber}  —  {o.TotalPrice:C2} {o.Currency}";
            _lblCustomer.Text     = $"{o.CustomerName}  ·  {o.ContactEmail}";
            _lblStatus.Text       = $"Status: {o.Status}";
            _lblStatus.ForeColor  = o.Status switch
            {
                "Packing" => Color.FromArgb(255, 195, 60),
                "Shipped" => Color.FromArgb(100, 210, 100),
                _         => Theme.TextSecondary
            };
            _lblNotes.Text = string.IsNullOrWhiteSpace(o.Notes) ? "" : $"Notes: {o.Notes}";

            // Pre-fill tracking if already shipped
            _txtTracking.Text = o.TrackingNumber ?? "";
            _txtCarrier.Text  = o.Carrier ?? "";

            // Load line items
            var items = _svc.GetOrderItems(o.SalesOrderID);
            _dgvItems.Rows.Clear();
            foreach (var item in items)
                _dgvItems.Rows.Add(item.SKU, item.Title, item.Quantity, $"{item.UnitPrice:C2}");
        }

        private void ClearDetail()
        {
            _current = null;
            _lblDetailHeader.Text = "Select an order";
            _lblCustomer.Text     = "";
            _lblStatus.Text       = "";
            _lblNotes.Text        = "";
            _txtTracking.Text     = "";
            _txtCarrier.Text      = "";
            _dgvItems.Rows.Clear();
            UpdateButtons();
        }

        // ── Grid events ───────────────────────────────────────────────────────────

        private void DgvOrders_SelectionChanged(object? sender, EventArgs e)
        {
            if (_dgvOrders.SelectedRows.Count == 0) { ClearDetail(); return; }
            _current = _dgvOrders.SelectedRows[0].Tag as FulfillmentOrder;
            if (_current != null) LoadDetail(_current);
            UpdateButtons();
        }

        private void DgvOrders_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || _dgvOrders.Rows[e.RowIndex].Tag is not FulfillmentOrder fo) return;
            if (_dgvOrders.Columns[e.ColumnIndex].Name == "colStatus")
            {
                e.CellStyle.ForeColor = fo.Status switch
                {
                    "Packing" => Color.FromArgb(255, 195,  60),
                    "Shipped" => Color.FromArgb(100, 210, 100),
                    _         => Theme.TextPrimary
                };
                e.FormattingApplied = true;
            }
        }

        // ── Button actions ────────────────────────────────────────────────────────

        private void BtnShip_Click(object? sender, EventArgs e)
        {
            if (_current == null || _current.Status != "Packing") return;

            string tracking = _txtTracking.Text.Trim();
            string carrier  = _txtCarrier.Text.Trim();

            if (string.IsNullOrEmpty(tracking))
            {
                var ans = MessageBox.Show(this,
                    "No tracking number entered.\nShip without tracking?",
                    "No Tracking", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ans != DialogResult.Yes) return;
            }

            try
            {
                _svc.RecordShipment(_current.SalesOrderID,
                    string.IsNullOrEmpty(tracking) ? null : tracking,
                    string.IsNullOrEmpty(carrier)  ? null : carrier);
                RefreshOrders();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to record shipment:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnComplete_Click(object? sender, EventArgs e)
        {
            if (_current == null) return;
            if (_current.Status != "Shipped") return;

            var ans = MessageBox.Show(this,
                $"Mark Order #{_current.OrderNumber} as Complete?\n\nThis will deduct inventory for all line items.",
                "Confirm Complete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ans != DialogResult.Yes) return;

            try
            {
                _svc.MarkComplete(_current.SalesOrderID);
                Logging.AppLogger.Audit(AppSession.CurrentUser?.Username ?? "system",
                    "OrderComplete", $"OrderID={_current.SalesOrderID} #{_current.OrderNumber}");
                RefreshOrders();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to complete order:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void UpdateButtons()
        {
            bool hasPacking  = _current?.Status == "Packing";
            bool hasShipped  = _current?.Status == "Shipped";

            _btnShip.Enabled     = hasPacking;
            _btnComplete.Enabled = hasShipped;
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
