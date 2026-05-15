using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Logging;
using JaneERP.Models;
using JaneERP.Security;

namespace JaneERP
{
    /// <summary>
    /// Accounting module: P&amp;L summary (Revenue, COGS, Gross Profit, Expenses, Net Profit)
    /// with expense entry, user-defined expense categories, revenue drill-through, and COGS breakdown.
    /// </summary>
    public class FormAccounting : Form
    {
        private readonly IAccountingRepository _repo = AppServices.Get<IAccountingRepository>();

        // Date range
        private DateTimePicker _dtpFrom   = new();
        private DateTimePicker _dtpTo     = new();
        private Button         _btnLoad   = new();

        // Summary KPI labels
        private Label _lblRevenue        = new();   // shows Net Revenue
        private Label _lblRevenueGross   = new();   // sub-line: Gross Revenue
        private Label _lblRevenueReturns = new();   // sub-line: Returns/Credits
        private Label _lblCOGS           = new();
        private Label _lblGrossProfit    = new();
        private Label _lblExpenses       = new();
        private Label _lblNetProfit      = new();

        // COGS detail button
        private Button _btnCogsDetail = new();

        // Expense entry buttons
        private Button        _btnAddExp      = new();
        private Button        _btnAddExpBulk  = new();

        // Invoice filter toggle
        private CheckBox      _chkPaidOnly = new();

        // Revenue panel toggle button + wrapper panel
        private Button _btnViewRevenue  = new();
        private Button _btnViewExpenses = new();
        private Panel  _pnlRevenue      = new();
        private Panel  _pnlExpenses     = new();

        // Revenue grid (order-level drill-through)
        private DataGridView        _dgvRevenue  = new();
        private List<RevenueRow>    _revenueRows = new();
        private Panel               _pnlOrderDetail = new();
        private Label               _lblOrderDetail  = new();

        // Pagination
        private int _revenuePage    = 1;
        private int _revenueTotalCount = 0;
        private const int RevenuePageSize = 50;
        private Panel _pnlRevenuePager = new();

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
            ClientSize    = new Size(1160, 780);
            MinimumSize   = new Size(960, 640);
            StartPosition = FormStartPosition.CenterParent;

            // ── Header bar ───────────────────────────────────────────────────────
            Theme.AddFormHeader(this, "💰  Accounting");

            int y = 12;

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
            _btnLoad.Click   += (_, _) => { _revenuePage = 1; LoadData(); };
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
                btn.Click += (_, _) => { setDates(); _revenuePage = 1; LoadData(); };
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
            // Revenue tile is wider to accommodate three sub-lines (Gross / Returns / Net)
            int kx = 14, kh = 80;
            const int kw      = 190;   // standard tile width
            const int kRevW   = 250;   // revenue tile is a bit wider

            // Revenue tile — custom layout with three sub-labels
            {
                var pnl = new Panel { Location = new Point(kx, y), Size = new Size(kRevW, kh), BackColor = Theme.Surface };
                pnl.Controls.Add(new Label
                {
                    Text      = "Revenue",
                    Font      = new Font("Segoe UI", 8F),
                    ForeColor = Theme.TextSecondary,
                    Location  = new Point(8, 5),
                    AutoSize  = true
                });

                // Net Revenue (large, primary)
                _lblRevenue.Text      = "—";
                _lblRevenue.Font      = new Font("Segoe UI", 13F, FontStyle.Bold);
                _lblRevenue.ForeColor = Theme.Teal;
                _lblRevenue.Location  = new Point(8, 20);
                _lblRevenue.Size      = new Size(kRevW - 16, 24);
                _lblRevenue.TextAlign = ContentAlignment.MiddleLeft;
                pnl.Controls.Add(_lblRevenue);

                // Gross Revenue sub-line
                _lblRevenueGross.Text      = "";
                _lblRevenueGross.Font      = new Font("Segoe UI", 7.5F);
                _lblRevenueGross.ForeColor = Theme.TextSecondary;
                _lblRevenueGross.Location  = new Point(8, 46);
                _lblRevenueGross.Size      = new Size(kRevW - 16, 14);
                pnl.Controls.Add(_lblRevenueGross);

                // Returns sub-line
                _lblRevenueReturns.Text      = "";
                _lblRevenueReturns.Font      = new Font("Segoe UI", 7.5F);
                _lblRevenueReturns.ForeColor = Color.FromArgb(210, 100, 100);
                _lblRevenueReturns.Location  = new Point(8, 62);
                _lblRevenueReturns.Size      = new Size(kRevW - 16, 14);
                pnl.Controls.Add(_lblRevenueReturns);

                Controls.Add(pnl);
                kx += kRevW + 8;
            }

            // COGS tile — with a "Detail" button
            {
                var pnl = new Panel { Location = new Point(kx, y), Size = new Size(kw, kh), BackColor = Theme.Surface };
                pnl.Controls.Add(new Label
                {
                    Text      = "COGS",
                    Font      = new Font("Segoe UI", 8F),
                    ForeColor = Theme.TextSecondary,
                    Location  = new Point(8, 5),
                    AutoSize  = true
                });
                _lblCOGS.Text      = "—";
                _lblCOGS.Font      = new Font("Segoe UI", 13F, FontStyle.Bold);
                _lblCOGS.ForeColor = Theme.Danger;
                _lblCOGS.Location  = new Point(8, 20);
                _lblCOGS.Size      = new Size(kw - 16, 24);
                _lblCOGS.TextAlign = ContentAlignment.MiddleLeft;
                pnl.Controls.Add(_lblCOGS);

                _btnCogsDetail.Text      = "Detail";
                _btnCogsDetail.Font      = new Font("Segoe UI", 7.5F);
                _btnCogsDetail.FlatStyle = FlatStyle.Flat;
                _btnCogsDetail.Size      = new Size(50, 18);
                _btnCogsDetail.Location  = new Point(8, 55);
                _btnCogsDetail.Cursor    = Cursors.Hand;
                _btnCogsDetail.FlatAppearance.BorderColor = Theme.Border;
                _btnCogsDetail.BackColor = Theme.Surface;
                _btnCogsDetail.ForeColor = Theme.TextSecondary;
                _btnCogsDetail.Click    += BtnCogsDetail_Click;
                pnl.Controls.Add(_btnCogsDetail);

                Controls.Add(pnl);
                kx += kw + 8;
            }

            // Standard tiles: Gross Profit, Expenses, Net Profit
            void AddKpiTile(string title, Color color, Label valueLabel)
            {
                var pnl = new Panel { Location = new Point(kx, y), Size = new Size(kw, kh), BackColor = Theme.Surface };
                pnl.Controls.Add(new Label
                {
                    Text      = title,
                    Font      = new Font("Segoe UI", 8F),
                    ForeColor = Theme.TextSecondary,
                    Location  = new Point(8, 5),
                    AutoSize  = true
                });
                valueLabel.Text      = "—";
                valueLabel.Font      = new Font("Segoe UI", 13F, FontStyle.Bold);
                valueLabel.ForeColor = color;
                valueLabel.Location  = new Point(8, 20);
                valueLabel.Size      = new Size(kw - 16, 24);
                valueLabel.TextAlign = ContentAlignment.MiddleLeft;
                pnl.Controls.Add(valueLabel);
                Controls.Add(pnl);
                kx += kw + 8;
            }

            AddKpiTile("Gross Profit",   Theme.Gold,                   _lblGrossProfit);
            AddKpiTile("Expenses",       Theme.Danger,                 _lblExpenses);
            AddKpiTile("Net Profit",     Color.FromArgb(80, 210, 100), _lblNetProfit);
            y += kh + 14;

            // ── Add Expense buttons + paid filter toggle ───────────────────────────
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

            _btnAddExpBulk.Text     = "+ Bulk Add Expenses";
            _btnAddExpBulk.Location = new Point(162, y);
            _btnAddExpBulk.Size     = new Size(160, 32);
            _btnAddExpBulk.Font     = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _btnAddExpBulk.UseVisualStyleBackColor = true;
            _btnAddExpBulk.Click   += (_, _) =>
            {
                using var dlg = new FormAddExpense(_repo, startInBulkMode: true);
                dlg.ShowDialog(this);
                LoadData();
            };
            Controls.Add(_btnAddExpBulk);

            _chkPaidOnly.Text     = "Show Paid Only";
            _chkPaidOnly.Location = new Point(340, y + 6);
            _chkPaidOnly.AutoSize = true;
            _chkPaidOnly.Checked  = false;
            _chkPaidOnly.CheckedChanged += (_, _) => { _revenuePage = 1; LoadData(); };
            Controls.Add(_chkPaidOnly);

            y += 50;

            // ── Dashboard action buttons ───────────────────────────────────────────
            _btnViewRevenue.Text      = "▼ View Revenue";
            _btnViewRevenue.Location  = new Point(14, y);
            _btnViewRevenue.Size      = new Size(160, 36);
            _btnViewRevenue.Font      = new Font("Segoe UI", 10F, FontStyle.Bold);
            _btnViewRevenue.FlatStyle = FlatStyle.Flat;
            _btnViewRevenue.Cursor    = Cursors.Hand;
            _btnViewRevenue.BackColor = Theme.Surface;
            _btnViewRevenue.ForeColor = Theme.Teal;
            _btnViewRevenue.FlatAppearance.BorderColor = Theme.Teal;
            _btnViewRevenue.Click += (_, _) =>
            {
                _pnlRevenue.Visible  = !_pnlRevenue.Visible;
                _pnlExpenses.Visible = false;
                _btnViewRevenue.Text  = _pnlRevenue.Visible  ? "▲ Revenue"      : "▼ View Revenue";
                _btnViewExpenses.Text = "▼ View Expenses";
            };
            Controls.Add(_btnViewRevenue);

            _btnViewExpenses.Text      = "▼ View Expenses";
            _btnViewExpenses.Location  = new Point(186, y);
            _btnViewExpenses.Size      = new Size(160, 36);
            _btnViewExpenses.Font      = new Font("Segoe UI", 10F, FontStyle.Bold);
            _btnViewExpenses.FlatStyle = FlatStyle.Flat;
            _btnViewExpenses.Cursor    = Cursors.Hand;
            _btnViewExpenses.BackColor = Theme.Surface;
            _btnViewExpenses.ForeColor = Theme.Danger;
            _btnViewExpenses.FlatAppearance.BorderColor = Theme.Danger;
            _btnViewExpenses.Click += (_, _) =>
            {
                _pnlExpenses.Visible = !_pnlExpenses.Visible;
                _pnlRevenue.Visible  = false;
                _btnViewExpenses.Text = _pnlExpenses.Visible ? "▲ Expenses"      : "▼ View Expenses";
                _btnViewRevenue.Text  = "▼ View Revenue";
            };
            Controls.Add(_btnViewExpenses);

            y += 48;

            // ── Revenue panel (hidden by default) ──────────────────────────────────
            _pnlRevenue.Location  = new Point(14, y);
            _pnlRevenue.Anchor    = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _pnlRevenue.Size      = new Size(ClientSize.Width - 28, ClientSize.Height - y - 30);
            _pnlRevenue.Visible   = false;

            _pnlRevenue.Controls.Add(new Label
            {
                Text      = "Revenue Orders  (double-click to view detail)",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(0, 0),
                AutoSize  = true
            });

            _dgvRevenue.Location             = new Point(0, 24);
            _dgvRevenue.Anchor               = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _dgvRevenue.Size                 = new Size(_pnlRevenue.Width, _pnlRevenue.Height - 122);
            _dgvRevenue.ReadOnly             = true;
            _dgvRevenue.AllowUserToAddRows   = false;
            _dgvRevenue.AllowUserToDeleteRows= false;
            _dgvRevenue.SelectionMode        = DataGridViewSelectionMode.FullRowSelect;
            _dgvRevenue.MultiSelect          = false;
            _dgvRevenue.AutoGenerateColumns  = false;
            _dgvRevenue.RowHeadersVisible    = false;
            _dgvRevenue.Columns.Add(new DataGridViewTextBoxColumn { Name = "rDate",     HeaderText = "Date",     Width = 100 });
            _dgvRevenue.Columns.Add(new DataGridViewTextBoxColumn { Name = "rOrder",    HeaderText = "Order #",  Width = 80  });
            _dgvRevenue.Columns.Add(new DataGridViewTextBoxColumn { Name = "rCustomer", HeaderText = "Customer", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _dgvRevenue.Columns.Add(new DataGridViewTextBoxColumn { Name = "rAmount",   HeaderText = "Amount",   Width = 100 });
            _dgvRevenue.Columns.Add(new DataGridViewTextBoxColumn { Name = "rStatus",   HeaderText = "Status",   Width = 80  });
            _dgvRevenue.CellDoubleClick  += DgvRevenue_CellDoubleClick;
            _dgvRevenue.CellMouseEnter   += DgvRevenue_CellMouseEnter;
            _dgvRevenue.CellMouseLeave   += DgvRevenue_CellMouseLeave;
            _dgvRevenue.SelectionChanged += DgvRevenue_SelectionChanged;
            _pnlRevenue.Controls.Add(_dgvRevenue);

            // Order detail inline panel (shown below the revenue grid on row select)
            _pnlOrderDetail.Location  = new Point(0, _dgvRevenue.Bottom + 6);
            _pnlOrderDetail.Anchor    = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _pnlOrderDetail.Size      = new Size(_pnlRevenue.Width, 52);
            _pnlOrderDetail.BackColor = Theme.Surface;
            _pnlOrderDetail.Visible   = false;

            _lblOrderDetail.Location  = new Point(8, 6);
            _lblOrderDetail.Size      = new Size(_pnlRevenue.Width - 16, 40);
            _lblOrderDetail.Font      = new Font("Segoe UI", 8.5F);
            _lblOrderDetail.ForeColor = Theme.TextSecondary;
            _pnlOrderDetail.Controls.Add(_lblOrderDetail);
            _pnlRevenue.Controls.Add(_pnlOrderDetail);

            // Pagination bar — docked to the bottom of the revenue panel
            _pnlRevenuePager.Dock      = DockStyle.Bottom;
            _pnlRevenuePager.Height    = 36;
            _pnlRevenuePager.BackColor = Theme.Surface;
            _pnlRevenue.Controls.Add(_pnlRevenuePager);

            Controls.Add(_pnlRevenue);

            // ── Expenses panel (hidden by default) ────────────────────────────────
            _pnlExpenses.Location  = new Point(14, y);
            _pnlExpenses.Anchor    = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _pnlExpenses.Size      = new Size(ClientSize.Width - 28, ClientSize.Height - y - 30);
            _pnlExpenses.Visible   = false;

            _pnlExpenses.Controls.Add(new Label
            {
                Text      = "Expense Transactions",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(0, 0),
                AutoSize  = true
            });

            _btnDeleteExp.Text     = "Delete Selected";
            _btnDeleteExp.Location = new Point(_pnlExpenses.Width - 134, 0);
            _btnDeleteExp.Size     = new Size(120, 24);
            _btnDeleteExp.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            _btnDeleteExp.UseVisualStyleBackColor = true;
            _btnDeleteExp.Click   += BtnDeleteExp_Click;
            _pnlExpenses.Controls.Add(_btnDeleteExp);

            _dgvExpenses.Location        = new Point(0, 28);
            _dgvExpenses.Anchor          = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _dgvExpenses.Size            = new Size(_pnlExpenses.Width, _pnlExpenses.Height - 28);
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
            _pnlExpenses.Controls.Add(_dgvExpenses);

            Controls.Add(_pnlExpenses);

            // ── Status bar ─────────────────────────────────────────────────────────
            _lblStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _lblStatus.Location = new Point(14, ClientSize.Height - 22);
            _lblStatus.AutoSize = true;
            Controls.Add(_lblStatus);

            SizeChanged += (_, _) =>
            {
                _lblStatus.Location = new Point(14, ClientSize.Height - 22);
                // Keep order detail panel anchored below revenue grid (inside _pnlRevenue)
                _pnlOrderDetail.Location = new Point(0, _dgvRevenue.Bottom + 6);
                _pnlOrderDetail.Width    = _pnlRevenue.Width;
                _lblOrderDetail.Width    = _pnlRevenue.Width - 16;
            };
        }

        // ── Data loading ──────────────────────────────────────────────────────────

        private void LoadData()
        {
            try
            {
                var from = _dtpFrom.Value.Date;
                var to   = _dtpTo.Value.Date.AddDays(1).AddTicks(-1);

                var summary     = _repo.GetSummary(from, to, _chkPaidOnly.Checked);
                var expenseRows = _repo.GetExpenseRows(from, to);

                // ── Revenue: prefer paged method; fall back to GetRevenueRows + client-side slice ──
                try
                {
                    (_revenueRows, _revenueTotalCount) = _repo.GetPagedRevenue(
                        _revenuePage, RevenuePageSize, from, to, _chkPaidOnly.Checked);
                }
                catch
                {
                    // Fallback: load all, slice client-side
                    var all = _repo.GetRevenueRows(from, to, _chkPaidOnly.Checked);
                    _revenueTotalCount = all.Count;
                    _revenueRows = all
                        .Skip((_revenuePage - 1) * RevenuePageSize)
                        .Take(RevenuePageSize)
                        .ToList();
                }

                // ── Revenue KPI tile ──────────────────────────────────────────────
                _lblRevenue.Text         = $"${summary.NetRevenue:N2}";
                _lblRevenue.ForeColor    = Theme.Teal;
                if (summary.CreditNotes > 0)
                {
                    _lblRevenueGross.Text   = $"Gross: ${summary.Revenue:N2}";
                    _lblRevenueReturns.Text = $"Returns: (${summary.CreditNotes:N2})";
                }
                else
                {
                    _lblRevenueGross.Text   = $"Gross: ${summary.Revenue:N2}";
                    _lblRevenueReturns.Text = "";
                }

                _lblCOGS.Text        = $"${summary.Cogs:N2}";
                _lblGrossProfit.Text = $"${summary.GrossProfit:N2}";
                _lblExpenses.Text    = $"${summary.Expenses:N2}";
                _lblNetProfit.Text   = $"${summary.NetProfit:N2}";
                _lblNetProfit.ForeColor = summary.NetProfit >= 0
                    ? Color.FromArgb(80, 210, 100)
                    : Color.FromArgb(210, 80, 80);

                // ── Revenue grid ──────────────────────────────────────────────────
                _dgvRevenue.Rows.Clear();
                _pnlOrderDetail.Visible = false;
                foreach (var r in _revenueRows)
                {
                    int idx  = _dgvRevenue.Rows.Add();
                    var row  = _dgvRevenue.Rows[idx];
                    row.Cells["rDate"].Value     = r.OrderDate.ToString("yyyy-MM-dd");
                    row.Cells["rOrder"].Value    = r.OrderNumber;
                    row.Cells["rCustomer"].Value = r.CustomerName;
                    row.Cells["rAmount"].Value   = $"${r.TotalPrice:N2}";
                    row.Cells["rStatus"].Value   = r.IsPaid ? "Paid" : r.Status;
                    if (r.IsPaid)
                        row.DefaultCellStyle.ForeColor = Color.FromArgb(80, 210, 100);
                    else if (r.Status == "Live" || r.Status == "WIP")
                        row.DefaultCellStyle.ForeColor = Color.FromArgb(220, 160, 60);
                }

                // ── Refresh pagination bar ────────────────────────────────────────
                _pnlRevenuePager.Controls.Clear();
                var pager = BuildPaginationBar(ref _revenuePage, _revenueTotalCount, RevenuePageSize, () => LoadData());
                pager.Dock = DockStyle.Fill;
                _pnlRevenuePager.Controls.Add(pager);

                // ── Expense grid ──────────────────────────────────────────────────
                _dgvExpenses.Rows.Clear();
                foreach (var r in expenseRows)
                {
                    int idx = _dgvExpenses.Rows.Add();
                    var row = _dgvExpenses.Rows[idx];
                    row.Cells["cDate"].Value   = r.ExpenseDate.ToString("yyyy-MM-dd");
                    row.Cells["cCat"].Value    = r.Category;
                    row.Cells["cAmount"].Value = $"${r.Amount:N2}";
                    row.Cells["cDesc"].Value   = r.Description ?? "";
                    row.Tag = r.ExpenseID;
                }

                _lblStatus.Text = $"Period: {from:yyyy-MM-dd} → {_dtpTo.Value:yyyy-MM-dd}  |  {_revenueTotalCount:N0} order(s)  |  {expenseRows.Count} expense(s)";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "Error: " + ex.Message;
            }
        }

        // ── Pagination helper ─────────────────────────────────────────────────────

        private Panel BuildPaginationBar(
            ref int currentPage, int totalCount, int pageSize,
            Action reload)
        {
            var panel   = new Panel { Height = 36, Dock = DockStyle.Bottom };
            var btnPrev = new Button { Text = "← Prev", Size = new Size(80, 28), Left = 8, Top = 4 };
            var lblPage = new Label  { AutoSize = true, Top = 10, Left = 96 };
            var btnNext = new Button { Text = "Next →", Size = new Size(80, 28), Left = 0, Top = 4 };

            int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            lblPage.Text     = $"Page {currentPage} of {totalPages}  ({totalCount:N0} records)";
            btnPrev.Enabled  = currentPage > 1;
            btnNext.Enabled  = currentPage < totalPages;
            btnNext.Left     = lblPage.PreferredWidth + 96 + 8;

            btnPrev.Click += (s, e) => { currentPage--; reload(); };
            btnNext.Click += (s, e) => { currentPage++; reload(); };

            panel.Controls.AddRange(new Control[] { btnPrev, lblPage, btnNext });
            return panel;
        }

        // ── Revenue grid actions ──────────────────────────────────────────────────

        private void DgvRevenue_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _revenueRows.Count) return;
            var rev = _revenueRows[e.RowIndex];
            ShowOrderDetailPanel(rev);

            // Show full detail in a dialog for easy reading
            string payTag   = rev.IsPaid ? "PAID" : "UNPAID";
            string msg =
                $"Order #:      {rev.OrderNumber}\n" +
                $"Customer:     {rev.CustomerName}\n" +
                $"Date:         {rev.OrderDate:yyyy-MM-dd}\n" +
                $"Amount:       ${rev.TotalPrice:N2}\n" +
                $"Status:       {rev.Status}\n" +
                $"Payment:      {payTag}";
            MessageBox.Show(this, msg, $"Order Detail — #{rev.OrderNumber}",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void DgvRevenue_SelectionChanged(object? sender, EventArgs e)
        {
            int idx = _dgvRevenue.CurrentCell?.RowIndex ?? -1;
            if (idx >= 0 && idx < _revenueRows.Count)
                ShowOrderDetailPanel(_revenueRows[idx]);
            else
                _pnlOrderDetail.Visible = false;
        }

        private void ShowOrderDetailPanel(RevenueRow rev)
        {
            string payTag  = rev.IsPaid ? "PAID" : "UNPAID";
            _lblOrderDetail.Text = $"Order #{rev.OrderNumber}   |   {rev.CustomerName}   |   {rev.OrderDate:yyyy-MM-dd}   |   ${rev.TotalPrice:N2}   |   {rev.Status}  [{payTag}]";
            _pnlOrderDetail.Visible = true;
        }

        private void DgvRevenue_CellMouseEnter(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
                _dgvRevenue.Cursor = Cursors.Hand;
        }

        private void DgvRevenue_CellMouseLeave(object? sender, DataGridViewCellEventArgs e)
        {
            _dgvRevenue.Cursor = Cursors.Default;
        }

        // ── COGS Detail button ────────────────────────────────────────────────────

        private void BtnCogsDetail_Click(object? sender, EventArgs e)
        {
            try
            {
                var from = _dtpFrom.Value.Date;
                var to   = _dtpTo.Value.Date.AddDays(1).AddTicks(-1);
                var bd   = _repo.GetCOGSBreakdown(from, to);

                string note = bd.IsPlaceholder
                    ? "\n\nNote: Sub-component columns (MaterialsCost, LaborCost, BatchLossCost)\nhave not yet been added to the WorkOrders table.\nAll COGS is shown as 'Other / Total' until those columns are populated."
                    : "";

                string msg =
                    $"COGS Breakdown  ({from:yyyy-MM-dd} → {_dtpTo.Value:yyyy-MM-dd})\n" +
                    $"─────────────────────────────────────\n" +
                    $"Materials:    ${bd.MaterialsCost,12:N2}\n" +
                    $"Labor:        ${bd.LaborCost,12:N2}\n" +
                    $"Batch Loss:   ${bd.BatchLossCost,12:N2}\n" +
                    $"Other/Total:  ${bd.OtherCost,12:N2}\n" +
                    $"─────────────────────────────────────\n" +
                    $"Total COGS:   ${bd.Total,12:N2}" +
                    note;

                MessageBox.Show(this, msg, "COGS Breakdown",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not load COGS breakdown: {ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        private readonly bool _bulkMode;
        private List<Models.ExpenseCategory> _categories = new();

        // Single-add controls
        private ComboBox       _cboCategory  = new();
        private NumericUpDown  _nudAmount    = new();
        private DateTimePicker _dtpDate      = new();
        private TextBox        _txtDesc      = new();
        private DataGridView   _dgvAdded     = new();
        private Label          _lblCount     = new();
        private Button         _btnAdd       = new();
        private Button         _btnDone      = new();
        private Button         _btnManageCat = new();

        // Bulk-mode controls
        private DataGridView _dgvBulk    = new();
        private Button       _btnAddRow  = new();
        private Button       _btnSaveAll = new();
        private DataGridViewComboBoxColumn _bulkCatCol = new();

        public FormAddExpense(IAccountingRepository repo, bool startInBulkMode = false)
        {
            _repo     = repo;
            _bulkMode = startInBulkMode;
            if (_bulkMode)
                BuildBulkUI();
            else
                BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
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
                if (!_bulkMode)
                {
                    _cboCategory.Items.Clear();
                    foreach (var cat in _categories)
                        _cboCategory.Items.Add(cat.Name);
                    if (_cboCategory.Items.Count > 0 && _cboCategory.SelectedIndex < 0)
                        _cboCategory.SelectedIndex = 0;
                }
                else
                {
                    _bulkCatCol.Items.Clear();
                    foreach (var cat in _categories)
                        _bulkCatCol.Items.Add(cat.Name);
                    string defaultCat = _categories.Count > 0 ? _categories[0].Name : "";
                    foreach (DataGridViewRow r in _dgvBulk.Rows)
                        if (r.Cells["cCat"].Value == null || r.Cells["cCat"].Value?.ToString() == "")
                            r.Cells["cCat"].Value = defaultCat;
                }
            }
            catch (Exception ex) { AppLogger.Info($"[FormAddExpense.LoadCategories]: {ex.Message}"); }
        }

        // ── Bulk UI ───────────────────────────────────────────────────────────────

        private void BuildBulkUI()
        {
            Text          = "Bulk Add Expenses";
            ClientSize    = new Size(700, 520);
            MinimumSize   = new Size(600, 420);
            StartPosition = FormStartPosition.CenterParent;

            int y = 12;
            Controls.Add(new Label
            {
                Text      = "Bulk Add Expenses",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(14, y),
                AutoSize  = true
            });
            Controls.Add(new Label
            {
                Text      = "Enter multiple expense rows below, then click Save All.",
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(14, y + 28),
                AutoSize  = true
            });
            y += 58;

            _bulkCatCol.Name         = "cCat";
            _bulkCatCol.HeaderText   = "Category";
            _bulkCatCol.Width        = 180;
            _bulkCatCol.DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton;

            _dgvBulk.Location             = new Point(14, y);
            _dgvBulk.Anchor               = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _dgvBulk.Size                 = new Size(668, 360);
            _dgvBulk.AllowUserToAddRows   = false;
            _dgvBulk.AllowUserToDeleteRows = false;
            _dgvBulk.AutoGenerateColumns  = false;
            _dgvBulk.RowHeadersVisible    = false;
            _dgvBulk.EditMode             = DataGridViewEditMode.EditOnEnter;
            _dgvBulk.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDate",   HeaderText = "Date (yyyy-MM-dd)", Width = 130 });
            _dgvBulk.Columns.Add(_bulkCatCol);
            _dgvBulk.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAmount", HeaderText = "Amount ($)",        Width = 110 });
            _dgvBulk.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDesc",   HeaderText = "Notes",             AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            Controls.Add(_dgvBulk);

            _btnAddRow.Text     = "+ Add Row";
            _btnAddRow.Size     = new Size(100, 28);
            _btnAddRow.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnAddRow.Location = new Point(14, ClientSize.Height - 38);
            _btnAddRow.UseVisualStyleBackColor = true;
            _btnAddRow.Click   += (_, _) => AddBulkRow();
            Controls.Add(_btnAddRow);

            _btnSaveAll.Text     = "Save All";
            _btnSaveAll.Size     = new Size(110, 28);
            _btnSaveAll.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnSaveAll.Location = new Point(ClientSize.Width - 230, ClientSize.Height - 38);
            _btnSaveAll.Font     = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _btnSaveAll.UseVisualStyleBackColor = true;
            _btnSaveAll.Click   += BtnSaveAll_Click;
            Controls.Add(_btnSaveAll);

            _btnDone.Text     = "Cancel";
            _btnDone.Size     = new Size(88, 28);
            _btnDone.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnDone.Location = new Point(ClientSize.Width - 106, ClientSize.Height - 38);
            _btnDone.UseVisualStyleBackColor = true;
            _btnDone.Click   += (_, _) => Close();
            Controls.Add(_btnDone);

            SizeChanged += (_, _) =>
            {
                _dgvBulk.Size        = new Size(ClientSize.Width - 28, ClientSize.Height - 110);
                _btnAddRow.Location  = new Point(14, ClientSize.Height - 38);
                _btnSaveAll.Location = new Point(ClientSize.Width - 230, ClientSize.Height - 38);
                _btnDone.Location    = new Point(ClientSize.Width - 106, ClientSize.Height - 38);
            };

            AddBulkRow(); // seed one empty row
        }

        private void AddBulkRow()
        {
            int idx = _dgvBulk.Rows.Add();
            _dgvBulk.Rows[idx].Cells["cDate"].Value = DateTime.Today.ToString("yyyy-MM-dd");
            if (_categories.Count > 0)
                _dgvBulk.Rows[idx].Cells["cCat"].Value = _categories[0].Name;
        }

        private void BtnSaveAll_Click(object? sender, EventArgs e)
        {
            _dgvBulk.CommitEdit(DataGridViewDataErrorContexts.Commit);
            _dgvBulk.EndEdit();

            var errors = new List<string>();
            var valid  = new List<(int catId, string catName, decimal amount, string? desc, DateTime date)>();

            foreach (DataGridViewRow row in _dgvBulk.Rows)
            {
                string dateStr = row.Cells["cDate"].Value?.ToString()?.Trim()   ?? "";
                string catName = row.Cells["cCat"].Value?.ToString()?.Trim()    ?? "";
                string amtStr  = row.Cells["cAmount"].Value?.ToString()?.Trim() ?? "";
                string desc    = row.Cells["cDesc"].Value?.ToString()?.Trim()   ?? "";

                if (string.IsNullOrEmpty(dateStr) && string.IsNullOrEmpty(catName) && string.IsNullOrEmpty(amtStr))
                    continue; // skip blank rows

                int rowNum = row.Index + 1;
                if (!DateTime.TryParse(dateStr, out var date))
                { errors.Add($"Row {rowNum}: invalid date '{dateStr}'."); continue; }

                var cat = _categories.FirstOrDefault(c => c.Name == catName);
                if (cat == null)
                { errors.Add($"Row {rowNum}: unknown category '{catName}'."); continue; }

                if (!decimal.TryParse(amtStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount <= 0)
                { errors.Add($"Row {rowNum}: amount must be a positive number."); continue; }

                valid.Add((cat.CategoryID, cat.Name, amount, string.IsNullOrEmpty(desc) ? null : desc, date.Date));
            }

            if (errors.Count > 0)
            {
                MessageBox.Show(this, "Please fix these errors before saving:\n\n" + string.Join("\n", errors),
                    "Validation Errors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (valid.Count == 0)
            {
                MessageBox.Show(this, "No expense rows to save.", "Nothing to save",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                foreach (var (catId, catName, amount, desc, date) in valid)
                {
                    _repo.AddExpense(catId, amount, desc, date, AppSession.CurrentUser?.Username);
                    AppLogger.Audit(AppSession.CurrentUser?.Username, "AddExpense",
                        $"Bulk: Category={catName} Amount={amount:N2} Date={date:yyyy-MM-dd}");
                }
                MessageBox.Show(this, $"{valid.Count} expense(s) saved successfully.",
                    "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _dgvBulk.Rows.Clear();
                AddBulkRow();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error saving expenses",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

            _dgv.Location        = new Point(12, 64);
            _dgv.Size            = new Size(374, 250);
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
                _dgv.Size = new Size(ClientSize.Width - 24, ClientSize.Height - 150);
                _txtNew.Location = new Point(110, ClientSize.Height - 74);
                _btnAdd.Location = new Point(ClientSize.Width - 100, ClientSize.Height - 76);
            };

            Theme.AddFormHeader(this, "🏷️  Expense Categories");
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
