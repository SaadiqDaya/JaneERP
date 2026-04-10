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

            // ── Title label ──────────────────────────────────────────────────────
            var lblTitle = new Label
            {
                Text      = "Purchase Orders",
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                AutoSize  = false,
                Location  = new Point(12, 12),
                Size      = new Size(300, 30)
            };
            Controls.Add(lblTitle);

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
            cmbFilter.Items.AddRange(new object[] { "All", "Draft", "Sent", "PartiallyReceived", "Received", "Cancelled" });
            cmbFilter.SelectedIndex    = 0;
            cmbFilter.SelectedIndexChanged += (_, _) => LoadOrders();
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

            // ── DataGridView ─────────────────────────────────────────────────────
            dgvPOs.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvPOs.Location = new Point(12, 92);
            dgvPOs.Size     = new Size(976, 472);
            dgvPOs.ReadOnly = true;
            dgvPOs.AllowUserToAddRows    = false;
            dgvPOs.AllowUserToDeleteRows = false;
            dgvPOs.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvPOs.MultiSelect           = false;
            dgvPOs.AutoGenerateColumns   = false;
            dgvPOs.RowHeadersVisible     = false;

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
                e.Value = $"R {d:N2}";
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
        }

        private void LoadOrders()
        {
            try
            {
                string? filter = cmbFilter.SelectedItem?.ToString();
                if (filter == "All") filter = null;

                _orders = _repo.GetOrders(filter);
                dgvPOs.DataSource = null;
                dgvPOs.DataSource = _orders;
                lblStatus.Text    = $"{_orders.Count} order(s)";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error loading orders: " + ex.Message;
            }
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
            using var frm = new FormCreatePO(_repo, po);
            frm.ShowDialog(this);
            LoadOrders();
        }
    }
}
