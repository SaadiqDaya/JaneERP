using JaneERP.Models;

namespace JaneERP
{
    /// <summary>Customer search dialog — search by name, email. Returns selected customer on OK.</summary>
    internal class FormCustomerSearch : Form
    {
        private readonly List<Customer> _all;

        private TextBox      txtSearch   = new();
        private DataGridView dgvCustomers = new();
        private Button       btnSelect   = new();
        private Button       btnCancel   = new();
        private Label        lblCount    = new();

        public Customer? SelectedCustomer { get; private set; }

        public FormCustomerSearch(List<Customer> customers)
        {
            _all = customers;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            ApplyFilter();
        }

        private void BuildUI()
        {
            Text          = "Search Customers";
            ClientSize    = new Size(560, 420);
            MinimumSize   = new Size(480, 360);
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label
            {
                Text      = "Search Customers",
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            });

            Controls.Add(new Label { Text = "Search:", Location = new Point(12, 50), AutoSize = true });
            txtSearch.Location        = new Point(70, 47);
            txtSearch.Size            = new Size(300, 23);
            txtSearch.PlaceholderText = "Name, email…";
            txtSearch.TextChanged    += (_, _) => ApplyFilter();
            Controls.Add(txtSearch);

            // Grid
            dgvCustomers.AutoGenerateColumns = false;
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName",  HeaderText = "Name",  Width = 180, ReadOnly = true });
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { Name = "colEmail", HeaderText = "Email", Width = 250, ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvCustomers.AllowUserToAddRows    = false;
            dgvCustomers.AllowUserToDeleteRows = false;
            dgvCustomers.ReadOnly              = true;
            dgvCustomers.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvCustomers.MultiSelect           = false;
            dgvCustomers.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvCustomers.Location = new Point(12, 80);
            dgvCustomers.Size     = new Size(536, 286);
            dgvCustomers.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) SelectCurrent(); };
            Controls.Add(dgvCustomers);

            lblCount.AutoSize = true;
            lblCount.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            lblCount.Location = new Point(12, 380);
            Controls.Add(lblCount);

            btnSelect.Text     = "Select";
            btnSelect.Size     = new Size(90, 28);
            btnSelect.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSelect.Location = new Point(356, 378);
            btnSelect.Click   += (_, _) => SelectCurrent();
            Controls.Add(btnSelect);

            btnCancel.Text     = "Cancel";
            btnCancel.Size     = new Size(80, 28);
            btnCancel.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.Location = new Point(456, 378);
            btnCancel.Click   += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);
        }

        private void ApplyFilter()
        {
            var term = txtSearch.Text.Trim();
            var filtered = string.IsNullOrEmpty(term)
                ? _all
                : _all.Where(c =>
                    c.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (c.FullName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();

            dgvCustomers.Rows.Clear();
            foreach (var c in filtered)
            {
                int idx = dgvCustomers.Rows.Add();
                dgvCustomers.Rows[idx].Cells["colName"].Value  = c.FullName ?? "";
                dgvCustomers.Rows[idx].Cells["colEmail"].Value = c.Email;
                dgvCustomers.Rows[idx].Tag = c;
            }
            lblCount.Text = $"{filtered.Count} customer(s)";
        }

        private void SelectCurrent()
        {
            if (dgvCustomers.SelectedRows.Count == 0) return;
            SelectedCustomer = dgvCustomers.SelectedRows[0].Tag as Customer;
            if (SelectedCustomer != null) { DialogResult = DialogResult.OK; Close(); }
        }
    }
}
