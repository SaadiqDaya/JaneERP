using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;
using JaneERP.Security;

namespace JaneERP
{
    /// <summary>
    /// Packing and Shipping Dashboard.
    /// Packing phase: assign order items to boxes (Shipments).
    /// Shipping phase: enter tracking, mark each box as Shipped.
    /// </summary>
    public class FormPackingDash : Form
    {
        // Required for OS-level resize grip on borderless forms on Windows 11
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= 0x00040000; // WS_THICKFRAME
                return cp;
            }
        }

        // ── Services / state ──────────────────────────────────────────────────────

        private readonly IShopifySyncService? _svc;
        private List<FulfillmentOrder>        _orders    = [];
        private List<SalesOrderItem>          _items     = [];
        private List<Shipment>                _shipments = [];
        private FulfillmentOrder?             _current;
        private Shipment?                     _selectedShipment;

        // ── Layout skeleton ───────────────────────────────────────────────────────

        private readonly Panel           _pnlHeader      = new();
        private readonly Label           _lblOrderCount  = new();
        private readonly SplitContainer  _split          = new();

        // Left panel
        private readonly ListBox         _lstOrders      = new();
        private readonly Button          _btnRefresh     = new() { Text = "↺  Refresh" };

        // Right panel — order header
        private readonly Label           _lblOrderHeader = new();
        private readonly Label           _lblOrderSub    = new();

        // Right panel — items grid (top half of right)
        private readonly DataGridView    _dgvItems       = new();

        // Right panel — shipments (bottom half of right)
        private readonly DataGridView    _dgvShipments   = new();
        private readonly Button          _btnAddBox      = new() { Text = "+ Add Box" };

        // Details panel under shipments grid
        private readonly Panel           _pnlBoxDetail   = new();
        private readonly ListBox         _lstContents    = new();
        private readonly Label           _lblContents    = new();
        private readonly Label           _lblTracking    = new();
        private readonly TextBox         _txtTracking    = new();
        private readonly Label           _lblCarrier     = new();
        private readonly ComboBox        _cboCarrier     = new();
        private readonly Button          _btnPackItems   = new() { Text = "Pack Items into Box" };
        private readonly Button          _btnShipBox     = new() { Text = "Ship This Box"       };
        private readonly Button          _btnDeleteBox   = new() { Text = "Delete Box"          };

        // ── Constructor ───────────────────────────────────────────────────────────

        public FormPackingDash()
        {
            Text            = "Packing & Shipping Dashboard";
            ClientSize      = new Size(1200, 720);
            MinimumSize     = new Size(900, 560);
            StartPosition   = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;

            string? serviceError = null;
            try   { _svc = AppServices.Get<IShopifySyncService>(); }
            catch (Exception ex) { serviceError = ex.Message; }

            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            Theme.MakeDraggable(this, _pnlHeader);

            Load += (_, _) =>
            {
                if (_svc == null || serviceError != null)
                {
                    _lblOrderHeader.Text = $"Service error: {serviceError ?? "IShopifySyncService not registered."}";
                    return;
                }
                try { RefreshOrders(); }
                catch (Exception ex)
                {
                    Logging.AppLogger.Error($"[FormPackingDash.Load] {ex}");
                    _lblOrderHeader.Text = $"Load error: {ex.Message}";
                }
            };
        }

        // ── UI construction ───────────────────────────────────────────────────────

        private void BuildUI()
        {
            // Header
            _pnlHeader.Dock      = DockStyle.Top;
            _pnlHeader.Height    = 52;
            _pnlHeader.BackColor = Theme.Header;
            _pnlHeader.Tag       = "header";
            _pnlHeader.Controls.Add(new Label
            {
                Text      = "Packing & Shipping",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(14, 13),
                AutoSize  = true
            });

            _lblOrderCount.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            _lblOrderCount.ForeColor = Color.FromArgb(180, 220, 255);
            _lblOrderCount.Location  = new Point(270, 17);
            _lblOrderCount.AutoSize  = true;
            _pnlHeader.Controls.Add(_lblOrderCount);

            // Split — left 280 px, right fills
            _split.Dock             = DockStyle.Fill;
            _split.Orientation      = Orientation.Vertical;
            _split.SplitterDistance = 280;
            _split.SplitterWidth    = 6;
            _split.Panel1MinSize    = 220;
            _split.Panel2MinSize    = 520;

            BuildLeftPanel();
            BuildRightPanel();

            Controls.Add(_split);
            Controls.Add(_pnlHeader);
        }

        // ── Left panel ────────────────────────────────────────────────────────────

        private void BuildLeftPanel()
        {
            var lbl = MakeSectionLabel("Packing & Shipping");

            _lstOrders.Dock          = DockStyle.Fill;
            _lstOrders.Font          = new Font("Segoe UI", 9.5F);
            _lstOrders.BorderStyle   = BorderStyle.None;
            _lstOrders.DrawMode      = DrawMode.OwnerDrawFixed;
            _lstOrders.ItemHeight    = 44;
            _lstOrders.DrawItem     += LstOrders_DrawItem;
            _lstOrders.SelectedIndexChanged += LstOrders_SelectedIndexChanged;

            Theme.StyleSecondaryButton(_btnRefresh);
            _btnRefresh.Size   = new Size(100, 30);
            _btnRefresh.Dock   = DockStyle.Bottom;
            _btnRefresh.Click += (_, _) => RefreshOrders();

            var pnl = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
            pnl.Controls.Add(_lstOrders);
            pnl.Controls.Add(lbl);
            _split.Panel1.BackColor = Theme.Surface;
            _split.Panel1.Controls.Add(pnl);
            _split.Panel1.Controls.Add(_btnRefresh);
        }

        private void LstOrders_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _lstOrders.Items.Count) return;
            var fo = _lstOrders.Items[e.Index] as FulfillmentOrder;
            if (fo == null) return;

            e.DrawBackground();
            bool selected = (e.State & DrawItemState.Selected) != 0;
            var bg = selected ? Color.FromArgb(50, 180, 200, 255) : Theme.Surface;
            using var brush = new SolidBrush(bg);
            e.Graphics.FillRectangle(brush, e.Bounds);

            // Order number in gold
            using var fntBold  = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            using var fntSmall = new Font("Segoe UI", 8.5F);
            using var brGold   = new SolidBrush(Theme.Gold);
            using var brSub    = new SolidBrush(Theme.TextSecondary);

            string line1 = $"#{fo.OrderNumber}  —  {fo.CustomerName}";
            string line2 = $"{fo.ItemCount} item{(fo.ItemCount == 1 ? "" : "s")}  ·  {fo.Status}";

            e.Graphics.DrawString(line1, fntBold,  brGold, e.Bounds.X + 8, e.Bounds.Y + 6);
            e.Graphics.DrawString(line2, fntSmall, brSub,  e.Bounds.X + 8, e.Bounds.Y + 26);

            // Bottom separator
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        // ── Right panel ───────────────────────────────────────────────────────────

        private void BuildRightPanel()
        {
            // Order header labels (docked top)
            _lblOrderHeader.Dock      = DockStyle.Top;
            _lblOrderHeader.Height    = 30;
            _lblOrderHeader.Font      = new Font("Segoe UI", 11F, FontStyle.Bold);
            _lblOrderHeader.ForeColor = Theme.TextPrimary;
            _lblOrderHeader.Padding   = new Padding(8, 6, 0, 0);
            _lblOrderHeader.Text      = "Select an order";

            _lblOrderSub.Dock         = DockStyle.Top;
            _lblOrderSub.Height       = 22;
            _lblOrderSub.Font         = new Font("Segoe UI", 8.5F);
            _lblOrderSub.ForeColor    = Theme.TextSecondary;
            _lblOrderSub.Padding      = new Padding(8, 2, 0, 0);

            // Inner split: items grid (top) / shipments panel (bottom)
            var innerSplit = new SplitContainer
            {
                Dock             = DockStyle.Fill,
                Orientation      = Orientation.Horizontal,
                SplitterDistance = 260,
                SplitterWidth    = 6,
                Panel1MinSize    = 140,
                Panel2MinSize    = 240
            };

            BuildItemsGrid(innerSplit.Panel1);
            BuildShipmentsPanel(innerSplit.Panel2);

            var rightPnl = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
            rightPnl.Controls.Add(innerSplit);
            rightPnl.Controls.Add(_lblOrderSub);
            rightPnl.Controls.Add(_lblOrderHeader);

            _split.Panel2.BackColor = Theme.Surface;
            _split.Panel2.Controls.Add(rightPnl);
        }

        private void BuildItemsGrid(SplitterPanel parent)
        {
            _dgvItems.Dock                  = DockStyle.Fill;
            _dgvItems.AutoGenerateColumns   = false;
            _dgvItems.AllowUserToAddRows    = false;
            _dgvItems.AllowUserToDeleteRows = false;
            _dgvItems.AllowUserToResizeRows = false;
            _dgvItems.RowHeadersVisible     = false;
            _dgvItems.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgvItems.EditMode              = DataGridViewEditMode.EditOnKeystrokeOrF2;
            _dgvItems.Columns.AddRange(
                new DataGridViewTextBoxColumn { Name = "colProduct",  HeaderText = "Product",  Width = 200, ReadOnly = true  },
                new DataGridViewTextBoxColumn { Name = "colSku",      HeaderText = "SKU",      Width = 80,  ReadOnly = true  },
                new DataGridViewTextBoxColumn { Name = "colOrdered",  HeaderText = "Ordered",  Width = 60,  ReadOnly = true  },
                new DataGridViewTextBoxColumn { Name = "colPicked",   HeaderText = "Picked",   Width = 60,  ReadOnly = true  },
                new DataGridViewTextBoxColumn { Name = "colShipped",  HeaderText = "Shipped",  Width = 60,  ReadOnly = true  },
                new DataGridViewTextBoxColumn { Name = "colPackQty",  HeaderText = "Pack Qty", Width = 70,  ReadOnly = false }
            );
            _dgvItems.CellValidating += DgvItems_CellValidating;
            _dgvItems.CellFormatting += DgvItems_CellFormatting;

            var lbl = MakeSectionLabel("Order Items");
            var pnl = new Panel { Dock = DockStyle.Fill };
            pnl.Controls.Add(_dgvItems);
            pnl.Controls.Add(lbl);
            parent.Controls.Add(pnl);
        }

        private void BuildShipmentsPanel(SplitterPanel parent)
        {
            // Top row: label + Add Box button
            var topBar = new Panel { Dock = DockStyle.Top, Height = 36 };
            var lblBoxes = MakeSectionLabel("Boxes / Shipments");
            lblBoxes.Dock = DockStyle.Left;
            lblBoxes.Width = 180;
            lblBoxes.AutoSize = false;

            Theme.StyleButton(_btnAddBox);
            _btnAddBox.Size     = new Size(100, 28);
            _btnAddBox.Location = new Point(188, 4);
            _btnAddBox.Click   += BtnAddBox_Click;
            topBar.Controls.Add(lblBoxes);
            topBar.Controls.Add(_btnAddBox);

            // Shipments grid
            _dgvShipments.Dock                  = DockStyle.Fill;
            _dgvShipments.AutoGenerateColumns   = false;
            _dgvShipments.AllowUserToAddRows    = false;
            _dgvShipments.AllowUserToDeleteRows = false;
            _dgvShipments.AllowUserToResizeRows = false;
            _dgvShipments.ReadOnly              = true;
            _dgvShipments.RowHeadersVisible     = false;
            _dgvShipments.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgvShipments.MultiSelect           = false;
            _dgvShipments.Columns.AddRange(
                new DataGridViewTextBoxColumn { Name = "colLabel",    HeaderText = "Label",    Width = 100 },
                new DataGridViewTextBoxColumn { Name = "colType",     HeaderText = "Type",     Width = 100 },
                new DataGridViewTextBoxColumn { Name = "colStatus",   HeaderText = "Status",   Width = 70  },
                new DataGridViewTextBoxColumn { Name = "colItems",    HeaderText = "Items",    Width = 50  },
                new DataGridViewTextBoxColumn { Name = "colTracking", HeaderText = "Tracking", Width = 150 }
            );
            _dgvShipments.SelectionChanged += DgvShipments_SelectionChanged;
            _dgvShipments.CellFormatting   += DgvShipments_CellFormatting;

            // Box details panel (docked bottom of the shipments panel)
            BuildBoxDetailPanel();

            // Inner container for grid + detail
            var innerSplit = new SplitContainer
            {
                Dock             = DockStyle.Fill,
                Orientation      = Orientation.Horizontal,
                SplitterDistance = 110,
                SplitterWidth    = 5,
                Panel1MinSize    = 60,
                Panel2MinSize    = 160
            };
            innerSplit.Panel1.Controls.Add(_dgvShipments);
            innerSplit.Panel2.Controls.Add(_pnlBoxDetail);

            parent.Controls.Add(innerSplit);
            parent.Controls.Add(topBar);
        }

        private void BuildBoxDetailPanel()
        {
            _pnlBoxDetail.Dock      = DockStyle.Fill;
            _pnlBoxDetail.BackColor = Theme.Surface;
            _pnlBoxDetail.Padding   = new Padding(8);

            // Contents label + listbox
            _lblContents.Text      = "Contents:";
            _lblContents.Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _lblContents.ForeColor = Theme.Gold;
            _lblContents.AutoSize  = true;
            _lblContents.Location  = new Point(8, 8);

            _lstContents.Location    = new Point(8, 28);
            _lstContents.Size        = new Size(240, 80);
            _lstContents.Font        = new Font("Segoe UI", 8.5F);
            _lstContents.BorderStyle = BorderStyle.FixedSingle;
            _lstContents.BackColor   = Theme.Surface;
            _lstContents.ForeColor   = Theme.TextPrimary;

            // Tracking
            _lblTracking.Text      = "Tracking #:";
            _lblTracking.Font      = new Font("Segoe UI", 8.5F);
            _lblTracking.ForeColor = Theme.TextSecondary;
            _lblTracking.AutoSize  = true;
            _lblTracking.Location  = new Point(8, 116);

            _txtTracking.Location    = new Point(82, 113);
            _txtTracking.Size        = new Size(200, 22);
            _txtTracking.Font        = new Font("Segoe UI", 9F);
            _txtTracking.BackColor   = Theme.Surface;
            _txtTracking.ForeColor   = Theme.TextPrimary;
            _txtTracking.BorderStyle = BorderStyle.FixedSingle;

            // Carrier
            _lblCarrier.Text      = "Carrier:";
            _lblCarrier.Font      = new Font("Segoe UI", 8.5F);
            _lblCarrier.ForeColor = Theme.TextSecondary;
            _lblCarrier.AutoSize  = true;
            _lblCarrier.Location  = new Point(8, 143);

            _cboCarrier.Location         = new Point(82, 140);
            _cboCarrier.Size             = new Size(160, 22);
            _cboCarrier.Font             = new Font("Segoe UI", 9F);
            _cboCarrier.DropDownStyle    = ComboBoxStyle.DropDownList;
            _cboCarrier.BackColor        = Theme.Surface;
            _cboCarrier.ForeColor        = Theme.TextPrimary;
            _cboCarrier.Items.AddRange(new object[] { "Other", "DHL", "FedEx", "UPS", "USPS", "PostNet", "Courier Guy" });
            _cboCarrier.SelectedIndex    = 0;

            // Buttons (right column)
            Theme.StyleButton(_btnPackItems);
            _btnPackItems.Size     = new Size(160, 30);
            _btnPackItems.Location = new Point(260, 8);
            _btnPackItems.Click   += BtnPackItems_Click;

            Theme.StyleButton(_btnShipBox);
            _btnShipBox.Size     = new Size(160, 30);
            _btnShipBox.Location = new Point(260, 46);
            _btnShipBox.Click   += BtnShipBox_Click;

            Theme.StyleSecondaryButton(_btnDeleteBox);
            _btnDeleteBox.Size     = new Size(100, 30);
            _btnDeleteBox.Location = new Point(260, 84);
            _btnDeleteBox.Click   += BtnDeleteBox_Click;

            _pnlBoxDetail.Controls.AddRange(new Control[]
            {
                _lblContents, _lstContents,
                _lblTracking, _txtTracking,
                _lblCarrier,  _cboCarrier,
                _btnPackItems, _btnShipBox, _btnDeleteBox
            });

            UpdateBoxDetailButtons();
        }

        // ── Data loading ──────────────────────────────────────────────────────────

        private void RefreshOrders()
        {
            if (_svc == null) return;
            int? prevId = _current?.SalesOrderID;

            List<FulfillmentOrder> fresh;
            try { fresh = _svc.GetFulfillmentOrders("Packing"); }
            catch (Exception ex)
            {
                Logging.AppLogger.Error($"[FormPackingDash.RefreshOrders] {ex}");
                MessageBox.Show(this, $"Could not load orders:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _orders = fresh;
            int n = _orders.Count;
            _lblOrderCount.Text = n == 0
                ? "No orders ready to pack"
                : $"{n} order{(n == 1 ? "" : "s")} ready to pack";

            _lstOrders.BeginUpdate();
            _lstOrders.Items.Clear();
            foreach (var o in _orders)
                _lstOrders.Items.Add(o);
            _lstOrders.EndUpdate();

            // Restore selection
            if (prevId.HasValue)
            {
                for (int i = 0; i < _lstOrders.Items.Count; i++)
                {
                    if (_lstOrders.Items[i] is FulfillmentOrder fo && fo.SalesOrderID == prevId)
                    {
                        _lstOrders.SelectedIndex = i;
                        return;
                    }
                }
            }
            ClearRight();
        }

        private void LoadOrderDetail(FulfillmentOrder o)
        {
            _current = o;
            _lblOrderHeader.Text = $"Order #{o.OrderNumber}  —  {o.CustomerName}";
            _lblOrderSub.Text    = $"Status: {o.Status}  ·  {o.ItemCount} items  ·  {o.TotalPrice:C2}";

            LoadItems(o.SalesOrderID);
            LoadShipments(o.SalesOrderID);
        }

        private void LoadItems(int salesOrderId)
        {
            if (_svc == null) return;
            try { _items = _svc.GetOrderItemsWithPicking(salesOrderId); }
            catch (Exception ex)
            {
                Logging.AppLogger.Error($"[FormPackingDash.LoadItems] {ex}");
                _items = [];
            }

            _dgvItems.SuspendLayout();
            _dgvItems.Rows.Clear();
            foreach (var item in _items)
            {
                int defaultPackQty = Math.Max(0,
                    Math.Min(item.PickedQty, item.Quantity - item.ShippedQty));
                int i = _dgvItems.Rows.Add(
                    item.Title ?? "",
                    item.SKU   ?? "",
                    item.Quantity,
                    item.PickedQty,
                    item.ShippedQty,
                    defaultPackQty
                );
                _dgvItems.Rows[i].Tag = item;
            }
            _dgvItems.ResumeLayout();
        }

        private void LoadShipments(int salesOrderId)
        {
            if (_svc == null) return;
            IReadOnlyList<Shipment> fresh;
            try { fresh = _svc.GetShipmentsForOrder(salesOrderId); }
            catch (Exception ex)
            {
                Logging.AppLogger.Error($"[FormPackingDash.LoadShipments] {ex}");
                fresh = Array.Empty<Shipment>();
            }

            _shipments = fresh.ToList();

            int? prevShipId = _selectedShipment?.ShipmentID;
            _selectedShipment = null;

            _dgvShipments.SuspendLayout();
            _dgvShipments.Rows.Clear();
            foreach (var s in _shipments)
            {
                int i = _dgvShipments.Rows.Add(
                    s.DisplayLabel,
                    string.IsNullOrWhiteSpace(s.BoxTypeName) ? "—" : s.BoxTypeName,
                    s.Status,
                    s.Items.Count,
                    string.IsNullOrWhiteSpace(s.TrackingNumber) ? "—" : s.TrackingNumber
                );
                _dgvShipments.Rows[i].Tag = s;
            }
            _dgvShipments.ResumeLayout();

            // Restore selection
            if (prevShipId.HasValue)
            {
                foreach (DataGridViewRow row in _dgvShipments.Rows)
                {
                    if (row.Tag is Shipment s && s.ShipmentID == prevShipId)
                    {
                        row.Selected = true;
                        return;
                    }
                }
            }
            ClearBoxDetail();
        }

        private void ClearRight()
        {
            _current          = null;
            _selectedShipment = null;
            _lblOrderHeader.Text = "Select an order";
            _lblOrderSub.Text    = "";
            _items.Clear();
            _shipments.Clear();
            _dgvItems.Rows.Clear();
            _dgvShipments.Rows.Clear();
            ClearBoxDetail();
        }

        private void ClearBoxDetail()
        {
            _selectedShipment = null;
            _lstContents.Items.Clear();
            _txtTracking.Text    = "";
            _cboCarrier.SelectedIndex = 0;
            UpdateBoxDetailButtons();
        }

        private void PopulateBoxDetail(Shipment s)
        {
            _lstContents.Items.Clear();
            foreach (var si in s.Items)
                _lstContents.Items.Add($"{si.ProductTitle}  ×  {si.Quantity}");

            _txtTracking.Text = s.TrackingNumber ?? "";
            int idx = _cboCarrier.Items.IndexOf(s.Carrier ?? "");
            _cboCarrier.SelectedIndex = idx >= 0 ? idx : 0;
            UpdateBoxDetailButtons();
        }

        // ── Grid events ───────────────────────────────────────────────────────────

        private void LstOrders_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_lstOrders.SelectedItem is FulfillmentOrder fo)
                LoadOrderDetail(fo);
            else
                ClearRight();
        }

        private void DgvShipments_SelectionChanged(object? sender, EventArgs e)
        {
            if (_dgvShipments.SelectedRows.Count == 0) { ClearBoxDetail(); return; }
            _selectedShipment = _dgvShipments.SelectedRows[0].Tag as Shipment;
            if (_selectedShipment != null)
                PopulateBoxDetail(_selectedShipment);
            else
                ClearBoxDetail();
        }

        private void DgvShipments_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || _dgvShipments.Rows[e.RowIndex].Tag is not Shipment s) return;
            if (_dgvShipments.Columns[e.ColumnIndex].Name == "colStatus")
            {
                e.CellStyle.ForeColor = s.Status switch
                {
                    "Shipped" => Color.FromArgb(100, 210, 100),
                    "Packed"  => Color.FromArgb(255, 195,  60),
                    _         => Theme.TextSecondary       // Open
                };
                e.FormattingApplied = true;
            }
        }

        private void DgvItems_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
        {
            if (_dgvItems.Columns[e.ColumnIndex].Name != "colPackQty") return;
            if (_dgvItems.Rows[e.RowIndex].Tag is not SalesOrderItem item) return;
            int max = Math.Max(0, item.PickedQty - item.ShippedQty);
            if (!int.TryParse(e.FormattedValue?.ToString(), out int val) || val < 0 || val > max)
            {
                e.Cancel = true;
                _dgvItems.Rows[e.RowIndex].ErrorText = $"Enter 0–{max} (Picked minus already shipped).";
            }
        }

        private void DgvItems_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || _dgvItems.Rows[e.RowIndex].Tag is not SalesOrderItem item) return;
            var row = _dgvItems.Rows[e.RowIndex];
            if (item.ShippedQty >= item.Quantity)
                row.DefaultCellStyle.ForeColor = Theme.TextSecondary;   // muted — fully shipped
            else
                row.DefaultCellStyle.ForeColor = Theme.TextPrimary;
        }

        // ── Button actions ────────────────────────────────────────────────────────

        private void BtnAddBox_Click(object? sender, EventArgs e)
        {
            if (_svc == null || _current == null) return;

            IReadOnlyList<BoxType> boxTypes;
            try { boxTypes = _svc.GetBoxTypes(true); }
            catch (Exception ex)
            {
                Logging.AppLogger.Error($"[FormPackingDash.BtnAddBox_Click] {ex}");
                boxTypes = Array.Empty<BoxType>();
            }

            // Build inline dialog
            int nextBoxNum = _shipments.Count + 1;
            using var dlg  = new Form
            {
                Text            = "Add Box",
                ClientSize      = new Size(320, 170),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition   = FormStartPosition.CenterParent,
                MaximizeBox     = false,
                MinimizeBox     = false,
                BackColor       = Theme.Surface
            };

            var lblType = new Label { Text = "Box Type:", Location = new Point(12, 16), AutoSize = true, ForeColor = Theme.TextSecondary };
            var cboType = new ComboBox
            {
                Location      = new Point(100, 13),
                Size          = new Size(200, 22),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = Theme.Surface,
                ForeColor     = Theme.TextPrimary
            };
            cboType.Items.Add(new BoxTypeEntry(null, "(No Type)"));
            foreach (var bt in boxTypes)
                cboType.Items.Add(new BoxTypeEntry(bt.BoxTypeID, bt.BoxName));
            cboType.SelectedIndex = 0;

            var lblLbl = new Label { Text = "Label:", Location = new Point(12, 52), AutoSize = true, ForeColor = Theme.TextSecondary };
            var txtLbl = new TextBox
            {
                Location    = new Point(100, 49),
                Size        = new Size(200, 22),
                Text        = $"Box {nextBoxNum}",
                BackColor   = Theme.Surface,
                ForeColor   = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font        = new Font("Segoe UI", 9.5F)
            };

            var btnOk = new Button { Text = "Add",    DialogResult = DialogResult.OK,     Size = new Size(80, 30), Location = new Point(100, 95) };
            var btnCx = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = new Size(80, 30), Location = new Point(190, 95) };
            Theme.StyleButton(btnOk);
            Theme.StyleSecondaryButton(btnCx);
            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnCx;
            dlg.Controls.AddRange(new Control[] { lblType, cboType, lblLbl, txtLbl, btnOk, btnCx });

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            var entry    = (BoxTypeEntry)cboType.SelectedItem!;
            string label = txtLbl.Text.Trim();
            if (string.IsNullOrEmpty(label)) label = $"Box {nextBoxNum}";
            string user  = AppSession.CurrentUser?.Username ?? "system";

            try
            {
                _svc.CreateShipment(_current.SalesOrderID, entry.Id, label, user);
                Logging.AppLogger.Audit(user, "CreateShipment", $"OrderID={_current.SalesOrderID} Label={label}");
                LoadShipments(_current.SalesOrderID);
            }
            catch (Exception ex)
            {
                Logging.AppLogger.Error($"[FormPackingDash.BtnAddBox_Click] CreateShipment: {ex}");
                MessageBox.Show(this, $"Could not create box:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnPackItems_Click(object? sender, EventArgs e)
        {
            if (_svc == null || _current == null || _selectedShipment == null) return;
            if (_selectedShipment.Status == "Shipped")
            {
                MessageBox.Show(this, "This box has already been shipped. Create a new box to add more items.",
                    "Box Shipped", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Commit any in-progress edit
            _dgvItems.CommitEdit(DataGridViewDataErrorContexts.Commit);
            _dgvItems.EndEdit();

            // Collect Pack Qty > 0
            var lines = new List<(int SalesOrderItemId, int Qty)>();
            foreach (DataGridViewRow row in _dgvItems.Rows)
            {
                if (row.Tag is not SalesOrderItem item) continue;
                if (!int.TryParse(row.Cells["colPackQty"].Value?.ToString(), out int qty) || qty <= 0) continue;

                int max = Math.Max(0, item.PickedQty - item.ShippedQty);
                if (qty > max)
                {
                    MessageBox.Show(this,
                        $"Pack Qty for '{item.Title}' ({qty}) exceeds available picked qty ({max}).\nPlease correct and try again.",
                        "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                lines.Add((item.SalesOrderItemID, qty));
            }

            if (lines.Count == 0)
            {
                MessageBox.Show(this, "No items to pack — set Pack Qty > 0 for at least one item.",
                    "Nothing to Pack", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string user = AppSession.CurrentUser?.Username ?? "system";
            try
            {
                _svc.SetShipmentItems(_selectedShipment.ShipmentID, lines, user);
                Logging.AppLogger.Audit(user, "PackItems",
                    $"ShipmentID={_selectedShipment.ShipmentID} Lines={lines.Count}");
                // Reload both grids (ShippedQty may not change yet; items list updates)
                LoadItems(_current.SalesOrderID);
                LoadShipments(_current.SalesOrderID);
            }
            catch (Exception ex)
            {
                Logging.AppLogger.Error($"[FormPackingDash.BtnPackItems_Click] {ex}");
                MessageBox.Show(this, $"Could not pack items:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnShipBox_Click(object? sender, EventArgs e)
        {
            if (_svc == null || _current == null || _selectedShipment == null) return;
            if (_selectedShipment.Status == "Shipped")
            {
                MessageBox.Show(this, "This box is already shipped.", "Already Shipped",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string tracking = _txtTracking.Text.Trim();
            if (string.IsNullOrEmpty(tracking))
            {
                MessageBox.Show(this, "Please enter a tracking number before shipping.",
                    "Tracking Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string carrier = _cboCarrier.SelectedItem?.ToString() ?? "Other";
            string user    = AppSession.CurrentUser?.Username ?? "system";

            try
            {
                _svc.MarkShipmentShipped(_selectedShipment.ShipmentID, tracking, carrier, user);
                Logging.AppLogger.Audit(user, "ShipBox",
                    $"ShipmentID={_selectedShipment.ShipmentID} Tracking={tracking} Carrier={carrier}");

                // Reload so ShippedQty is updated
                LoadItems(_current.SalesOrderID);
                LoadShipments(_current.SalesOrderID);

                // Check if all items are now shipped
                bool allShipped = _items.All(i => i.ShippedQty >= i.Quantity);
                if (allShipped)
                {
                    string orderNum = _current.OrderNumber.ToString();
                    MessageBox.Show(this,
                        $"Order #{orderNum} is now complete — all items shipped.",
                        "Order Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    // Remove from list and clear
                    RefreshOrders();
                }
            }
            catch (Exception ex)
            {
                Logging.AppLogger.Error($"[FormPackingDash.BtnShipBox_Click] {ex}");
                MessageBox.Show(this, $"Could not ship box:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDeleteBox_Click(object? sender, EventArgs e)
        {
            if (_svc == null || _selectedShipment == null) return;
            if (_selectedShipment.Status == "Shipped")
            {
                MessageBox.Show(this, "Shipped boxes cannot be deleted.", "Cannot Delete",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var ans = MessageBox.Show(this,
                $"Delete box '{_selectedShipment.DisplayLabel}'?\nThis will remove all packed items from this box.",
                "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ans != DialogResult.Yes) return;

            string user = AppSession.CurrentUser?.Username ?? "system";
            try
            {
                int deletedId = _selectedShipment.ShipmentID;
                _svc.DeleteShipment(deletedId);
                Logging.AppLogger.Audit(user, "DeleteShipment", $"ShipmentID={deletedId}");
                if (_current != null)
                {
                    LoadItems(_current.SalesOrderID);
                    LoadShipments(_current.SalesOrderID);
                }
            }
            catch (Exception ex)
            {
                Logging.AppLogger.Error($"[FormPackingDash.BtnDeleteBox_Click] {ex}");
                MessageBox.Show(this, $"Could not delete box:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Button state ──────────────────────────────────────────────────────────

        private void UpdateBoxDetailButtons()
        {
            bool hasShipment  = _selectedShipment != null;
            bool isNotShipped = hasShipment && _selectedShipment!.Status != "Shipped";

            _btnPackItems.Enabled = hasShipment && isNotShipped;
            _btnShipBox.Enabled   = hasShipment && isNotShipped;
            _btnDeleteBox.Enabled = hasShipment && isNotShipped;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static Label MakeSectionLabel(string text) => new()
        {
            Text      = text,
            Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Theme.Gold,
            Dock      = DockStyle.Top,
            Height    = 26,
            Padding   = new Padding(4, 6, 0, 0)
        };

        /// <summary>Simple value holder for the Add Box combo (supports null BoxTypeID).</summary>
        private sealed record BoxTypeEntry(int? Id, string Name)
        {
            public override string ToString() => Name;
        }
    }
}
