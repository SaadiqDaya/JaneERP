using System.Configuration;
using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace JaneERP
{
    /// <summary>
    /// Read-only KPI dashboard showing key business metrics at a glance.
    /// </summary>
    public class FormKPIDashboard : Form
    {
        private readonly string _cs =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        // Tile labels (large number + description)
        private Label lblOrdersTodayVal     = new();
        private Label lblRevenueTodayVal    = new();
        private Label lblPendingOrdersVal   = new();
        private Label lblProductsInStockVal = new();
        private Label lblOutOfStockVal      = new();
        private Label lblLowStockVal        = new();
        private Label lblOpenWorkOrdersVal  = new();
        private Label lblTasksOverdueVal    = new();
        private Label lblInventoryValueVal  = new();

        // Panel backgrounds for conditional colouring
        private Panel pnlOutOfStock  = new();
        private Panel pnlLowStock    = new();
        private Panel pnlTaskOverdue = new();

        private Label  lblStatus  = new();
        private Button btnRefresh = new();

        // ── Tile colour themes ────────────────────────────────────────────────────
        private static readonly Color TileBase   = Color.FromArgb(22, 40, 60);
        private static readonly Color TileBlue   = Color.FromArgb(20, 40, 70);
        private static readonly Color TileGreen  = Color.FromArgb(18, 52, 36);
        private static readonly Color TilePurple = Color.FromArgb(38, 28, 66);
        private static readonly Color TileRed    = Color.FromArgb(70, 18, 18);
        private static readonly Color TileAmber  = Color.FromArgb(68, 44, 10);

        public FormKPIDashboard()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
        }

        private void BuildUI()
        {
            Text          = "KPI Dashboard";
            ClientSize    = new Size(900, 620);
            MinimumSize   = new Size(900, 620);
            StartPosition = FormStartPosition.CenterParent;

            // ── Header bar ───────────────────────────────────────────────────────
            var pnlHeader = new Panel
            {
                Tag      = "header",
                Dock     = DockStyle.Top,
                Height   = 52,
                BackColor = Color.FromArgb(8, 16, 28)
            };

            var lblTitle = new Label
            {
                Text      = "KPI Dashboard",
                Font      = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                AutoSize  = false,
                Size      = new Size(400, 36),
                Location  = new Point(14, 8),
                BackColor = Color.Transparent
            };

            btnRefresh = new Button
            {
                Text     = "Refresh",
                Size     = new Size(90, 30),
                Location = new Point(780, 11),
                Font     = new Font("Segoe UI", 9F)
            };
            btnRefresh.Click += (_, _) => LoadKPIs();
            Theme.StyleButton(btnRefresh);

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(btnRefresh);
            Controls.Add(pnlHeader);
            Theme.MakeDraggable(this, pnlHeader);

            // ── TableLayoutPanel 3x3 ─────────────────────────────────────────────
            var table = new TableLayoutPanel
            {
                Location    = new Point(14, 62),
                Size        = new Size(872, 510),
                ColumnCount = 3,
                RowCount    = 3,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34F));

            // Row 1 — blue theme
            table.Controls.Add(MakeTile("Orders Today",   TileBlue,   ref lblOrdersTodayVal),    0, 0);
            table.Controls.Add(MakeTile("Revenue Today",  TileBlue,   ref lblRevenueTodayVal),   1, 0);
            table.Controls.Add(MakeTile("Pending Orders", TileBlue,   ref lblPendingOrdersVal),  2, 0);

            // Row 2 — green / amber theme (panels stored for conditional recolouring)
            table.Controls.Add(MakeTile("Products In Stock", TileGreen,  ref lblProductsInStockVal), 0, 1);
            pnlOutOfStock = MakeTile("Out of Stock",      TileGreen,  ref lblOutOfStockVal);
            table.Controls.Add(pnlOutOfStock, 1, 1);
            pnlLowStock   = MakeTile("Low Stock",         TileAmber,  ref lblLowStockVal);
            table.Controls.Add(pnlLowStock, 2, 1);

            // Row 3 — purple / grey theme
            table.Controls.Add(MakeTile("Open Work Orders",  TilePurple, ref lblOpenWorkOrdersVal),  0, 2);
            pnlTaskOverdue = MakeTile("Tasks Overdue",    TilePurple, ref lblTasksOverdueVal);
            table.Controls.Add(pnlTaskOverdue, 1, 2);
            table.Controls.Add(MakeTile("Inventory Value",   TileBase,   ref lblInventoryValueVal),  2, 2);

            Controls.Add(table);

            // ── Status bar ───────────────────────────────────────────────────────
            lblStatus = new Label
            {
                Dock      = DockStyle.Bottom,
                Height    = 24,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Theme.TextMuted,
                BackColor = Color.FromArgb(8, 16, 28),
                Padding   = new Padding(6, 0, 0, 0)
            };
            Controls.Add(lblStatus);

            Load += (_, _) => LoadKPIs();
        }

        /// <summary>Creates a styled KPI tile panel. The value label ref is populated for later updates.</summary>
        private static Panel MakeTile(string caption, Color bgColor, ref Label valueLabel)
        {
            var pnl = new Panel
            {
                Margin    = new Padding(6),
                Dock      = DockStyle.Fill,
                BackColor = bgColor,
                Padding   = new Padding(8)
            };

            // Coloured left-border accent
            pnl.Paint += (s, e) =>
            {
                using var pen = new Pen(Theme.Gold, 3);
                e.Graphics.DrawLine(pen, 0, 0, 0, ((Panel)s!).Height);
            };

            var lblVal = new Label
            {
                Dock      = DockStyle.Fill,
                Text      = "—",
                Font      = new Font("Segoe UI", 32F, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var lblCaption = new Label
            {
                Dock      = DockStyle.Bottom,
                Height    = 28,
                Text      = caption,
                Font      = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(160, 175, 195),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            pnl.Controls.Add(lblVal);
            pnl.Controls.Add(lblCaption);

            valueLabel = lblVal;
            return pnl;
        }

        // ── Data loading ──────────────────────────────────────────────────────────

        private void LoadKPIs()
        {
            btnRefresh.Enabled = false;
            try
            {
                using IDbConnection db = new SqlConnection(_cs);

                // 1. Orders Today
                int ordersToday = SafeQueryScalar<int>(db,
                    "SELECT COUNT(*) FROM SalesOrders WHERE CAST(OrderDate AS DATE) = CAST(GETDATE() AS DATE)");
                lblOrdersTodayVal.Text = ordersToday.ToString("N0");

                // 2. Revenue Today
                decimal revenueToday = SafeQueryScalar<decimal>(db,
                    "SELECT ISNULL(SUM(TotalPrice),0) FROM SalesOrders WHERE CAST(OrderDate AS DATE) = CAST(GETDATE() AS DATE)");
                lblRevenueTodayVal.Text = revenueToday.ToString("C");

                // 3. Pending Orders (Draft or Live)
                int pending = SafeQueryScalar<int>(db,
                    "SELECT COUNT(*) FROM SalesOrders WHERE Status IN ('Draft','Live')");
                lblPendingOrdersVal.Text = pending.ToString("N0");

                // 4. Products In Stock (ledger subquery)
                int inStock = SafeQueryScalar<int>(db, @"
                    SELECT COUNT(*)
                    FROM   Products p
                    WHERE  p.IsActive = 1
                      AND  ISNULL((
                               SELECT SUM(t.QuantityChange)
                               FROM   InventoryTransactions t
                               WHERE  t.ProductID = p.ProductID
                           ), 0) > 0");
                lblProductsInStockVal.Text = inStock.ToString("N0");

                // 5. Out of Stock
                int outOfStock = SafeQueryScalar<int>(db, @"
                    SELECT COUNT(*)
                    FROM   Products p
                    WHERE  p.IsActive = 1
                      AND  ISNULL((
                               SELECT SUM(t.QuantityChange)
                               FROM   InventoryTransactions t
                               WHERE  t.ProductID = p.ProductID
                           ), 0) <= 0");
                lblOutOfStockVal.Text    = outOfStock.ToString("N0");
                pnlOutOfStock.BackColor  = outOfStock > 0 ? TileRed : TileGreen;

                // 6. Low Stock
                int lowStock = SafeQueryScalar<int>(db, @"
                    SELECT COUNT(*)
                    FROM   Products p
                    WHERE  p.IsActive = 1
                      AND  p.ReorderPoint > 0
                      AND  ISNULL((
                               SELECT SUM(t.QuantityChange)
                               FROM   InventoryTransactions t
                               WHERE  t.ProductID = p.ProductID
                           ), 0) > 0
                      AND  ISNULL((
                               SELECT SUM(t.QuantityChange)
                               FROM   InventoryTransactions t
                               WHERE  t.ProductID = p.ProductID
                           ), 0) <= p.ReorderPoint");
                lblLowStockVal.Text    = lowStock.ToString("N0");
                pnlLowStock.BackColor  = lowStock > 0 ? TileAmber : TileGreen;

                // 7. Open Work Orders
                int openWOs = SafeQueryScalar<int>(db,
                    "SELECT COUNT(*) FROM WorkOrders WHERE Status <> 'Complete'");
                lblOpenWorkOrdersVal.Text = openWOs.ToString("N0");

                // 8. Tasks Overdue
                int overdue = SafeQueryScalar<int>(db,
                    "SELECT COUNT(*) FROM Tasks WHERE Status <> 'Done' AND DueDate < GETDATE()");
                lblTasksOverdueVal.Text    = overdue.ToString("N0");
                pnlTaskOverdue.BackColor   = overdue > 0 ? TileRed : TilePurple;

                // 9. Inventory Value  SUM(CurrentStock * WholesalePrice)
                decimal invValue = SafeQueryScalar<decimal>(db, @"
                    SELECT ISNULL(SUM(
                               ISNULL((
                                   SELECT SUM(t.QuantityChange)
                                   FROM   InventoryTransactions t
                                   WHERE  t.ProductID = p.ProductID
                               ), 0) * p.WholesalePrice
                           ), 0)
                    FROM   Products p
                    WHERE  p.IsActive = 1");
                lblInventoryValueVal.Text = invValue.ToString("C");

                lblStatus.Text = $"Last refreshed: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error loading KPIs: {ex.Message}";
            }
            finally
            {
                btnRefresh.Enabled = true;
            }
        }

        /// <summary>Execute a scalar query, returning default(T) on any error (e.g. table doesn't exist yet).</summary>
        private static T SafeQueryScalar<T>(IDbConnection db, string sql)
        {
            try { return db.ExecuteScalar<T>(sql) ?? default!; }
            catch { return default!; }
        }
    }
}
