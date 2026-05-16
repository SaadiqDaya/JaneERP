using System.Data;
using System.Text;
using JaneERP.Infrastructure;
using JaneERP.Interfaces;

namespace JaneERP
{
    /// <summary>
    /// Multi-tab reporting screen: Stock on Hand, Sales by Period, COGS Summary, Cycle Count Variance.
    /// </summary>
    public class FormReports : Form
    {
        private readonly IReportingRepository _repo = AppServices.Get<IReportingRepository>();

        // Tab control
        private TabControl tabMain = new();

        // Tab 1 – Stock on Hand
        private TabPage   tabStock     = new();
        private DataGridView dgvStock  = new();
        private Button    btnRefreshStock  = new();
        private Button    btnExportStock   = new();

        // Tab 2 – Sales by Period
        private TabPage   tabSales     = new();
        private DataGridView dgvSales  = new();
        private DateTimePicker dtpSalesFrom = new();
        private DateTimePicker dtpSalesTo   = new();
        private Button    btnRefreshSales   = new();
        private Button    btnExportSales    = new();
        private Label     lblSalesTotals    = new();

        // Tab 3 – COGS Summary
        private TabPage   tabCogs      = new();
        private DataGridView dgvCogs   = new();
        private Button    btnRefreshCogs    = new();
        private Button    btnExportCogs     = new();
        private Label     lblCogsTotals     = new();

        // Tab 4 – Cycle Count Variance
        private TabPage   tabCycle     = new();
        private DataGridView dgvCycle  = new();
        private DateTimePicker dtpCycleFrom = new();
        private DateTimePicker dtpCycleTo   = new();
        private Button    btnRefreshCycle   = new();
        private Button    btnExportCycle    = new();

        // Tab 5 – Gross Profit
        private TabPage      tabGP           = new();
        private DataGridView dgvGP           = new();
        private DateTimePicker dtpGPFrom     = new();
        private DateTimePicker dtpGPTo       = new();
        private Button       btnRefreshGP    = new();
        private Button       btnExportGP     = new();
        private Label        lblGPTotals     = new();

        public FormReports()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            // Load the first visible tab
            LoadStock();
        }

        // ── UI Construction ──────────────────────────────────────────────────────

        private void BuildUI()
        {
            Text            = "Reports";
            ClientSize      = new Size(1100, 700);
            MinimumSize     = new Size(900, 560);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;

            tabMain.Dock = DockStyle.Fill;
            tabMain.SelectedIndexChanged += (_, _) => RefreshCurrentTab();
            Controls.Add(tabMain);
            Theme.AddFormHeader(this, "📊  Reports");

            BuildStockTab();
            BuildSalesTab();
            BuildCogsTab();
            BuildCycleTab();
            BuildGPTab();

            tabMain.TabPages.Add(tabStock);
            tabMain.TabPages.Add(tabSales);
            tabMain.TabPages.Add(tabCogs);
            tabMain.TabPages.Add(tabCycle);
            tabMain.TabPages.Add(tabGP);
        }

        // ── Tab helpers ─────────────────────────────────────────────────────────

        private static DataGridView MakeGrid()
        {
            var dgv = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                ReadOnly              = true,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible     = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
            };
            return dgv;
        }

        private static Panel MakeToolbar(params Control[] controls)
        {
            var pnl = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 8, 8, 4) };
            int x = 8;
            foreach (var c in controls)
            {
                c.Location = new Point(x, 8);
                pnl.Controls.Add(c);
                x += c.Width + 8;
            }
            return pnl;
        }

        private static Button MakeBtn(string text, int width = 100)
        {
            return new Button { Text = text, Size = new Size(width, 28), UseVisualStyleBackColor = true };
        }

        // ── Tab 1: Stock on Hand ─────────────────────────────────────────────────

        private void BuildStockTab()
        {
            tabStock.Text = "Stock on Hand";

            dgvStock = MakeGrid();

            btnRefreshStock = MakeBtn("Refresh");
            btnExportStock  = MakeBtn("Export CSV");
            var btnPrintStock = MakeBtn("Print / PDF", 110);
            btnRefreshStock.Click += (_, _) => LoadStock();
            btnExportStock.Click  += (_, _) => ExportCsv(dgvStock, "StockOnHand.csv");
            btnPrintStock.Click   += (_, _) => PrintGrid(dgvStock, "Stock on Hand", this);

            var toolbar = MakeToolbar(btnRefreshStock, btnExportStock, btnPrintStock);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(dgvStock, 0, 1);

            tabStock.Controls.Add(layout);
        }

        private void LoadStock()
        {
            try
            {
                var data = _repo.GetStockOnHand().ToList();

                BindGrid(dgvStock, data, new[]
                {
                    ("LocationName",   "Location",         80),
                    ("SKU",            "SKU",               90),
                    ("ProductName",    "Product",          200),
                    ("StockQty",       "Stock Qty",         80),
                    ("RetailValue",    "Retail Value",     120),
                    ("WholesaleValue", "Wholesale Value",  120),
                });

                FormatDecimalColumns(dgvStock, "RetailValue", "WholesaleValue");
            }
            catch (Exception ex)
            {
                ShowError("Stock on Hand", ex);
            }
        }

        // ── Tab 2: Sales by Period ────────────────────────────────────────────────

        private void BuildSalesTab()
        {
            tabSales.Text = "Sales by Period";

            dgvSales = MakeGrid();

            dtpSalesFrom = new DateTimePicker { Size = new Size(130, 28), Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(-30) };
            dtpSalesTo   = new DateTimePicker { Size = new Size(130, 28), Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(1) };

            var lblFrom = new Label { Text = "From:", AutoSize = true, Location = new Point(8, 14) };
            var lblTo   = new Label { Text = "To:",   AutoSize = true };

            btnRefreshSales = MakeBtn("Refresh");
            btnExportSales  = MakeBtn("Export CSV");
            var btnPrintSales = MakeBtn("Print / PDF", 110);
            btnRefreshSales.Click += (_, _) => LoadSales();
            btnExportSales.Click  += (_, _) => ExportCsv(dgvSales, "SalesByPeriod.csv");
            btnPrintSales.Click   += (_, _) => PrintGrid(dgvSales, "Sales by Period", this);

            lblSalesTotals = new Label { Text = "", AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(8, 4, 8, 4) };

            var toolbar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 8, 8, 4) };
            int x = 8;
            void Add(Control c) { c.Location = new Point(x, 8); toolbar.Controls.Add(c); x += c.Width + 6; }
            Add(lblFrom);
            lblFrom.Location = new Point(x - lblFrom.Width - 6, 14);
            // Redo positioning manually for labels
            toolbar.Controls.Clear();
            x = 8;
            var lf = new Label { Text = "From:", AutoSize = true }; lf.Location = new Point(x, 14); toolbar.Controls.Add(lf); x += 44;
            dtpSalesFrom.Location = new Point(x, 8); toolbar.Controls.Add(dtpSalesFrom); x += 138;
            var lt = new Label { Text = "To:", AutoSize = true }; lt.Location = new Point(x, 14); toolbar.Controls.Add(lt); x += 28;
            dtpSalesTo.Location = new Point(x, 8); toolbar.Controls.Add(dtpSalesTo); x += 138;
            btnRefreshSales.Location = new Point(x, 8); toolbar.Controls.Add(btnRefreshSales); x += 108;
            btnExportSales.Location  = new Point(x, 8); toolbar.Controls.Add(btnExportSales);  x += 108;
            btnPrintSales.Location   = new Point(x, 8); toolbar.Controls.Add(btnPrintSales);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.Controls.Add(toolbar,        0, 0);
            layout.Controls.Add(dgvSales,       0, 1);
            layout.Controls.Add(lblSalesTotals, 0, 2);

            tabSales.Controls.Add(layout);
        }

        private void LoadSales()
        {
            try
            {
                var from = dtpSalesFrom.Value.Date;
                var to   = dtpSalesTo.Value.Date.AddDays(1);
                var data = _repo.GetSalesByPeriod(from, to).ToList();

                BindGrid(dgvSales, data, new[]
                {
                    ("Order#",   "Order #",  80),
                    ("Date",     "Date",    120),
                    ("Customer", "Customer",180),
                    ("Store",    "Store",   130),
                    ("Total",    "Total",    90),
                    ("Currency", "Currency", 70),
                    ("Status",   "Status",   80),
                    ("Type",     "Type",     90),
                });

                FormatDecimalColumns(dgvSales, "Total");
                FormatDateColumns(dgvSales, "Date");

                int count = data.Count;
                decimal sum = data.Sum(r => (decimal)(r.Total ?? 0m));
                lblSalesTotals.Text = $"Orders: {count}   |   Total Revenue: {sum:N2}";
            }
            catch (Exception ex)
            {
                ShowError("Sales by Period", ex);
            }
        }

        // ── Tab 3: COGS Summary ───────────────────────────────────────────────────

        private void BuildCogsTab()
        {
            tabCogs.Text = "COGS Summary";

            dgvCogs = MakeGrid();

            btnRefreshCogs = MakeBtn("Refresh");
            btnExportCogs  = MakeBtn("Export CSV");
            var btnPrintCogs = MakeBtn("Print / PDF", 110);
            btnRefreshCogs.Click += (_, _) => LoadCogs();
            btnExportCogs.Click  += (_, _) => ExportCsv(dgvCogs, "COGSSummary.csv");
            btnPrintCogs.Click   += (_, _) => PrintGrid(dgvCogs, "COGS Summary", this);

            lblCogsTotals = new Label { Text = "", AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(8, 4, 8, 4) };

            var toolbar = MakeToolbar(btnRefreshCogs, btnExportCogs, btnPrintCogs);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.Controls.Add(toolbar,       0, 0);
            layout.Controls.Add(dgvCogs,       0, 1);
            layout.Controls.Add(lblCogsTotals, 0, 2);

            tabCogs.Controls.Add(layout);
        }

        private void LoadCogs()
        {
            try
            {
                var data = _repo.GetCogsSummary().ToList();

                BindGrid(dgvCogs, data, new[]
                {
                    ("WorkOrderID",  "Work Order #",  100),
                    ("SKU",          "SKU",            90),
                    ("ProductName",  "Product",       200),
                    ("Quantity",     "Qty Produced",   90),
                    ("CostOfGoods",  "Parts Cost",    120),
                    ("CompletedAt",  "Date Completed",140),
                });

                FormatDecimalColumns(dgvCogs, "CostOfGoods");
                FormatDateColumns(dgvCogs, "CompletedAt");

                decimal total = data.Sum(r => (decimal)(r.CostOfGoods ?? 0m));
                lblCogsTotals.Text = $"Total COGS: {total:N2}";
            }
            catch (Exception ex)
            {
                ShowError("COGS Summary", ex);
            }
        }

        // ── Tab 4: Cycle Count Variance ───────────────────────────────────────────

        private void BuildCycleTab()
        {
            tabCycle.Text = "Cycle Count Variance";

            dgvCycle = MakeGrid();

            dtpCycleFrom = new DateTimePicker { Size = new Size(130, 28), Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(-90) };
            dtpCycleTo   = new DateTimePicker { Size = new Size(130, 28), Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(1) };

            btnRefreshCycle = MakeBtn("Refresh");
            btnExportCycle  = MakeBtn("Export CSV");
            var btnPrintCycle = MakeBtn("Print / PDF", 110);
            btnRefreshCycle.Click += (_, _) => LoadCycle();
            btnExportCycle.Click  += (_, _) => ExportCsv(dgvCycle, "CycleCountVariance.csv");
            btnPrintCycle.Click   += (_, _) => PrintGrid(dgvCycle, "Cycle Count Variance", this);

            var toolbar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 8, 8, 4) };
            int x = 8;
            var lf = new Label { Text = "From:", AutoSize = true }; lf.Location = new Point(x, 14); toolbar.Controls.Add(lf); x += 44;
            dtpCycleFrom.Location = new Point(x, 8); toolbar.Controls.Add(dtpCycleFrom); x += 138;
            var lt = new Label { Text = "To:", AutoSize = true }; lt.Location = new Point(x, 14); toolbar.Controls.Add(lt); x += 28;
            dtpCycleTo.Location = new Point(x, 8); toolbar.Controls.Add(dtpCycleTo); x += 138;
            btnRefreshCycle.Location = new Point(x, 8); toolbar.Controls.Add(btnRefreshCycle); x += 108;
            btnExportCycle.Location  = new Point(x, 8); toolbar.Controls.Add(btnExportCycle);  x += 108;
            btnPrintCycle.Location   = new Point(x, 8); toolbar.Controls.Add(btnPrintCycle);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(toolbar,  0, 0);
            layout.Controls.Add(dgvCycle, 0, 1);

            tabCycle.Controls.Add(layout);
        }

        private void LoadCycle()
        {
            try
            {
                var from = dtpCycleFrom.Value.Date;
                var to   = dtpCycleTo.Value.Date.AddDays(1);
                var data = _repo.GetCycleCountVariance(from, to).ToList();

                BindGrid(dgvCycle, data, new[]
                {
                    ("SKU",           "SKU",            90),
                    ("ProductName",   "Product",       200),
                    ("LocationName",  "Location",      120),
                    ("ExpectedQty",   "Expected Qty",   90),
                    ("CountedQty",    "Counted Qty",    90),
                    ("AdjustmentQty", "Variance",       80),
                    ("DateCounted",   "Date Counted",  140),
                    ("VerifiedBy",    "Verified By",   120),
                });

                FormatDateColumns(dgvCycle, "DateCounted");
            }
            catch (Exception ex)
            {
                ShowError("Cycle Count Variance", ex);
            }
        }

        // ── Tab 5: Gross Profit ───────────────────────────────────────────────────

        private void BuildGPTab()
        {
            tabGP.Text = "Gross Profit";

            dgvGP = MakeGrid();

            dtpGPFrom = new DateTimePicker { Size = new Size(130, 28), Format = DateTimePickerFormat.Short, Value = new DateTime(DateTime.Today.Year, 1, 1) };
            dtpGPTo   = new DateTimePicker { Size = new Size(130, 28), Format = DateTimePickerFormat.Short, Value = DateTime.Today };

            btnRefreshGP = MakeBtn("Refresh");
            btnExportGP  = MakeBtn("Export CSV");
            var btnPrintGP = MakeBtn("Print / PDF", 110);
            btnRefreshGP.Click += (_, _) => LoadGP();
            btnExportGP.Click  += (_, _) => ExportCsv(dgvGP, "GrossProfit.csv");
            btnPrintGP.Click   += (_, _) => PrintGrid(dgvGP, "Gross Profit", this);

            lblGPTotals = new Label { Text = "", AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(8, 4, 8, 4) };

            var toolbar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 8, 8, 4) };
            int x = 8;
            var lf = new Label { Text = "From:", AutoSize = true }; lf.Location = new Point(x, 14); toolbar.Controls.Add(lf); x += 44;
            dtpGPFrom.Location = new Point(x, 8); toolbar.Controls.Add(dtpGPFrom); x += 138;
            var lt = new Label { Text = "To:", AutoSize = true }; lt.Location = new Point(x, 14); toolbar.Controls.Add(lt); x += 28;
            dtpGPTo.Location = new Point(x, 8); toolbar.Controls.Add(dtpGPTo); x += 138;
            btnRefreshGP.Location = new Point(x, 8); toolbar.Controls.Add(btnRefreshGP); x += 108;
            btnExportGP.Location  = new Point(x, 8); toolbar.Controls.Add(btnExportGP);  x += 108;
            btnPrintGP.Location   = new Point(x, 8); toolbar.Controls.Add(btnPrintGP);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.Controls.Add(toolbar,    0, 0);
            layout.Controls.Add(dgvGP,      0, 1);
            layout.Controls.Add(lblGPTotals, 0, 2);

            tabGP.Controls.Add(layout);
        }

        private void LoadGP()
        {
            try
            {
                var from = dtpGPFrom.Value.Date;
                var to   = dtpGPTo.Value.Date.AddDays(1);
                var data = _repo.GetGrossProfitByProduct(from, to).ToList();

                if (data.Count == 0)
                {
                    int anyItems = _repo.GetSalesOrderItemCount();
                    if (anyItems == 0)
                        lblGPTotals.Text = "No line-item data found. Use 'Sync to ERP' in the Sales screen to populate order details.";
                    else
                        lblGPTotals.Text = $"No orders found between {dtpGPFrom.Value:yyyy-MM-dd} and {dtpGPTo.Value:yyyy-MM-dd}. Try widening the date range.";
                }

                BindGrid(dgvGP, data, new[]
                {
                    ("SKU",         "SKU",          90),
                    ("Product",     "Product",      200),
                    ("UnitsSold",   "Units Sold",    80),
                    ("Revenue",     "Revenue",      110),
                    ("COGS",        "COGS",         110),
                    ("GrossProfit", "Gross Profit", 120),
                });

                FormatDecimalColumns(dgvGP, "Revenue", "COGS", "GrossProfit");

                if (data.Count > 0)
                {
                    decimal totalRev  = data.Sum(r => (decimal)(r.Revenue     ?? 0m));
                    decimal totalCogs = data.Sum(r => (decimal)(r.COGS        ?? 0m));
                    decimal totalGP   = data.Sum(r => (decimal)(r.GrossProfit ?? 0m));
                    decimal margin    = totalRev > 0 ? totalGP / totalRev * 100m : 0m;
                    lblGPTotals.Text  = $"Revenue: {totalRev:N2}   |   COGS: {totalCogs:N2}   |   Gross Profit: {totalGP:N2}   |   Margin: {margin:N1}%";
                }
            }
            catch (Exception ex)
            {
                ShowError("Gross Profit", ex);
            }
        }

        // ── Refresh dispatcher ────────────────────────────────────────────────────

        private void RefreshCurrentTab()
        {
            switch (tabMain.SelectedIndex)
            {
                case 0: LoadStock(); break;
                case 1: LoadSales(); break;
                case 2: LoadCogs();  break;
                case 3: LoadCycle(); break;
                case 4: LoadGP();    break;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static void BindGrid(DataGridView dgv, IEnumerable<dynamic> data,
            (string prop, string header, int width)[] columns)
        {
            dgv.Columns.Clear();
            foreach (var (prop, header, width) in columns)
            {
                dgv.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name             = prop,
                    HeaderText       = header,
                    DataPropertyName = prop,
                    Width            = width,
                    AutoSizeMode     = DataGridViewAutoSizeColumnMode.None,
                    FillWeight       = 1
                });
            }
            // Last column fills remaining space
            if (dgv.Columns.Count > 0)
                dgv.Columns[dgv.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            var table = new DataTable();
            foreach (var (prop, _, _) in columns)
                table.Columns.Add(prop);

            foreach (IDictionary<string, object> row in data)
            {
                var dr = table.NewRow();
                foreach (var (prop, _, _) in columns)
                    dr[prop] = row.TryGetValue(prop, out var v) ? (v ?? DBNull.Value) : DBNull.Value;
                table.Rows.Add(dr);
            }
            dgv.DataSource = table;
        }

        private static void FormatDecimalColumns(DataGridView dgv, params string[] cols)
        {
            foreach (var col in cols)
                if (dgv.Columns.Contains(col))
                    dgv.Columns[col].DefaultCellStyle.Format = "N2";
        }

        private static void FormatDateColumns(DataGridView dgv, params string[] cols)
        {
            foreach (var col in cols)
                if (dgv.Columns.Contains(col))
                    dgv.Columns[col].DefaultCellStyle.Format = "yyyy-MM-dd";
        }

        private void ExportCsv(DataGridView dgv, string defaultFileName)
        {
            using var dlg = new SaveFileDialog
            {
                Filter   = "CSV files (*.csv)|*.csv",
                FileName = defaultFileName
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var sb = new StringBuilder();
                // Header row
                var headers = dgv.Columns.Cast<DataGridViewColumn>()
                    .Select(c => $"\"{c.HeaderText}\"");
                sb.AppendLine(string.Join(",", headers));

                // Data rows
                foreach (DataGridViewRow row in dgv.Rows)
                {
                    if (row.IsNewRow) continue;
                    var cells = row.Cells.Cast<DataGridViewCell>()
                        .Select(c => $"\"{(c.Value?.ToString() ?? "").Replace("\"", "\"\"")}\"");
                    sb.AppendLine(string.Join(",", cells));
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show(this, $"Exported {dgv.Rows.Count} row(s) to:\n{dlg.FileName}",
                    "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Export failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowError(string context, Exception ex)
        {
            MessageBox.Show(this, $"Could not load {context}:\n{ex.Message}", "Report Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ── Print / PDF ───────────────────────────────────────────────────────────

        /// <summary>
        /// Shows a print-preview of <paramref name="dgv"/> using GDI+ drawing.
        /// Users can send to any printer including "Microsoft Print to PDF".
        /// </summary>
        internal static void PrintGrid(DataGridView dgv, string reportTitle, Form owner)
        {
            if (dgv.Rows.Cast<DataGridViewRow>().All(r => r.IsNewRow))
            {
                MessageBox.Show(owner, "No data to print.", "Empty",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Snapshot visible columns and all formatted cell values before handing off to the print thread
            var cols = dgv.Columns.Cast<DataGridViewColumn>()
                          .Where(c => c.Visible)
                          .ToList();
            var headers = cols.Select(c => c.HeaderText).ToArray();
            var rows = dgv.Rows.Cast<DataGridViewRow>()
                          .Where(r => !r.IsNewRow)
                          .Select(r => cols.Select(c => r.Cells[c.Index].FormattedValue?.ToString() ?? "").ToArray())
                          .ToArray();

            int currentRow = 0;
            const int marginPx   = 50;
            const int rowHeightPx = 20;
            const int hdrHeightPx = 24;

            var pd = new System.Drawing.Printing.PrintDocument { DocumentName = reportTitle };
            pd.PrintPage += (_, e) =>
            {
                if (e.Graphics == null) return;
                var g         = e.Graphics;
                int pageW     = e.PageBounds.Width  - marginPx * 2;
                int pageH     = e.PageBounds.Height - marginPx * 2;
                int colW      = headers.Length > 0 ? pageW / headers.Length : pageW;
                int y         = marginPx;

                // Title + timestamp on every page
                using var titleFont = new Font("Segoe UI", 11F, FontStyle.Bold);
                g.DrawString(reportTitle, titleFont, Brushes.Black, marginPx, y);
                y += 22;
                using var smallFont = new Font("Segoe UI", 8F);
                g.DrawString($"Printed {DateTime.Now:yyyy-MM-dd HH:mm}   |   Page {pd.PrintController?.ToString() ?? ""}",
                    smallFont, Brushes.Gray, marginPx, y);
                y += 18;
                g.DrawLine(Pens.DarkGray, marginPx, y, marginPx + pageW, y);
                y += 4;

                // Column headers
                using var hdrFont = new Font("Segoe UI", 9F, FontStyle.Bold);
                g.FillRectangle(Brushes.LightSteelBlue, marginPx, y, pageW, hdrHeightPx);
                for (int ci = 0; ci < headers.Length; ci++)
                {
                    var rect = new RectangleF(marginPx + ci * colW + 2, y + 4, colW - 4, hdrHeightPx - 4);
                    g.DrawString(headers[ci], hdrFont, Brushes.Black, rect,
                        new StringFormat { Trimming = StringTrimming.EllipsisCharacter });
                }
                y += hdrHeightPx;

                // Data rows — fill until bottom margin
                using var cellFont  = new Font("Segoe UI", 8.5F);
                using var altBrush  = new SolidBrush(Color.FromArgb(240, 244, 250));
                int startRow = currentRow;
                while (currentRow < rows.Length && y + rowHeightPx <= marginPx + pageH)
                {
                    int rowIdx = currentRow - startRow;
                    if (rowIdx % 2 == 1) g.FillRectangle(altBrush, marginPx, y, pageW, rowHeightPx);
                    for (int ci = 0; ci < headers.Length; ci++)
                    {
                        var rect = new RectangleF(marginPx + ci * colW + 2, y + 2, colW - 4, rowHeightPx - 2);
                        g.DrawString(rows[currentRow][ci], cellFont, Brushes.Black, rect,
                            new StringFormat { Trimming = StringTrimming.EllipsisCharacter });
                    }
                    y += rowHeightPx;
                    currentRow++;
                }

                g.DrawLine(Pens.LightGray, marginPx, marginPx + pageH, marginPx + pageW, marginPx + pageH);
                e.HasMorePages = currentRow < rows.Length;
            };

            using var preview = new PrintPreviewDialog
            {
                Document      = pd,
                Width         = 1000,
                Height        = 750,
                Text          = $"Print Preview — {reportTitle}",
                StartPosition = FormStartPosition.CenterParent
            };
            preview.ShowDialog(owner);
        }
    }
}
