using System.Configuration;
using Dapper;
using Microsoft.Data.SqlClient;

namespace JaneERP
{
    public class FormCustomers : Form
    {
        private readonly string _cs =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found.");

        private DataGridView _dgvCustomers = new();
        private DataGridView _dgvOrders    = new();
        private TextBox      _txtSearch    = new();
        private Label        _lblName      = new();
        private Label        _lblEmail     = new();
        private Label        _lblStats     = new();
        private Label        _lblStatus    = new();

        private List<dynamic> _allCustomers = [];

        public FormCustomers()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            LoadCustomers();
        }

        private void BuildUI()
        {
            Text          = "Customers";
            ClientSize    = new Size(1020, 620);
            MinimumSize   = new Size(840, 520);
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label
            {
                Text      = "Customers",
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            });

            // Search
            Controls.Add(new Label { Text = "Search:", Location = new Point(12, 48), AutoSize = true });
            _txtSearch.Location       = new Point(68, 44);
            _txtSearch.Size           = new Size(220, 23);
            _txtSearch.PlaceholderText = "Filter by name or email...";
            _txtSearch.TextChanged   += (_, _) => ApplyFilter();
            Controls.Add(_txtSearch);

            // Left: customer list
            _dgvCustomers.Location        = new Point(12, 74);
            _dgvCustomers.Size            = new Size(420, 514);
            _dgvCustomers.Anchor          = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            _dgvCustomers.ReadOnly        = true;
            _dgvCustomers.AllowUserToAddRows    = false;
            _dgvCustomers.AllowUserToDeleteRows = false;
            _dgvCustomers.SelectionMode   = DataGridViewSelectionMode.FullRowSelect;
            _dgvCustomers.MultiSelect     = false;
            _dgvCustomers.AutoGenerateColumns = false;
            _dgvCustomers.RowHeadersVisible   = false;
            _dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName",   HeaderText = "Name",        Width = 150, ReadOnly = true });
            _dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { Name = "colEmail",  HeaderText = "Email",       AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            _dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOrders", HeaderText = "Orders",      Width = 60,  ReadOnly = true });
            _dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSpent",  HeaderText = "Spent",       Width = 90,  ReadOnly = true });
            _dgvCustomers.SelectionChanged += DgvCustomers_SelectionChanged;
            Controls.Add(_dgvCustomers);

            // Right: customer detail + recent orders
            int x = 448;

            _lblName.Location  = new Point(x, 74);
            _lblName.Font      = new Font("Segoe UI", 12F, FontStyle.Bold);
            _lblName.ForeColor = Theme.TextPrimary;
            _lblName.Size      = new Size(540, 28);
            Controls.Add(_lblName);

            _lblEmail.Location = new Point(x, 102);
            _lblEmail.Font     = new Font("Segoe UI", 9F);
            _lblEmail.ForeColor = Theme.TextSecondary;
            _lblEmail.AutoSize = true;
            Controls.Add(_lblEmail);

            _lblStats.Location  = new Point(x, 120);
            _lblStats.Font      = new Font("Segoe UI", 9F);
            _lblStats.ForeColor = Theme.Gold;
            _lblStats.AutoSize  = true;
            Controls.Add(_lblStats);

            Controls.Add(new Label
            {
                Text      = "Recent Orders:",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(x, 146),
                AutoSize  = true
            });

            _dgvOrders.Location        = new Point(x, 166);
            _dgvOrders.Size            = new Size(554, 422);
            _dgvOrders.Anchor          = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _dgvOrders.ReadOnly        = true;
            _dgvOrders.AllowUserToAddRows    = false;
            _dgvOrders.AllowUserToDeleteRows = false;
            _dgvOrders.AllowUserToResizeRows = false;
            _dgvOrders.AutoGenerateColumns   = false;
            _dgvOrders.RowHeadersVisible     = false;
            _dgvOrders.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colON",     HeaderText = "Order #",    Width = 80,  ReadOnly = true });
            _dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDate",   HeaderText = "Date",       Width = 110, ReadOnly = true });
            _dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTotal",  HeaderText = "Total",      Width = 90,  ReadOnly = true });
            _dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurr",   HeaderText = "Currency",   Width = 72,  ReadOnly = true });
            _dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colType",   HeaderText = "Type",       Width = 80,  ReadOnly = true });
            _dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStatus", HeaderText = "Status",     Width = 80,  ReadOnly = true });
            _dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPaid",   HeaderText = "Payment",    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            _dgvOrders.CellDoubleClick += DgvOrders_CellDoubleClick;
            Controls.Add(_dgvOrders);

            _lblStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _lblStatus.Location = new Point(12, ClientSize.Height - 24);
            _lblStatus.AutoSize = true;
            Controls.Add(_lblStatus);

            SizeChanged += (_, _) => _lblStatus.Location = new Point(12, ClientSize.Height - 24);
        }

        private void LoadCustomers()
        {
            try
            {
                using var db = new SqlConnection(_cs);
                _allCustomers = db.Query(@"
                    SELECT c.CustomerID,
                           ISNULL(c.FullName, '') AS FullName,
                           c.Email,
                           COUNT(so.SalesOrderID)         AS OrderCount,
                           ISNULL(SUM(so.TotalPrice), 0)  AS TotalSpent
                    FROM   Customers c
                    LEFT JOIN SalesOrders so ON so.CustomerID = c.CustomerID
                    GROUP BY c.CustomerID, c.FullName, c.Email
                    ORDER  BY TotalSpent DESC").Cast<dynamic>().ToList();

                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load customers: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyFilter()
        {
            string q = _txtSearch.Text.Trim();
            IEnumerable<dynamic> filtered = string.IsNullOrEmpty(q)
                ? _allCustomers
                : _allCustomers.Where(c =>
                    ((string)c.FullName).Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    ((string)c.Email).Contains(q, StringComparison.OrdinalIgnoreCase));

            _dgvCustomers.Rows.Clear();
            foreach (IDictionary<string, object> row in filtered)
            {
                int idx = _dgvCustomers.Rows.Add();
                var r   = _dgvCustomers.Rows[idx];
                r.Cells["colName"].Value   = row["FullName"];
                r.Cells["colEmail"].Value  = row["Email"];
                r.Cells["colOrders"].Value = row["OrderCount"];
                r.Cells["colSpent"].Value  = Convert.ToDecimal(row["TotalSpent"]).ToString("N2");
                r.Tag = (int)row["CustomerID"];
            }

            int total = _allCustomers.Count;
            int shown = _dgvCustomers.Rows.Count;
            _lblStatus.Text = q.Length > 0
                ? $"{shown} of {total} customer(s)"
                : $"{total} customer(s)";
        }

        private void DgvCustomers_SelectionChanged(object? sender, EventArgs e)
        {
            if (_dgvCustomers.SelectedRows.Count == 0) return;
            var row = _dgvCustomers.SelectedRows[0];
            if (row.Tag is not int customerId) return;

            _lblName.Text  = row.Cells["colName"].Value?.ToString() ?? "(No Name)";
            _lblEmail.Text = row.Cells["colEmail"].Value?.ToString() ?? "";

            _dgvOrders.Rows.Clear();
            try
            {
                using var db = new SqlConnection(_cs);
                var orders = db.Query(@"
                    SELECT SalesOrderID, OrderNumber, OrderDate, TotalPrice, Currency, OrderType, Status,
                           ISNULL(IsPaid, 0) AS IsPaid, PaidAt
                    FROM   SalesOrders
                    WHERE  CustomerID = @customerId
                    ORDER  BY OrderDate DESC",
                    new { customerId }).ToList();

                decimal total = 0m, totalPaid = 0m;
                foreach (IDictionary<string, object> o in orders)
                {
                    int idx = _dgvOrders.Rows.Add();
                    var r   = _dgvOrders.Rows[idx];
                    r.Cells["colON"].Value     = o["OrderNumber"];
                    r.Cells["colDate"].Value   = o["OrderDate"] is DateTime dt ? dt.ToString("yyyy-MM-dd") : "";
                    decimal price = Convert.ToDecimal(o["TotalPrice"]);
                    r.Cells["colTotal"].Value  = price.ToString("N2");
                    r.Cells["colCurr"].Value   = o["Currency"];
                    r.Cells["colType"].Value   = o["OrderType"];
                    r.Cells["colStatus"].Value = o["Status"];
                    r.Tag = (int)o["SalesOrderID"]; // stored for double-click
                    total += price;

                    bool paid = Convert.ToBoolean(o["IsPaid"]);
                    if (paid)
                    {
                        string paidDate = o["PaidAt"] is DateTime pd ? pd.ToString("yyyy-MM-dd") : "";
                        r.Cells["colPaid"].Value   = $"Paid {paidDate}".Trim();
                        r.DefaultCellStyle.ForeColor = Color.FromArgb(80, 210, 100);
                        totalPaid += price;
                    }
                    else
                    {
                        r.Cells["colPaid"].Value = "Unpaid";
                    }
                }

                string paidSummary = totalPaid > 0 ? $"  |  Paid: {totalPaid:N2}" : "";
                _lblStats.Text = $"{orders.Count} order(s)  |  Total: {total:N2}{paidSummary}";
            }
            catch
            {
                _lblStats.Text = "";
            }
        }

        private void DgvOrders_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = _dgvOrders.Rows[e.RowIndex];
            if (row.Tag is not int salesOrderId) return;
            string orderNum = row.Cells["colON"].Value?.ToString() ?? $"#{salesOrderId}";
            ShowOrderDetail(salesOrderId, orderNum);
        }

        private void ShowOrderDetail(int salesOrderId, string orderNumber)
        {
            try
            {
                using var db = new SqlConnection(_cs);
                var items = db.Query(@"
                    SELECT ISNULL(soi.SKU, '') AS SKU,
                           ISNULL(soi.Title, p.ProductName) AS ProductName,
                           soi.Quantity,
                           soi.UnitPrice,
                           soi.Quantity * soi.UnitPrice AS LineTotal
                    FROM   SalesOrderItems soi
                    LEFT JOIN Products p ON p.ProductID = soi.ProductID
                    WHERE  soi.SalesOrderID = @salesOrderId
                    ORDER  BY soi.SalesOrderItemID",
                    new { salesOrderId }).ToList();

                var frm = new Form
                {
                    Text          = $"Order {orderNumber} — Items",
                    ClientSize    = new Size(700, 400),
                    MinimumSize   = new Size(500, 300),
                    StartPosition = FormStartPosition.CenterParent,
                    Font          = this.Font
                };
                Theme.Apply(frm);
                Theme.MakeBorderless(frm);
                Theme.AddCloseButton(frm);
                Theme.MakeResizable(frm);

                var hdr = new Panel { Tag = "header", Dock = DockStyle.Top, Height = 44 };
                var lblTitle = new Label
                {
                    Text      = $"Order {orderNumber}",
                    Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                    ForeColor = Theme.Gold,
                    Location  = new Point(12, 8),
                    AutoSize  = true
                };
                hdr.Controls.Add(lblTitle);
                Theme.Apply(hdr);
                Theme.MakeDraggable(frm, hdr);

                var dgv = new DataGridView
                {
                    Dock                  = DockStyle.Fill,
                    ReadOnly              = true,
                    AllowUserToAddRows    = false,
                    AllowUserToDeleteRows = false,
                    AutoGenerateColumns   = false,
                    RowHeadersVisible     = false,
                    SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect           = false
                };
                dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSKU",      HeaderText = "SKU",          Width = 100 });
                dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProduct",  HeaderText = "Product",      AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
                dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colQty",      HeaderText = "Qty",          Width = 60  });
                dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUnit",     HeaderText = "Unit Price",   Width = 100 });
                dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLine",     HeaderText = "Line Total",   Width = 110 });
                Theme.StyleGrid(dgv);

                foreach (IDictionary<string, object> item in items)
                {
                    int idx = dgv.Rows.Add();
                    dgv.Rows[idx].Cells["colSKU"].Value     = item["SKU"];
                    dgv.Rows[idx].Cells["colProduct"].Value = item["ProductName"];
                    dgv.Rows[idx].Cells["colQty"].Value     = item["Quantity"];
                    dgv.Rows[idx].Cells["colUnit"].Value    = Convert.ToDecimal(item["UnitPrice"]).ToString("N2");
                    dgv.Rows[idx].Cells["colLine"].Value    = Convert.ToDecimal(item["LineTotal"]).ToString("N2");
                }

                if (items.Count == 0)
                    dgv.Rows.Add("", "(no items)", "", "", "");

                frm.Controls.Add(dgv);
                frm.Controls.Add(hdr);
                frm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load order items: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
