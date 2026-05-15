using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;

namespace JaneERP
{
    public class FormCustomers : Form
    {
        private readonly ICustomerRepository _custRepo   = AppServices.Get<ICustomerRepository>();
        private readonly IReturnRepository   _returnRepo = AppServices.Get<IReturnRepository>();

        private DataGridView _dgvCustomers      = new();
        private DataGridView _dgvOrders         = new();
        private DataGridView _dgvNotes          = new();
        private TextBox      _txtSearch         = new();
        private Label        _lblName           = new();
        private Label        _lblEmail          = new();
        private Label        _lblStats          = new();
        private Label        _lblStatus         = new();
        private Label        _lblCredit         = new();
        private Button       _btnAddNote        = new();
        private Button       _btnDelNote        = new();
        private Button       _btnReceivePayment = new();
        private Button       _btnTxnPrev        = new();
        private Button       _btnTxnNext        = new();
        private Label        _lblTxnPage        = new();

        /// <summary>Tag placed on each row of the transactions grid to identify its type and ID.</summary>
        private sealed record TxnTag(string TxnType, int ID, bool IsPaid, decimal Amount);

        private int _selectedCustomerId = -1;
        private List<CustomerSummary> _allCustomers = [];

        // ── Transaction pagination state ─────────────────────────────────────
        private const int TxnPageSize = 25;
        private int _txnCurrentPage   = 1;
        private int _txnTotalCount    = 0;

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

            // ── Header bar ───────────────────────────────────────────────────────
            Theme.AddFormHeader(this, "👥  Customers");

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
            _dgvCustomers.AllowUserToResizeRows = false;
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

            _lblCredit.Location  = new Point(x + 300, 120);
            _lblCredit.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            _lblCredit.ForeColor = Color.FromArgb(80, 210, 100);
            _lblCredit.AutoSize  = true;
            Controls.Add(_lblCredit);

            Controls.Add(new Label
            {
                Text      = "Transactions:",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(x, 146),
                AutoSize  = true
            });

            // Transactions grid — shows sales orders and returns
            _dgvOrders.Location        = new Point(x, 166);
            _dgvOrders.Size            = new Size(554, 210);
            _dgvOrders.Anchor          = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _dgvOrders.ReadOnly        = true;
            _dgvOrders.AllowUserToAddRows    = false;
            _dgvOrders.AllowUserToDeleteRows = false;
            _dgvOrders.AllowUserToResizeRows = false;
            _dgvOrders.AutoGenerateColumns   = false;
            _dgvOrders.RowHeadersVisible     = false;
            _dgvOrders.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTxnType", HeaderText = "Type",      Width = 75,  ReadOnly = true });
            _dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRef",     HeaderText = "Reference", Width = 95,  ReadOnly = true });
            _dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDate",    HeaderText = "Date",      Width = 110, ReadOnly = true });
            _dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAmount",  HeaderText = "Amount",    Width = 90,  ReadOnly = true });
            _dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStatus",  HeaderText = "Status",    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            _dgvOrders.CellDoubleClick += DgvOrders_CellDoubleClick;
            _dgvOrders.SelectionChanged += (_, _) =>
            {
                if (_dgvOrders.SelectedRows.Count == 0) { _btnReceivePayment.Enabled = false; return; }
                var tag = _dgvOrders.SelectedRows[0].Tag as TxnTag;
                _btnReceivePayment.Enabled = tag?.TxnType == "Sale" && tag.IsPaid == false;
            };
            Controls.Add(_dgvOrders);

            // Pagination controls — below the transactions grid
            _btnTxnPrev.Text     = "← Prev";
            _btnTxnPrev.Location = new Point(x, 382);
            _btnTxnPrev.Size     = new Size(72, 24);
            _btnTxnPrev.Enabled  = false;
            _btnTxnPrev.Click   += (_, _) => { _txnCurrentPage--; LoadTransactionPage(); };
            Theme.StyleButton(_btnTxnPrev);
            Controls.Add(_btnTxnPrev);

            _lblTxnPage.Location  = new Point(x + 80, 386);
            _lblTxnPage.AutoSize  = true;
            _lblTxnPage.Font      = new Font("Segoe UI", 8.5F);
            _lblTxnPage.ForeColor = Theme.TextSecondary;
            _lblTxnPage.Text      = "";
            Controls.Add(_lblTxnPage);

            _btnTxnNext.Text     = "Next →";
            _btnTxnNext.Location = new Point(x + 300, 382);
            _btnTxnNext.Size     = new Size(72, 24);
            _btnTxnNext.Enabled  = false;
            _btnTxnNext.Click   += (_, _) => { _txnCurrentPage++; LoadTransactionPage(); };
            Theme.StyleButton(_btnTxnNext);
            Controls.Add(_btnTxnNext);

            // Buttons below the pagination row
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

            _btnReceivePayment.Text     = "Receive Payment";
            _btnReceivePayment.Location = new Point(x + 130, 414);
            _btnReceivePayment.Size     = new Size(130, 27);
            _btnReceivePayment.Enabled  = false;
            _btnReceivePayment.Click   += BtnReceivePayment_Click;
            Theme.StyleButton(_btnReceivePayment);
            Controls.Add(_btnReceivePayment);

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
            _btnReceivePayment.Enabled = false;

            _lblName.Text  = row.Cells["colName"].Value?.ToString() ?? "(No Name)";
            _lblEmail.Text = row.Cells["colEmail"].Value?.ToString() ?? "";

            try
            {
                decimal balance = _returnRepo.GetActiveCreditBalance(customerId);
                _lblCredit.Text = balance > 0 ? $"Credit: {balance:C}" : "";
            }
            catch (Exception ex) { Logging.AppLogger.Error($"[FormCustomers.GetActiveCreditBalance] customerId={customerId}: {ex}"); _lblCredit.Text = ""; }

            LoadNotes(customerId);

            // Reset to page 1 and load the paged transaction view
            _txnCurrentPage = 1;
            LoadTransactionPage();

            // Load summary stats from full order list (lightweight aggregate query)
            try
            {
                var orders = _custRepo.GetOrders(customerId);
                decimal total = 0m, totalPaid = 0m;
                foreach (var o in orders)
                {
                    total += o.TotalPrice;
                    if (o.IsPaid) totalPaid += o.TotalPrice;
                }

                int returnCount = 0;
                try { returnCount = _returnRepo.GetReturns(customerId).Count; }
                catch (Exception ex) { Logging.AppLogger.Error($"[FormCustomers.GetReturns] customerId={customerId}: {ex}"); }

                string paidSummary = totalPaid > 0 ? $"  |  Paid: ${totalPaid:N2}" : "";
                _lblStats.Text = $"{orders.Count} sale(s)  |  {returnCount} return(s)  |  Total: ${total:N2}{paidSummary}";
            }
            catch (Exception ex)
            {
                Logging.AppLogger.Error($"[FormCustomers.DgvCustomers_SelectionChanged] stats for customerId={customerId}: {ex}");
                _lblStats.Text = "";
            }
        }

        /// <summary>Loads the current page of unified transactions into the grid.</summary>
        private void LoadTransactionPage()
        {
            if (_selectedCustomerId < 0) return;
            _dgvOrders.Rows.Clear();
            _btnReceivePayment.Enabled = false;

            try
            {
                var (rows, total) = _custRepo.GetPagedTransactions(_selectedCustomerId, _txnCurrentPage, TxnPageSize);
                _txnTotalCount = total;

                int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)TxnPageSize));

                foreach (var row in rows)
                {
                    int idx = _dgvOrders.Rows.Add();
                    var r   = _dgvOrders.Rows[idx];
                    r.Cells["colTxnType"].Value = row.Type;
                    r.Cells["colRef"].Value     = row.Reference;
                    r.Cells["colDate"].Value    = row.TransDate.ToString("yyyy-MM-dd");
                    r.Cells["colAmount"].Value  = row.Amount > 0 ? $"${row.Amount:N2}" : "\u2014";
                    r.Cells["colStatus"].Value  = row.Status;

                    bool isInvoice = row.Type == "Invoice";
                    bool isPaid    = row.Status == "Paid";
                    r.Tag = new TxnTag(isInvoice ? "Sale" : "Payment", row.RefId, isPaid, row.Amount);

                    r.DefaultCellStyle.ForeColor = row.Type switch
                    {
                        "Payment" => Color.FromArgb(80, 210, 100),
                        "Invoice" when isPaid => Color.FromArgb(80, 210, 100),
                        "Invoice" => Color.FromArgb(220, 80, 80),
                        _ => Color.FromArgb(255, 180, 80)
                    };
                }

                _lblTxnPage.Text  = total > 0
                    ? $"Page {_txnCurrentPage} of {totalPages} ({total} transactions)"
                    : "No transactions";
                _btnTxnPrev.Enabled = _txnCurrentPage > 1;
                _btnTxnNext.Enabled = _txnCurrentPage < totalPages;
            }
            catch (Exception ex)
            {
                Logging.AppLogger.Error($"[FormCustomers.LoadTransactionPage] customerId={_selectedCustomerId} page={_txnCurrentPage}: {ex}");
                _lblTxnPage.Text = "Error loading transactions";
                _btnTxnPrev.Enabled = false;
                _btnTxnNext.Enabled = false;
            }
        }

        private void DgvOrders_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var tag = _dgvOrders.Rows[e.RowIndex].Tag as TxnTag;
            if (tag?.TxnType != "Sale") return;
            string orderNum = _dgvOrders.Rows[e.RowIndex].Cells["colRef"].Value?.ToString() ?? $"#{tag.ID}";
            ShowOrderDetail(tag.ID, orderNum);
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
            catch (Exception ex) { Logging.AppLogger.Error($"[FormCustomers.LoadNotes] customerId={customerId}: {ex}"); }
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
            var tag = _dgvOrders.SelectedRows[0].Tag as TxnTag;
            if (tag?.TxnType != "Sale") return;
            string orderNum = _dgvOrders.SelectedRows[0].Cells["colRef"].Value?.ToString() ?? $"#{tag.ID}";
            using var frm = new FormCreateReturn(tag.ID, orderNum);
            frm.ShowDialog(this);
        }

        private void BtnReceivePayment_Click(object? sender, EventArgs e)
        {
            if (_dgvOrders.SelectedRows.Count == 0) return;
            var tag = _dgvOrders.SelectedRows[0].Tag as TxnTag;
            if (tag == null || tag.TxnType != "Sale" || tag.IsPaid) return;
            if (_selectedCustomerId < 0) return;

            string orderRef = _dgvOrders.SelectedRows[0].Cells["colRef"].Value?.ToString() ?? $"#{tag.ID}";

            using var dlg = new Form
            {
                Text          = $"Receive Payment — {orderRef}",
                ClientSize    = new Size(400, 280),
                StartPosition = FormStartPosition.CenterParent,
                Font          = this.Font
            };
            Theme.Apply(dlg);
            Theme.MakeBorderless(dlg);
            Theme.AddCloseButton(dlg);

            var lblAmount = new Label { Text = "Amount:", Location = new Point(12, 12), AutoSize = true };
            var nudAmount = new NumericUpDown
            {
                Location      = new Point(12, 32),
                Size          = new Size(160, 26),
                DecimalPlaces = 2,
                Minimum       = 0m,
                Maximum       = 9_999_999m,
                Value         = tag.Amount
            };

            var lblMethod = new Label { Text = "Payment Method:", Location = new Point(190, 12), AutoSize = true };
            var cboMethod = new ComboBox
            {
                Location      = new Point(190, 32),
                Size          = new Size(190, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboMethod.Items.AddRange(new object[] { "Cash", "Credit Card", "Bank Transfer", "E-Transfer", "Cheque", "Store Credit", "Other" });
            cboMethod.SelectedIndex = 0;

            var lblDate = new Label { Text = "Date:", Location = new Point(12, 68), AutoSize = true };
            var dtpDate = new DateTimePicker
            {
                Location = new Point(12, 88),
                Size     = new Size(160, 23),
                Value    = DateTime.Today,
                Format   = DateTimePickerFormat.Short
            };

            var lblNotes = new Label { Text = "Notes (optional):", Location = new Point(12, 122), AutoSize = true };
            var txtNotes = new TextBox
            {
                Location  = new Point(12, 142),
                Size      = new Size(368, 72),
                Multiline = true
            };

            var btnSave   = new Button { Text = "Save",   Location = new Point(12,  228), Size = new Size(80, 28) };
            var btnCancel = new Button { Text = "Cancel", Location = new Point(100, 228), Size = new Size(80, 28) };
            btnSave.Click   += (_, _) => { dlg.DialogResult = DialogResult.OK;     dlg.Close(); };
            btnCancel.Click += (_, _) => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); };
            Theme.StyleButton(btnSave);
            Theme.StyleButton(btnCancel);

            dlg.Controls.AddRange(new Control[] { lblAmount, nudAmount, lblMethod, cboMethod,
                                                  lblDate, dtpDate, lblNotes, txtNotes,
                                                  btnSave, btnCancel });

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                decimal  amount = nudAmount.Value;
                string   method = cboMethod.SelectedItem?.ToString() ?? "Cash";
                DateTime date   = dtpDate.Value.Date;
                string?  notes  = string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim();

                _custRepo.RecordPayment(tag.ID, _selectedCustomerId, amount, method, date, notes);
                MessageBox.Show(this, $"Payment of {amount:C} recorded successfully.", "Payment Recorded",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                // Refresh the transactions panel and customer list totals
                DgvCustomers_SelectionChanged(null, EventArgs.Empty);
                LoadCustomers();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Payment Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
