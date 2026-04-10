using System.Configuration;
using System.Data;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;

namespace JaneERP
{
    /// <summary>
    /// Multi-tab reporting screen: Stock on Hand, Sales by Period, COGS Summary, Cycle Count Variance.
    /// </summary>
    public class FormReports : Form
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

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

            BuildStockTab();
            BuildSalesTab();
            BuildCogsTab();
            BuildCycleTab();

            tabMain.TabPages.Add(tabStock);
            tabMain.TabPages.Add(tabSales);
            tabMain.TabPages.Add(tabCogs);
            tabMain.TabPages.Add(tabCycle);
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
            btnRefreshStock.Click += (_, _) => LoadStock();
            btnExportStock.Click  += (_, _) => ExportCsv(dgvStock, "StockOnHand.csv");

            var toolbar = MakeToolbar(btnRefreshStock, btnExportStock);

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
                using IDbConnection db = new SqlConnection(_connectionString);
                var data = db.Query(@"
                    SELECT l.LocationName, p.SKU, p.ProductName,
                           SUM(t.QuantityChange) AS StockQty,
                           SUM(t.QuantityChange) * p.RetailPrice    AS RetailValue,
                           SUM(t.QuantityChange) * p.WholesalePrice AS WholesaleValue
                    FROM InventoryTransactions t
                    JOIN Products p   ON p.ProductID   = t.ProductID
                    LEFT JOIN Locations l ON l.LocationID = t.LocationID
                    GROUP BY l.LocationName, p.SKU, p.ProductName, p.RetailPrice, p.WholesalePrice
                    HAVING SUM(t.QuantityChange) > 0
                    ORDER BY l.LocationName, p.SKU").ToList();

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
            btnRefreshSales.Click += (_, _) => LoadSales();
            btnExportSales.Click  += (_, _) => ExportCsv(dgvSales, "SalesByPeriod.csv");

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
            btnExportSales.Location  = new Point(x, 8); toolbar.Controls.Add(btnExportSales);

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
                using IDbConnection db = new SqlConnection(_connectionString);
                var from = dtpSalesFrom.Value.Date;
                var to   = dtpSalesTo.Value.Date.AddDays(1);
                var data = db.Query(@"
                    SELECT so.OrderNumber AS [Order#],
                           so.OrderDate  AS [Date],
                           c.FullName    AS Customer,
                           ISNULL(st.StoreName, so.OrderType) AS Store,
                           so.TotalPrice AS Total,
                           so.Currency,
                           so.Status,
                           so.OrderType  AS [Type]
                    FROM SalesOrders so
                    JOIN Customers c   ON c.CustomerID = so.CustomerID
                    LEFT JOIN Stores st ON st.StoreID  = so.StoreID
                    WHERE so.OrderDate >= @from AND so.OrderDate < @to
                    ORDER BY so.OrderDate DESC",
                    new { from, to }).ToList();

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
            btnRefreshCogs.Click += (_, _) => LoadCogs();
            btnExportCogs.Click  += (_, _) => ExportCsv(dgvCogs, "COGSSummary.csv");

            lblCogsTotals = new Label { Text = "", AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(8, 4, 8, 4) };

            var toolbar = MakeToolbar(btnRefreshCogs, btnExportCogs);

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
                using IDbConnection db = new SqlConnection(_connectionString);
                var data = db.Query(@"
                    SELECT wo.WorkOrderID, p.SKU, p.ProductName,
                           wo.Quantity,
                           ISNULL(wo.CostOfGoods, 0) AS CostOfGoods,
                           wo.CompletedAt
                    FROM WorkOrders wo
                    JOIN Products p ON p.ProductID = wo.ProductID
                    WHERE wo.Status = 'Complete' AND wo.CostOfGoods IS NOT NULL
                    ORDER BY wo.CompletedAt DESC").ToList();

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
            btnRefreshCycle.Click += (_, _) => LoadCycle();
            btnExportCycle.Click  += (_, _) => ExportCsv(dgvCycle, "CycleCountVariance.csv");

            var toolbar = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 8, 8, 4) };
            int x = 8;
            var lf = new Label { Text = "From:", AutoSize = true }; lf.Location = new Point(x, 14); toolbar.Controls.Add(lf); x += 44;
            dtpCycleFrom.Location = new Point(x, 8); toolbar.Controls.Add(dtpCycleFrom); x += 138;
            var lt = new Label { Text = "To:", AutoSize = true }; lt.Location = new Point(x, 14); toolbar.Controls.Add(lt); x += 28;
            dtpCycleTo.Location = new Point(x, 8); toolbar.Controls.Add(dtpCycleTo); x += 138;
            btnRefreshCycle.Location = new Point(x, 8); toolbar.Controls.Add(btnRefreshCycle); x += 108;
            btnExportCycle.Location  = new Point(x, 8); toolbar.Controls.Add(btnExportCycle);

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
                using IDbConnection db = new SqlConnection(_connectionString);
                var from = dtpCycleFrom.Value.Date;
                var to   = dtpCycleTo.Value.Date.AddDays(1);

                // Show verified cycle counts within the date range.
                // The "expected" (pre-count system qty) is reconstructed as: actual_qty - adjustment_qty
                // The cycle count adjustment transactions are recorded with TransactionType = 'Cycle Count'.
                // For each product, take the most recent verified cycle count in the range
                // and show: product, location, expected qty (before adjustment), counted qty, variance, date, verified by.
                var data = db.Query(@"
                    SELECT p.SKU,
                           p.ProductName,
                           l.LocationName,
                           -- The adjustment is (actual - expected), so expected = actual - adj
                           it.QuantityChange                      AS AdjustmentQty,
                           (p.LastVerifiedAt)                     AS DateCounted,
                           p.LastVerifiedBy                       AS VerifiedBy,
                           -- Reconstruct: system qty before adjustment and counted qty
                           -- SystemQtyBefore = current_system_qty - adjustment
                           ISNULL((SELECT SUM(t2.QuantityChange)
                                   FROM InventoryTransactions t2
                                   WHERE t2.ProductID = p.ProductID
                                     AND t2.TransactionID <= it.TransactionID), 0)
                               - it.QuantityChange                AS ExpectedQty,
                           ISNULL((SELECT SUM(t2.QuantityChange)
                                   FROM InventoryTransactions t2
                                   WHERE t2.ProductID = p.ProductID
                                     AND t2.TransactionID <= it.TransactionID), 0) AS CountedQty
                    FROM   Products p
                    JOIN   InventoryTransactions it ON it.ProductID = p.ProductID
                                                   AND it.TransactionType = 'Cycle Count'
                                                   AND it.TransactionDate >= @from
                                                   AND it.TransactionDate <  @to
                    LEFT JOIN Locations l ON l.LocationID = it.LocationID
                    WHERE  p.IsActive = 1
                      AND  p.LastVerifiedAt IS NOT NULL
                    ORDER  BY it.TransactionDate DESC, p.ProductName",
                    new { from, to }).ToList();

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

        // ── Refresh dispatcher ────────────────────────────────────────────────────

        private void RefreshCurrentTab()
        {
            switch (tabMain.SelectedIndex)
            {
                case 0: LoadStock(); break;
                case 1: LoadSales(); break;
                case 2: LoadCogs();  break;
                case 3: LoadCycle(); break;
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
    }
}
