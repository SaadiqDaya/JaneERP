using JaneERP.Data;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>Product picker dialog for sales orders. Supports search, type, and attribute filters.
    /// Returns selected products via <see cref="SelectedProducts"/> on DialogResult.OK.</summary>
    internal class FormOrderProductPicker : Form
    {
        private readonly List<Product>          _allProducts;
        private readonly List<ProductType>      _productTypes;
        private readonly List<ProductAttribute> _allAttributes;

        private TextBox      txtSearch    = new();
        private ComboBox     cboType      = new();
        private ComboBox     cboAttrName  = new();
        private ComboBox     cboAttrValue = new();
        private DataGridView dgvProducts  = new();
        private Button       btnAdd       = new();
        private Button       btnCancel    = new();
        private Label        lblCount     = new();

        public List<Product> SelectedProducts { get; } = new();

        public FormOrderProductPicker()
        {
            _allProducts   = LoadProducts();
            _productTypes  = LoadProductTypes();
            _allAttributes = LoadAttributes(_allProducts.Select(p => p.ProductID));
            BuildUI();

            // Populate attribute name filter from loaded data
            var attrNames = _allAttributes
                .Select(a => a.AttributeName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n);
            foreach (var n in attrNames) cboAttrName.Items.Add(n);

            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            ApplyFilter();
        }

        private static List<Product> LoadProducts()
        {
            try { return new ProductRepository().GetProducts().ToList(); }
            catch { return new List<Product>(); }
        }

        private static List<ProductType> LoadProductTypes()
        {
            try { return new ProductTypeRepository().GetAll().ToList(); }
            catch { return new List<ProductType>(); }
        }

        private static List<ProductAttribute> LoadAttributes(IEnumerable<int> productIds)
        {
            try { return new ProductRepository().GetProductAttributes(productIds).ToList(); }
            catch { return new List<ProductAttribute>(); }
        }

        private void BuildUI()
        {
            Text          = "Select Products";
            ClientSize    = new Size(720, 520);
            MinimumSize   = new Size(600, 420);
            StartPosition = FormStartPosition.CenterParent;

            var lblTitle = new Label
            {
                Text      = "Add Products to Order",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            };
            Controls.Add(lblTitle);

            // ── Filter row ────────────────────────────────────────────────────────
            Controls.Add(new Label { Text = "Search:", Location = new Point(12, 52), AutoSize = true });
            txtSearch.Location    = new Point(72, 49);
            txtSearch.Size        = new Size(210, 23);
            txtSearch.PlaceholderText = "SKU or name...";
            txtSearch.TextChanged += (_, _) => ApplyFilter();
            Controls.Add(txtSearch);

            Controls.Add(new Label { Text = "Type:", Location = new Point(296, 52), AutoSize = true });
            cboType.Location      = new Point(332, 49);
            cboType.Size          = new Size(200, 23);
            cboType.DropDownStyle = ComboBoxStyle.DropDownList;
            cboType.Items.Add("(All Types)");
            foreach (var t in _productTypes) cboType.Items.Add(t);
            cboType.SelectedIndex        = 0;
            cboType.SelectedIndexChanged += (_, _) => ApplyFilter();
            Controls.Add(cboType);

            // ── Attribute filter row ──────────────────────────────────────────────
            Controls.Add(new Label { Text = "Attr:", Location = new Point(12, 82), AutoSize = true, ForeColor = Theme.TextSecondary });
            cboAttrName.Location      = new Point(50, 79);
            cboAttrName.Size          = new Size(180, 23);
            cboAttrName.DropDownStyle = ComboBoxStyle.DropDownList;
            cboAttrName.Items.Add("(All Attributes)");
            cboAttrName.SelectedIndex        = 0;
            cboAttrName.SelectedIndexChanged += CboAttrName_Changed;
            Controls.Add(cboAttrName);

            Controls.Add(new Label { Text = "Value:", Location = new Point(244, 82), AutoSize = true, ForeColor = Theme.TextSecondary });
            cboAttrValue.Location      = new Point(290, 79);
            cboAttrValue.Size          = new Size(180, 23);
            cboAttrValue.DropDownStyle = ComboBoxStyle.DropDownList;
            cboAttrValue.Items.Add("(All Values)");
            cboAttrValue.SelectedIndex        = 0;
            cboAttrValue.SelectedIndexChanged += (_, _) => ApplyFilter();
            Controls.Add(cboAttrValue);

            // ── Grid ─────────────────────────────────────────────────────────────
            dgvProducts.AutoGenerateColumns = false;
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSKU",   HeaderText = "SKU",          Width = 130, ReadOnly = true });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName",  HeaderText = "Product Name", Width = 220, ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colType",  HeaderText = "Type",         Width = 120, ReadOnly = true });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStock", HeaderText = "In Stock",     Width = 80,  ReadOnly = true });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPrice", HeaderText = "Price",        Width = 90,  ReadOnly = true });
            dgvProducts.AllowUserToAddRows    = false;
            dgvProducts.AllowUserToDeleteRows = false;
            dgvProducts.ReadOnly              = true;
            dgvProducts.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvProducts.MultiSelect           = true;
            dgvProducts.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvProducts.Location = new Point(12, 110);
            dgvProducts.Size     = new Size(696, 352);
            Controls.Add(dgvProducts);

            // ── Bottom bar ────────────────────────────────────────────────────────
            lblCount.AutoSize = true;
            lblCount.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            lblCount.Location = new Point(12, 476);
            Controls.Add(lblCount);

            btnAdd.Text     = "Add Selected";
            btnAdd.Size     = new Size(120, 30);
            btnAdd.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnAdd.Location = new Point(478, 474);
            btnAdd.Click   += BtnAdd_Click;
            Controls.Add(btnAdd);

            btnCancel.Text     = "Cancel";
            btnCancel.Size     = new Size(80, 30);
            btnCancel.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.Location = new Point(610, 474);
            btnCancel.Click   += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);
        }

        private void CboAttrName_Changed(object? sender, EventArgs e)
        {
            cboAttrValue.Items.Clear();
            cboAttrValue.Items.Add("(All Values)");
            if (cboAttrName.SelectedItem is string attrName && attrName != "(All Attributes)")
            {
                var values = _allAttributes
                    .Where(a => a.AttributeName.Equals(attrName, StringComparison.OrdinalIgnoreCase))
                    .Select(a => a.AttributeValue)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(v => v);
                foreach (var v in values) cboAttrValue.Items.Add(v);
            }
            cboAttrValue.SelectedIndex = 0;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var search    = txtSearch.Text.Trim();
            int? typeId   = (cboType.SelectedItem is ProductType pt) ? pt.ProductTypeID : (int?)null;
            var attrName  = cboAttrName.SelectedItem  is string an && an != "(All Attributes)" ? an  : null;
            var attrValue = cboAttrValue.SelectedItem is string av && av != "(All Values)"      ? av  : null;

            var typeMap = _productTypes.ToDictionary(t => t.ProductTypeID, t => t.TypeName);

            var filtered = _allProducts
                .Where(p =>
                    (string.IsNullOrEmpty(search) ||
                     p.SKU.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                     p.ProductName.Contains(search, StringComparison.OrdinalIgnoreCase)) &&
                    (typeId == null || p.ProductTypeID == typeId) &&
                    (attrName == null || _allAttributes.Any(a =>
                        a.ProductID == p.ProductID &&
                        a.AttributeName.Equals(attrName, StringComparison.OrdinalIgnoreCase) &&
                        (attrValue == null || a.AttributeValue.Equals(attrValue, StringComparison.OrdinalIgnoreCase)))))
                .ToList();

            dgvProducts.Rows.Clear();
            foreach (var p in filtered)
            {
                int idx = dgvProducts.Rows.Add();
                var row = dgvProducts.Rows[idx];
                row.Cells["colSKU"].Value   = p.SKU;
                row.Cells["colName"].Value  = p.ProductName;
                row.Cells["colType"].Value  =
                    (p.ProductTypeID.HasValue && typeMap.TryGetValue(p.ProductTypeID.Value, out var tn)) ? tn : "";
                row.Cells["colStock"].Value = p.CurrentStock;
                row.Cells["colPrice"].Value = p.RetailPrice.ToString("C2");
                row.Tag = p;
            }

            lblCount.Text = $"{filtered.Count} product(s) shown";
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dgvProducts.SelectedRows)
            {
                if (row.Tag is Product p) SelectedProducts.Add(p);
            }
            if (SelectedProducts.Count == 0)
            {
                MessageBox.Show(this, "Select at least one product.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
