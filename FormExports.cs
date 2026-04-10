using System.Configuration;
using System.Data;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;

namespace JaneERP
{
    /// <summary>Centralised export hub — generates CSV files from live ERP data.</summary>
    public class FormExports : Form
    {
        private readonly string _connStr =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString ?? "";

        public FormExports()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
        }

        // ── UI ────────────────────────────────────────────────────────────────────

        private void BuildUI()
        {
            Text          = "Export Data";
            ClientSize    = new Size(800, 600);
            MinimumSize   = new Size(700, 480);
            StartPosition = FormStartPosition.CenterParent;

            // Header
            var pnlHeader = new Panel { Tag = "header", Dock = DockStyle.Top, Height = 56 };
            var lblTitle = new Label
            {
                Text      = "📤  Export Data",
                Font      = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(20, 12),
                AutoSize  = true
            };
            var lblSub = new Label
            {
                Text      = "Generate CSV exports from live ERP data",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(22, 36),
                AutoSize  = true
            };
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSub);

            // Scrollable panel for export rows
            var scroll = new Panel
            {
                AutoScroll = true,
                Dock       = DockStyle.Fill,
                Padding    = new Padding(16, 10, 16, 10)
            };

            int y = 10;

            // ── Products & Inventory ──────────────────────────────────────────────
            y = AddCategoryLabel(scroll, "Products & Inventory", y);
            y = AddExportRow(scroll, y, "Products CSV",
                "All active products (SKU, Name, Prices, Stock, Reorder, Type, Location)",
                ExportProductsCsv);
            y = AddExportRow(scroll, y, "Inventory by Location",
                "Stock per product per location (from InventoryTransactions)",
                ExportInventoryByLocation);
            y = AddExportRow(scroll, y, "Reorder Summary",
                "Products where current stock ≤ ReorderPoint (SKU, Name, Stock, Shortfall)",
                ExportReorderSummary);
            y = AddExportRow(scroll, y, "Parts List",
                "All active parts (PartNumber, Name, Stock, UnitCost, ReorderPoint)",
                ExportPartsList);

            // ── Orders & Sales ────────────────────────────────────────────────────
            y += 6;
            y = AddCategoryLabel(scroll, "Orders & Sales", y);
            y = AddExportRow(scroll, y, "Sales Orders CSV",
                "All ERP sales orders (OrderNumber, Customer, Date, Status, Total, Discount)",
                ExportSalesOrders);
            y = AddExportRow(scroll, y, "Purchase Orders CSV",
                "All purchase orders (PONumber, Supplier, Status, OrderDate, TotalCost)",
                ExportPurchaseOrders);

            // ── Manufacturing ─────────────────────────────────────────────────────
            y += 6;
            y = AddCategoryLabel(scroll, "Manufacturing", y);
            y = AddExportRow(scroll, y, "Work Orders CSV",
                "All work orders (WO Number, Product, Quantity, Status, COGS)",
                ExportWorkOrders);
            y = AddExportRow(scroll, y, "COGS Summary",
                "Completed work orders with cost of goods sold breakdown",
                ExportCogsSummary);

            // ── Customers ─────────────────────────────────────────────────────────
            y += 6;
            y = AddCategoryLabel(scroll, "Customers", y);
            y = AddExportRow(scroll, y, "Customer List",
                "All customers (Name, Email, Phone, Discount Tier)",
                ExportCustomerList);

            // ── Audit ─────────────────────────────────────────────────────────────
            y += 6;
            y = AddCategoryLabel(scroll, "Audit", y);
            y = AddExportRow(scroll, y, "Activity Log CSV",
                "Last 30 days of audit entries from the AppLogger activity log",
                ExportActivityLog);

            Controls.Add(scroll);
            Controls.Add(pnlHeader);
        }

        private static int AddCategoryLabel(Panel parent, string text, int y)
        {
            var lbl = new Label
            {
                Text      = text,
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(0, y),
                Size      = new Size(740, 24),
                AutoSize  = false
            };
            parent.Controls.Add(lbl);
            return y + 28;
        }

        private static int AddExportRow(Panel parent, int y, string name, string description, EventHandler exportHandler)
        {
            const int rowH    = 56;
            const int btnW    = 100;
            const int btnH    = 30;

            var pnlRow = new Panel
            {
                Location  = new Point(0, y),
                Size      = new Size(740, rowH),
                BackColor = Theme.Surface
            };

            var lblName = new Label
            {
                Text      = name,
                Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                Location  = new Point(10, 6),
                AutoSize  = true
            };

            var lblDesc = new Label
            {
                Text      = description,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(10, 26),
                Size      = new Size(600, 22),
                AutoSize  = false
            };

            var btn = new Button
            {
                Text      = "Export →",
                Size      = new Size(btnW, btnH),
                Location  = new Point(630, (rowH - btnH) / 2),
                BackColor = Color.FromArgb(0, 164, 153),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(0, 130, 120);
            btn.Click += exportHandler;

            pnlRow.Controls.Add(lblName);
            pnlRow.Controls.Add(lblDesc);
            pnlRow.Controls.Add(btn);
            parent.Controls.Add(pnlRow);

            return y + rowH + 4;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private string? PickSavePath(string defaultFileName)
        {
            using var dlg = new SaveFileDialog
            {
                Title            = "Save CSV Export",
                Filter           = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt       = "csv",
                FileName         = defaultFileName,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };
            return dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileName : null;
        }

        private void WriteAndConfirm(string path, string csv)
        {
            File.WriteAllText(path, csv, new UTF8Encoding(true));
            MessageBox.Show(this, $"Export saved to:\n{path}", "Export Complete",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string Date => DateTime.Today.ToString("yyyyMMdd");

        private IDbConnection OpenDb() => new SqlConnection(_connStr);

        // ── Export implementations ────────────────────────────────────────────────

        private void ExportProductsCsv(object? sender, EventArgs e)
        {
            var path = PickSavePath($"products_{Date}.csv");
            if (path == null) return;
            try
            {
                using var db = OpenDb();
                var rows = db.Query(@"
                    SELECT  p.SKU,
                            p.ProductName,
                            p.RetailPrice,
                            p.WholesalePrice,
                            p.ReorderPoint,
                            ISNULL(p.OrderUpTo, 0) AS OrderUpTo,
                            ISNULL((SELECT SUM(t.QuantityChange) FROM InventoryTransactions t WHERE t.ProductID = p.ProductID), 0) AS CurrentStock,
                            pt.TypeName   AS [Type],
                            l.LocationName AS Location
                    FROM    Products p
                    LEFT JOIN ProductTypes pt ON pt.ProductTypeID = p.ProductTypeID
                    LEFT JOIN Locations    l  ON l.LocationID     = p.DefaultLocationID
                    WHERE   p.IsActive = 1
                    ORDER BY p.SKU").AsList();

                var sb = new StringBuilder();
                sb.AppendLine("SKU,Name,RetailPrice,WholesalePrice,ReorderPoint,OrderUpTo,CurrentStock,Type,Location");
                foreach (var r in rows)
                {
                    sb.AppendLine($"{Esc(r.SKU)},{Esc(r.ProductName)},{r.RetailPrice},{r.WholesalePrice}," +
                                  $"{r.ReorderPoint},{r.OrderUpTo},{r.CurrentStock},{Esc(r.Type)},{Esc(r.Location)}");
                }
                WriteAndConfirm(path, sb.ToString());
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ExportInventoryByLocation(object? sender, EventArgs e)
        {
            var path = PickSavePath($"inventory_by_location_{Date}.csv");
            if (path == null) return;
            try
            {
                using var db = OpenDb();
                var rows = db.Query(@"
                    SELECT  p.SKU,
                            p.ProductName,
                            l.LocationName,
                            SUM(t.QuantityChange) AS StockQty
                    FROM    InventoryTransactions t
                    JOIN    Products  p ON p.ProductID  = t.ProductID
                    JOIN    Locations l ON l.LocationID = t.LocationID
                    WHERE   p.IsActive = 1
                    GROUP BY p.SKU, p.ProductName, l.LocationName
                    ORDER BY p.SKU, l.LocationName").AsList();

                var sb = new StringBuilder();
                sb.AppendLine("SKU,ProductName,Location,StockQty");
                foreach (var r in rows)
                    sb.AppendLine($"{Esc(r.SKU)},{Esc(r.ProductName)},{Esc(r.LocationName)},{r.StockQty}");
                WriteAndConfirm(path, sb.ToString());
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ExportReorderSummary(object? sender, EventArgs e)
        {
            var path = PickSavePath($"reorder_summary_{Date}.csv");
            if (path == null) return;
            try
            {
                using var db = OpenDb();
                var rows = db.Query(@"
                    SELECT  p.SKU,
                            p.ProductName,
                            ISNULL((SELECT SUM(t.QuantityChange) FROM InventoryTransactions t WHERE t.ProductID = p.ProductID), 0) AS CurrentStock,
                            p.ReorderPoint,
                            p.ReorderPoint - ISNULL((SELECT SUM(t.QuantityChange) FROM InventoryTransactions t WHERE t.ProductID = p.ProductID), 0) AS Shortfall
                    FROM    Products p
                    WHERE   p.IsActive = 1
                      AND   ISNULL((SELECT SUM(t.QuantityChange) FROM InventoryTransactions t WHERE t.ProductID = p.ProductID), 0) <= p.ReorderPoint
                    ORDER BY Shortfall DESC").AsList();

                if (!rows.Any())
                {
                    MessageBox.Show(this, "No products currently at or below reorder point.", "Reorder Summary",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                var sb = new StringBuilder();
                sb.AppendLine("SKU,Name,CurrentStock,ReorderPoint,Shortfall");
                foreach (var r in rows)
                    sb.AppendLine($"{Esc(r.SKU)},{Esc(r.ProductName)},{r.CurrentStock},{r.ReorderPoint},{r.Shortfall}");
                WriteAndConfirm(path, sb.ToString());
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ExportPartsList(object? sender, EventArgs e)
        {
            var path = PickSavePath($"parts_list_{Date}.csv");
            if (path == null) return;
            try
            {
                using var db = OpenDb();
                var rows = db.Query(@"
                    SELECT  PartNumber,
                            PartName,
                            CurrentStock,
                            UnitCost,
                            ReorderPoint
                    FROM    Parts
                    WHERE   IsActive = 1
                    ORDER BY PartNumber").AsList();

                var sb = new StringBuilder();
                sb.AppendLine("PartNumber,PartName,CurrentStock,UnitCost,ReorderPoint");
                foreach (var r in rows)
                    sb.AppendLine($"{Esc(r.PartNumber)},{Esc(r.PartName)},{r.CurrentStock},{r.UnitCost},{r.ReorderPoint}");
                WriteAndConfirm(path, sb.ToString());
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ExportSalesOrders(object? sender, EventArgs e)
        {
            var path = PickSavePath($"sales_orders_{Date}.csv");
            if (path == null) return;
            try
            {
                using var db = OpenDb();
                var rows = db.Query(@"
                    SELECT  o.OrderNumber,
                            o.CustomerName,
                            o.CreatedAt   AS OrderDate,
                            o.Status,
                            o.TotalAmount AS Total,
                            ISNULL(o.DiscountAmount, 0) AS Discount
                    FROM    Orders o
                    ORDER BY o.CreatedAt DESC").AsList();

                var sb = new StringBuilder();
                sb.AppendLine("OrderNumber,Customer,Date,Status,Total,Discount");
                foreach (var r in rows)
                    sb.AppendLine($"{Esc(r.OrderNumber)},{Esc(r.CustomerName)},{r.OrderDate:yyyy-MM-dd}," +
                                  $"{Esc(r.Status)},{r.Total},{r.Discount}");
                WriteAndConfirm(path, sb.ToString());
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ExportPurchaseOrders(object? sender, EventArgs e)
        {
            var path = PickSavePath($"purchase_orders_{Date}.csv");
            if (path == null) return;
            try
            {
                using var db = OpenDb();
                var rows = db.Query(@"
                    SELECT  po.PONumber,
                            s.SupplierName,
                            po.Status,
                            po.OrderDate,
                            po.TotalCost
                    FROM    PurchaseOrders po
                    LEFT JOIN Suppliers s ON s.SupplierID = po.SupplierID
                    ORDER BY po.OrderDate DESC").AsList();

                var sb = new StringBuilder();
                sb.AppendLine("PONumber,Supplier,Status,OrderDate,TotalCost");
                foreach (var r in rows)
                    sb.AppendLine($"{Esc(r.PONumber)},{Esc(r.SupplierName)},{Esc(r.Status)}," +
                                  $"{r.OrderDate:yyyy-MM-dd},{r.TotalCost}");
                WriteAndConfirm(path, sb.ToString());
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ExportWorkOrders(object? sender, EventArgs e)
        {
            var path = PickSavePath($"work_orders_{Date}.csv");
            if (path == null) return;
            try
            {
                using var db = OpenDb();
                var rows = db.Query(@"
                    SELECT  mo.WorkOrderNumber,
                            p.ProductName,
                            mo.Quantity,
                            mo.Status,
                            ISNULL(mo.COGS, 0) AS COGS
                    FROM    ManufacturingOrders mo
                    LEFT JOIN Products p ON p.ProductID = mo.ProductID
                    ORDER BY mo.CreatedAt DESC").AsList();

                var sb = new StringBuilder();
                sb.AppendLine("WorkOrderNumber,Product,Quantity,Status,COGS");
                foreach (var r in rows)
                    sb.AppendLine($"{Esc(r.WorkOrderNumber)},{Esc(r.ProductName)},{r.Quantity},{Esc(r.Status)},{r.COGS}");
                WriteAndConfirm(path, sb.ToString());
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ExportCogsSummary(object? sender, EventArgs e)
        {
            var path = PickSavePath($"cogs_summary_{Date}.csv");
            if (path == null) return;
            try
            {
                using var db = OpenDb();
                var rows = db.Query(@"
                    SELECT  mo.WorkOrderNumber,
                            p.ProductName,
                            mo.Quantity,
                            ISNULL(mo.COGS, 0)                       AS TotalCOGS,
                            CASE WHEN mo.Quantity > 0
                                 THEN ISNULL(mo.COGS, 0) / mo.Quantity
                                 ELSE 0 END                           AS COGSPerUnit,
                            mo.CompletedAt
                    FROM    ManufacturingOrders mo
                    LEFT JOIN Products p ON p.ProductID = mo.ProductID
                    WHERE   mo.Status = 'Completed'
                    ORDER BY mo.CompletedAt DESC").AsList();

                var sb = new StringBuilder();
                sb.AppendLine("WorkOrderNumber,Product,Quantity,TotalCOGS,COGSPerUnit,CompletedAt");
                foreach (var r in rows)
                    sb.AppendLine($"{Esc(r.WorkOrderNumber)},{Esc(r.ProductName)},{r.Quantity}," +
                                  $"{r.TotalCOGS},{r.COGSPerUnit},{r.CompletedAt:yyyy-MM-dd}");
                WriteAndConfirm(path, sb.ToString());
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ExportCustomerList(object? sender, EventArgs e)
        {
            var path = PickSavePath($"customers_{Date}.csv");
            if (path == null) return;
            try
            {
                using var db = OpenDb();
                // Try with DiscountTier join; fall back to without if table doesn't exist
                IEnumerable<dynamic> rows;
                try
                {
                    rows = db.Query(@"
                        SELECT  c.CustomerName AS Name,
                                c.Email,
                                c.Phone,
                                ISNULL(dt.TierName, '') AS DiscountTier
                        FROM    Customers c
                        LEFT JOIN DiscountTiers dt ON dt.DiscountTierID = c.DiscountTierID
                        ORDER BY c.CustomerName");
                }
                catch
                {
                    rows = db.Query(@"
                        SELECT  CustomerName AS Name,
                                Email,
                                Phone,
                                '' AS DiscountTier
                        FROM    Customers
                        ORDER BY CustomerName");
                }

                var sb = new StringBuilder();
                sb.AppendLine("Name,Email,Phone,DiscountTier");
                foreach (var r in rows)
                    sb.AppendLine($"{Esc(r.Name)},{Esc(r.Email)},{Esc(r.Phone)},{Esc(r.DiscountTier)}");
                WriteAndConfirm(path, sb.ToString());
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ExportActivityLog(object? sender, EventArgs e)
        {
            var path = PickSavePath($"activity_log_{Date}.csv");
            if (path == null) return;
            try
            {
                using var db = OpenDb();
                var rows = db.Query(@"
                    SELECT  Username,
                            Action,
                            Detail,
                            LoggedAt
                    FROM    AppLog
                    WHERE   LoggedAt >= DATEADD(day, -30, GETDATE())
                    ORDER BY LoggedAt DESC").AsList();

                var sb = new StringBuilder();
                sb.AppendLine("Username,Action,Detail,LoggedAt");
                foreach (var r in rows)
                    sb.AppendLine($"{Esc(r.Username)},{Esc(r.Action)},{Esc(r.Detail)},{r.LoggedAt:yyyy-MM-dd HH:mm:ss}");
                WriteAndConfirm(path, sb.ToString());
            }
            catch (Exception ex) { ShowError(ex); }
        }

        // ── CSV helpers ───────────────────────────────────────────────────────────

        /// <summary>Escape a value for CSV output — wraps in quotes if it contains comma/quote/newline.</summary>
        private static string Esc(object? value)
        {
            var s = value?.ToString() ?? "";
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }

        private void ShowError(Exception ex)
        {
            if (ex.Message.Contains("Invalid object name") || ex.Message.Contains("doesn't exist"))
                MessageBox.Show(this, "No data available — the table does not exist yet.", "Export",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                MessageBox.Show(this, $"Export failed: {ex.Message}", "Export Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
