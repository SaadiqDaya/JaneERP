using JaneERP.Interfaces;
using JaneERP.Logging;
using JaneERP.Models;
using JaneERP.Security;

namespace JaneERP
{
    /// <summary>
    /// One-dialog quick fulfilment: advances an order from its current status all the way to
    /// Complete in a single action. Intended for small or in-person / walk-in orders.
    /// </summary>
    internal class FormQuickFulfil : Form
    {
        private readonly int                 _salesOrderId;
        private readonly string              _orderNumber;
        private readonly string              _customerName;
        private readonly string              _currentStatus;
        private readonly IShopifySyncService _svc;

        private List<ReservationLine>? _confirmedReservations;

        // Controls
        private readonly Label        _lblInfo             = new();
        private readonly DataGridView _dgvItems            = new();
        private readonly ComboBox     _cboShipMethod       = new();
        private readonly TextBox      _txtTracking         = new();
        private readonly TextBox      _txtNotes            = new();
        private readonly Label        _lblReservStatus     = new();
        private readonly Button       _btnAssignInventory  = new();
        private readonly Label        _lblStepsInfo        = new();
        private readonly Button       _btnFulfil           = new();
        private readonly Button       _btnCancel           = new();

        public FormQuickFulfil(int salesOrderId, string orderNumber, string customerName,
                               string currentStatus, IShopifySyncService svc)
        {
            _salesOrderId  = salesOrderId;
            _orderNumber   = orderNumber;
            _customerName  = customerName;
            _currentStatus = currentStatus;
            _svc           = svc;

            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            LoadItems();
        }

        private void BuildUI()
        {
            Text          = $"Quick Fulfil — Order #{_orderNumber}";
            ClientSize    = new Size(740, 540);
            MinimumSize   = new Size(640, 480);
            StartPosition = FormStartPosition.CenterParent;

            // ── Header ────────────────────────────────────────────────────────────
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Theme.Header };
            pnlHeader.Controls.Add(new Label
            {
                Text      = $"Quick Fulfil  —  Order #{_orderNumber}  ({_customerName})",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 10),
                AutoSize  = true
            });
            Theme.MakeDraggable(this, pnlHeader);
            Controls.Add(pnlHeader);

            // ── Bottom action bar ─────────────────────────────────────────────────
            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = Theme.Header };

            _btnCancel.Text   = "Cancel";
            _btnCancel.Size   = new Size(88, 30);
            _btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            _btnFulfil.Text   = "Fulfil Now \u2192";
            _btnFulfil.Size   = new Size(130, 30);
            _btnFulfil.Click += BtnFulfil_Click;
            Theme.StyleButton(_btnFulfil);

            void PositionButtons()
            {
                _btnCancel.Location = new Point(pnlBottom.ClientSize.Width - _btnCancel.Width - 10, 9);
                _btnFulfil.Location = new Point(_btnCancel.Left - _btnFulfil.Width - 8, 9);
            }
            pnlBottom.Resize += (_, _) => PositionButtons();
            pnlBottom.Controls.Add(_btnCancel);
            pnlBottom.Controls.Add(_btnFulfil);
            Controls.Add(pnlBottom);
            Load += (_, _) => PositionButtons();

            // ── Body ──────────────────────────────────────────────────────────────
            var pnlBody = new Panel
            {
                Dock        = DockStyle.Fill,
                Padding     = new Padding(14, 10, 14, 6),
                AutoScroll  = true
            };
            Controls.Add(pnlBody);

            int y = 10;

            // Summary line
            _lblInfo.Text      = $"Order #{_orderNumber}  |  {_customerName}  |  Status: {_currentStatus}";
            _lblInfo.Font      = new Font("Segoe UI", 9F);
            _lblInfo.ForeColor = Theme.TextSecondary;
            _lblInfo.Location  = new Point(0, y);
            _lblInfo.AutoSize  = true;
            pnlBody.Controls.Add(_lblInfo);
            y += 22;

            // Items section
            pnlBody.Controls.Add(MakeSectionLabel("Order Items", y));
            y += 24;

            _dgvItems.Location             = new Point(0, y);
            _dgvItems.Size                 = new Size(708, 150);
            _dgvItems.Anchor               = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            _dgvItems.ReadOnly             = true;
            _dgvItems.AllowUserToAddRows   = false;
            _dgvItems.AllowUserToDeleteRows = false;
            _dgvItems.SelectionMode        = DataGridViewSelectionMode.FullRowSelect;
            _dgvItems.AutoGenerateColumns  = false;
            _dgvItems.ColumnHeadersHeight  = 24;
            _dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Product",    DataPropertyName = "Title",     Width = 330 });
            _dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "SKU",        DataPropertyName = "SKU",       Width = 130 });
            _dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Qty",        DataPropertyName = "Quantity",  Width = 55  });
            _dgvItems.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Unit Price", DataPropertyName = "UnitPrice", Width = 100 });
            pnlBody.Controls.Add(_dgvItems);
            y += 160;

            // Fulfillment details section
            pnlBody.Controls.Add(MakeSectionLabel("Fulfillment Details", y));
            y += 26;

            pnlBody.Controls.Add(new Label { Text = "Shipping Method:", AutoSize = true, Location = new Point(0, y + 3) });
            _cboShipMethod.Location      = new Point(130, y);
            _cboShipMethod.Size          = new Size(210, 23);
            _cboShipMethod.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (var m in AppSettings.Current.ShippingMethods)
                _cboShipMethod.Items.Add(m);
            if (_cboShipMethod.Items.Count > 0) _cboShipMethod.SelectedIndex = 0;
            pnlBody.Controls.Add(_cboShipMethod);

            pnlBody.Controls.Add(new Label { Text = "Tracking #:", AutoSize = true, Location = new Point(360, y + 3) });
            _txtTracking.Location        = new Point(440, y);
            _txtTracking.Size            = new Size(268, 23);
            _txtTracking.PlaceholderText = "optional";
            pnlBody.Controls.Add(_txtTracking);
            y += 32;

            pnlBody.Controls.Add(new Label { Text = "Notes:", AutoSize = true, Location = new Point(0, y + 4) });
            _txtNotes.Location        = new Point(130, y);
            _txtNotes.Size            = new Size(578, 50);
            _txtNotes.Multiline       = true;
            _txtNotes.PlaceholderText = "optional internal notes";
            pnlBody.Controls.Add(_txtNotes);
            y += 60;

            // Inventory section
            pnlBody.Controls.Add(MakeSectionLabel("Inventory", y));
            y += 26;

            _btnAssignInventory.Text     = "Assign Inventory Locations\u2026";
            _btnAssignInventory.Size     = new Size(210, 28);
            _btnAssignInventory.Location = new Point(0, y);
            _btnAssignInventory.Click   += BtnAssignInventory_Click;
            pnlBody.Controls.Add(_btnAssignInventory);

            _lblReservStatus.Text      = "No locations assigned";
            _lblReservStatus.ForeColor = Theme.TextSecondary;
            _lblReservStatus.AutoSize  = true;
            _lblReservStatus.Font      = new Font("Segoe UI", 9F, FontStyle.Italic);
            _lblReservStatus.Location  = new Point(220, y + 8);
            pnlBody.Controls.Add(_lblReservStatus);
            y += 42;

            // Steps info footer
            _lblStepsInfo.Text      = BuildStepsText();
            _lblStepsInfo.ForeColor = Theme.TextSecondary;
            _lblStepsInfo.Font      = new Font("Segoe UI", 8.5F, FontStyle.Italic);
            _lblStepsInfo.AutoSize  = true;
            _lblStepsInfo.Location  = new Point(0, y);
            pnlBody.Controls.Add(_lblStepsInfo);
        }

        private static Label MakeSectionLabel(string text, int y) => new()
        {
            Text      = text,
            Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Theme.Gold,
            AutoSize  = true,
            Location  = new Point(0, y)
        };

        private string BuildStepsText()
        {
            var ordered = new[] { "Draft", "Live", "Picking", "Packing", "Shipped", "Complete" };
            int idx     = Array.IndexOf(ordered, _currentStatus);
            if (idx < 0) idx = 0;

            var parts = new List<string>();
            foreach (var s in ordered)
            {
                int si = Array.IndexOf(ordered, s);
                parts.Add(si < idx ? $"[{s}]" : s);
            }
            string suffix = idx > 0 ? $"  (resuming from {_currentStatus})" : "";
            return "Steps: " + string.Join(" \u2192 ", parts) + suffix;
        }

        private void LoadItems()
        {
            try
            {
                var items = _svc.GetOrderItemsWithPicking(_salesOrderId);
                _dgvItems.DataSource = items;
                _lblInfo.Text =
                    $"Order #{_orderNumber}  |  {_customerName}  |  Status: {_currentStatus}  |  {items.Count} item(s)";
            }
            catch { /* non-fatal — form still usable */ }
        }

        private void BtnAssignInventory_Click(object? sender, EventArgs e)
        {
            try
            {
                var lines = _svc.GetSOReservationItems(_salesOrderId);
                if (lines.Count == 0)
                {
                    MessageBox.Show(this,
                        "No inventory items found for this order.\n\n" +
                        "This may mean the products are not tracked in any location.",
                        "Nothing to Reserve", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var resForm = new FormStockReservation(
                    $"Assign Inventory — Order #{_orderNumber}", lines);
                if (resForm.ShowDialog(this) == DialogResult.OK && resForm.ConfirmedLines?.Count > 0)
                {
                    _confirmedReservations          = resForm.ConfirmedLines;
                    _lblReservStatus.Text           = $"{_confirmedReservations.Count} line(s) assigned";
                    _lblReservStatus.ForeColor      = Color.FromArgb(80, 210, 100);
                    _lblReservStatus.Font           = new Font("Segoe UI", 9F);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not load inventory lines:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnFulfil_Click(object? sender, EventArgs e)
        {
            if (_cboShipMethod.SelectedItem is not string shipMethod || string.IsNullOrWhiteSpace(shipMethod))
            {
                MessageBox.Show(this,
                    "Select a shipping method before fulfilling.",
                    "Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ── Stock availability check ──────────────────────────────────────────
            // Skip if inventory was already deducted (e.g. pre-existing manual Live order)
            bool alreadyAffected = false;
            try { alreadyAffected = _svc.IsInventoryAffected(_salesOrderId); }
            catch { /* non-critical */ }

            if (!alreadyAffected)
            {
                try
                {
                    var lines = _svc.GetSOReservationItems(_salesOrderId);
                    var insufficient = lines
                        .GroupBy(l => l.ItemId)
                        .Where(g => g.Sum(l => l.Available) < g.First().Required)
                        .Select(g => g.First().DisplayLabel)
                        .ToList();

                    if (insufficient.Count > 0)
                    {
                        MessageBox.Show(this,
                            "Cannot fulfil — insufficient stock for:\n\n  • " +
                            string.Join("\n  • ", insufficient),
                            "Insufficient Stock", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                catch { /* if reservation check fails, fall through — don't block on a non-critical error */ }
            }

            string tracking = _txtTracking.Text.Trim();
            string notes    = _txtNotes.Text.Trim();

            var confirm = MessageBox.Show(this,
                $"Quick-fulfil Order #{_orderNumber} for {_customerName}?\n\n" +
                $"Shipping: {shipMethod}" +
                (string.IsNullOrEmpty(tracking) ? "" : $"\nTracking: {tracking}") +
                "\n\nAll remaining steps will be applied now, and inventory will be deducted on completion.",
                "Confirm Quick Fulfil", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            _btnFulfil.Enabled = false;
            Cursor = Cursors.WaitCursor;
            string currentStep = "initialising";
            try
            {
                string user = AppSession.CurrentUser?.Username ?? "system";

                // Save inventory reservations first (they must exist before Picking status)
                if (_confirmedReservations?.Count > 0)
                    _svc.SaveSOReservations(_salesOrderId, _confirmedReservations);

                // Advance through each remaining step
                foreach (var step in GetRemainingSteps())
                {
                    currentStep = step;
                    if (step == "Shipped")
                    {
                        _svc.RecordShipment(_salesOrderId,
                            string.IsNullOrEmpty(tracking) ? null : tracking,
                            shipMethod);
                    }
                    else if (step == "Complete")
                    {
                        _svc.MarkComplete(_salesOrderId);
                    }
                    else
                    {
                        _svc.UpdateOrderStatus(_salesOrderId, step);

                        // Stamp all items as fully picked so picking history is accurate
                        if (step == "Picking")
                        {
                            string picker = Security.AppSession.CurrentUser?.Username ?? "system";
                            try
                            {
                                var items = _svc.GetOrderItemsWithPicking(_salesOrderId);
                                foreach (var item in items)
                                    _svc.UpdatePickedQty(item.SalesOrderItemID, item.Quantity, picker);
                            }
                            catch { /* non-fatal — picking stamps can be corrected on the picking screen */ }
                        }
                    }
                }

                AppLogger.Audit(user, "QuickFulfil",
                    $"OrderID={_salesOrderId} #{_orderNumber} ShipMethod={shipMethod}" +
                    (string.IsNullOrEmpty(tracking) ? "" : $" Tracking={tracking}") +
                    (string.IsNullOrEmpty(notes)    ? "" : $" Notes={notes}"));

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    $"Fulfilment failed at step '{currentStep}':\n\n{ex.Message}\n\n" +
                    "Earlier steps may have already been applied. Check the order status before retrying.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnFulfil.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        /// <summary>
        /// Returns the ordered list of status transitions still needed to reach Complete,
        /// starting from the step after the current status.
        /// </summary>
        private List<string> GetRemainingSteps()
        {
            var ordered  = new[] { "Draft", "Live", "Picking", "Packing", "Shipped", "Complete" };
            var toRun    = new[] { "Live",  "Picking", "Packing", "Shipped", "Complete" };
            int currentIdx = Array.IndexOf(ordered, _currentStatus);
            if (currentIdx < 0) currentIdx = 0;

            var result = new List<string>();
            foreach (var step in toRun)
            {
                int stepIdx = Array.IndexOf(ordered, step);
                if (stepIdx > currentIdx)
                    result.Add(step);
            }
            return result;
        }
    }
}
