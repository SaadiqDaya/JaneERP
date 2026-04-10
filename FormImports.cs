using System.Configuration;
using System.Data;
using System.Globalization;
using System.Text;
using Dapper;
using JaneERP.Data;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP
{
    /// <summary>Centralised import hub — loads data from CSV files into the ERP.</summary>
    public class FormImports : Form
    {
        private readonly string _connStr =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString ?? "";

        public FormImports()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
        }

        private void BuildUI()
        {
            Text          = "Import Data";
            ClientSize    = new Size(800, 600);
            MinimumSize   = new Size(700, 480);
            StartPosition = FormStartPosition.CenterParent;

            var pnlHeader = new Panel { Tag = "header", Dock = DockStyle.Top, Height = 56 };
            pnlHeader.Controls.Add(new Label
            {
                Text      = "📥  Import Data",
                Font      = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(20, 12),
                AutoSize  = true
            });
            pnlHeader.Controls.Add(new Label
            {
                Text      = "Load records from CSV files into the ERP database",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(22, 36),
                AutoSize  = true
            });

            var scroll = new Panel
            {
                AutoScroll = true,
                Dock       = DockStyle.Fill,
                Padding    = new Padding(16, 10, 16, 10)
            };

            int y = 10;
            y = AddCategoryLabel(scroll, "Products & Parts", y);
            y = AddImportRow(scroll, y, "Products CSV",
                "SKU (required), ProductName (required), RetailPrice, WholesalePrice, ReorderPoint, CurrentStock",
                "SKU,ProductName,RetailPrice,WholesalePrice,ReorderPoint,CurrentStock",
                ImportProductsCsv);
            y = AddImportRow(scroll, y, "Parts CSV",
                "PartNumber (required), PartName (required), UnitCost, CurrentStock",
                "PartNumber,PartName,UnitCost,CurrentStock",
                ImportPartsCsv);

            y += 6;
            y = AddCategoryLabel(scroll, "Pricing & Discounts", y);
            y = AddImportRow(scroll, y, "Discount Tiers CSV",
                "TierName (required), DiscountPercent (required, 0–100), Description (optional)",
                "TierName,DiscountPercent,Description",
                ImportDiscountTiersCsv);

            y += 6;
            y = AddCategoryLabel(scroll, "Customers", y);
            y = AddImportRow(scroll, y, "Customers CSV",
                "Email (required), FullName, Phone — creates or updates customer records",
                "Email,FullName,Phone",
                ImportCustomersCsv);

            Controls.Add(scroll);
            Controls.Add(pnlHeader);
        }

        private static int AddCategoryLabel(Panel parent, string text, int y)
        {
            parent.Controls.Add(new Label
            {
                Text      = text,
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(0, y),
                Size      = new Size(740, 24),
                AutoSize  = false
            });
            return y + 28;
        }

        private static int AddImportRow(Panel parent, int y, string name, string description,
                                        string sampleColumns, EventHandler importHandler)
        {
            const int rowH = 72;
            const int btnW = 100;
            const int btnH = 30;

            var pnlRow = new Panel
            {
                Location  = new Point(0, y),
                Size      = new Size(740, rowH),
                BackColor = Theme.Surface
            };

            pnlRow.Controls.Add(new Label
            {
                Text      = name,
                Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                Location  = new Point(10, 6),
                AutoSize  = true
            });

            pnlRow.Controls.Add(new Label
            {
                Text      = description,
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(10, 26),
                Size      = new Size(600, 18),
                AutoSize  = false
            });

            pnlRow.Controls.Add(new Label
            {
                Text      = $"Columns: {sampleColumns}",
                Font      = new Font("Segoe UI", 7.5F),
                ForeColor = Theme.TextMuted,
                Location  = new Point(10, 46),
                Size      = new Size(600, 16),
                AutoSize  = false
            });

            var btn = new Button
            {
                Text      = "Import →",
                Size      = new Size(btnW, btnH),
                Location  = new Point(630, (rowH - btnH) / 2),
                BackColor = Theme.Gold,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = Theme.GoldDark;
            btn.Click += importHandler;
            pnlRow.Controls.Add(btn);

            parent.Controls.Add(pnlRow);
            return y + rowH + 4;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private string? PickOpenPath(string title, string defaultName)
        {
            using var dlg = new OpenFileDialog
            {
                Title            = title,
                Filter           = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName         = defaultName,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };
            return dlg.ShowDialog(this) == DialogResult.OK ? dlg.FileName : null;
        }

        private IDbConnection OpenDb() => new SqlConnection(_connStr);

        private void ShowResult(int inserted, int updated, int skipped, string? extraInfo = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"✔  {inserted} inserted");
            if (updated > 0)  sb.AppendLine($"↺  {updated} updated");
            if (skipped > 0)  sb.AppendLine($"—  {skipped} skipped / invalid");
            if (extraInfo != null) { sb.AppendLine(); sb.AppendLine(extraInfo); }
            MessageBox.Show(this, sb.ToString().TrimEnd(), "Import Complete",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowError(Exception ex)
            => MessageBox.Show(this, $"Import failed:\n{ex.Message}", "Import Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

        // ── CSV parsing ───────────────────────────────────────────────────────────

        /// <summary>Reads a CSV file and returns rows as dictionaries (key = header, value = cell).</summary>
        private static List<Dictionary<string, string>> ReadCsv(string path)
        {
            var result = new List<Dictionary<string, string>>();
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length < 2) return result;

            var headers = lines[0].Split(',').Select(h => h.Trim().Trim('"')).ToArray();
            foreach (var line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cells = SplitCsvLine(line);
                var row   = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < headers.Length && i < cells.Length; i++)
                    row[headers[i]] = cells[i].Trim().Trim('"');
                result.Add(row);
            }
            return result;
        }

        private static string[] SplitCsvLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            var current   = new StringBuilder();
            foreach (char c in line)
            {
                if (c == '"') { inQuotes = !inQuotes; }
                else if (c == ',' && !inQuotes) { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
            fields.Add(current.ToString());
            return fields.ToArray();
        }

        // ── Import implementations ─────────────────────────────────────────────────

        private void ImportProductsCsv(object? sender, EventArgs e)
        {
            var path = PickOpenPath("Import Products CSV", "products.csv");
            if (path == null) return;
            try
            {
                var rows = ReadCsv(path);
                int inserted = 0, updated = 0, skipped = 0;
                using var db = OpenDb();
                foreach (var row in rows)
                {
                    var sku  = row.GetValueOrDefault("SKU", "").Trim();
                    var name = row.GetValueOrDefault("ProductName", "").Trim();
                    if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(name)) { skipped++; continue; }

                    decimal.TryParse(row.GetValueOrDefault("RetailPrice", "0"), NumberStyles.Any, CultureInfo.InvariantCulture, out var retail);
                    decimal.TryParse(row.GetValueOrDefault("WholesalePrice", "0"), NumberStyles.Any, CultureInfo.InvariantCulture, out var wholesale);
                    int.TryParse(row.GetValueOrDefault("ReorderPoint", "0"), out var reorder);
                    int.TryParse(row.GetValueOrDefault("CurrentStock", "0"), out var stock);

                    var existing = db.QueryFirstOrDefault<int?>("SELECT ProductID FROM Products WHERE SKU = @sku", new { sku });
                    if (existing.HasValue)
                    {
                        db.Execute(@"UPDATE Products SET ProductName=@name, RetailPrice=@retail,
                                     WholesalePrice=@wholesale, ReorderPoint=@reorder WHERE ProductID=@id",
                            new { name, retail, wholesale, reorder, id = existing.Value });
                        if (stock != 0)
                            db.Execute("UPDATE Products SET CurrentStock = @stock WHERE ProductID = @id",
                                new { stock, id = existing.Value });
                        updated++;
                    }
                    else
                    {
                        db.Execute(@"INSERT INTO Products (SKU, ProductName, RetailPrice, WholesalePrice, ReorderPoint, IsActive)
                                     VALUES (@sku, @name, @retail, @wholesale, @reorder, 1)",
                            new { sku, name, retail, wholesale, reorder });
                        inserted++;
                    }
                }
                ShowResult(inserted, updated, skipped);
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ImportPartsCsv(object? sender, EventArgs e)
        {
            var path = PickOpenPath("Import Parts CSV", "parts.csv");
            if (path == null) return;
            try
            {
                var repo = new PartRepository();
                repo.EnsureSchema();
                var rows = ReadCsv(path);
                int inserted = 0, updated = 0, skipped = 0;
                using var db = OpenDb();
                foreach (var row in rows)
                {
                    var num  = row.GetValueOrDefault("PartNumber", "").Trim();
                    var name = row.GetValueOrDefault("PartName", "").Trim();
                    if (string.IsNullOrWhiteSpace(num) || string.IsNullOrWhiteSpace(name)) { skipped++; continue; }

                    decimal.TryParse(row.GetValueOrDefault("UnitCost", "0"), NumberStyles.Any, CultureInfo.InvariantCulture, out var cost);
                    int.TryParse(row.GetValueOrDefault("CurrentStock", "0"), out var stock);

                    var existing = db.QueryFirstOrDefault<int?>("SELECT PartID FROM Parts WHERE PartNumber = @num", new { num });
                    if (existing.HasValue)
                    {
                        db.Execute("UPDATE Parts SET PartName=@name, UnitCost=@cost WHERE PartID=@id",
                            new { name, cost, id = existing.Value });
                        if (stock != 0) db.Execute("UPDATE Parts SET CurrentStock=@stock WHERE PartID=@id", new { stock, id = existing.Value });
                        updated++;
                    }
                    else
                    {
                        db.Execute(@"INSERT INTO Parts (PartNumber, PartName, UnitCost, CurrentStock, IsActive)
                                     VALUES (@num, @name, @cost, @stock, 1)", new { num, name, cost, stock });
                        inserted++;
                    }
                }
                ShowResult(inserted, updated, skipped);
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ImportDiscountTiersCsv(object? sender, EventArgs e)
        {
            var path = PickOpenPath("Import Discount Tiers CSV", "discount_tiers.csv");
            if (path == null) return;
            try
            {
                var repo = new DiscountTierRepository();
                repo.EnsureSchema();
                var rows = ReadCsv(path);
                int inserted = 0, updated = 0, skipped = 0;
                using var db = OpenDb();
                foreach (var row in rows)
                {
                    var name = row.GetValueOrDefault("TierName", "").Trim();
                    if (string.IsNullOrWhiteSpace(name)) { skipped++; continue; }
                    if (!decimal.TryParse(row.GetValueOrDefault("DiscountPercent", ""),
                        NumberStyles.Any, CultureInfo.InvariantCulture, out var pct) || pct < 0 || pct > 100)
                    { skipped++; continue; }

                    var desc = row.GetValueOrDefault("Description", "").Trim();
                    var existing = db.QueryFirstOrDefault<int?>(
                        "SELECT TierID FROM DiscountTiers WHERE TierName = @name", new { name });

                    if (existing.HasValue)
                    {
                        db.Execute("UPDATE DiscountTiers SET DiscountPercent=@pct, Description=@desc WHERE TierID=@id",
                            new { pct, desc, id = existing.Value });
                        updated++;
                    }
                    else
                    {
                        db.Execute(@"INSERT INTO DiscountTiers (TierName, DiscountPercent, Description, IsActive)
                                     VALUES (@name, @pct, @desc, 1)", new { name, pct, desc });
                        inserted++;
                    }
                }
                ShowResult(inserted, updated, skipped);
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ImportCustomersCsv(object? sender, EventArgs e)
        {
            var path = PickOpenPath("Import Customers CSV", "customers.csv");
            if (path == null) return;
            try
            {
                var rows = ReadCsv(path);
                int inserted = 0, updated = 0, skipped = 0;
                using var db = OpenDb();
                // Ensure Customers table has Email column (basic guard)
                foreach (var row in rows)
                {
                    var email = row.GetValueOrDefault("Email", "").Trim();
                    if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) { skipped++; continue; }

                    var fullName = row.GetValueOrDefault("FullName", "").Trim();
                    var phone    = row.GetValueOrDefault("Phone", "").Trim();

                    var existing = db.QueryFirstOrDefault<int?>(
                        "SELECT CustomerID FROM Customers WHERE Email = @email", new { email });
                    if (existing.HasValue)
                    {
                        db.Execute(@"UPDATE Customers SET CustomerName=@fullName, Phone=@phone WHERE CustomerID=@id",
                            new { fullName, phone, id = existing.Value });
                        updated++;
                    }
                    else
                    {
                        try
                        {
                            db.Execute(@"INSERT INTO Customers (Email, CustomerName, Phone)
                                         VALUES (@email, @fullName, @phone)", new { email, fullName, phone });
                            inserted++;
                        }
                        catch { skipped++; }
                    }
                }
                ShowResult(inserted, updated, skipped);
            }
            catch (Exception ex) { ShowError(ex); }
        }

    }
}
