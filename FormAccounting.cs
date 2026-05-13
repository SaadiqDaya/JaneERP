using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Logging;
using JaneERP.Models;
using JaneERP.Security;

namespace JaneERP
{
    /// <summary>
    /// Accounting module: P&amp;L summary (Revenue, COGS, Gross Profit, Expenses, Net Profit)
    /// with expense entry and user-defined expense categories.
    /// </summary>
    public class FormAccounting : Form
    {
        private readonly IAccountingRepository _repo = AppServices.Get<IAccountingRepository>();

        // Date range
        private DateTimePicker _dtpFrom   = new();
        private DateTimePicker _dtpTo     = new();
        private Button         _btnLoad   = new();

        // Summary KPI labels
        private Label _lblRevenue     = new();
        private Label _lblCOGS        = new();
        private Label _lblGrossProfit = new();
        private Label _lblExpenses    = new();
        private Label _lblNetProfit   = new();

        // Expense entry button (opens popup)
        private Button        _btnAddExp   = new();

        // Expenses grid
        private DataGridView _dgvExpenses = new();
        private Button       _btnDeleteExp = new();

        private Label _lblStatus = new();

        public FormAccounting()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            Load += (_, _) => LoadData();
        }

        // ── UI ────────────────────────────────────────────────────────────────────

        private void BuildUI()
        {
            Text          = "Accounting";
            ClientSize    = new Size(1060, 720);
            MinimumSize   = new Size(900, 600);
            StartPosition = FormStartPosition.CenterParent;

            int y = 12;

            // Title
            Controls.Add(new Label
            {
                Text      = "Accounting",
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(14, y),
                AutoSize  = true
            });
            y += 38;

            // ── Date range ─────────────────────────────────────────────────────────
            Controls.Add(new Label { Text = "From:", Location = new Point(14, y + 3), AutoSize = true, ForeColor = Theme.TextSecondary });
            _dtpFrom.Location = new Point(54, y);
            _dtpFrom.Size     = new Size(120, 24);
            _dtpFrom.Format   = DateTimePickerFormat.Short;
            _dtpFrom.Value    = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            Controls.Add(_dtpFrom);

            Controls.Add(new Label { Text = "To:", Location = new Point(186, y + 3), AutoSize = true, ForeColor = Theme.TextSecondary });
            _dtpTo.Location = new Point(208, y);
            _dtpTo.Size     = new Size(120, 24);
            _dtpTo.Format   = DateTimePickerFormat.Short;
            _dtpTo.Value    = DateTime.Today;
            Controls.Add(_dtpTo);

            _btnLoad.Text     = "Load";
            _btnLoad.Location = new Point(340, y - 1);
            _btnLoad.Size     = new Size(70, 26);
            _btnLoad.UseVisualStyleBackColor = true;
            _btnLoad.Click   += (_, _) => LoadData();
            Controls.Add(_btnLoad);

            // ── Date quick-picks ──────────────────────────────────────────────────
            void AddQuickPick(string label, int x, Action setDates)
            {
                var btn = new Button
                {
                    Text      = label,
                    Location  = new Point(x, y - 1),
                    Size      = new Size(label.Length < 7 ? 60 : label.Length < 10 ? 78 : 100, 26),
                    Font      = new Font("Segoe UI", 8F),
                    FlatStyle = FlatStyle.Flat,
                    Cursor    = Cursors.Hand
                };
                btn.FlatAppearance.BorderColor = Theme.Border;
                btn.BackColor = Theme.Surface;
                btn.ForeColor = Theme.TextSecondary;
                btn.Click += (_, _) => { setDates(); LoadData(); };
                Controls.Add(btn);
            }

            int qx = 420;
            AddQuickPick("Today", qx, () => {
                _dtpFrom.Value = DateTime.Today; _dtpTo.Value = DateTime.Today;
            });
            qx += 64;
            AddQuickPick("Last Week", qx, () => {
                var mon = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Monday);
                if (DateTime.Today.DayOfWeek == DayOfWeek.Sunday) mon = mon.AddDays(-7);
                _dtpFrom.Value = mon.AddDays(-7); _dtpTo.Value = mon.AddDays(-1);
            });
            qx += 82;
            AddQuickPick("Last Month", qx, () => {
                var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
                _dtpFrom.Value = first; _dtpTo.Value = first.AddMonths(1).AddDays(-1);
            });
            qx += 84;
            AddQuickPick("Last Quarter", qx, () => {
                int q = (DateTime.Today.Month - 1) / 3;
                if (q == 0) { q = 4; var yr = DateTime.Today.Year - 1; _dtpFrom.Value = new DateTime(yr, 10, 1); _dtpTo.Value = new DateTime(yr, 12, 31); }
                else { var fm = (q - 1) * 3 + 1; _dtpFrom.Value = new DateTime(DateTime.Today.Year, fm, 1); _dtpTo.Value = new DateTime(DateTime.Today.Year, fm + 2, DateTime.DaysInMonth(DateTime.Today.Year, fm + 2)); }
            });
            qx += 106;
            AddQuickPick("Last Year", qx, () => {
                int yr = DateTime.Today.Year - 1;
                _dtpFrom.Value = new DateTime(yr, 1, 1); _dtpTo.Value = new DateTime(yr, 12, 31);
            });

            y += 36;

            // ── KPI tiles ──────────────────────────────────────────────────────────
            int kx = 14, kw = 190, kh = 70;
            void AddKpiTile(string title, Color color, Label valueLabel)
            {
                var pnl = new Panel { Location = new Point(kx, y), Size = new Size(kw, kh), BackColor = Theme.Surface };
                pnl.Controls.Add(new Label
                {
                    Text      = title,
                    Font      = new Font("Segoe UI", 8F),
                    ForeColor = Theme.TextSecondary,
                    Location  = new Point(8, 6),
                    AutoSize  = true
                });
                valueLabel.Text      = "—";
                valueLabel.Font      = new Font("Segoe UI", 14F, FontStyle.Bold);
                valueLabel.ForeColor = color;
                valueLabel.Location  = new Point(8, 24);
                valueLabel.Size      = new Size(kw - 16, 36);
                valueLabel.TextAlign = ContentAlignment.MiddleLeft;
                pnl.Controls.Add(valueLabel);
                Controls.Add(pnl);
                kx += kw + 8;
            }

            AddKpiTile("Total Revenue",  Theme.Teal,                        _lblRevenue);
            AddKpiTile("COGS",           Theme.Danger,                      _lblCOGS);
            AddKpiTile("Gross Profit",   Theme.Gold,                        _lblGrossProfit);
            AddKpiTile("Expenses",       Theme.Danger,                      _lblExpenses);
            AddKpiTile("Net Profit",     Color.FromArgb(80, 210, 100),      _lblNetProfit);
            y += kh + 16;

            // ── Add Expense button (opens popup) ───────────────────────────────────
            _btnAddExp.Text     = "+ Add Expense";
            _btnAddExp.Location = new Point(14, y);
            _btnAddExp.Size     = new Size(140, 32);
            _btnAddExp.Font     = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _btnAddExp.UseVisualStyleBackColor = true;
            _btnAddExp.Click   += (_, _) =>
            {
                using var dlg = new FormAddExpense(_repo);
                dlg.ShowDialog(this);
                LoadData();
            };
            Controls.Add(_btnAddExp);
            y += 50;

            // ── Expenses grid ──────────────────────────────────────────────────────
            var lblExpTitle = new Label
            {
                Text      = "Expense Transactions",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(490, y),
                AutoSize  = true
            };
            Controls.Add(lblExpTitle);

            _btnDeleteExp.Text     = "Delete Selected";
            _btnDeleteExp.Location = new Point(760, y - 2);
            _btnDeleteExp.Size     = new Size(120, 24);
            _btnDeleteExp.UseVisualStyleBackColor = true;
            _btnDeleteExp.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            _btnDeleteExp.Click   += BtnDeleteExp_Click;
            Controls.Add(_btnDeleteExp);

            _dgvExpenses.Location        = new Point(490, y + 22);
            _dgvExpenses.Anchor          = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _dgvExpenses.Size            = new Size(556, 300);
            _dgvExpenses.ReadOnly        = true;
            _dgvExpenses.AllowUserToAddRows    = false;
            _dgvExpenses.AllowUserToDeleteRows = false;
            _dgvExpenses.SelectionMode   = DataGridViewSelectionMode.FullRowSelect;
            _dgvExpenses.MultiSelect     = false;
            _dgvExpenses.AutoGenerateColumns = false;
            _dgvExpenses.RowHeadersVisible   = false;
            _dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDate",   HeaderText = "Date",     Width = 100 });
            _dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCat",    HeaderText = "Category", Width = 150 });
            _dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAmount", HeaderText = "Amount",   Width = 100 });
            _dgvExpenses.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDesc",   HeaderText = "Notes",    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            Controls.Add(_dgvExpenses);

            // ── Status bar ─────────────────────────────────────────────────────────
            _lblStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _lblStatus.Location = new Point(14, ClientSize.Height - 22);
            _lblStatus.AutoSize = true;
            Controls.Add(_lblStatus);

            SizeChanged += (_, _) => _lblStatus.Location = new Point(14, ClientSize.Height - 22);
        }

        // ── Data loading ──────────────────────────────────────────────────────────

        private void LoadData()
        {
            try
            {
                var from = _dtpFrom.Value.Date;
                var to   = _dtpTo.Value.Date.AddDays(1).AddTicks(-1);

                var summary = _repo.GetSummary(from, to);
                var rows    = _repo.GetExpenseRows(from, to);

                _lblRevenue.Text     = $"${summary.Revenue:N2}";
                _lblCOGS.Text        = $"${summary.Cogs:N2}";
                _lblGrossProfit.Text = $"${summary.GrossProfit:N2}";
                _lblExpenses.Text    = $"${summary.Expenses:N2}";
                _lblNetProfit.Text   = $"${summary.NetProfit:N2}";
                _lblNetProfit.ForeColor = summary.NetProfit >= 0
                    ? Color.FromArgb(80, 210, 100)
                    : Color.FromArgb(210, 80, 80);

                _dgvExpenses.Rows.Clear();
                foreach (var r in rows)
                {
                    int idx = _dgvExpenses.Rows.Add();
                    var row = _dgvExpenses.Rows[idx];
                    row.Cells["cDate"].Value   = r.ExpenseDate.ToString("yyyy-MM-dd");
                    row.Cells["cCat"].Value    = r.Category;
                    row.Cells["cAmount"].Value = $"${r.Amount:N2}";
                    row.Cells["cDesc"].Value   = r.Description ?? "";
                    row.Tag = r.ExpenseID;
                }

                _lblStatus.Text = $"Period: {from:yyyy-MM-dd} → {_dtpTo.Value:yyyy-MM-dd}  |  {rows.Count} expense(s)";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Error: " + ex.Message;
            }
        }

        // ── Expense actions ───────────────────────────────────────────────────────

        private void BtnDeleteExp_Click(object? sender, EventArgs e)
        {
            if (_dgvExpenses.CurrentRow?.Tag is not int expenseId) return;
            if (MessageBox.Show(this, "Delete this expense?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                _repo.DeleteExpense(expenseId);
                AppLogger.Audit(AppSession.CurrentUser?.Username, "DeleteExpense", $"ExpenseID={expenseId}");
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Delete failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Add Expense dialog — supports adding multiple expenses before closing
    // ─────────────────────────────────────────────────────────────────────────────

    internal sealed class FormAddExpense : Form
    {
        private readonly IAccountingRepository _repo;
        private List<Models.ExpenseCategory> _categories = new();

        private ComboBox       _cboCategory  = new();
        private NumericUpDown  _nudAmount    = new();
        private DateTimePicker _dtpDate      = new();
        private TextBox        _txtDesc      = new();
        private DataGridView   _dgvAdded     = new();
        private Label          _lblCount     = new();
        private Button         _btnAdd       = new();
        private Button         _btnDone      = new();
        private Button         _btnManageCat = new();

        public FormAddExpense(IAccountingRepository repo)
        {
            _repo = repo;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            LoadCategories();
        }

        private void BuildUI()
        {
            Text          = "Add Expense";
            ClientSize    = new Size(560, 500);
            MinimumSize   = new Size(480, 440);
            StartPosition = FormStartPosition.CenterParent;

            int y = 12, x = 12, cx = 130;

            Controls.Add(new Label
            {
                Text      = "Add Expense",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(x, y),
                AutoSize  = true
            });
            y += 38;

            void AddRow(string label, Control ctl, int ctlW = 260)
            {
                Controls.Add(new Label { Text = label, Location = new Point(x, y + 3), AutoSize = true, ForeColor = Theme.TextSecondary });
                ctl.Location = new Point(cx, y);
                ctl.Size     = new Size(ctlW, 24);
                Controls.Add(ctl);
                y += 34;
            }

            _cboCategory.DropDownStyle = ComboBoxStyle.DropDownList;
            AddRow("Category:", _cboCategory, 240);

            _btnManageCat.Text     = "+ Manage";
            _btnManageCat.Size     = new Size(84, 24);
            _btnManageCat.Location = new Point(cx + 246, y - 34);
            _btnManageCat.UseVisualStyleBackColor = true;
            _btnManageCat.Click   += (_, _) =>
            {
                using var frm = new FormExpenseCategories(_repo);
                frm.ShowDialog(this);
                LoadCategories();
            };
            Controls.Add(_btnManageCat);

            _nudAmount.DecimalPlaces = 2;
            _nudAmount.Maximum       = 9_999_999m;
            _nudAmount.Increment     = 10m;
            AddRow("Amount ($):", _nudAmount, 120);

            _dtpDate.Format = DateTimePickerFormat.Short;
            _dtpDate.Value  = DateTime.Today;
            AddRow("Date:", _dtpDate, 140);

            _txtDesc.PlaceholderText = "Description / notes";
            AddRow("Notes:", _txtDesc, 340);

            // Add button
            _btnAdd.Text     = "Add Expense";
            _btnAdd.Size     = new Size(130, 30);
            _btnAdd.Location = new Point(cx, y);
            _btnAdd.Font     = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _btnAdd.UseVisualStyleBackColor = true;
            _btnAdd.Click   += BtnAdd_Click;
            Controls.Add(_btnAdd);
            y += 42;

            // Recently added list
            Controls.Add(new Label
            {
                Text      = "Added this session:",
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(x, y),
                AutoSize  = true
            });
            _lblCount.Location  = new Point(cx + 80, y);
            _lblCount.AutoSize  = true;
            _lblCount.ForeColor = Theme.Teal;
            Controls.Add(_lblCount);
            y += 20;

            _dgvAdded.Location             = new Point(x, y);
            _dgvAdded.Anchor               = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _dgvAdded.Size                 = new Size(534, 160);
            _dgvAdded.ReadOnly             = true;
            _dgvAdded.AllowUserToAddRows   = false;
            _dgvAdded.AllowUserToDeleteRows= false;
            _dgvAdded.AutoGenerateColumns  = false;
            _dgvAdded.RowHeadersVisible    = false;
            _dgvAdded.SelectionMode        = DataGridViewSelectionMode.FullRowSelect;
            _dgvAdded.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDate", HeaderText = "Date",     Width = 90 });
            _dgvAdded.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCat",  HeaderText = "Category", Width = 140 });
            _dgvAdded.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAmt",  HeaderText = "Amount",   Width = 90 });
            _dgvAdded.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDesc", HeaderText = "Notes",    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            Controls.Add(_dgvAdded);

            // Done button
            _btnDone.Text     = "Done";
            _btnDone.Size     = new Size(88, 30);
            _btnDone.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnDone.Location = new Point(ClientSize.Width - 100, ClientSize.Height - 40);
            _btnDone.UseVisualStyleBackColor = true;
            _btnDone.Click   += (_, _) => Close();
            Controls.Add(_btnDone);

            SizeChanged += (_, _) =>
                _btnDone.Location = new Point(ClientSize.Width - 100, ClientSize.Height - 40);
        }

        private void LoadCategories()
        {
            try
            {
                _categories = _repo.GetActiveCategories();
                _cboCategory.Items.Clear();
                foreach (var cat in _categories)
                    _cboCategory.Items.Add(cat.Name);
                if (_cboCategory.Items.Count > 0 && _cboCategory.SelectedIndex < 0)
                    _cboCategory.SelectedIndex = 0;
            }
            catch (Exception ex) { AppLogger.Info($"[FormAddExpense.LoadCategories]: {ex.Message}"); }
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            if (_nudAmount.Value <= 0)
            {
                MessageBox.Show(this, "Amount must be greater than 0.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_cboCategory.SelectedIndex < 0 || _cboCategory.SelectedIndex >= _categories.Count)
            {
                MessageBox.Show(this, "Select a category.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var cat    = _categories[_cboCategory.SelectedIndex];
                var desc   = string.IsNullOrWhiteSpace(_txtDesc.Text) ? (string?)null : _txtDesc.Text.Trim();
                var date   = _dtpDate.Value.Date;
                var amount = _nudAmount.Value;

                _repo.AddExpense(cat.CategoryID, amount, desc, date, AppSession.CurrentUser?.Username);
                AppLogger.Audit(AppSession.CurrentUser?.Username, "AddExpense",
                    $"Category={cat.Name} Amount={amount:N2}");

                // Add to the session list
                int idx = _dgvAdded.Rows.Add();
                var row = _dgvAdded.Rows[idx];
                row.Cells["cDate"].Value = date.ToString("yyyy-MM-dd");
                row.Cells["cCat"].Value  = cat.Name;
                row.Cells["cAmt"].Value  = $"${amount:N2}";
                row.Cells["cDesc"].Value = desc ?? "";
                _lblCount.Text = $"{_dgvAdded.Rows.Count} added";

                // Reset for next entry
                _nudAmount.Value = 0;
                _txtDesc.Clear();
                _nudAmount.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error adding expense",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Expense Category Manager dialog
    // ─────────────────────────────────────────────────────────────────────────────

    internal sealed class FormExpenseCategories : Form
    {
        private readonly IAccountingRepository _repo;
        private DataGridView _dgv    = new();
        private TextBox      _txtNew = new();
        private Button       _btnAdd = new();

        public FormExpenseCategories(IAccountingRepository repo)
        {
            _repo = repo;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            LoadCategories();
        }

        private void BuildUI()
        {
            Text          = "Manage Expense Categories";
            ClientSize    = new Size(400, 400);
            MinimumSize   = new Size(360, 320);
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label
            {
                Text      = "Expense Categories",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            });

            _dgv.Location        = new Point(12, 44);
            _dgv.Size            = new Size(374, 270);
            _dgv.Anchor          = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _dgv.ReadOnly        = true;
            _dgv.AllowUserToAddRows    = false;
            _dgv.AllowUserToDeleteRows = false;
            _dgv.SelectionMode   = DataGridViewSelectionMode.FullRowSelect;
            _dgv.AutoGenerateColumns = false;
            _dgv.RowHeadersVisible   = false;
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",   HeaderText = "Category Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cActive", HeaderText = "Active",        Width = 60,   ReadOnly = true });
            Controls.Add(_dgv);

            Controls.Add(new Label { Text = "New category:", Location = new Point(12, 326), AutoSize = true });
            _txtNew.Location = new Point(110, 322);
            _txtNew.Size     = new Size(180, 23);
            Controls.Add(_txtNew);

            _btnAdd.Text     = "Add";
            _btnAdd.Location = new Point(298, 320);
            _btnAdd.Size     = new Size(88, 26);
            _btnAdd.UseVisualStyleBackColor = true;
            _btnAdd.Click   += (_, _) => AddCategory();
            Controls.Add(_btnAdd);

            var btnToggle = new Button
            {
                Text     = "Toggle Active",
                Location = new Point(12, 358),
                Size     = new Size(120, 26),
                UseVisualStyleBackColor = true
            };
            btnToggle.Click += (_, _) => ToggleSelected();
            Controls.Add(btnToggle);

            SizeChanged += (_, _) =>
            {
                _dgv.Size = new Size(ClientSize.Width - 24, ClientSize.Height - 130);
                _txtNew.Location = new Point(110, ClientSize.Height - 74);
                _btnAdd.Location = new Point(ClientSize.Width - 100, ClientSize.Height - 76);
            };
        }

        private void LoadCategories()
        {
            _dgv.Rows.Clear();
            try
            {
                var cats = _repo.GetAllCategories();
                foreach (var c in cats)
                {
                    int idx = _dgv.Rows.Add();
                    var r   = _dgv.Rows[idx];
                    r.Cells["cName"].Value   = c.Name;
                    r.Cells["cActive"].Value = c.IsActive ? "Yes" : "No";
                    r.Tag = c.CategoryID;
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void AddCategory()
        {
            string name = _txtNew.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;
            try
            {
                _repo.AddCategory(name);
                _txtNew.Clear();
                LoadCategories();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void ToggleSelected()
        {
            if (_dgv.CurrentRow?.Tag is not int catId) return;
            try
            {
                _repo.ToggleCategory(catId);
                LoadCategories();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}
