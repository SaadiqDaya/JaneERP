using CsvHelper;
using CsvHelper.Configuration;
using JaneERP.Data;
using JaneERP.Logging;
using JaneERP.Models;
using JaneERP.Security;
using System.Globalization;

namespace JaneERP
{
    public partial class InventoryDashboard : Form
    {
        public InventoryDashboard()
        {
            InitializeComponent();
        }

        // Full unfiltered product list — search filters in-memory against this
        private List<Product> _allProducts = [];

        // Class-level repos — avoid creating a new instance on every selection change
        private readonly ProductRepository _repo    = new();
        private readonly Data.PartRepository _partRepo = new();

        // 200 ms debounce so rapid keyboard/arrow navigation doesn't hammer the DB
        private readonly System.Windows.Forms.Timer _selectionDebounce =
            new System.Windows.Forms.Timer { Interval = 200 };

        private void ScheduleSelectionRefresh()
        {
            _selectionDebounce.Stop();
            _selectionDebounce.Start();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.MakeDraggable(this, lblHeader);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            ApplyPermissions();
            txtSearch.PlaceholderText = "Search SKU or Name…";
            txtSearch.TextChanged    += TxtSearch_TextChanged;
            try { LoadProducts(); }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error loading products",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // Double-click opens the edit dialog
            dgvProducts.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (dgvProducts.Rows[e.RowIndex].DataBoundItem is not Product p) return;
                int id = p.ProductID;
                using var frm = new FormAddProduct(p);
                if (frm.ShowDialog(this) == DialogResult.OK)
                {
                    try { LoadProducts(); SelectProductById(id); }
                    catch { }
                }
            };

            // Wire debounced selection refresh
            _selectionDebounce.Tick += (_, _) => { _selectionDebounce.Stop(); RefreshDetailsPanel(); };
            dgvProducts.SelectionChanged += (_, _) => ScheduleSelectionRefresh();

            ConfigureHistoryGrid();
            ConfigureProductGrid();

            // Configure the details DataGridView columns once
            dgvDetails.AutoGenerateColumns = false;
            dgvDetails.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colProperty", HeaderText = "Property", Width = 130, ReadOnly = true
            });
            dgvDetails.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colValue", HeaderText = "Value",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true
            });

            // Row coloring for low/out-of-stock
            dgvProducts.RowPrePaint += DgvProducts_RowPrePaint;
        }

        // Configure product grid columns — hide raw ID columns, show joined name columns
        private bool _productGridConfigured = false;
        private void ConfigureProductGrid()
        {
            dgvProducts.AutoGenerateColumns = true; // allow auto-gen initially
            dgvProducts.DataBindingComplete -= DgvProductGrid_DataBindingComplete;
            dgvProducts.DataBindingComplete += DgvProductGrid_DataBindingComplete;
        }

        private void DgvProductGrid_DataBindingComplete(object? sender, DataGridViewBindingCompleteEventArgs e)
        {
            if (_productGridConfigured) return;
            _productGridConfigured = true;

            // Hide raw ID and internal columns
            foreach (string col in new[] { "ProductID", "DefaultLocationID", "ProductTypeID",
                                           "DefaultVendorID", "Attributes", "IsActive" })
                if (dgvProducts.Columns.Contains(col))
                    dgvProducts.Columns[col]!.Visible = false;

            // Rename/reorder visible columns
            void Rename(string name, string header, int w = 0)
            {
                if (!dgvProducts.Columns.Contains(name)) return;
                dgvProducts.Columns[name]!.HeaderText = header;
                if (w > 0) dgvProducts.Columns[name]!.Width = w;
            }

            Rename("SKU",                 "SKU",         90);
            Rename("ProductName",         "Product",     200);
            Rename("CurrentStock",        "Stock",        60);
            Rename("ReorderPoint",        "Reorder",      75);
            Rename("OrderUpTo",           "Order To",     75);
            Rename("RetailPrice",         "Retail",       75);
            Rename("WholesalePrice",      "Wholesale",    80);
            Rename("ProductTypeName",     "Type",        100);
            Rename("DefaultLocationName", "Location",    110);
            Rename("DefaultVendorName",   "Vendor",      110);
        }

        // One-time column visibility setup for the transaction history grid
        private void ConfigureHistoryGrid()
        {
            // Columns only exist after DataSource is set — hook DataBindingComplete instead
            dgvHistory.DataBindingComplete -= DgvHistory_DataBindingComplete;
            dgvHistory.DataBindingComplete += DgvHistory_DataBindingComplete;
        }

        private bool _historyGridConfigured = false;
        private void DgvHistory_DataBindingComplete(object? sender, DataGridViewBindingCompleteEventArgs e)
        {
            if (_historyGridConfigured) return;
            _historyGridConfigured = true;

            // Hide raw IDs and the redundant DisplayType column
            foreach (string col in new[] { "ProductID", "TransactionID", "LocationID", "StoreID", "DisplayType" })
                if (dgvHistory.Columns.Contains(col) && dgvHistory.Columns[col] != null)
                    dgvHistory.Columns[col]!.Visible = false;

            // Rename and size the visible columns
            void RenameH(string name, string header, int w = 0)
            {
                if (!dgvHistory.Columns.Contains(name)) return;
                dgvHistory.Columns[name]!.HeaderText = header;
                if (w > 0) dgvHistory.Columns[name]!.Width = w;
            }
            RenameH("TransactionDate",  "Date",          120);
            RenameH("QuantityChange",   "Qty",            50);
            RenameH("TransactionType",  "Type",           90);
            RenameH("Notes",            "Notes",         160);
            RenameH("LocationName",     "Location",      110);
            RenameH("StoreName",        "Store",          90);
            RenameH("LotNumber",        "Lot #",          70);
            RenameH("ExpirationDate",   "Expiry",         90);

            // Mark transactions with no store as "Manual"
            dgvHistory.CellFormatting -= DgvHistory_CellFormatting;
            dgvHistory.CellFormatting += DgvHistory_CellFormatting;
        }

        private void DgvHistory_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgvHistory.Columns["StoreName"] is DataGridViewColumn storeCol &&
                e.ColumnIndex == storeCol.Index &&
                (e.Value == null || string.IsNullOrEmpty(e.Value.ToString())))
            {
                e.Value = "Manual";
                e.FormattingApplied = true;
            }
        }

        private void TxtSearch_TextChanged(object? sender, EventArgs e)
        {
            string term = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(term))
            {
                dgvProducts.DataSource = _allProducts;
                return;
            }
            dgvProducts.DataSource = _allProducts
                .Where(p => (p.SKU         ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)
                         || (p.ProductName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private void ApplyPermissions()
        {
            bool canEdit = PermissionHelper.CanEdit("Inventory");
            btnAdd.Enabled         = canEdit;
            btnEdit.Enabled        = canEdit;
            btnDeactivate.Enabled  = canEdit;
            btnAdjustStock.Enabled = canEdit;
            btnTransfer.Enabled    = canEdit;
            btnImportCSV.Enabled   = canEdit;
            btnExportCSV.Enabled   = true; // export is read-only, visible to all
            // Locations visible to all, editing within that form is unrestricted (admin task)
            btnLocations.Enabled   = true;
        }

        private void btnLocations_Click(object sender, EventArgs e)
        {
            using var frm = new FormLocationManager();
            frm.ShowDialog(this);
        }

        private void LoadProducts()
        {
            _allProducts           = new ProductRepository().GetProducts(chkShowInactive.Checked).ToList();
            dgvProducts.DataSource = _allProducts;
            // Re-apply any active search filter
            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
                TxtSearch_TextChanged(null, EventArgs.Empty);
            UpdateInventorySummary();
        }

        private void UpdateInventorySummary()
        {
            int  totalProducts  = _allProducts.Count;
            long totalUnits     = _allProducts.Sum(p => (long)p.CurrentStock);
            decimal estValue    = _allProducts.Sum(p => p.CurrentStock * p.WholesalePrice);
            int  outOfStock     = _allProducts.Count(p => p.CurrentStock <= 0);
            int  lowStock       = _allProducts.Count(p => p.ReorderPoint > 0 && p.CurrentStock > 0 && p.CurrentStock <= p.ReorderPoint);
            int  noBOM          = _repo.CountProductsWithNoBOM();

            lblInventorySummary.Text =
                $"{totalProducts} products  ·  {totalUnits:N0} units  ·  Est. value: {estValue:C0}" +
                (outOfStock > 0 ? $"  ·  ⚠ {outOfStock} out of stock" : "") +
                (lowStock   > 0 ? $"  ·  ↓ {lowStock} low stock"      : "") +
                (noBOM      > 0 ? $"  ·  ⚠ {noBOM} have no BOM"       : "");
        }

        // Re-selects a product row by ID after a grid refresh
        private void SelectProductById(int productId)
        {
            foreach (DataGridViewRow row in dgvProducts.Rows)
            {
                if (row.DataBoundItem is Product p && p.ProductID == productId)
                {
                    row.Selected = true;
                    dgvProducts.FirstDisplayedScrollingRowIndex = row.Index;
                    return;
                }
            }
        }

        private Product? GetSelectedProduct()
        {
            if (dgvProducts.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a product first.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            return dgvProducts.SelectedRows[0].DataBoundItem as Product;
        }

        // ── Selection changed: debounced via ScheduleSelectionRefresh ───────────
        private void dgvProducts_SelectionChanged(object sender, EventArgs e)
            => ScheduleSelectionRefresh();

        private void RefreshDetailsPanel()
        {
            dgvDetails.Rows.Clear();
            dgvHistory.DataSource = null;

            if (dgvProducts.SelectedRows.Count == 0) return;
            if (dgvProducts.SelectedRows[0].DataBoundItem is not Product product) return;

            void AddRow(string prop, string val) => dgvDetails.Rows.Add(prop, val);

            AddRow("SKU",         product.SKU          ?? "—");
            AddRow("Name",        product.ProductName  ?? "—");
            AddRow("Retail",      product.RetailPrice.ToString("C"));
            AddRow("Wholesale",   product.WholesalePrice.ToString("C"));
            AddRow("Stock",       product.CurrentStock.ToString());
            AddRow("Reorder At",  product.ReorderPoint.ToString());
            AddRow("Order Up To", product.OrderUpTo > 0 ? product.OrderUpTo.ToString() : "—");
            if (!string.IsNullOrEmpty(product.DefaultLocationName))
                AddRow("Location",    product.DefaultLocationName);
            if (!string.IsNullOrEmpty(product.ProductTypeName))
                AddRow("Type",        product.ProductTypeName);
            if (!string.IsNullOrEmpty(product.DefaultVendorName))
                AddRow("Vendor",      product.DefaultVendorName);

            try
            {
                var attrs = _repo.GetAttributes(product.ProductID).ToList();
                if (attrs.Any())
                {
                    dgvDetails.Rows.Add("── Attributes ──", "");
                    foreach (var attr in attrs)
                        AddRow(attr.AttributeName, attr.AttributeValue ?? "");
                }

                try
                {
                    var bom = _partRepo.GetBom(product.ProductID);
                    if (bom.Count > 0)
                    {
                        dgvDetails.Rows.Add("── BOM ──", "");
                        foreach (var entry in bom)
                            AddRow($"  {entry.PartNumber}", $"{entry.PartName} × {entry.Quantity}");
                    }
                }
                catch { /* BOM optional */ }

                try
                {
                    var pkgRepo = new Data.PackageRepository();
                    var components = pkgRepo.GetComponents(product.ProductID);
                    if (components.Count > 0)
                    {
                        dgvDetails.Rows.Add("── Package Contents ──", "");
                        foreach (var c in components)
                            AddRow($"  {c.ComponentSKU}", $"{c.ComponentName} × {c.Quantity}");
                    }
                }
                catch { }

                var transactions = _repo.GetTransactions(product.ProductID).ToList();
                dgvHistory.DataSource = transactions;
            }
            catch { dgvDetails.Rows.Add("Error", "Could not load details"); }
        }

        private void DgvProducts_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvProducts.Rows.Count) return;
            if (dgvProducts.Rows[e.RowIndex].DataBoundItem is not Product p) return;

            var row = dgvProducts.Rows[e.RowIndex];
            if (p.CurrentStock <= 0)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 200, 200); // red
                row.DefaultCellStyle.ForeColor = Color.DarkRed;
            }
            else if (p.ReorderPoint > 0 && p.CurrentStock <= p.ReorderPoint)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 240, 180); // amber
                row.DefaultCellStyle.ForeColor = Color.DarkGoldenrod;
            }
            else
            {
                row.DefaultCellStyle.BackColor = Color.Empty;
                row.DefaultCellStyle.ForeColor = Color.Empty;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F5:
                    try { LoadProducts(); } catch { }
                    return true;
                case Keys.Escape:
                    Close();
                    return true;
                case Keys.Control | Keys.N:
                    btnAdd_Click(this, EventArgs.Empty);
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ── Toolbar ───────────────────────────────────────────────────────────────
        private void chkShowInactive_CheckedChanged(object sender, EventArgs e)
        {
            if (chkShowInactive.Checked)
            {
                btnDeactivate.Text = "Restore Product";
                dgvProducts.DefaultCellStyle.ForeColor                = Color.Gray;
                dgvProducts.DefaultCellStyle.BackColor                = Color.WhiteSmoke;
                dgvProducts.AlternatingRowsDefaultCellStyle.BackColor = Color.Gainsboro;
            }
            else
            {
                btnDeactivate.Text = "Deactivate Selected";
                dgvProducts.DefaultCellStyle.ForeColor                = SystemColors.ControlText;
                dgvProducts.DefaultCellStyle.BackColor                = SystemColors.Window;
                dgvProducts.AlternatingRowsDefaultCellStyle.BackColor = Color.Empty;
            }

            try { LoadProducts(); }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error loading products",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            try { LoadProducts(); }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error loading products",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            using var form = new FormAddProduct();
            if (form.ShowDialog() == DialogResult.OK)
            {
                try { LoadProducts(); }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error refreshing products",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            var product = GetSelectedProduct();
            if (product is null) return;

            int id = product.ProductID;
            using var form = new FormAddProduct(product);
            if (form.ShowDialog() == DialogResult.OK)
            {
                try { LoadProducts(); SelectProductById(id); }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error refreshing products",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnTransfer_Click(object sender, EventArgs e)
        {
            var product = GetSelectedProduct();
            if (product is null) return;
            using var frm = new FormStockTransfer(product);
            if (frm.ShowDialog(this) == DialogResult.OK)
            {
                try { LoadProducts(); SelectProductById(product.ProductID); }
                catch (Exception ex) { AppLogger.Info($"InventoryDashboard.btnTransfer_Click: {ex.Message}"); }
            }
        }

        private void btnAdjustStock_Click(object sender, EventArgs e)
        {
            var product = GetSelectedProduct();
            if (product is null) return;

            int id = product.ProductID;
            using var form = new FormAdjustStock(product);
            if (form.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    LoadProducts();
                    SelectProductById(id); // re-selects row → triggers SelectionChanged → refreshes history
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error refreshing products",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnDeactivate_Click(object sender, EventArgs e)
        {
            var product = GetSelectedProduct();
            if (product is null) return;

            var repo = new ProductRepository();

            if (chkShowInactive.Checked)
            {
                if (MessageBox.Show(
                        $"Restore '{product.ProductName}' as an active product?",
                        "Confirm Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                    != DialogResult.Yes) return;

                try { repo.RestoreProduct(product.ProductID); LoadProducts(); }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error restoring product",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                if (MessageBox.Show(
                        $"Are you sure you want to deactivate '{product.ProductName}'?",
                        "Confirm Deactivate", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                    != DialogResult.Yes) return;

                try { repo.DeactivateProduct(product.ProductID); LoadProducts(); }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error deactivating product",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ── CSV Import ────────────────────────────────────────────────────────────
        private void btnImportCSV_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title  = "Select a CSV file",
                Filter = "CSV Files (*.csv)|*.csv"
            };
            if (dialog.ShowDialog() != DialogResult.OK) return;

            try
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord   = true,
                    MissingFieldFound = null,
                    BadDataFound      = null,
                };

                var products = new List<Product>();
                int skipped  = 0;

                using var reader = new StreamReader(dialog.FileName);
                using var csv    = new CsvReader(reader, config);

                csv.Read();
                csv.ReadHeader();

                var headers  = csv.HeaderRecord ?? [];
                var required = new[] { "SKU", "ProductName" };
                var missing  = required
                    .Where(r => !headers.Any(h =>
                        h.Equals(r, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (missing.Count > 0)
                {
                    MessageBox.Show(
                        $"CSV is missing required column(s): {string.Join(", ", missing)}",
                        "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var knownColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "SKU", "ProductName", "RetailPrice", "WholesalePrice", "CurrentStock" };

                var extraHeaders = headers
                    .Where(h => !knownColumns.Contains(h))
                    .ToList();

                while (csv.Read())
                {
                    string sku         = csv.GetField<string>("SKU")         ?? string.Empty;
                    string productName = csv.GetField<string>("ProductName") ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(productName))
                    {
                        skipped++;
                        continue;
                    }

                    decimal.TryParse(csv.GetField<string>("RetailPrice"),    out decimal price);
                    decimal.TryParse(csv.GetField<string>("WholesalePrice"), out decimal wholesale);
                    int.TryParse(    csv.GetField<string>("CurrentStock"),   out int     stock);

                    var product = new Product
                    {
                        SKU            = sku.Trim(),
                        ProductName    = productName.Trim(),
                        RetailPrice    = price,
                        WholesalePrice = wholesale,
                        CurrentStock   = stock,
                        IsActive       = true
                    };

                    foreach (string header in extraHeaders)
                    {
                        string val = csv.GetField<string>(header) ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(val))
                            product.Attributes.Add(new ProductAttribute
                            {
                                AttributeName  = header,
                                AttributeValue = val.Trim()
                            });
                    }

                    products.Add(product);
                }

                if (products.Count == 0)
                {
                    MessageBox.Show("No valid products found in the CSV file.",
                        "Nothing Imported", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var (ins, upd) = new ProductRepository().UpsertProducts(products);
                LoadProducts();

                string msg = $"Import complete: {ins} new, {upd} updated.";
                if (skipped > 0)        msg += $"\n{skipped} row(s) skipped (missing SKU or Name).";
                if (extraHeaders.Any()) msg += $"\nExtra columns saved as attributes: {string.Join(", ", extraHeaders)}";

                MessageBox.Show(msg, "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Import Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── CSV Export ────────────────────────────────────────────────────────────
        private void btnExportCSV_Click(object sender, EventArgs e)
        {
            using var dlg = new SaveFileDialog
            {
                Title    = "Export Inventory to CSV",
                Filter   = "CSV Files (*.csv)|*.csv",
                FileName = $"inventory_{DateTime.Today:yyyyMMdd}.csv"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var repo         = new ProductRepository();
                var typeRepo     = new Data.ProductTypeRepository();
                var products     = repo.GetProducts(chkShowInactive.Checked).ToList();
                var typeMap      = typeRepo.GetAll().ToDictionary(t => t.ProductTypeID, t => t.TypeName);
                var allAttrNames = repo.GetAllAttributeNames().ToList();

                using var writer = new StreamWriter(dlg.FileName, false, System.Text.Encoding.UTF8);

                // Header
                var header = new List<string>
                    { "SKU", "ProductName", "ProductType", "RetailPrice", "WholesalePrice", "CurrentStock" };
                header.AddRange(allAttrNames);
                writer.WriteLine(string.Join(",", header.Select(CsvQuote)));

                // Rows
                foreach (var p in products)
                {
                    var attrs    = repo.GetAttributes(p.ProductID)
                                       .ToDictionary(a => a.AttributeName, a => a.AttributeValue ?? "",
                                                     StringComparer.OrdinalIgnoreCase);
                    var typeName = (p.ProductTypeID.HasValue && typeMap.TryGetValue(p.ProductTypeID.Value, out var tn))
                                   ? tn : "";

                    var row = new List<string>
                    {
                        CsvQuote(p.SKU),
                        CsvQuote(p.ProductName),
                        CsvQuote(typeName),
                        p.RetailPrice.ToString(CultureInfo.InvariantCulture),
                        p.WholesalePrice.ToString(CultureInfo.InvariantCulture),
                        p.CurrentStock.ToString()
                    };
                    foreach (var attr in allAttrNames)
                        row.Add(CsvQuote(attrs.TryGetValue(attr, out var v) ? v : ""));

                    writer.WriteLine(string.Join(",", row));
                }

                MessageBox.Show($"Exported {products.Count} product(s) to:\n{dlg.FileName}",
                    "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string CsvQuote(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}
