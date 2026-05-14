using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using JaneERP.Data;
using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Logging;
using JaneERP.Models;
using JaneERP.Security;

namespace JaneERP
{
    public partial class FormAddProduct : Form
    {
        private readonly Product?        _editingProduct;
        private readonly IUomRepository  _uomRepo = AppServices.Get<IUomRepository>();

        public FormAddProduct() : this(null) { }

        /// <param name="product">Existing product to edit, or null to create.</param>
        /// <param name="presetPackage">When true and creating new, pre-selects "Package Bundle".</param>
        public FormAddProduct(Product? product, bool presetPackage = false)
        {
            InitializeComponent();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            _editingProduct = product;

            LoadLocations();
            LoadProductTypes();
            LoadVendors();
            LoadUom();
            LoadAttributeNames();

            // Source type wiring — load dependent dropdowns and wire change event
            LoadLinkedParts();
            LoadLinkedBOMs();
            cboSourceType.SelectedIndex = 0; // default to BOM
            cboSourceType.SelectedIndexChanged += (_, _) => UpdateSourcePanelVisibility();
            UpdateSourcePanelVisibility();

            // Pre-select Package source when called from Package Explorer
            if (presetPackage && _editingProduct is null)
                cboSourceType.SelectedItem = "Package";

            if (_editingProduct is not null)
            {
                Text         = "Edit Product";
                btnSave.Text = "Update Product";

                txtSKU.Text              = product!.SKU;
                txtProductName.Text      = product.ProductName;
                txtPrice.Text            = product.RetailPrice.ToString("G");
                txtWholesalePrice.Text   = product.WholesalePrice.ToString("G");
                nudReorderPoint.Value    = product.ReorderPoint;
                nudOrderUpTo.Value       = product.OrderUpTo;
                cboUom.Text              = product.UnitOfMeasure ?? "";

                // Pre-select the product's default location
                if (product.DefaultLocationID.HasValue)
                {
                    foreach (Location loc in cboDefaultLocation.Items)
                    {
                        if (loc.LocationID == product.DefaultLocationID.Value)
                        {
                            cboDefaultLocation.SelectedItem = loc;
                            break;
                        }
                    }
                }

                // Pre-select the product's default vendor
                if (product.DefaultVendorID.HasValue)
                {
                    foreach (Vendor v in cboVendor.Items)
                    {
                        if (v is Vendor vendor && vendor.VendorID == product.DefaultVendorID.Value)
                        {
                            cboVendor.SelectedItem = v;
                            break;
                        }
                    }
                }

                // Stock is managed exclusively via Adjust Stock — hide in edit mode
                lblStock.Visible = false;
                txtStock.Visible = false;

                // Load saved attributes FIRST — before setting the product type.
                // Setting the type fires SelectedIndexChanged which pre-fills template rows;
                // if the saved attrs are already in the grid the duplicate-check skips them.
                try
                {
                    var col   = dgvAttributes.Columns["colProperty"] as DataGridViewComboBoxColumn;
                    if (col != null)
                    {
                        var attrs = AppServices.Get<IProductRepository>().GetAttributes(product.ProductID);
                        foreach (var attr in attrs)
                        {
                            if (!col.Items.Contains(attr.AttributeName))
                                col.Items.Add(attr.AttributeName);
                            dgvAttributes.Rows.Add(attr.AttributeName, attr.AttributeValue);
                        }
                    }
                }
                catch (Exception ex) { AppLogger.Info($"[FormAddProduct.FormAddProduct]: {ex.Message}"); }

                // Now set the product type — SelectedIndexChanged fires but existingNames
                // already contains the loaded attrs, so no duplicates are added.
                if (product.ProductTypeID.HasValue)
                {
                    foreach (Models.ProductType pt in cboProductType.Items)
                    {
                        if (pt.ProductTypeID == product.ProductTypeID.Value)
                        {
                            cboProductType.SelectedItem = pt;
                            break;
                        }
                    }
                }

                // Detect source type from saved data after form loads
                this.Load += (_, _) => LoadSourceFromProductData();
            }
        }

        private void LoadSourceFromProductData()
        {
            if (_editingProduct == null) return;
            try
            {
                // Check for package components first
                var components = new Data.PackageRepository().GetComponents(_editingProduct.ProductID);
                if (components.Count > 0)
                {
                    cboSourceType.SelectedItem = "Package"; // triggers UpdateSourcePanelVisibility
                    EnsurePackageProductsLoaded();
                    dgvPackageComponents.Rows.Clear();
                    foreach (var c in components)
                    {
                        var skuCol = dgvPackageComponents.Columns["colPkgSKU"] as DataGridViewComboBoxColumn;
                        if (skuCol != null && !skuCol.Items.Contains(c.ComponentSKU))
                            skuCol.Items.Add(c.ComponentSKU);
                        dgvPackageComponents.Rows.Add(c.ComponentSKU, c.ComponentName, c.Quantity.ToString(), c.Notes ?? string.Empty);
                    }
                    return;
                }

                // Check BOM entries to determine source mode
                var bom = AppServices.Get<IPartRepository>().GetBom(_editingProduct.ProductID);
                if (bom.Count == 1)
                {
                    // Single-entry BOM → Part mode, pre-select that part
                    cboSourceType.SelectedItem = "Part"; // triggers UpdateSourcePanelVisibility
                    foreach (var item in cboLinkedPart.Items)
                    {
                        if (item is Part p && p.PartID == bom[0].PartID)
                        {
                            cboLinkedPart.SelectedItem = item;
                            break;
                        }
                    }
                }
                else if (bom.Count > 1)
                {
                    // Multi-entry BOM → BOM mode, pre-select this product's own BOM entry
                    cboSourceType.SelectedItem = "BOM"; // triggers UpdateSourcePanelVisibility
                    foreach (var item in cboLinkedBOM.Items)
                    {
                        if (item is BomChoice choice && choice.SourceProductID == _editingProduct.ProductID)
                        {
                            cboLinkedBOM.SelectedItem = item;
                            break;
                        }
                    }
                }
                // else: no BOM yet → BOM mode with "(New / Custom)" selected (default)
            }
            catch (Exception ex) { AppLogger.Info($"[FormAddProduct.LoadSourceFromProductData]: {ex.Message}"); }
        }

        // All products for Package SKU picker — loaded once
        private List<Product> _allProductsForPkg = new();

        private void LoadProductTypes()
        {
            try
            {
                var types = new Data.ProductTypeRepository().GetAll();
                // Add a blank first item so "no type" is selectable
                cboProductType.Items.Clear();
                cboProductType.Items.Add(new Models.ProductType { ProductTypeID = 0, TypeName = "(None)" });
                foreach (var t in types) cboProductType.Items.Add(t);
                cboProductType.SelectedIndex = 0;
                cboProductType.SelectedIndexChanged += CboProductType_SelectedIndexChanged;
            }
            catch (Exception ex) { AppLogger.Info($"[FormAddProduct.LoadProductTypes]: {ex.Message}"); }
        }

        private void EnsurePackageProductsLoaded()
        {
            if (_allProductsForPkg.Count > 0) return;
            try
            {
                _allProductsForPkg = AppServices.Get<IProductRepository>().GetProducts().ToList();
                var col = dgvPackageComponents.Columns["colPkgSKU"] as DataGridViewComboBoxColumn;
                if (col == null) return;
                col.Items.Clear();
                foreach (var p in _allProductsForPkg)
                    col.Items.Add(p.SKU);
            }
            catch (Exception ex) { AppLogger.Info($"[FormAddProduct.EnsurePackageProductsLoaded]: {ex.Message}"); }
        }

        private void LoadLinkedParts()
        {
            try
            {
                var parts = AppServices.Get<IPartRepository>().GetAll();
                cboLinkedPart.Items.Clear();
                foreach (var p in parts)
                    cboLinkedPart.Items.Add(p);
                if (cboLinkedPart.Items.Count > 0)
                    cboLinkedPart.SelectedIndex = 0;
            }
            catch (Exception ex) { AppLogger.Info($"[FormAddProduct.LoadLinkedParts]: {ex.Message}"); }
        }

        private void LoadLinkedBOMs()
        {
            try
            {
                cboLinkedBOM.Items.Clear();
                cboLinkedBOM.Items.Add(new BomChoice { SourceProductID = 0, Display = "(New / Custom)" });
                var boms = AppServices.Get<IPartRepository>().GetProductsWithBoms();
                foreach (var b in boms)
                {
                    string display = !string.IsNullOrWhiteSpace(b.BomNumber)
                        ? $"{b.BomNumber} — {b.ProductName} ({b.PartCount} parts)"
                        : $"{b.ProductName} ({b.PartCount} parts)";
                    cboLinkedBOM.Items.Add(new BomChoice { SourceProductID = b.ProductID, Display = display });
                }
                cboLinkedBOM.SelectedIndex = 0;
            }
            catch (Exception ex) { AppLogger.Info($"[FormAddProduct.LoadLinkedBOMs]: {ex.Message}"); }
        }

        private sealed class BomChoice
        {
            public int    SourceProductID { get; init; }
            public string Display         { get; init; } = "";
            public override string ToString() => Display;
        }

        private void SaveLinkedPart(int productId, int partId)
        {
            AppServices.Get<IPartRepository>().SetBom(productId,
                new[] { (partId, 1m, false, 0m) });
        }

        private bool IsPackageTypeSelected()
        {
            return cboProductType.SelectedItem is Models.ProductType pt
                && pt.TypeName.Equals("Package", StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateSourcePanelVisibility()
        {
            string source  = cboSourceType.SelectedItem?.ToString() ?? "BOM";
            bool isPackage = source == "Package";
            bool isPart    = source == "Part";
            bool isBom     = source == "BOM";

            pnlPackage.Visible    = isPackage;
            lblLinkedBOM.Visible  = isBom;
            cboLinkedBOM.Visible  = isBom;
            lblLinkedPart.Visible = isPart;
            cboLinkedPart.Visible = isPart;
            btnManageBOM.Visible  = isBom;

            if (isPackage)
            {
                EnsurePackageProductsLoaded();
                dgvPackageComponents.CellEndEdit -= DgvPackageComponents_CellEndEdit;
                dgvPackageComponents.CellEndEdit += DgvPackageComponents_CellEndEdit;
                dgvPackageComponents.DataError   -= DgvPackageComponents_DataError;
                dgvPackageComponents.DataError   += DgvPackageComponents_DataError;
            }

            // Position buttons below the attributes grid (or package panel for Package mode)
            int btnY;
            if (isPackage)
                btnY = pnlPackage.Bottom + 10;
            else
                btnY = 654; // below attributes grid (y=496 h=148 → bottom=644 + 10)

            btnManageBOM.Location = new Point(20,  btnY);
            btnSave.Location      = new Point(200, btnY);
            btnCancel.Location    = new Point(320, btnY);

            ClientSize = new Size(ClientSize.Width, btnY + 50);
        }

        private void DgvPackageComponents_DataError(object? sender, DataGridViewDataErrorEventArgs e)
        {
            e.Cancel = true;
        }

        private void DgvPackageComponents_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvPackageComponents.Rows[e.RowIndex];
            if (row.IsNewRow) return;

            string? sku = row.Cells["colPkgSKU"].Value?.ToString();
            if (string.IsNullOrEmpty(sku)) return;

            var product = _allProductsForPkg.FirstOrDefault(
                p => p.SKU.Equals(sku, StringComparison.OrdinalIgnoreCase));
            if (product != null)
                row.Cells["colPkgName"].Value = product.ProductName;
        }

        private void CboProductType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Sync the source type dropdown when the product type changes to/from Package
            bool typeIsPackage = IsPackageTypeSelected();
            string currentSource = cboSourceType.SelectedItem?.ToString() ?? "BOM";
            if (typeIsPackage && currentSource != "Package")
                cboSourceType.SelectedItem = "Package"; // fires SelectedIndexChanged → UpdateSourcePanelVisibility
            else if (!typeIsPackage && currentSource == "Package")
                cboSourceType.SelectedItem = "BOM";
            else
                UpdateSourcePanelVisibility();

            if (cboProductType.SelectedItem is not Models.ProductType pt || pt.ProductTypeID == 0)
                return;

            // Load attribute definitions so we can sort and colour by category
            List<Models.AttributeDefinition> defs;
            try   { defs = new Data.ProductTypeRepository().GetAttributeDefinitions(); }
            catch { defs = new List<Models.AttributeDefinition>(); }

            var defMap = defs.ToDictionary(d => d.Name, d => d, StringComparer.OrdinalIgnoreCase);

            static int CategoryOrder(string cat) => cat switch
            {
                "Manufacturing" => 0,
                "Marketing"     => 1,
                _               => 2
            };

            var existingNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow row in dgvAttributes.Rows)
            {
                if (!row.IsNewRow)
                    existingNames.Add(row.Cells["colProperty"].Value?.ToString() ?? string.Empty);
            }

            var col = dgvAttributes.Columns["colProperty"] as DataGridViewComboBoxColumn;
            if (col == null) return;

            // Insert attributes sorted: Manufacturing → Marketing → General
            var sorted = pt.AllAttributes
                .Where(a => !existingNames.Contains(a.AttributeName))
                .OrderBy(a => defMap.TryGetValue(a.AttributeName, out var d) ? CategoryOrder(d.Category) : 2)
                .ThenBy(a => a.AttributeName);

            foreach (var attr in sorted)
            {
                if (!col.Items.Contains(attr.AttributeName)) col.Items.Add(attr.AttributeName);
                int idx = dgvAttributes.Rows.Add(attr.AttributeName, string.Empty);
                ApplyAttributeRowColor(dgvAttributes.Rows[idx], defMap, attr.AttributeName);
            }

            // Re-colour any rows that were already loaded (e.g. from the edit-mode constructor path)
            foreach (DataGridViewRow row in dgvAttributes.Rows)
            {
                if (row.IsNewRow) continue;
                var name = row.Cells["colProperty"].Value?.ToString() ?? "";
                ApplyAttributeRowColor(row, defMap, name);
            }
        }

        private static void ApplyAttributeRowColor(
            DataGridViewRow row,
            Dictionary<string, Models.AttributeDefinition> defMap,
            string attrName)
        {
            if (!defMap.TryGetValue(attrName, out var def)) return;
            row.DefaultCellStyle.BackColor = def.Category switch
            {
                "Manufacturing" => Color.FromArgb(18, 50, 58),   // dark teal tint
                "Marketing"     => Color.FromArgb(50, 42, 14),   // dark gold tint
                _               => Color.Empty
            };
            // Show unit hint in the value cell tooltip for Number-type attributes
            if (def.DataType == "Number" && !string.IsNullOrWhiteSpace(def.Unit))
                row.Cells["colValue"].ToolTipText = $"Enter a number in {def.Unit}";
        }

        private void LoadLocations()
        {
            try
            {
                var locations = AppServices.Get<ILocationRepository>().GetAll().ToList();
                cboDefaultLocation.DataSource    = locations;
                cboDefaultLocation.DisplayMember = "LocationName";
                cboDefaultLocation.ValueMember   = "LocationID";
                cboDefaultLocation.SelectedIndex = -1;
            }
            catch (Exception ex) { AppLogger.Info($"[FormAddProduct.LoadLocations]: {ex.Message}"); }
        }

        private void LoadVendors()
        {
            try
            {
                var vendors = AppServices.Get<IVendorRepository>().GetAll().ToList();
                cboVendor.Items.Clear();
                // Sentinel object for "(None)" — VendorID 0 means no vendor selected
                cboVendor.Items.Add(new Vendor { VendorID = 0, VendorName = "(None)" });
                foreach (var v in vendors) cboVendor.Items.Add(v);
                // ToString() override on Vendor provides the display text (DisplayMember not used with Items.Add)
                cboVendor.SelectedIndex = 0;
            }
            catch (Exception ex) { AppLogger.Info($"[FormAddProduct.LoadVendors]: {ex.Message}"); }
        }

        private void LoadUom()
        {
            try
            {
                var abbrevs = _uomRepo.GetAbbreviations();
                cboUom.Items.Clear();
                cboUom.Items.Add("");
                foreach (var u in abbrevs) cboUom.Items.Add(u);
            }
            catch { /* non-fatal */ }
        }

        private void LoadAttributeNames()
        {
            try
            {
                var names = AppServices.Get<IProductRepository>().GetAllAttributeNames().ToList();
                var col   = dgvAttributes.Columns["colProperty"] as DataGridViewComboBoxColumn;
                if (col == null) return;
                col.Items.Clear();
                foreach (var n in names) col.Items.Add(n);
            }
            catch (Exception ex) { AppLogger.Info($"[FormAddProduct.LoadAttributeNames]: {ex.Message}"); }

            // Allow free typing in the attribute name column
            dgvAttributes.EditingControlShowing += (s, e) =>
            {
                if (dgvAttributes.Columns["colProperty"] is DataGridViewColumn propCol &&
                    dgvAttributes.CurrentCell?.ColumnIndex == propCol.Index
                    && e.Control is ComboBox cb)
                {
                    cb.DropDownStyle      = ComboBoxStyle.DropDown;
                    cb.AutoCompleteMode   = AutoCompleteMode.SuggestAppend;
                    cb.AutoCompleteSource = AutoCompleteSource.ListItems;
                }
            };

            // Suppress "value not in list" validation error so new names are accepted
            dgvAttributes.DataError += (s, e) => { e.Cancel = true; };
        }

        private void btnManageBOM_Click(object sender, EventArgs e)
        {
            if (_editingProduct is null)
            {
                MessageBox.Show(this,
                    "Save the product first before managing BOM entries.",
                    "Save First", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var partRepo = AppServices.Get<IPartRepository>();
            using var bomEditor = new FormBomEditor(_editingProduct, partRepo);
            bomEditor.ShowDialog(this);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSKU.Text)        ||
                string.IsNullOrWhiteSpace(txtProductName.Text) ||
                string.IsNullOrWhiteSpace(txtPrice.Text))
            {
                MessageBox.Show("Please fill in SKU, Product Name, and Retail Price.",
                    "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Validate product source
            string _sourceType = cboSourceType.SelectedItem?.ToString() ?? "BOM";
            if (_sourceType == "Package")
            {
                // Package: must have at least one component
                dgvPackageComponents.CommitEdit(DataGridViewDataErrorContexts.Commit);
                bool hasComponent = dgvPackageComponents.Rows
                    .Cast<DataGridViewRow>()
                    .Any(r => !r.IsNewRow &&
                              !string.IsNullOrWhiteSpace(r.Cells["colPkgSKU"].Value?.ToString()));
                if (!hasComponent)
                {
                    MessageBox.Show(this,
                        "Package bundles must have at least one component product.\n\n" +
                        "Add component products in the Package Contents grid.",
                        "No Components", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            else if (_sourceType == "Part")
            {
                // Part mode: must have a part selected
                if (!(cboLinkedPart.SelectedItem is Part))
                {
                    MessageBox.Show(this,
                        "Please select a linked part for this product.",
                        "Part Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else if (_editingProduct != null && _sourceType == "BOM")
            {
                // BOM mode — edit: block save if no BOM entries
                try
                {
                    int bomCount = new Data.ProductRepository().GetBomCount(_editingProduct.ProductID);
                    if (bomCount == 0)
                    {
                        MessageBox.Show(this,
                            "This product has no BOM (Bill of Materials) entries.\n\n" +
                            "Every non-package product must have at least one part in the BOM.\n" +
                            "Click 'Manage BOM / Parts' to add parts before saving.",
                            "BOM Required", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                catch { /* don't block save on check failure */ }
            }

            if (!decimal.TryParse(txtPrice.Text, out decimal price))
            {
                MessageBox.Show("Retail Price must be a valid number.", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal.TryParse(txtWholesalePrice.Text, out decimal wholesalePrice);

            // Opening stock only applies when adding a new product
            int openingStock = 0;
            if (_editingProduct is null)
            {
                if (!int.TryParse(txtStock.Text, out openingStock) || openingStock < 0)
                {
                    MessageBox.Show("Opening Stock must be 0 or a positive whole number.",
                        "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (openingStock > 0 && cboDefaultLocation.SelectedItem as Location == null)
                {
                    MessageBox.Show("A default location is required when opening stock is greater than 0.",
                        "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cboDefaultLocation.Focus();
                    return;
                }
            }

            // Commit any in-progress cell edit before reading rows
            dgvAttributes.CommitEdit(DataGridViewDataErrorContexts.Commit);

            var attributes = new List<ProductAttribute>();
            foreach (DataGridViewRow row in dgvAttributes.Rows)
            {
                if (row.IsNewRow) continue;
                string name  = row.Cells["colProperty"].Value?.ToString() ?? string.Empty;
                string value = row.Cells["colValue"].Value?.ToString()    ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name))
                    attributes.Add(new ProductAttribute
                    {
                        AttributeName  = name.Trim(),
                        AttributeValue = value.Trim()
                    });
            }

            // Validate required attributes (only when a type with configured attrs is selected)
            if (cboProductType.SelectedItem is Models.ProductType selType && selType.ProductTypeID != 0)
            {
                var requiredAttrs  = selType.RequiredAttributes;
                var filledAttrMap  = attributes.ToDictionary(a => a.AttributeName, a => a.AttributeValue,
                    StringComparer.OrdinalIgnoreCase);
                var missingRequired = requiredAttrs
                    .Where(r => !filledAttrMap.TryGetValue(r, out var v) || string.IsNullOrWhiteSpace(v))
                    .ToList();
                if (missingRequired.Count > 0)
                {
                    MessageBox.Show(this,
                        $"The following required attributes must have a value:\n  • {string.Join("\n  • ", missingRequired)}",
                        "Required Attributes Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            int? defaultLocationId = (cboDefaultLocation.SelectedItem as Location)?.LocationID;

            int? productTypeId = (cboProductType.SelectedItem is Models.ProductType selPt && selPt.ProductTypeID != 0)
                ? selPt.ProductTypeID : (int?)null;

            // Read selected vendor (null if "(None)" or nothing selected)
            int? defaultVendorId = null;
            if (cboVendor.SelectedItem is Vendor selVendor && selVendor.VendorID != 0)
                defaultVendorId = selVendor.VendorID;

            bool isPackage = _sourceType == "Package";

            try
            {
                var repo = AppServices.Get<IProductRepository>();

                string? uom = string.IsNullOrWhiteSpace(cboUom.Text) ? null : cboUom.Text.Trim();
                if (_editingProduct is not null)
                {
                    _editingProduct.SKU               = txtSKU.Text.Trim();
                    _editingProduct.ProductName       = txtProductName.Text.Trim();
                    _editingProduct.RetailPrice       = price;
                    _editingProduct.WholesalePrice    = wholesalePrice;
                    _editingProduct.DefaultLocationID = defaultLocationId;
                    _editingProduct.ProductTypeID     = productTypeId;
                    _editingProduct.ReorderPoint      = (int)nudReorderPoint.Value;
                    _editingProduct.OrderUpTo         = (int)nudOrderUpTo.Value;
                    _editingProduct.DefaultVendorID   = defaultVendorId;
                    _editingProduct.UnitOfMeasure     = uom;
                    _editingProduct.Attributes        = attributes;
                    repo.UpdateProduct(_editingProduct);
                    AppLogger.Audit(AppSession.CurrentUser?.Username, "ProductUpdate",
                        $"SKU={_editingProduct.SKU} Name={_editingProduct.ProductName}");

                    // Save source-specific data
                    if (isPackage)
                        SavePackageComponents(_editingProduct.ProductID);
                    else if (_sourceType == "Part" && cboLinkedPart.SelectedItem is Part editLinkedPart)
                        SaveLinkedPart(_editingProduct.ProductID, editLinkedPart.PartID);

                    MessageBox.Show("Product updated successfully!", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    var newProduct = new Product
                    {
                        SKU               = txtSKU.Text.Trim(),
                        ProductName       = txtProductName.Text.Trim(),
                        RetailPrice       = price,
                        WholesalePrice    = wholesalePrice,
                        CurrentStock      = openingStock,
                        IsActive          = true,
                        DefaultLocationID = defaultLocationId,
                        ProductTypeID     = productTypeId,
                        ReorderPoint      = (int)nudReorderPoint.Value,
                        OrderUpTo         = (int)nudOrderUpTo.Value,
                        DefaultVendorID   = defaultVendorId,
                        UnitOfMeasure     = uom,
                        Attributes        = attributes
                    };
                    repo.AddProduct(newProduct);
                    AppLogger.Audit(AppSession.CurrentUser?.Username, "ProductAdd",
                        $"SKU={newProduct.SKU} Name={newProduct.ProductName}");

                    MessageBox.Show("Product saved successfully!", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    if (isPackage)
                    {
                        // Reload to get the new ProductID, then save package components
                        try
                        {
                            var savedProduct = AppServices.Get<IProductRepository>().GetProducts()
                                .FirstOrDefault(p => p.SKU == newProduct.SKU);
                            if (savedProduct != null)
                                SavePackageComponents(savedProduct.ProductID);
                        }
                        catch (Exception ex2)
                        {
                            AppLogger.Info($"[FormAddProduct.btnSave_Click PackageComponents]: {ex2.Message}");
                        }
                    }
                    else if (_sourceType == "Part")
                    {
                        // Save linked part as single-entry BOM
                        try
                        {
                            var savedProduct = AppServices.Get<IProductRepository>().GetProducts()
                                .FirstOrDefault(p => p.SKU == newProduct.SKU);
                            if (savedProduct != null && cboLinkedPart.SelectedItem is Part newLinkedPart)
                                SaveLinkedPart(savedProduct.ProductID, newLinkedPart.PartID);
                        }
                        catch (Exception ex2)
                        {
                            AppLogger.Info($"[FormAddProduct.btnSave_Click LinkedPart]: {ex2.Message}");
                        }
                    }
                    else
                    {
                        // Auto-open BOM editor for newly created BOM products;
                        // optionally pre-populate by copying from a selected existing BOM.
                        try
                        {
                            var savedProduct = AppServices.Get<IProductRepository>().GetProducts()
                                .FirstOrDefault(p => p.SKU == newProduct.SKU);
                            if (savedProduct != null)
                            {
                                var partRepo = AppServices.Get<IPartRepository>();

                                // Copy BOM entries from selected source product if one was chosen
                                if (cboLinkedBOM.SelectedItem is BomChoice choice && choice.SourceProductID != 0)
                                {
                                    var srcBom = partRepo.GetBom(choice.SourceProductID);
                                    if (srcBom.Count > 0)
                                        partRepo.SetBom(savedProduct.ProductID,
                                            srcBom.Select(b => (b.PartID, b.Quantity, b.CreatesBatchLoss, b.BatchLossRate)));
                                }

                                using var bomEditor = new FormBomEditor(savedProduct, partRepo);
                                bomEditor.ShowDialog(this);

                                // Warn if still no BOM parts after the editor was closed
                                int bomCount = partRepo.GetBom(savedProduct.ProductID).Count;
                                if (bomCount == 0)
                                    MessageBox.Show(this,
                                        "No BOM parts were added. All products should have at least one part in the BOM before being used in manufacturing.",
                                        "BOM Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }
                        catch (Exception ex2)
                        {
                            AppLogger.Info($"[FormAddProduct.btnSave_Click BOM]: {ex2.Message}");
                        }
                    }
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error saving product",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SavePackageComponents(int packageProductID)
        {
            dgvPackageComponents.CommitEdit(DataGridViewDataErrorContexts.Commit);

            var components = new List<Models.PackageComponent>();
            foreach (DataGridViewRow row in dgvPackageComponents.Rows)
            {
                if (row.IsNewRow) continue;
                string sku = row.Cells["colPkgSKU"].Value?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(sku)) continue;

                var product = _allProductsForPkg.FirstOrDefault(
                    p => p.SKU.Equals(sku, StringComparison.OrdinalIgnoreCase));
                if (product == null) continue;

                int.TryParse(row.Cells["colPkgQty"].Value?.ToString(), out int qty);
                if (qty <= 0) qty = 1;

                components.Add(new Models.PackageComponent
                {
                    PackageProductID   = packageProductID,
                    ComponentProductID = product.ProductID,
                    Quantity           = qty,
                    Notes              = row.Cells["colPkgNotes"].Value?.ToString()
                });
            }

            new Data.PackageRepository().SetComponents(packageProductID, components);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
