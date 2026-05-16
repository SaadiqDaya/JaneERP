using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>
    /// Central screen for reviewing, approving, and rejecting customer returns.
    /// Left panel lists all returns; right panel shows the selected return's items
    /// and actions.  Approving a return credits inventory (resalable) and issues
    /// a CustomerCredit that flows into the accounting summary.
    /// </summary>
    public class FormReturnsManager : Form
    {
        private readonly IReturnRepository _repo = AppServices.Get<IReturnRepository>();

        // ── Layout controls ──────────────────────────────────────────────────────
        private DataGridView _dgvReturns = new();
        private DataGridView _dgvItems   = new();
        private ComboBox     _cboFilter  = new();
        private Button       _btnRefresh    = new();
        private Button       _btnApprove    = new();
        private Button       _btnReject     = new();
        private Button       _btnViewOrder  = new();
        private Label        _lblReturnMeta  = new();
        private Label        _lblCreditAmt   = new();
        private Label        _lblStatus      = new();

        private List<ReturnOrder> _allReturns = [];

        public FormReturnsManager()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            Load += (_, _) => LoadData();
        }

        private void BuildUI()
        {
            Text          = "Returns Manager";
            ClientSize    = new Size(1140, 660);
            MinimumSize   = new Size(900, 520);
            StartPosition = FormStartPosition.CenterParent;

            // ── Header ────────────────────────────────────────────────────────
            Controls.Add(new Label { Text = "Filter:", Location = new Point(12, 62), AutoSize = true });
            _cboFilter.Location      = new Point(54, 58);
            _cboFilter.Size          = new Size(160, 23);
            _cboFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboFilter.Items.AddRange(new object[] { "All", "Pending", "Approved", "Rejected" });
            _cboFilter.SelectedIndex    = 0;
            _cboFilter.SelectedIndexChanged += (_, _) => ApplyFilter();
            Controls.Add(_cboFilter);

            _btnRefresh.Text     = "Refresh";
            _btnRefresh.Location = new Point(224, 58);
            _btnRefresh.Size     = new Size(80, 27);
            _btnRefresh.Click   += (_, _) => LoadData();
            Theme.StyleButton(_btnRefresh);
            Controls.Add(_btnRefresh);

            // ── Left: returns list ─────────────────────────────────────────────
            _dgvReturns.Location        = new Point(12, 80);
            _dgvReturns.Size            = new Size(560, 548);
            _dgvReturns.Anchor          = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            _dgvReturns.ReadOnly        = true;
            _dgvReturns.AllowUserToAddRows    = false;
            _dgvReturns.AllowUserToDeleteRows = false;
            _dgvReturns.AutoGenerateColumns   = false;
            _dgvReturns.RowHeadersVisible     = false;
            _dgvReturns.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgvReturns.CellFormatting       += DgvReturns_CellFormatting;
            _dgvReturns.SelectionChanged     += DgvReturns_SelectionChanged;
            _dgvReturns.CellDoubleClick      += (_, _) => BtnViewOrder_Click(null, EventArgs.Empty);

            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRID",      HeaderText = "Return #",  Width = 72  });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn { Name = "colON",       HeaderText = "Order #",   Width = 80  });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCustomer", HeaderText = "Customer",  AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDate",     HeaderText = "Date",      Width = 95  });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn { Name = "colItems",    HeaderText = "Items",     Width = 50  });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCredit",   HeaderText = "Credit",    Width = 85  });
            _dgvReturns.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStatus",   HeaderText = "Status",    Width = 80  });
            Theme.StyleGrid(_dgvReturns);
            Controls.Add(_dgvReturns);

            // ── Right: detail panel ────────────────────────────────────────────
            int rx = 584;

            Controls.Add(new Label
            {
                Text      = "Return Detail",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(rx, 80),
                AutoSize  = true
            });

            _lblReturnMeta.Location  = new Point(rx, 106);
            _lblReturnMeta.Size      = new Size(544, 60);
            _lblReturnMeta.Font      = new Font("Segoe UI", 9F);
            _lblReturnMeta.ForeColor = Theme.TextSecondary;
            Controls.Add(_lblReturnMeta);

            // Items breakdown grid
            _dgvItems.Location        = new Point(rx, 174);
            _dgvItems.Size            = new Size(544, 280);
            _dgvItems.Anchor          = AnchorStyles.Top | AnchorStyles.Right;
            _dgvItems.ReadOnly        = true;
            _dgvItems.AllowUserToAddRows    = false;
            _dgvItems.AllowUserToDeleteRows = false;
            _dgvItems.AutoGenerateColumns   = false;
            _dgvItems.RowHeadersVisible     = false;
            _dgvItems.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgvItems.CellFormatting       += DgvItems_CellFormatting;

            _dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSKU",       HeaderText = "SKU",       Width = 100 });
            _dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProduct",   HeaderText = "Product",   AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOQty",      HeaderText = "Ordered",   Width = 65  });
            _dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRQty",      HeaderText = "Returned",  Width = 72  });
            _dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCond",      HeaderText = "Condition", Width = 90  });
            Theme.StyleGrid(_dgvItems);
            Controls.Add(_dgvItems);

            // Credit amount display
            _lblCreditAmt.Location  = new Point(rx, 462);
            _lblCreditAmt.Font      = new Font("Segoe UI", 11F, FontStyle.Bold);
            _lblCreditAmt.ForeColor = Theme.Gold;
            _lblCreditAmt.AutoSize  = true;
            Controls.Add(_lblCreditAmt);

            // Action buttons
            _btnApprove.Text     = "Approve + Issue Credit";
            _btnApprove.Location = new Point(rx, 494);
            _btnApprove.Size     = new Size(180, 32);
            _btnApprove.Enabled  = false;
            _btnApprove.Click   += BtnApprove_Click;
            Theme.StyleButton(_btnApprove);
            Controls.Add(_btnApprove);

            _btnReject.Text     = "Reject";
            _btnReject.Location = new Point(rx + 190, 494);
            _btnReject.Size     = new Size(90, 32);
            _btnReject.Enabled  = false;
            _btnReject.Click   += BtnReject_Click;
            Theme.StyleButton(_btnReject);
            Controls.Add(_btnReject);

            _btnViewOrder.Text     = "View Original Order";
            _btnViewOrder.Location = new Point(rx, 534);
            _btnViewOrder.Size     = new Size(160, 30);
            _btnViewOrder.Enabled  = false;
            _btnViewOrder.Click   += BtnViewOrder_Click;
            Theme.StyleButton(_btnViewOrder);
            Controls.Add(_btnViewOrder);

            _lblStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _lblStatus.Location = new Point(12, ClientSize.Height - 22);
            _lblStatus.AutoSize = true;
            Controls.Add(_lblStatus);
            SizeChanged += (_, _) => _lblStatus.Location = new Point(12, ClientSize.Height - 22);
            Theme.AddFormHeader(this, "↩️  Returns Manager");
        }

        // ── Data loading ─────────────────────────────────────────────────────────

        private void LoadData()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _allReturns = _repo.GetReturns();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load returns: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void ApplyFilter()
        {
            string filter = _cboFilter.SelectedItem?.ToString() ?? "All";
            var rows = filter == "All"
                ? _allReturns
                : _allReturns.Where(r => r.Status == filter).ToList();

            _dgvReturns.Rows.Clear();
            foreach (var ret in rows)
            {
                int idx = _dgvReturns.Rows.Add();
                var row = _dgvReturns.Rows[idx];
                row.Cells["colRID"].Value      = ret.ReturnID;
                row.Cells["colON"].Value       = ret.OriginalOrderNumber;
                row.Cells["colCustomer"].Value = ret.CustomerName;
                row.Cells["colDate"].Value     = ret.ReturnDate.ToString("yyyy-MM-dd");
                row.Cells["colItems"].Value    = ret.Items.Count;
                row.Cells["colStatus"].Value   = ret.Status;
                // Credit column: show if already approved
                row.Cells["colCredit"].Value   = ret.Status == "Approved" ? "Issued" : "—";
                row.Tag = ret;
            }

            int pending = _allReturns.Count(r => r.Status == "Pending");
            _lblStatus.Text = $"{rows.Count} return(s) shown  |  {pending} pending approval";
            ClearDetail();
        }

        private void ClearDetail()
        {
            _lblReturnMeta.Text   = "";
            _lblCreditAmt.Text    = "";
            _dgvItems.Rows.Clear();
            _btnApprove.Enabled   = false;
            _btnReject.Enabled    = false;
            _btnViewOrder.Enabled = false;
        }

        // ── Selection ────────────────────────────────────────────────────────────

        private void DgvReturns_SelectionChanged(object? sender, EventArgs e)
        {
            if (_dgvReturns.SelectedRows.Count == 0
                || _dgvReturns.SelectedRows[0].Tag is not ReturnOrder ret)
            {
                ClearDetail();
                return;
            }

            // Load full detail (with items) for selected return
            var full = _repo.GetById(ret.ReturnID) ?? ret;

            _lblReturnMeta.Text =
                $"Order: #{full.OriginalOrderNumber}   Customer: {full.CustomerName}\r\n" +
                $"Reason: {full.Reason ?? "—"}\r\n" +
                $"Notes: {full.Notes ?? "—"}   Created by: {full.CreatedBy ?? "—"}";

            // Items grid
            _dgvItems.Rows.Clear();
            foreach (var item in full.Items)
            {
                int idx = _dgvItems.Rows.Add();
                var row = _dgvItems.Rows[idx];
                row.Cells["colSKU"].Value     = item.SKU;
                row.Cells["colProduct"].Value = item.ProductName;
                row.Cells["colOQty"].Value    = item.OriginalQty;
                row.Cells["colRQty"].Value    = item.ReturnQty;
                row.Cells["colCond"].Value    = item.Condition;
                row.Tag = item;
            }

            // Estimate credit (RetailPrice × ReturnQty — actual is computed on approve)
            _lblCreditAmt.Text = full.Status == "Approved"
                ? "Credit: Already issued"
                : $"Est. credit: see approve step";

            bool isPending = full.Status == "Pending";
            _btnApprove.Enabled   = isPending;
            _btnReject.Enabled    = isPending;
            _btnViewOrder.Enabled = true;
        }

        // ── Actions ──────────────────────────────────────────────────────────────

        private void BtnApprove_Click(object? sender, EventArgs e)
        {
            if (_dgvReturns.SelectedRows.Count == 0
                || _dgvReturns.SelectedRows[0].Tag is not ReturnOrder ret) return;

            var confirm = MessageBox.Show(this,
                $"Approve Return #{ret.ReturnID}?\n\n" +
                "This will:\n" +
                "  • Restock Resalable items into inventory\n" +
                "  • Issue a customer credit note\n" +
                "  • Record the credit in accounting",
                "Confirm Approval", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                _repo.ApproveReturn(ret.ReturnID);
                LoadData();

                // Build per-product restock summary for confirmation message.
                var approved = _repo.GetById(ret.ReturnID);
                decimal balance = _repo.GetActiveCreditBalance(approved?.CustomerID ?? 0);

                var resalableItems = approved?.Items
                    .Where(i => i.Condition == "Resalable")
                    .ToList() ?? [];

                string restockLines = resalableItems.Count > 0
                    ? string.Join("\n", resalableItems.Select(i =>
                        $"  • {i.ReturnQty} unit(s) of {i.ProductName ?? i.SKU ?? $"ProductID {i.ProductID}"} restocked"))
                    : "  (no resalable items — no inventory restocked)";

                MessageBox.Show(this,
                    $"Return #{ret.ReturnID} approved.\n\n" +
                    $"Inventory restocked:\n{restockLines}\n\n" +
                    $"Customer credit balance: {balance:C}",
                    "Approved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Approval failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnReject_Click(object? sender, EventArgs e)
        {
            if (_dgvReturns.SelectedRows.Count == 0
                || _dgvReturns.SelectedRows[0].Tag is not ReturnOrder ret) return;

            var confirm = MessageBox.Show(this,
                $"Reject Return #{ret.ReturnID}? No inventory or credit changes will be made.",
                "Confirm Rejection", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            try
            {
                _repo.RejectReturn(ret.ReturnID);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Rejection failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnViewOrder_Click(object? sender, EventArgs e)
        {
            if (_dgvReturns.SelectedRows.Count == 0
                || _dgvReturns.SelectedRows[0].Tag is not ReturnOrder ret) return;

            // Load full detail in case items aren't populated yet
            var full = _repo.GetById(ret.ReturnID) ?? ret;

            string itemLines = full.Items.Count > 0
                ? string.Join("\n", full.Items.Select(i =>
                    $"  • {i.SKU}  {i.ProductName}  ×{i.ReturnQty}  [{i.Condition}]"))
                : "  (no items loaded)";

            MessageBox.Show(this,
                $"Original Order #:  {full.OriginalOrderNumber}\n" +
                $"Customer:          {full.CustomerName ?? "—"}\n" +
                $"Return Date:       {full.ReturnDate:yyyy-MM-dd}\n" +
                $"Status:            {full.Status}\n" +
                $"Reason:            {full.Reason ?? "—"}\n" +
                $"Notes:             {full.Notes ?? "—"}\n" +
                $"Created by:        {full.CreatedBy ?? "—"}\n\n" +
                $"Return Items:\n{itemLines}",
                $"Original Order #{full.OriginalOrderNumber} — Return #{full.ReturnID}",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ── Cell formatting ──────────────────────────────────────────────────────

        private void DgvReturns_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || _dgvReturns.Rows[e.RowIndex].Tag is not ReturnOrder ret) return;
            e.CellStyle.ForeColor = ret.Status switch
            {
                "Pending"  => Color.FromArgb(255, 200, 60),
                "Approved" => Color.FromArgb(80, 210, 100),
                "Rejected" => Color.FromArgb(200, 80, 80),
                _          => Theme.TextPrimary
            };
        }

        private void DgvItems_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || _dgvItems.Rows[e.RowIndex].Tag is not ReturnOrderItem item) return;
            if (_dgvItems.Columns[e.ColumnIndex].Name != "colCond") return;
            e.CellStyle.ForeColor = item.Condition switch
            {
                "Resalable" => Color.FromArgb(80, 210, 100),
                "Damaged"   => Color.FromArgb(255, 160, 60),
                "Destroy"   => Color.FromArgb(200, 80, 80),
                _           => Theme.TextPrimary
            };
        }
    }
}
