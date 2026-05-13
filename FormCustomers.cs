using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;

namespace JaneERP
{
    public class FormCustomers : Form
    {
        private readonly ICustomerRepository _custRepo   = AppServices.Get<ICustomerRepository>();
        private readonly IReturnRepository   _returnRepo = AppServices.Get<IReturnRepository>();

        private DataGridView _dgvCustomers = new();
        private DataGridView _dgvOrders    = new();
        private DataGridView _dgvNotes     = new();
        private TextBox      _txtSearch    = new();
        private Label        _lblName      = new();
        private Label        _lblEmail     = new();
        private Label        _lblStats     = new();
        private Label        _lblStatus    = new();
        private Button       _btnAddNote   = new();
        private Button       _btnDelNote   = new();

        private int _selectedCustomerId = -1;
        private List<CustomerSummary> _allCustomers = [];

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

            // Orders grid — reduced height to make room for notes panel
            _dgvOrders.Location        = new Point(x, 166);
            _dgvOrders.Size            = new Size(554, 240);
            _dgvOrders.Anchor          = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
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

            // "Create Return" button — appears below orders grid, only enabled when an order is selected
            var btnReturn = new Button
            {
                Text     = "Create Return",
                Location = new Point(x, 414),
                Size     = new Size(120, 27),
                Tag      = "btnReturn"
            };
            btnReturn.Click += BtnReturn_Click;
            Theme.StyleButton(btnReturn);
            Controls.Add(btnReturn);

            // ── Notes panel ─────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = "CRM Notes:",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(x, 450),
                AutoSize  = true
            });

            _btnAddNote.Text     = "+ Add Note";
            _btnAddNote.Location = new Point(x + 80, 446);
            _btnAddNote.Size     = new Size(90, 23);
            _btnAddNote.Click   += BtnAddNote_Click;
            _btnAddNote.Enabled  = false;
            Theme.StyleButton(_btnAddNote);
            Controls.Add(_btnAddNote);

            _btnDelNote.Text     = "Delete";
            _btnDelNote.Location = new Point(x + 178, 446);
            _btnDelNote.Size     = new Size(70, 23);
            _btnDelNote.Click   += BtnDelNote_Click;
            _btnDelNote.Enabled  = false;
            Theme.StyleButton(_btnDelNote);
            Controls.Add(_btnDelNote);

            _dgvNotes.Location        = new Point(x, 474);
            _dgvNotes.Size            = new Size(554, 112);
            _dgvNotes.Anchor          = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _dgvNotes.ReadOnly        = true;
            _dgvNotes.AllowUserToAddRows    = false;
            _dgvNotes.AllowUserToDeleteRows = false;
            _dgvNotes.AutoGenerateColumns   = false;
            _dgvNotes.RowHeadersVisible     = false;
            _dgvNotes.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgvNotes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNoteType", HeaderText = "Type",    Width = 70,  ReadOnly = true });
            _dgvNotes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNoteText", HeaderText = "Note",    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            _dgvNotes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNoteBy",   HeaderText = "By",      Width = 80,  ReadOnly = true });
            _dgvNotes.Columns.Add(new DataGridViewTextBoxColumn { Name = "colNoteDate", HeaderText = "Date",    Width = 95,  ReadOnly = true });
            _dgvNotes.SelectionChanged += (_, _) => _btnDelNote.Enabled = _dgvNotes.SelectedRows.Count > 0;
            Theme.StyleGrid(_dgvNotes);
            Controls.Add(_dgvNotes);

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
                _allCustomers = _custRepo.GetSummaries();
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
            var filtered = string.IsNullOrEmpty(q)
                ? _allCustomers
                : _allCustomers.Where(c =>
                    c.FullName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (c.Email ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

            _dgvCustomers.Rows.Clear();
            foreach (var c in filtered)
            {
                int idx = _dgvCustomers.Rows.Add();
                var r   = _dgvCustomers.Rows[idx];
                r.Cells["colName"].Value   = c.FullName;
                r.Cells["colEmail"].Value  = c.Email;
                r.Cells["colOrders"].Value = c.OrderCount;
                r.Cells["colSpent"].Value  = c.TotalSpent.ToString("N2");
                r.Tag = c.CustomerID;
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

            _selectedCustomerId = customerId;
            _btnAddNote.Enabled = true;

            _lblName.Text  = row.Cells["colName"].Value?.ToString() ?? "(No Name)";
            _lblEmail.Text = row.Cells["colEmail"].Value?.ToString() ?? "";

            LoadNotes(customerId);
            _dgvOrders.Rows.Clear();
            try
            {
                var orders = _custRepo.GetOrders(customerId);

                decimal total = 0m, totalPaid = 0m;
                foreach (var o in orders)
                {
                    int idx = _dgvOrders.Rows.Add();
                    var r   = _dgvOrders.Rows[idx];
                    r.Cells["colON"].Value     = o.OrderNumber;
                    r.Cells["colDate"].Value   = o.OrderDate.ToString("yyyy-MM-dd");
                    r.Cells["colTotal"].Value  = o.TotalPrice.ToString("N2");
                    r.Cells["colCurr"].Value   = o.Currency;
                    r.Cells["colType"].Value   = o.OrderType;
                    r.Cells["colStatus"].Value = o.Status;
                    r.Tag = o.SalesOrderID;
                    total += o.TotalPrice;

                    if (o.IsPaid)
                    {
                        string paidDate = o.PaidAt.HasValue ? o.PaidAt.Value.ToString("yyyy-MM-dd") : "";
                        r.Cells["colPaid"].Value     = $"Paid {paidDate}".Trim();
                        r.DefaultCellStyle.ForeColor = Color.FromArgb(80, 210, 100);
                        totalPaid += o.TotalPrice;
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

        private void LoadNotes(int customerId)
        {
            _dgvNotes.Rows.Clear();
            try
            {
                var notes = _custRepo.GetNotes(customerId);
                foreach (var n in notes)
                {
                    int idx = _dgvNotes.Rows.Add();
                    var r   = _dgvNotes.Rows[idx];
                    r.Cells["colNoteType"].Value = n.NoteType;
                    r.Cells["colNoteText"].Value = n.NoteText;
                    r.Cells["colNoteBy"].Value   = n.CreatedBy;
                    r.Cells["colNoteDate"].Value = n.CreatedAt.ToString("yyyy-MM-dd");
                    r.Tag = n.NoteID;
                }
            }
            catch { /* non-fatal */ }
        }

        private void BtnAddNote_Click(object? sender, EventArgs e)
        {
            if (_selectedCustomerId < 0) return;

            using var dlg = new Form
            {
                Text          = "Add Note",
                ClientSize    = new Size(440, 220),
                StartPosition = FormStartPosition.CenterParent,
                Font          = this.Font
            };
            Theme.Apply(dlg);
            Theme.MakeBorderless(dlg);
            Theme.AddCloseButton(dlg);

            var lblType = new Label { Text = "Type:", Location = new Point(12, 12), AutoSize = true };
            var cboType = new ComboBox
            {
                Location      = new Point(12, 32),
                Size          = new Size(130, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboType.Items.AddRange(new object[] { "Note", "Call", "Email", "Visit" });
            cboType.SelectedIndex = 0;

            var lblText = new Label { Text = "Note:", Location = new Point(12, 64), AutoSize = true };
            var txtText = new TextBox
            {
                Location  = new Point(12, 84),
                Size      = new Size(416, 80),
                Multiline = true
            };

            var btnSave = new Button { Text = "Save", Location = new Point(12, 174), Size = new Size(80, 28) };
            btnSave.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(txtText.Text)) return;
                _custRepo.AddNote(new CustomerNote
                {
                    CustomerID = _selectedCustomerId,
                    NoteType   = cboType.SelectedItem?.ToString() ?? "Note",
                    NoteText   = txtText.Text.Trim()
                });
                dlg.DialogResult = DialogResult.OK;
                dlg.Close();
            };
            Theme.StyleButton(btnSave);

            dlg.Controls.AddRange(new Control[] { lblType, cboType, lblText, txtText, btnSave });
            if (dlg.ShowDialog(this) == DialogResult.OK)
                LoadNotes(_selectedCustomerId);
        }

        private void BtnDelNote_Click(object? sender, EventArgs e)
        {
            if (_dgvNotes.SelectedRows.Count == 0) return;
            if (_dgvNotes.SelectedRows[0].Tag is not int noteId) return;

            var confirm = MessageBox.Show(this, "Delete this note?", "Confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            _custRepo.DeleteNote(noteId);
            LoadNotes(_selectedCustomerId);
        }

        private void BtnReturn_Click(object? sender, EventArgs e)
        {
            if (_dgvOrders.SelectedRows.Count == 0) return;
            var row = _dgvOrders.SelectedRows[0];
            if (row.Tag is not int salesOrderId) return;
            string orderNum = row.Cells["colON"].Value?.ToString() ?? $"#{salesOrderId}";

            using var frm = new FormCreateReturn(salesOrderId, orderNum);
            frm.ShowDialog(this);
        }

        private void ShowOrderDetail(int salesOrderId, string orderNumber)
        {
            try
            {
                var items = _custRepo.GetOrderLineItems(salesOrderId);

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

                foreach (var item in items)
                {
                    int idx = dgv.Rows.Add();
                    dgv.Rows[idx].Cells["colSKU"].Value     = item.SKU;
                    dgv.Rows[idx].Cells["colProduct"].Value = item.ProductName;
                    dgv.Rows[idx].Cells["colQty"].Value     = item.Quantity;
                    dgv.Rows[idx].Cells["colUnit"].Value    = item.UnitPrice.ToString("N2");
                    dgv.Rows[idx].Cells["colLine"].Value    = item.LineTotal.ToString("N2");
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
