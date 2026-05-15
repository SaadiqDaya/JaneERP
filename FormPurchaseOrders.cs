using JaneERP.Data;
using JaneERP.Models;

namespace JaneERP
{
    public class FormPurchaseOrders : Form
    {
        private readonly SupplierRepository _repo = new();

        private ComboBox      cmbFilter    = new();
        private Button        btnNew       = new();
        private Button        btnReceive   = new();
        private DataGridView  dgvPOs       = new();
        private Label         lblStatus    = new();

        private List<PurchaseOrder> _orders = new();

        // Pagination
        private int _poPage      = 1;
        private int _poTotalCount = 0;
        private const int PoPageSize = 50;
        private Panel _pnlPoPager = new();

        public FormPurchaseOrders()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            LoadOrders();
        }

        private void BuildUI()
        {
            Text          = "Purchase Orders";
            ClientSize    = new Size(1000, 600);
            MinimumSize   = new Size(800, 500);
            StartPosition = FormStartPosition.CenterParent;

            // ── Header bar ───────────────────────────────────────────────────────
            Theme.AddFormHeader(this, "🚛  Purchase Orders");

            // ── Filter combo ─────────────────────────────────────────────────────
            var lblFilter = new Label
            {
                Text      = "Status:",
                AutoSize  = true,
                Location  = new Point(12, 56),
                ForeColor = Theme.TextSecondary
            };
            Controls.Add(lblFilter);

            cmbFilter.Location         = new Point(64, 52);
            cmbFilter.Size             = new Size(160, 24);
            cmbFilter.DropDownStyle    = ComboBoxStyle.DropDownList;
            cmbFilter.Items.AddRange(new object[] { "Draft + Sent", "All", "Draft", "Sent", "PartiallyReceived", "Received", "Cancelled" });
            cmbFilter.SelectedIndex    = 0;
            cmbFilter.SelectedIndexChanged += (_, _) => { _poPage = 1; LoadOrders(); };
            Controls.Add(cmbFilter);

            // ── Buttons ──────────────────────────────────────────────────────────
            btnNew.Text     = "+ New PO";
            btnNew.Location = new Point(250, 48);
            btnNew.Size     = new Size(110, 30);
            btnNew.UseVisualStyleBackColor = true;
            btnNew.Click   += BtnNew_Click;
            Controls.Add(btnNew);

            btnReceive.Text     = "Receive Items";
            btnReceive.Location = new Point(370, 48);
            btnReceive.Size     = new Size(120, 30);
            btnReceive.UseVisualStyleBackColor = true;
            btnReceive.Click   += BtnReceive_Click;
            Controls.Add(btnReceive);

            // ── Suppliers button ─────────────────────────────────────────────────
            var btnSuppliers = new Button
            {
                Text     = "Manage Suppliers",
                Location = new Point(500, 48),
                Size     = new Size(140, 30),
                UseVisualStyleBackColor = true
            };
            btnSuppliers.Click += (_, _) =>
            {
                using var frm = new FormSupplierManager(_repo);
                frm.ShowDialog(this);
            };
            Controls.Add(btnSuppliers);

            // ── Auto PO button ───────────────────────────────────────────────────
            var btnAutoPO = new Button
            {
                Text     = "Auto PO",
                Location = new Point(650, 48),
                Size     = new Size(90, 30),
                UseVisualStyleBackColor = true
            };
            btnAutoPO.Click += (_, _) =>
            {
                using var frm = new FormReorderReport();
                frm.ShowDialog(this);
                LoadOrders();   // always refresh — user may have created a PO from inside
            };
            Controls.Add(btnAutoPO);

            // ── Export CSV button ────────────────────────────────────────────────
            var btnExportCsv = new Button
            {
                Text     = "Export CSV\u2026",
                Location = new Point(750, 48),
                Size     = new Size(110, 30),
                UseVisualStyleBackColor = true
            };
            btnExportCsv.Click += BtnExportCsv_Click;
            Controls.Add(btnExportCsv);

            // ── DataGridView ─────────────────────────────────────────────────────
            dgvPOs.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvPOs.Location = new Point(12, 92);
            dgvPOs.Size     = new Size(976, 436);
            dgvPOs.ReadOnly = true;
            dgvPOs.AllowUserToAddRows     = false;
            dgvPOs.AllowUserToDeleteRows  = false;
            dgvPOs.AllowUserToResizeRows  = false;
            dgvPOs.SelectionMode          = DataGridViewSelectionMode.FullRowSelect;
            dgvPOs.MultiSelect            = false;
            dgvPOs.AutoGenerateColumns    = false;
            dgvPOs.RowHeadersVisible      = false;

            dgvPOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cNum",      HeaderText = "PO #",           DataPropertyName = "PONumber",     Width = 120 });
            dgvPOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSupplier", HeaderText = "Supplier",       DataPropertyName = "SupplierName", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvPOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cStatus",   HeaderText = "Status",         DataPropertyName = "Status",       Width = 130 });
            dgvPOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cOrder",    HeaderText = "Order Date",     DataPropertyName = "OrderDate",    Width = 110 });
            dgvPOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cExpected", HeaderText = "Expected",       DataPropertyName = "ExpectedDate", Width = 100 });
            dgvPOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cTotal",    HeaderText = "Total Cost",     DataPropertyName = "TotalCost",    Width = 100 });
            dgvPOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cBy",       HeaderText = "Created By",     DataPropertyName = "CreatedBy",    Width = 110 });

            dgvPOs.DefaultCellStyle.Format        = "";
            dgvPOs.CellFormatting += DgvPOs_CellFormatting;
            dgvPOs.CellDoubleClick += DgvPOs_CellDoubleClick;
            Controls.Add(dgvPOs);

            // ── Pagination bar ────────────────────────────────────────────────────
            _pnlPoPager.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _pnlPoPager.Location = new Point(12, 536);
            _pnlPoPager.Size     = new Size(976, 36);
            Controls.Add(_pnlPoPager);

            // ── Status label ─────────────────────────────────────────────────────
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(12, 576);
            lblStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(lblStatus);
        }

        private void DgvPOs_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _orders.Count) return;

            // Format dates nicely
            var col = dgvPOs.Columns[e.ColumnIndex].Name;
            if (col == "cOrder" || col == "cExpected")
            {
                if (e.Value is DateTime dt && dt != default)
                    e.Value = dt.ToString("dd MMM yyyy");
                else if (e.Value == null || e.Value == DBNull.Value)
                    e.Value = "-";
                e.FormattingApplied = true;
            }
            if (col == "cTotal" && e.Value is decimal d)
            {
                e.Value = $"${d:N2} CAD";
                e.FormattingApplied = true;
            }

            // Colour-code by status
            var order = _orders[e.RowIndex];
            var row   = dgvPOs.Rows[e.RowIndex];
            row.DefaultCellStyle.ForeColor = order.Status switch
            {
                "Received"          => Color.LimeGreen,
                "PartiallyReceived" => Color.Orange,
                "Cancelled"         => Color.Gray,
                "Sent"              => Theme.Teal,
                _                   => Theme.TextPrimary
            };

            // Overdue: past ExpectedDate and still actionable
            bool isOverdue = order.ExpectedDate.HasValue
                          && order.ExpectedDate.Value.Date < DateTime.Today
                          && order.Status is not ("Received" or "Cancelled");
            row.DefaultCellStyle.BackColor = isOverdue
                ? Color.FromArgb(70, 15, 15)
                : Color.Empty;
        }

        private void LoadOrders()
        {
            try
            {
                string? filter   = cmbFilter.SelectedItem?.ToString();
                string? statusFilter = filter == "All" ? null : filter;

                // Prefer server-side paged method; fall back to GetOrders + client-side slice
                try
                {
                    (_orders, _poTotalCount) = _repo.GetPagedOrders(_poPage, PoPageSize, statusFilter, null);
                }
                catch
                {
                    // Fallback: load all and slice
                    List<PurchaseOrder> all;
                    if (filter == "Draft + Sent")
                    {
                        all = _repo.GetOrders(null);
                        all = all.Where(o => o.Status is "Draft" or "Sent").ToList();
                    }
                    else
                    {
                        all = _repo.GetOrders(filter == "All" ? null : filter);
                    }
                    _poTotalCount = all.Count;
                    _orders = all.Skip((_poPage - 1) * PoPageSize).Take(PoPageSize).ToList();
                }

                dgvPOs.DataSource = null;
                dgvPOs.DataSource = _orders;

                // ── Refresh pagination bar ────────────────────────────────────────
                _pnlPoPager.Controls.Clear();
                var pager = BuildPaginationBar(() => _poPage, v => _poPage = v, _poTotalCount, PoPageSize, () => LoadOrders());
                pager.Dock = DockStyle.Fill;
                _pnlPoPager.Controls.Add(pager);

                lblStatus.Text = $"{_poTotalCount:N0} order(s)";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error loading orders: " + ex.Message;
            }
        }

        // ── Pagination helper ─────────────────────────────────────────────────────

        private Panel BuildPaginationBar(
            Func<int> getPage, Action<int> setPage, int totalCount, int pageSize,
            Action reload)
        {
            var panel   = new Panel { Height = 36, Dock = DockStyle.Bottom };
            var btnPrev = new Button { Text = "← Prev", Size = new Size(80, 28), Left = 8, Top = 4 };
            var lblPage = new Label  { AutoSize = true, Top = 10, Left = 96 };
            var btnNext = new Button { Text = "Next →", Size = new Size(80, 28), Left = 0, Top = 4 };

            int currentPage = getPage();
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            lblPage.Text    = $"Page {currentPage} of {totalPages}  ({totalCount:N0} records)";
            btnPrev.Enabled = currentPage > 1;
            btnNext.Enabled = currentPage < totalPages;
            btnNext.Left    = lblPage.PreferredWidth + 96 + 8;

            btnPrev.Click += (s, e) => { setPage(getPage() - 1); reload(); };
            btnNext.Click += (s, e) => { setPage(getPage() + 1); reload(); };

            panel.Controls.AddRange(new Control[] { btnPrev, lblPage, btnNext });
            return panel;
        }

        private void BtnNew_Click(object? sender, EventArgs e)
        {
            using var frm = new FormCreatePO(_repo);
            if (frm.ShowDialog(this) == DialogResult.OK)
                LoadOrders();
        }

        private void BtnReceive_Click(object? sender, EventArgs e)
        {
            if (dgvPOs.CurrentRow == null || dgvPOs.CurrentRow.Index < 0 || dgvPOs.CurrentRow.Index >= _orders.Count)
            {
                MessageBox.Show(this, "Select a Purchase Order first.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var selected = _orders[dgvPOs.CurrentRow.Index];
            if (selected.Status is "Received" or "Cancelled")
            {
                MessageBox.Show(this, $"PO is already '{selected.Status}'.", "Cannot Receive",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var po = _repo.GetOrder(selected.POID);
            if (po == null) return;

            using var frm = new FormReceiveItems(_repo, po);
            if (frm.ShowDialog(this) == DialogResult.OK)
                LoadOrders();
        }

        private void DgvPOs_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _orders.Count) return;
            var selected = _orders[e.RowIndex];
            var po = _repo.GetOrder(selected.POID);
            if (po == null) return;

            // Draft POs open in edit mode; all others are read-only
            bool isDraft = selected.Status == "Draft";
            using var frm = new FormCreatePO(_repo, po, editDraft: isDraft);
            frm.ShowDialog(this);
            LoadOrders();
        }

        private void BtnExportCsv_Click(object? sender, EventArgs e)
        {
            if (dgvPOs.CurrentRow == null || dgvPOs.CurrentRow.Index < 0 || dgvPOs.CurrentRow.Index >= _orders.Count)
            {
                MessageBox.Show(this, "Select a PO first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var selected = _orders[dgvPOs.CurrentRow.Index];
            var po = _repo.GetOrder(selected.POID);
            if (po == null) return;

            using var dlg = new SaveFileDialog
            {
                Filter   = "CSV Files (*.csv)|*.csv",
                FileName = $"{po.PONumber}.csv"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                ExportPOtoCsv(po, dlg.FileName);
                lblStatus.Text = $"Exported {po.PONumber} to CSV.";
                MessageBox.Show(this, $"Exported to {Path.GetFileName(dlg.FileName)}",
                    "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Export failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ExportPOtoCsv(PurchaseOrder po, string filePath)
        {
            var sb = new System.Text.StringBuilder();

            // Header block
            sb.AppendLine($"Purchase Order,{Csv(po.PONumber)}");
            sb.AppendLine($"Supplier,{Csv(po.SupplierName ?? "")}");
            sb.AppendLine($"Status,{Csv(po.Status)}");
            sb.AppendLine($"Order Date,{po.OrderDate:yyyy-MM-dd}");
            if (po.ExpectedDate.HasValue)
                sb.AppendLine($"Expected Date,{po.ExpectedDate.Value:yyyy-MM-dd}");
            sb.AppendLine($"Created By,{Csv(po.CreatedBy ?? "")}");
            if (!string.IsNullOrWhiteSpace(po.Notes))
                sb.AppendLine($"Notes,{Csv(po.Notes)}");
            sb.AppendLine();

            // Items
            sb.AppendLine("SKU,Item Name,Qty Ordered,Qty Received,Qty Remaining,Unit Cost,Line Total");
            foreach (var item in po.Items)
            {
                var line = item.QuantityOrdered * item.UnitCost;
                sb.AppendLine($"{Csv(item.SKU ?? "")},{Csv(item.ItemName)},{item.QuantityOrdered},{item.QuantityReceived},{item.QuantityRemaining},{item.UnitCost:F2},{line:F2}");
            }
            sb.AppendLine();

            // Totals
            sb.AppendLine($"Subtotal,,,,,,{po.TotalCost:F2}");
            if (po.ShippingCost != 0) sb.AppendLine($"Shipping,,,,,,{po.ShippingCost:F2}");
            if (po.TaxAmount    != 0) sb.AppendLine($"Tax,,,,,,{po.TaxAmount:F2}");
            sb.AppendLine($"Grand Total,,,,,,{(po.TotalCost + po.ShippingCost + po.TaxAmount):F2}");

            File.WriteAllText(filePath, sb.ToString(), System.Text.Encoding.UTF8);
        }

        private static string Csv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
        }
    }
}
