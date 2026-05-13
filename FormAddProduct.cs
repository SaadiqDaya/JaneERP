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
        private readonly Product? _editingProduct;

        public FormAddProduct() : this(null) { }

        public FormAddProduct(Product? product)
        {
            InitializeComponent();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            _editingProduct = product;

            LoadLocations();
            LoadProductTypes();
            LoadVendors();
            LoadAttributeNames();

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

                // Show Manage BOM button in edit mode
                btnManageBOM.Visible = true;

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

                // If this is a Package product, load its components after the form finishes loading
                this.Load += (_, _) => LoadPackageComponentsIfPackage();
            }
        }

        private void LoadPackageComponentsIfPackage()
        {
            if (_editingProduct == null || !IsPackageTypeSelected()) return;
            try
            {
                EnsurePackageProductsLoaded();
                var components = new Data.PackageRepository().GetComponents(_editingProduct.ProductID);
                dgvPackageComponents.Rows.Clear();
                foreach (var c in components)
                {
                    // Ensure SKU is in the combo
                    var skuCol = dgvPackageComponents.Columns["colPkgSKU"] as DataGridViewComboBoxColumn;
                    if (skuCol != null && !skuCol.Items.Contains(c.ComponentSKU))
                        skuCol.Items.Add(c.ComponentSKU);
                    dgvPackageComponents.Rows.Add(c.ComponentSKU, c.ComponentName, c.Quantity.ToString(), c.Notes ?? string.Empty);
                }
            }
            catch (Exception ex) { AppLogger.Info($"[FormAddProduct.LoadPackageComponentsIfPackage]: {ex.Message}"); }
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

        private bool IsPackageTypeSelected()
        {
            return cboProductType.SelectedItem is Models.ProductType pt
                && pt.TypeName.Equals("Package", StringComparison.OrdinalIgnoreCase);
        }

        private void UpdatePackagePanelVisibility()
        {
            bool isPackage = IsPackageTypeSelected();
            if (isPackage)
            {
                EnsurePackageProductsLoaded();

                // Wire CellEndEdit to auto-populate name column from SKU selection
                dgvPackageComponents.CellEndEdit -= DgvPackageComponents_CellEndEdit;
                dgvPackageComponents.CellEndEdit += DgvPackageComponents_CellEndEdit;

                dgvPackageComponents.DataError -= DgvPackageComponents_DataError;
                dgvPackageComponents.DataError += DgvPackageComponents_DataError;
            }

            pnlPackage.Visible = isPackage;

            // Shift buttons down/up to accommodate the panel
            int btnY = isPackage ? pnlPackage.Bottom + 10 : 555;
            btnManageBOM.Location = new Point(btnManageBOM.Location.X, btnY);
            btnSave.Location      = new Point(btnSave.Location.X,      btnY);
            btnCancel.Location    = new Point(btnCancel.Location.X,    btnY);

            int formH = isPackage ? 605 + 180 : 605;
            ClientSize = new Size(ClientSize.Width, formH);
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
            UpdatePackagePanelVisibility();

            if (cboProductType.SelectedItem is not Models.ProductType pt || pt.ProductTypeID == 0)
                return;

            // Pre-populate attribute rows for all attributes (required and optional)
            var existingNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataGridViewRow row in dgvAttributes.Rows)
            {
                if (!row.IsNewRow)
                    existingNames.Add(row.Cells["colProperty"].Value?.ToString() ?? string.Empty);
            }

            var col = dgvAttributes.Columns["colProperty"] as DataGridViewComboBoxColumn;
            if (col == null) return;
            foreach (var attr in pt.AllAttributes)
            {
                if (existingNames.Contains(attr.AttributeName)) continue;
                if (!col.Items.Contains(attr.AttributeName)) col.Items.Add(attr.AttributeName);
                dgvAttributes.Rows.Add(attr.AttributeName, string.Empty);
            }
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
            if (_editingProduct is null) return;
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

            // Warn if editing an existing product that has no BOM (ProductParts) entries
            if (_editingProduct != null)
            {
                try
                {
                    int bomCount = new Data.ProductRepository().GetBomCount(_editingProduct.ProductID);
                    if (bomCount == 0)
                    {
                        var ans = MessageBox.Show(
                            "This product has no BOM (Bill of Materials) entries.\n\n" +
                            "Without a BOM, manufacturing costs and parts consumption cannot be tracked.\n\n" +
                            "Save anyway?",
                            "No BOM Attached", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                        if (ans != DialogResult.Yes) return;
                    }
                }
                catch { /* best-effort — don't block save on check failure */ }
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

            bool isPackage = IsPackageTypeSelected();

            try
            {
                var repo = AppServices.Get<IProductRepository>();

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
                    _editingProduct.Attributes        = attributes;
                    repo.UpdateProduct(_editingProduct);
                    AppLogger.Audit(AppSession.CurrentUser?.Username, "ProductUpdate",
                        $"SKU={_editingProduct.SKU} Name={_editingProduct.ProductName}");

                    // Save package components if this is a Package product
                    if (isPackage)
                        SavePackageComponents(_editingProduct.ProductID);

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
                    else
                    {
                        // Auto-open BOM editor for newly created non-Package products
                        try
                        {
                            var savedProduct = AppServices.Get<IProductRepository>().GetProducts()
                                .FirstOrDefault(p => p.SKU == newProduct.SKU);
                            if (savedProduct != null)
                            {
                                var partRepo = AppServices.Get<IPartRepository>();
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
