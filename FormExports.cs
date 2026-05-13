using System.Text;
using JaneERP.Data;
using JaneERP.Infrastructure;
using JaneERP.Interfaces;

namespace JaneERP
{
    /// <summary>Centralised export hub — generates CSV files from live ERP data.</summary>
    public class FormExports : Form
    {
        private readonly IExportRepository _repo = AppServices.Get<IExportRepository>();

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
            y = AddExportRow(scroll, y, "Sales Order Line Items CSV",
                "Line-item detail per order (OrderNumber, Date, SKU, Product, Qty, UnitPrice, LineTotal, Status)",
                ExportSalesOrderLineItems);
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

            Theme.MakeDraggable(this, pnlHeader);
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
                Size      = new Size(parent.ClientSize.Width > 0 ? parent.ClientSize.Width - 4 : 740, 24),
                Anchor    = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                AutoSize  = false
            };
            parent.Controls.Add(lbl);
            return y + 28;
        }

        private static int AddExportRow(Panel parent, int y, string name, string description, EventHandler exportHandler)
        {
            const int rowH = 56;
            const int btnW = 100;
            const int btnH = 30;
            int initW = parent.ClientSize.Width > 0 ? parent.ClientSize.Width - 4 : 740;

            var pnlRow = new Panel
            {
                Location  = new Point(0, y),
                Size      = new Size(initW, rowH),
                BackColor = Theme.Surface,
                Anchor    = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
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
                Size      = new Size(initW - btnW - 30, 22),
                Anchor    = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                AutoSize  = false
            };

            var btn = new Button
            {
                Text      = "Export →",
                Size      = new Size(btnW, btnH),
                Location  = new Point(initW - btnW - 10, (rowH - btnH) / 2),
                Anchor    = AnchorStyles.Right | AnchorStyles.Top,
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
            var settings       = AppSettings.Current;
            var defaultDir     = settings.DefaultExportPath;
            if (string.IsNullOrWhiteSpace(defaultDir) || !Directory.Exists(defaultDir))
                defaultDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            // If a default path is configured, offer to use it directly
            if (!string.IsNullOrWhiteSpace(settings.DefaultExportPath) &&
                Directory.Exists(settings.DefaultExportPath))
            {
                var quickPath = Path.Combine(settings.DefaultExportPath, defaultFileName);
                var choice = MessageBox.Show(this,
                    $"Use default export folder?\n{settings.DefaultExportPath}\n\n" +
                    $"File: {defaultFileName}\n\nYes = use default  |  No = choose location",
                    "Export Location", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (choice == DialogResult.Cancel) return null;
                if (choice == DialogResult.Yes)    return quickPath;
            }

            using var dlg = new SaveFileDialog
            {
                Title            = "Save CSV Export",
                Filter           = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt       = "csv",
                FileName         = defaultFileName,
                InitialDirectory = defaultDir
            };
            return dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileName : null;
        }

        private void WriteAndConfirm(string path, string csv)
        {
            File.WriteAllText(path, csv, new UTF8Encoding(true));
            var result = MessageBox.Show(this, $"Export saved to:\n{path}\n\nOpen the file now?", "Export Complete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (result == DialogResult.Yes)
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
                catch { /* silently ignore if OS can't open */ }
            }
        }

        private static string Date => DateTime.Today.ToString("yyyyMMdd");

        // ── Export implementations ────────────────────────────────────────────────

        private void ExportProductsCsv(object? sender, EventArgs e)
        {
            var path = PickSavePath($"products_{Date}.csv");
            if (path == null) return;
            try
            {
                var rows = _repo.GetProductsForExport().ToList();
                var sb = new StringBuilder();
                sb.AppendLine("SKU,Name,UOM,RetailPrice,WholesalePrice,ReorderPoint,OrderUpTo,CurrentStock,Type,Location");
                foreach (var r in rows)
                    sb.AppendLine($"{Esc(r.SKU)},{Esc(r.ProductName)},{Esc(r.UnitOfMeasure)}," +
                                  $"{r.RetailPrice},{r.WholesalePrice}," +
                                  $"{r.ReorderPoint},{r.OrderUpTo},{r.CurrentStock},{Esc(r.Type)},{Esc(r.Location)}");
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
                var rows = _repo.GetInventoryByLocationForExport().ToList();
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
                var rows = _repo.GetReorderSummaryForExport().ToList();
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
                var rows = _repo.GetPartsForExport().ToList();
                var sb = new StringBuilder();
                sb.AppendLine("PartNumber,PartName,UOM,CurrentStock,UnitCost,ReorderPoint");
                foreach (var r in rows)
                    sb.AppendLine($"{Esc(r.PartNumber)},{Esc(r.PartName)},{Esc(r.UnitOfMeasure)},{r.CurrentStock},{r.UnitCost},{r.ReorderPoint}");
                WriteAndConfirm(path, sb.ToString());
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ExportSalesOrders(object? sender, EventArgs e)
        {
            using var filter = new FormExportDateFilter("Export Sales Orders");
            if (filter.ShowDialog(this) != DialogResult.OK) return;
            var (from, to) = (filter.FromDate, filter.ToDate.AddDays(1).AddTicks(-1));
            var path = PickSavePath($"sales_orders_{filter.FromDate:yyyyMMdd}_{filter.ToDate:yyyyMMdd}.csv");
            if (path == null) return;
            try
            {
                var rows = _repo.GetSalesOrdersForExport(from, to).ToList();
                var sb = new StringBuilder();
                sb.AppendLine("OrderNumber,Customer,Date,Status,Total,Discount");
                foreach (var r in rows)
                    sb.AppendLine($"{Esc(r.OrderNumber)},{Esc(r.CustomerName)},{r.OrderDate:yyyy-MM-dd}," +
                                  $"{Esc(r.Status)},{r.Total},{r.Discount}");
                WriteAndConfirm(path, sb.ToString());
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ExportSalesOrderLineItems(object? sender, EventArgs e)
        {
            using var filter = new FormExportDateFilter("Export Sales Order Line Items");
            if (filter.ShowDialog(this) != DialogResult.OK) return;
            var (from, to) = (filter.FromDate, filter.ToDate.AddDays(1).AddTicks(-1));
            var path = PickSavePath($"sales_order_line_items_{filter.FromDate:yyyyMMdd}_{filter.ToDate:yyyyMMdd}.csv");
            if (path == null) return;
            try
            {
                var rows = _repo.GetSalesOrderLineItemsForExport(from, to).ToList();
                var sb = new StringBuilder();
                sb.AppendLine("OrderNumber,Date,Status,Customer,SKU,Product,Qty,UnitPrice,LineTotal");
                foreach (var r in rows)
                    sb.AppendLine($"{r.OrderNumber},{r.OrderDate:yyyy-MM-dd},{Esc(r.Status)}," +
                                  $"{Esc(r.Customer)},{Esc(r.SKU)},{Esc(r.ProductName)}," +
                                  $"{r.Quantity},{r.UnitPrice},{r.LineTotal}");
                WriteAndConfirm(path, sb.ToString());
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ExportPurchaseOrders(object? sender, EventArgs e)
        {
            using var filter = new FormExportDateFilter("Export Purchase Orders");
            if (filter.ShowDialog(this) != DialogResult.OK) return;
            var (from, to) = (filter.FromDate, filter.ToDate.AddDays(1).AddTicks(-1));
            var path = PickSavePath($"purchase_orders_{filter.FromDate:yyyyMMdd}_{filter.ToDate:yyyyMMdd}.csv");
            if (path == null) return;
            try
            {
                var rows = _repo.GetPurchaseOrdersForExport(from, to).ToList();
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
                var rows = _repo.GetWorkOrdersForExport().ToList();
                var sb = new StringBuilder();
                sb.AppendLine("WorkOrderID,MONumber,Product,Quantity,Status,COGS,CompletedAt");
                foreach (var r in rows)
                    sb.AppendLine($"{r.WorkOrderID},{Esc(r.MONumber)},{Esc(r.ProductName)}," +
                                  $"{r.Quantity},{Esc(r.Status)},{r.COGS}," +
                                  $"{(r.CompletedAt != null ? ((DateTime)r.CompletedAt).ToString("yyyy-MM-dd") : "")}");
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
                var rows = _repo.GetCogsSummaryForExport().ToList();
                var sb = new StringBuilder();
                sb.AppendLine("WorkOrderID,MONumber,Product,Quantity,TotalCOGS,COGSPerUnit,CompletedAt");
                foreach (var r in rows)
                    sb.AppendLine($"{r.WorkOrderID},{Esc(r.MONumber)},{Esc(r.ProductName)},{r.Quantity}," +
                                  $"{r.TotalCOGS},{r.COGSPerUnit}," +
                                  $"{(r.CompletedAt != null ? ((DateTime)r.CompletedAt).ToString("yyyy-MM-dd") : "")}");
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
                var rows = _repo.GetCustomerListForExport().ToList();
                var sb = new StringBuilder();
                sb.AppendLine("Name,Email,OrderCount,TotalSpent");
                foreach (var r in rows)
                    sb.AppendLine($"{Esc(r.Name)},{Esc(r.Email)},{r.OrderCount},{r.TotalSpent}");
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
                using var db = new AppDbContext();
                var cutoff = DateTime.UtcNow.AddDays(-30);
                var rows = db.AuditLogs
                    .Where(a => a.When >= cutoff)
                    .OrderByDescending(a => a.When)
                    .ToList();

                var sb = new StringBuilder();
                sb.AppendLine("Username,Action,Details,LoggedAt");
                foreach (var r in rows)
                    sb.AppendLine($"{Esc(r.User)},{Esc(r.Action)},{Esc(r.Details)},{r.When.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
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

    /// <summary>Lightweight date-range picker shown before a filtered export.</summary>
    internal sealed class FormExportDateFilter : Form
    {
        public DateTime FromDate => _dtpFrom.Value.Date;
        public DateTime ToDate   => _dtpTo.Value.Date;

        private DateTimePicker _dtpFrom = new();
        private DateTimePicker _dtpTo   = new();

        public FormExportDateFilter(string title)
        {
            Text            = title;
            ClientSize      = new Size(340, 150);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;

            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);

            Controls.Add(new Label
            {
                Text      = title,
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            });

            Controls.Add(new Label { Text = "From:", Location = new Point(12, 48), AutoSize = true });
            _dtpFrom.Location = new Point(70, 44);
            _dtpFrom.Size     = new Size(120, 24);
            _dtpFrom.Format   = DateTimePickerFormat.Short;
            _dtpFrom.Value    = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            Controls.Add(_dtpFrom);

            Controls.Add(new Label { Text = "To:", Location = new Point(202, 48), AutoSize = true });
            _dtpTo.Location = new Point(222, 44);
            _dtpTo.Size     = new Size(104, 24);
            _dtpTo.Format   = DateTimePickerFormat.Short;
            _dtpTo.Value    = DateTime.Today;
            Controls.Add(_dtpTo);

            var btnOk = new Button
            {
                Text           = "Export",
                Size           = new Size(90, 30),
                Location       = new Point(140, 96),
                DialogResult   = DialogResult.OK,
                UseVisualStyleBackColor = true
            };
            var btnCancel = new Button
            {
                Text         = "Cancel",
                Size         = new Size(80, 30),
                Location     = new Point(246, 96),
                DialogResult = DialogResult.Cancel,
                UseVisualStyleBackColor = true
            };
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
