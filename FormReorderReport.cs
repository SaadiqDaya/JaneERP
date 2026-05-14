using System.Text;
using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>
    /// Shows products and parts at or below reorder point, with suggested order quantities and estimated costs.
    /// </summary>
    public class FormReorderReport : Form
    {

        private TabControl     tabControl          = new();
        private DataGridView   dgvProducts         = new();
        private DataGridView   dgvParts            = new();
        private Label          lblTotalCost        = new();
        private Button         btnExportCsv        = new();
        private Button         btnPrint            = new();
        private Button         btnCreatePO         = new();
        private Button         btnCreatePOByVendor = new();
        private Button         btnRefresh          = new();
        private ComboBox       cboVendorFilter     = new();

        // Backing data for export
        private List<ProductReorderRow> _productRows = new();
        private List<PartReorderRow>    _partRows    = new();

        public FormReorderReport()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            Load += (_, _) => LoadData();
        }

        private void BuildUI()
        {
            Text          = "Reorder Report — Purchase Suggestions";
            ClientSize    = new Size(900, 600);
            MinimumSize   = new Size(800, 500);
            StartPosition = FormStartPosition.CenterParent;

            // ── Header ────────────────────────────────────────────────────────────
            var pnlHeader = new Panel
            {
                Tag       = "header",
                Dock      = DockStyle.Top,
                Height    = 52,
                BackColor = Color.FromArgb(8, 16, 28)
            };

            var lblTitle = new Label
            {
                Text      = "Reorder Report — Purchase Suggestions",
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                AutoSize  = false,
                Size      = new Size(550, 36),
                Location  = new Point(14, 8),
                BackColor = Color.Transparent
            };

            btnRefresh = new Button
            {
                Text     = "Refresh",
                Size     = new Size(80, 30),
                Location = new Point(796, 11),
                Font     = new Font("Segoe UI", 9F)
            };
            btnRefresh.Click += (_, _) => LoadData();
            Theme.StyleButton(btnRefresh);

            var lblVendor = new Label
            {
                Text      = "Vendor:",
                AutoSize  = true,
                Location  = new Point(564, 18),
                ForeColor = Theme.TextSecondary,
                BackColor = Color.Transparent
            };
            cboVendorFilter.Location      = new Point(618, 12);
            cboVendorFilter.Size          = new Size(168, 26);
            cboVendorFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cboVendorFilter.Font          = new Font("Segoe UI", 9F);
            cboVendorFilter.Items.Add("(All Vendors)");
            cboVendorFilter.SelectedIndex = 0;
            cboVendorFilter.SelectedIndexChanged += (_, _) => ApplyVendorFilter();

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblVendor);
            pnlHeader.Controls.Add(cboVendorFilter);
            pnlHeader.Controls.Add(btnRefresh);
            Controls.Add(pnlHeader);
            Theme.MakeDraggable(this, pnlHeader);

            // ── Tab control ───────────────────────────────────────────────────────
            tabControl = new TabControl
            {
                Location = new Point(10, 62),
                Size     = new Size(880, 460),
                Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font     = new Font("Segoe UI", 9.5F)
            };

            var tabProducts = new TabPage("Products");
            var tabParts    = new TabPage("Parts");

            // Products grid
            dgvProducts = BuildGrid();
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSKU",          HeaderText = "SKU",               DataPropertyName = "SKU",               Width = 90,  ReadOnly = true });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",         HeaderText = "Product Name",       DataPropertyName = "ProductName",       Width = 200, ReadOnly = true });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cStock",        HeaderText = "On Hand",            DataPropertyName = "CurrentStock",      Width = 70,  ReadOnly = true });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cReserved",     HeaderText = "Reserved",           DataPropertyName = "ReservedQty",       Width = 70,  ReadOnly = true });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAvailable",    HeaderText = "Available",          DataPropertyName = "Available",         Width = 75,  ReadOnly = true });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cReorder",      HeaderText = "Reorder Point",      DataPropertyName = "ReorderPoint",      Width = 90,  ReadOnly = true });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cShortfall",    HeaderText = "Shortfall",          DataPropertyName = "Shortfall",         Width = 75,  ReadOnly = true });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSuggestedQty", HeaderText = "Order Qty ✎",        DataPropertyName = "SuggestedQty",      Width = 95,  ReadOnly = false });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cRetail",       HeaderText = "Retail Price",       DataPropertyName = "RetailPriceDisplay", Width = 90, ReadOnly = true });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEstCost",      HeaderText = "Est. Cost",          DataPropertyName = "EstCostDisplay",    Width = 90,  ReadOnly = true });
            tabProducts.Controls.Add(dgvProducts);

            // Parts grid
            dgvParts = BuildGrid();
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPartNo",       HeaderText = "Part Number",    DataPropertyName = "PartNumber",          Width = 110, ReadOnly = true });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPartName",     HeaderText = "Part Name",      DataPropertyName = "PartName",            Width = 180, ReadOnly = true });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cVendor",       HeaderText = "Vendor",         DataPropertyName = "DefaultVendorName",   Width = 130, ReadOnly = true });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cStock",        HeaderText = "Current Stock",  DataPropertyName = "CurrentStock",        Width = 80,  ReadOnly = true });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cReorder",      HeaderText = "Reorder Point",  DataPropertyName = "ReorderPoint",        Width = 80,  ReadOnly = true });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cShortfall",    HeaderText = "Shortfall",      DataPropertyName = "Shortfall",           Width = 70,  ReadOnly = true });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSuggestedQty", HeaderText = "Order Qty ✎",    DataPropertyName = "SuggestedQty",        Width = 90,  ReadOnly = false });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cUnitCost",     HeaderText = "Unit Cost",      DataPropertyName = "UnitCostDisplay",     Width = 80,  ReadOnly = true });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEstCost",      HeaderText = "Est. Cost",      DataPropertyName = "EstCostDisplay",      Width = 80,  ReadOnly = true });
            tabParts.Controls.Add(dgvParts);

            tabControl.TabPages.Add(tabProducts);
            tabControl.TabPages.Add(tabParts);
            tabControl.SelectedIndexChanged += (_, _) => UpdateTotalLabel();
            Controls.Add(tabControl);

            // ── Bottom bar ────────────────────────────────────────────────────────
            var pnlBottom = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 46,
                BackColor = Color.FromArgb(14, 26, 42)
            };

            lblTotalCost = new Label
            {
                AutoSize  = false,
                Size      = new Size(380, 30),
                Location  = new Point(12, 8),
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Text      = "Est. Total Reorder Cost: —"
            };

            btnExportCsv = new Button
            {
                Text     = "Export to CSV",
                Size     = new Size(120, 30),
                Location = new Point(490, 8),
                Font     = new Font("Segoe UI", 9F)
            };
            btnExportCsv.Click += BtnExportCsv_Click;
            Theme.StyleSecondaryButton(btnExportCsv);

            btnPrint = new Button
            {
                Text     = "Print / PDF",
                Size     = new Size(110, 30),
                Location = new Point(618, 8),
                Font     = new Font("Segoe UI", 9F)
            };
            btnPrint.Click += (_, _) =>
            {
                var dgv = tabControl.SelectedIndex == 0 ? dgvProducts : dgvParts;
                string title = tabControl.SelectedIndex == 0
                    ? "Reorder Report — Products"
                    : "Reorder Report — Parts";
                FormReports.PrintGrid(dgv, title, this);
            };
            Theme.StyleSecondaryButton(btnPrint);

            btnCreatePO = new Button
            {
                Text     = "Create PO",
                Size     = new Size(100, 30),
                Location = new Point(618, 8),
                Font     = new Font("Segoe UI", 9F)
            };
            btnCreatePO.Click += BtnCreatePO_Click;
            Theme.StyleButton(btnCreatePO);

            btnCreatePOByVendor = new Button
            {
                Text     = "Create POs by Vendor",
                Size     = new Size(160, 30),
                Location = new Point(726, 8),
                Font     = new Font("Segoe UI", 9F)
            };
            btnCreatePOByVendor.Click += BtnCreatePOsByVendor_Click;
            Theme.StyleSecondaryButton(btnCreatePOByVendor);

            pnlBottom.Controls.Add(lblTotalCost);
            pnlBottom.Controls.Add(btnExportCsv);
            pnlBottom.Controls.Add(btnPrint);
            pnlBottom.Controls.Add(btnCreatePO);
            pnlBottom.Controls.Add(btnCreatePOByVendor);
            Controls.Add(pnlBottom);
        }

        private static DataGridView BuildGrid()
        {
            var dgv = new DataGridView
            {
                Dock                     = DockStyle.Fill,
                ReadOnly                 = false,   // individual columns opt-in to ReadOnly
                AllowUserToAddRows       = false,
                AllowUserToDeleteRows    = false,
                AllowUserToResizeRows    = false,
                SelectionMode            = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect              = false,
                AutoGenerateColumns      = false,
                RowHeadersVisible        = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight      = 30,
                RowTemplate              = { Height = 26 }
            };
            Theme.StyleGrid(dgv);
            return dgv;
        }

        // ── Data loading ──────────────────────────────────────────────────────────

        private void LoadData()
        {
            btnRefresh.Enabled = false;
            try
            {
                LoadProducts();
                LoadParts();
                UpdateTotalLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error loading reorder data:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                btnRefresh.Enabled = true;
            }
        }

        private void LoadProducts()
        {
            _productRows = AppServices.Get<IProductRepository>().GetProductsAtReorderPoint();
            dgvProducts.DataSource = null;
            dgvProducts.DataSource = _productRows;
        }

        private void LoadParts()
        {
            _partRows = AppServices.Get<IPartRepository>().GetPartsAtReorderPoint();

            // Repopulate vendor filter from all active vendors, preserving selection
            var prevVendor = cboVendorFilter.SelectedItem?.ToString();
            cboVendorFilter.Items.Clear();
            cboVendorFilter.Items.Add("(All Vendors)");
            try
            {
                var allVendors = AppServices.Get<IVendorRepository>().GetAll()
                    .OrderBy(v => v.VendorName)
                    .ToList();
                foreach (var v in allVendors)
                    cboVendorFilter.Items.Add(v.VendorName);
            }
            catch
            {
                // Fallback: derive vendor names from part rows if vendor repo unavailable
                foreach (var name in _partRows
                    .Where(r => !string.IsNullOrWhiteSpace(r.DefaultVendorName))
                    .Select(r => r.DefaultVendorName!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n))
                {
                    cboVendorFilter.Items.Add(name);
                }
            }
            var prevIdx = cboVendorFilter.Items.IndexOf(prevVendor ?? "");
            cboVendorFilter.SelectedIndex = prevIdx >= 0 ? prevIdx : 0;

            ApplyVendorFilter();
        }

        private void ApplyVendorFilter()
        {
            var vendor = cboVendorFilter.SelectedItem?.ToString();
            var filtered = (vendor == null || vendor == "(All Vendors)")
                ? _partRows
                : _partRows.Where(r => string.Equals(r.DefaultVendorName, vendor,
                                       StringComparison.OrdinalIgnoreCase)).ToList();
            dgvParts.DataSource = null;
            dgvParts.DataSource = filtered;
            UpdateTotalLabel();
        }

        private void UpdateTotalLabel()
        {
            decimal total;
            if (tabControl.SelectedIndex == 0)
                total = _productRows.Sum(r => r.EstCost);
            else
                total = _partRows.Sum(r => r.EstCost);

            lblTotalCost.Text = $"Est. Total Reorder Cost: {total:C}";
        }

        // ── Export ────────────────────────────────────────────────────────────────

        private void BtnExportCsv_Click(object? sender, EventArgs e)
        {
            using var dlg = new SaveFileDialog
            {
                Title      = "Export to CSV",
                Filter     = "CSV files (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName   = tabControl.SelectedIndex == 0
                    ? $"ReorderReport_Products_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                    : $"ReorderReport_Parts_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var sb = new StringBuilder();
                if (tabControl.SelectedIndex == 0)
                {
                    sb.AppendLine("SKU,Product Name,On Hand,Reserved,Available,Reorder Point,Shortfall,Suggested Qty,Retail Price,Est. Cost");
                    foreach (var r in _productRows)
                        sb.AppendLine($"\"{r.SKU}\",\"{r.ProductName}\",{r.CurrentStock},{r.ReservedQty},{r.Available},{r.ReorderPoint},{r.Shortfall},{r.SuggestedQty},{r.RetailPrice:F2},{r.EstCost:F2}");
                }
                else
                {
                    sb.AppendLine("Part Number,Part Name,Current Stock,Reorder Point,Shortfall,Suggested Qty,Unit Cost,Est. Cost");
                    foreach (var r in _partRows)
                        sb.AppendLine($"\"{r.PartNumber}\",\"{r.PartName}\",{r.CurrentStock},{r.ReorderPoint},{r.Shortfall},{r.SuggestedQty},{r.UnitCost:F2},{r.EstCost:F2}");
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show(this, $"Exported successfully to:\n{dlg.FileName}",
                    "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Export failed:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnCreatePO_Click(object? sender, EventArgs e)
        {
            // Commit any in-progress cell edit before reading values
            dgvProducts.EndEdit();
            dgvParts.EndEdit();

            // Build pre-populated items reading Order Qty from the grid (respects user edits)
            var items = new List<JaneERP.Models.PurchaseOrderItem>();

            if (tabControl.SelectedIndex == 0)
            {
                for (int i = 0; i < dgvProducts.Rows.Count && i < _productRows.Count; i++)
                {
                    if (!int.TryParse(dgvProducts.Rows[i].Cells["cSuggestedQty"].Value?.ToString(), out int qty) || qty <= 0)
                        continue;
                    var r = _productRows[i];
                    items.Add(new JaneERP.Models.PurchaseOrderItem
                    {
                        SKU             = r.SKU,
                        ItemName        = r.ProductName,
                        QuantityOrdered = qty,
                        UnitCost        = r.WholesalePrice
                    });
                }
            }
            else
            {
                // Read from the grid's current (possibly filtered) data source
                var visibleRows = (dgvParts.DataSource as List<PartReorderRow>) ?? _partRows;
                for (int i = 0; i < dgvParts.Rows.Count && i < visibleRows.Count; i++)
                {
                    if (!int.TryParse(dgvParts.Rows[i].Cells["cSuggestedQty"].Value?.ToString(), out int qty) || qty <= 0)
                        continue;
                    var r = visibleRows[i];
                    items.Add(new JaneERP.Models.PurchaseOrderItem
                    {
                        SKU             = r.PartNumber,
                        ItemName        = r.PartName,
                        QuantityOrdered = qty,
                        UnitCost        = r.UnitCost
                    });
                }
            }

            if (items.Count == 0)
            {
                MessageBox.Show(this, "No items with a quantity > 0 on the current tab.",
                    "Nothing to Order", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var repo = new JaneERP.Data.SupplierRepository();
            using var frm = new FormCreatePO(repo, prePopulateItems: items);
            frm.ShowDialog(this);
        }

        private void BtnCreatePOsByVendor_Click(object? sender, EventArgs e)
        {
            try
            {
                // Only works for Parts (which have vendor info)
                if (tabControl.SelectedIndex != 1)
                {
                    MessageBox.Show(this,
                        "Create POs by Vendor is only available on the Parts tab.",
                        "Parts Tab Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                dgvParts.EndEdit();
                var visibleRows = (dgvParts.DataSource as List<PartReorderRow>) ?? _partRows;

                // Build items from grid (respects user edits to Suggested Qty)
                var allItems = new List<(string vendor, JaneERP.Models.PurchaseOrderItem item)>();
                for (int i = 0; i < dgvParts.Rows.Count && i < visibleRows.Count; i++)
                {
                    if (!int.TryParse(dgvParts.Rows[i].Cells["cSuggestedQty"].Value?.ToString(), out int qty) || qty <= 0)
                        continue;
                    var r = visibleRows[i];
                    allItems.Add((r.DefaultVendorName ?? "(No Vendor)", new JaneERP.Models.PurchaseOrderItem
                    {
                        SKU             = r.PartNumber,
                        ItemName        = r.PartName,
                        QuantityOrdered = qty,
                        UnitCost        = r.UnitCost
                    }));
                }

                if (allItems.Count == 0)
                {
                    MessageBox.Show(this, "No parts with a quantity > 0.",
                        "Nothing to Order", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Group by vendor and open one PO form per vendor
                var byVendor = allItems
                    .GroupBy(x => x.vendor, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var repo = new JaneERP.Data.SupplierRepository();
                int created = 0;
                foreach (var group in byVendor)
                {
                    var vendorName  = group.Key;
                    var vendorItems = group.Select(x => x.item).ToList();
                    using var frm = new FormCreatePO(repo, prePopulateItems: vendorItems,
                                                    preselectedSupplierName: vendorName);
                    frm.Text = $"New PO — {vendorName}";
                    frm.ShowDialog(this);
                    created++;
                }

                MessageBox.Show(this,
                    $"Opened {created} PO form(s) — one per vendor.",
                    "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logging.AppLogger.Error($"[CreatePOByVendor] {ex}");
                MessageBox.Show($"Failed to create PO: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

}
