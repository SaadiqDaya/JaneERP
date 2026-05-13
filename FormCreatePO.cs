using JaneERP.Data;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>Dialog for creating a new Purchase Order or viewing an existing one.</summary>
    public class FormCreatePO : Form
    {
        private readonly SupplierRepository _repo;
        private readonly PurchaseOrder?     _viewOnly;
        private readonly PartRepository     _partRepo    = new();
        private readonly ProductRepository  _productRepo = new();

        // Supplier dropdown
        private ComboBox  cboSupplier    = new();
        private Button    btnNewSupplier = new();

        private DateTimePicker  dtpExpected   = new();
        private CheckBox        chkNoExpected = new();
        private TextBox         txtNotes      = new();
        private NumericUpDown   nudShipping   = new();
        private DataGridView    dgvItems      = new();
        private Label           lblTotal      = new();
        private Button          btnAddLine    = new();
        private Button          btnRemoveLine = new();
        private Button          btnSave       = new();
        private Button          btnCancel     = new();

        private List<Supplier>          _suppliers = new();
        private List<Part>              _parts     = new();
        private List<Product>           _products  = new();
        private List<PurchaseOrderItem> _items     = new();

        /// <param name="prePopulateItems">Optional items to pre-load (e.g. from the Reorder Report).</param>
        public FormCreatePO(SupplierRepository repo, PurchaseOrder? po = null,
                            IEnumerable<PurchaseOrderItem>? prePopulateItems = null)
        {
            _repo     = repo;
            _viewOnly = po;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            LoadData();
            if (_viewOnly != null)
                PopulateView();
            else if (prePopulateItems != null)
            {
                _items.AddRange(prePopulateItems);
                RefreshGrid();
            }
        }

        private void BuildUI()
        {
            bool readOnly = _viewOnly != null;
            Text          = readOnly ? "View Purchase Order" : "New Purchase Order";
            ClientSize    = new Size(860, 640);
            MinimumSize   = new Size(760, 560);
            StartPosition = FormStartPosition.CenterParent;

            int y = 12, x = 12, cx = x + 120;

            // ── Title ─────────────────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = readOnly ? $"PO: {_viewOnly!.PONumber}" : "Create Purchase Order",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                AutoSize  = false,
                Location  = new Point(x, y),
                Size      = new Size(600, 28)
            });
            y += 40;

            // ── Supplier dropdown ─────────────────────────────────────────────────
            AddLabel("Supplier:", x, y);
            cboSupplier.Location      = new Point(cx, y - 3);
            cboSupplier.Size          = new Size(340, 24);
            cboSupplier.DropDownStyle = ComboBoxStyle.DropDownList;
            cboSupplier.Enabled       = !readOnly;
            cboSupplier.SelectedIndexChanged += (_, _) =>
            {
                if (cboSupplier.SelectedItem is Supplier s) _selectedSupplierFromCbo = s;
            };
            Controls.Add(cboSupplier);

            if (!readOnly)
            {
                btnNewSupplier.Text     = "+ New Supplier";
                btnNewSupplier.Location = new Point(cx + 348, y - 4);
                btnNewSupplier.Size     = new Size(110, 26);
                btnNewSupplier.UseVisualStyleBackColor = true;
                btnNewSupplier.Click   += BtnNewSupplier_Click;
                Controls.Add(btnNewSupplier);
            }
            y += 32;

            // ── Expected Date ─────────────────────────────────────────────────────
            AddLabel("Expected Date:", x, y);
            dtpExpected.Location = new Point(cx, y - 3);
            dtpExpected.Size     = new Size(180, 24);
            dtpExpected.Format   = DateTimePickerFormat.Short;
            dtpExpected.Enabled  = !readOnly;
            Controls.Add(dtpExpected);

            chkNoExpected.Text     = "No date";
            chkNoExpected.Location = new Point(cx + 190, y - 1);
            chkNoExpected.AutoSize = true;
            chkNoExpected.Enabled  = !readOnly;
            chkNoExpected.CheckedChanged += (_, _) => dtpExpected.Enabled = !chkNoExpected.Checked && !readOnly;
            Controls.Add(chkNoExpected);
            y += 32;

            // ── Notes ─────────────────────────────────────────────────────────────
            AddLabel("Notes:", x, y);
            txtNotes.Location   = new Point(cx, y - 3);
            txtNotes.Size       = new Size(700, 58);
            txtNotes.Multiline  = true;
            txtNotes.ScrollBars = ScrollBars.Vertical;
            txtNotes.ReadOnly   = readOnly;
            Controls.Add(txtNotes);
            y += 70;

            // ── Shipping Cost ─────────────────────────────────────────────────────
            AddLabel("Shipping Cost:", x, y);
            nudShipping.Location      = new Point(cx, y - 3);
            nudShipping.Size          = new Size(120, 24);
            nudShipping.DecimalPlaces = 2;
            nudShipping.Maximum       = 99999.99m;
            nudShipping.Increment     = 1m;
            nudShipping.Enabled       = !readOnly;
            nudShipping.ValueChanged += (_, _) => UpdateTotal();
            Controls.Add(nudShipping);
            y += 32;

            // ── Line items header ─────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = "Line Items",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Teal,
                AutoSize  = true,
                Location  = new Point(x, y)
            });

            if (!readOnly)
            {
                btnAddLine.Text     = "+ Add Item";
                btnAddLine.Location = new Point(x + 120, y - 2);
                btnAddLine.Size     = new Size(100, 26);
                btnAddLine.UseVisualStyleBackColor = true;
                btnAddLine.Click   += BtnAddLine_Click;
                Controls.Add(btnAddLine);

                btnRemoveLine.Text     = "Remove";
                btnRemoveLine.Location = new Point(x + 228, y - 2);
                btnRemoveLine.Size     = new Size(80, 26);
                btnRemoveLine.UseVisualStyleBackColor = true;
                btnRemoveLine.Click   += BtnRemoveLine_Click;
                Controls.Add(btnRemoveLine);
            }
            y += 30;

            // ── DataGridView (no DataSource binding — rows populated manually) ────
            dgvItems.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; // Bottom handled manually
            dgvItems.Location = new Point(x, y);
            dgvItems.Size     = new Size(830, 350);
            dgvItems.AllowUserToAddRows    = false;
            dgvItems.AllowUserToDeleteRows = false;
            dgvItems.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvItems.MultiSelect           = false;
            dgvItems.AutoGenerateColumns   = false;
            dgvItems.RowHeadersVisible     = false;
            dgvItems.CellEndEdit          += (_, _) => RecalcTotal();

            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cType",     HeaderText = "Type",         Width = 72,  ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSKU",      HeaderText = "SKU / Part #", Width = 110, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",     HeaderText = "Item Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cQty",      HeaderText = "Qty",          Width = 55,  ReadOnly = readOnly });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCost",     HeaderText = "Unit Cost",    Width = 90,  ReadOnly = readOnly });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cLineTotal",HeaderText = "Line Total",   Width = 100, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cReceived", HeaderText = "Received",     Width = 70,  ReadOnly = true, Visible = readOnly });

            dgvItems.DataError += (_, e) => e.Cancel = true;
            Controls.Add(dgvItems);

            // ── Total label ───────────────────────────────────────────────────────
            lblTotal.AutoSize  = false;
            lblTotal.Size      = new Size(280, 24);
            lblTotal.Font      = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblTotal.ForeColor = Theme.Gold;
            lblTotal.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(lblTotal);

            // ── Action buttons ────────────────────────────────────────────────────
            if (!readOnly)
            {
                btnSave.Text   = "Save PO";
                btnSave.Size   = new Size(100, 30);
                btnSave.UseVisualStyleBackColor = true;
                btnSave.Click += BtnSave_Click;
                Controls.Add(btnSave);
            }

            btnCancel.Text   = readOnly ? "Close" : "Cancel";
            btnCancel.Size   = new Size(80, 30);
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += (_, _) => Close();
            Controls.Add(btnCancel);

            SizeChanged += (_, _) => PositionBottomControls();
            Load        += (_, _) => PositionBottomControls();
        }

        // Tracks supplier selected via the ComboBox
        private Supplier? _selectedSupplierFromCbo;

        private void PositionBottomControls()
        {
            int bottom = ClientSize.Height - 8;
            int right  = ClientSize.Width  - 12;

            btnCancel.Location = new Point(right - btnCancel.Width, bottom - btnCancel.Height);
            if (btnSave.Parent != null)
                btnSave.Location = new Point(btnCancel.Left - btnSave.Width - 8, bottom - btnSave.Height);

            lblTotal.Location = new Point(right - lblTotal.Width, btnCancel.Top - lblTotal.Height - 4);

            // Resize DGV to fill space between its top and the total label, no overlap
            int dgvBottom = lblTotal.Top - 6;
            int newH      = Math.Max(80, dgvBottom - dgvItems.Top);
            if (dgvItems.Height != newH)   dgvItems.Height = newH;
            if (dgvItems.Width  != right - dgvItems.Left + 4)
                dgvItems.Width = right - dgvItems.Left + 4;
        }

        private void LoadData()
        {
            _suppliers = _repo.GetAllSuppliers(includeInactive: false);
            _parts     = _partRepo.GetAll(includeInactive: false);
            _products  = _productRepo.GetProducts().ToList();
            PopulateSupplierCombo();
        }

        private void PopulateSupplierCombo()
        {
            cboSupplier.Items.Clear();
            foreach (var s in _suppliers)
                cboSupplier.Items.Add(s);
            // Use supplier name as display text
            cboSupplier.DisplayMember = "SupplierName";
        }

        private void PopulateView()
        {
            var po  = _viewOnly!;
            var sup = _suppliers.FirstOrDefault(s => s.SupplierID == po.SupplierID);
            if (sup != null)
            {
                _selectedSupplierFromCbo = sup;
                cboSupplier.SelectedItem = sup;
            }
            else if (po.SupplierName != null)
            {
                // Supplier not in active list — show name as a read-only text box hint
                cboSupplier.Items.Insert(0, new Supplier { SupplierName = po.SupplierName });
                cboSupplier.SelectedIndex = 0;
            }

            if (po.ExpectedDate.HasValue)
            {
                dtpExpected.Value     = po.ExpectedDate.Value;
                chkNoExpected.Checked = false;
            }
            else
            {
                chkNoExpected.Checked = true;
            }

            txtNotes.Text     = po.Notes ?? "";
            nudShipping.Value = Math.Min(po.ShippingCost, nudShipping.Maximum);
            _items            = po.Items.ToList();
            RefreshGrid();
        }

        private void BtnNewSupplier_Click(object? sender, EventArgs e)
        {
            var oldIds = new HashSet<int>(_suppliers.Select(s => s.SupplierID));
            using var frm = new FormSupplierManager(_repo);
            frm.ShowDialog(this);
            _suppliers = _repo.GetAllSuppliers(includeInactive: false);
            PopulateSupplierCombo();
            // Auto-select the newly added supplier
            var newest = _suppliers.Where(s => !oldIds.Contains(s.SupplierID)).MaxBy(s => s.SupplierID);
            if (newest != null)
            {
                cboSupplier.SelectedItem = newest;
                _selectedSupplierFromCbo = newest;
            }
        }

        private void BtnAddLine_Click(object? sender, EventArgs e)
        {
            using var picker = new FormPOItemPicker(_parts, _products);
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            foreach (var item in picker.SelectedItems) _items.Add(item);
            _parts    = picker.Parts;
            _products = picker.Products;
            RefreshGrid();
        }

        private void BtnRemoveLine_Click(object? sender, EventArgs e)
        {
            if (dgvItems.CurrentRow == null || dgvItems.CurrentRow.Index < 0 ||
                dgvItems.CurrentRow.Index >= _items.Count) return;
            _items.RemoveAt(dgvItems.CurrentRow.Index);
            RefreshGrid();
        }

        /// <summary>Populates the grid from <see cref="_items"/> without using DataSource binding,
        /// so all columns (including Line Total) are set explicitly.</summary>
        private void RefreshGrid()
        {
            dgvItems.Rows.Clear();
            foreach (var item in _items)
            {
                int r = dgvItems.Rows.Add();
                var row = dgvItems.Rows[r];
                row.Cells["cType"].Value      = item.PartID.HasValue ? "Part" : "Product";
                row.Cells["cSKU"].Value       = item.SKU ?? "";
                row.Cells["cName"].Value      = item.ItemName;
                row.Cells["cQty"].Value       = item.QuantityOrdered.ToString();
                row.Cells["cCost"].Value      = item.UnitCost.ToString("F2");
                row.Cells["cLineTotal"].Value = $"${item.UnitCost * item.QuantityOrdered:N2} CAD";
                row.Cells["cReceived"].Value  = item.QuantityReceived.ToString();
            }
            UpdateTotal();
        }

        private void RecalcTotal()
        {
            SyncGridToItems();
            // Refresh line total column for each row
            for (int r = 0; r < dgvItems.Rows.Count && r < _items.Count; r++)
            {
                var item = _items[r];
                dgvItems.Rows[r].Cells["cLineTotal"].Value = $"${item.UnitCost * item.QuantityOrdered:N2} CAD";
            }
            UpdateTotal();
        }

        private void UpdateTotal()
        {
            decimal total = _items.Sum(i => i.UnitCost * i.QuantityOrdered) + nudShipping.Value;
            lblTotal.Text = $"Total Cost: ${total:N2} CAD";
        }

        private void SyncGridToItems()
        {
            for (int r = 0; r < dgvItems.Rows.Count && r < _items.Count; r++)
            {
                var row  = dgvItems.Rows[r];
                var item = _items[r];
                if (int.TryParse(row.Cells["cQty"].Value?.ToString(),      out int qty))       item.QuantityOrdered = qty;
                if (decimal.TryParse(row.Cells["cCost"].Value?.ToString(), out decimal cost))   item.UnitCost        = cost;
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            SyncGridToItems();

            var supplier = _selectedSupplierFromCbo
                           ?? cboSupplier.SelectedItem as Supplier;

            if (supplier == null)
            {
                MessageBox.Show(this, "Please select a supplier from the dropdown.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_items.Count == 0)
            {
                MessageBox.Show(this, "Add at least one line item.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_items.Any(i => i.QuantityOrdered <= 0))
            {
                MessageBox.Show(this, "All quantities must be greater than 0.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var po = new PurchaseOrder
                {
                    SupplierID   = supplier.SupplierID,
                    Status       = "Draft",
                    OrderDate    = DateTime.Now,
                    ExpectedDate = chkNoExpected.Checked ? (DateTime?)null : dtpExpected.Value.Date,
                    Notes        = string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim(),
                    ShippingCost = nudShipping.Value,
                    Items        = _items
                };
                _repo.CreateOrder(po);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to save PO:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                Text      = text,
                AutoSize  = false,
                Size      = new Size(115, 22),
                Location  = new Point(x, y + 2),
                ForeColor = Theme.TextSecondary,
                TextAlign = ContentAlignment.MiddleRight
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Supplier picker dialog
    // ─────────────────────────────────────────────────────────────────────────────

    internal sealed class FormSupplierPicker : Form
    {
        private readonly List<Supplier> _all;
        private TextBox      txtSearch = new();
        private DataGridView dgv       = new();
        private Button       btnPick   = new();

        public Supplier? SelectedSupplier { get; private set; }

        public FormSupplierPicker(List<Supplier> suppliers)
        {
            _all = suppliers;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            ApplyFilter();
        }

        private void BuildUI()
        {
            Text          = "Select Supplier";
            ClientSize    = new Size(500, 400);
            MinimumSize   = new Size(400, 300);
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label
            {
                Text      = "Select Supplier",
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            });

            Controls.Add(new Label { Text = "Search:", Location = new Point(12, 52), AutoSize = true });
            txtSearch.Location        = new Point(70, 49);
            txtSearch.Size            = new Size(260, 23);
            txtSearch.PlaceholderText = "Supplier name…";
            txtSearch.TextChanged    += (_, _) => ApplyFilter();
            Controls.Add(txtSearch);

            dgv.AutoGenerateColumns = false;
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",    HeaderText = "Supplier", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cContact", HeaderText = "Contact",  Width = 130, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPhone",   HeaderText = "Phone",    Width = 110, ReadOnly = true });
            dgv.AllowUserToAddRows    = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.ReadOnly              = true;
            dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect           = false;
            dgv.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgv.Location = new Point(12, 80);
            dgv.Size     = new Size(476, 268);
            dgv.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Pick(); };
            Controls.Add(dgv);

            btnPick.Text     = "Select";
            btnPick.Size     = new Size(90, 28);
            btnPick.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnPick.Location = new Point(398, 360);
            btnPick.UseVisualStyleBackColor = true;
            btnPick.Click += (_, _) => Pick();
            Controls.Add(btnPick);

            SizeChanged += (_, _) =>
                btnPick.Location = new Point(ClientSize.Width - btnPick.Width - 12, ClientSize.Height - btnPick.Height - 10);
        }

        private void ApplyFilter()
        {
            var term = txtSearch.Text.Trim();
            var list = string.IsNullOrEmpty(term)
                ? _all
                : _all.Where(s => s.SupplierName.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();

            dgv.Rows.Clear();
            foreach (var s in list)
            {
                int idx = dgv.Rows.Add();
                var row = dgv.Rows[idx];
                row.Cells["cName"].Value    = s.SupplierName;
                row.Cells["cContact"].Value = s.ContactName ?? "";
                row.Cells["cPhone"].Value   = s.Phone ?? "";
                row.Tag = s;
            }
        }

        private void Pick()
        {
            if (dgv.CurrentRow?.Tag is Supplier s)
            {
                SelectedSupplier = s;
                DialogResult     = DialogResult.OK;
                Close();
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // PO line-item picker — enforces selection from existing Parts or Products
    // ─────────────────────────────────────────────────────────────────────────────

    internal sealed class FormPOItemPicker : Form
    {
        private List<Part>    _parts;
        private List<Product> _products;

        private readonly PartRepository    _partRepo    = new();
        private readonly ProductRepository _productRepo = new();

        private TextBox      txtSearch   = new();
        private ComboBox     cboCategory = new();
        private DataGridView dgv         = new();
        private Button       btnAdd      = new();

        public List<PurchaseOrderItem> SelectedItems { get; } = new();
        public List<Part>    Parts    => _parts;
        public List<Product> Products => _products;

        public FormPOItemPicker(List<Part> parts, List<Product> products)
        {
            _parts    = parts;
            _products = products;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            ApplyFilter();
        }

        private void BuildUI()
        {
            Text          = "Add PO Line Items";
            ClientSize    = new Size(780, 480);
            MinimumSize   = new Size(700, 380);
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label
            {
                Text      = "Select Parts / Products",
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            });

            Controls.Add(new Label { Text = "Search:", Location = new Point(12, 52), AutoSize = true });
            txtSearch.Location        = new Point(70, 49);
            txtSearch.Size            = new Size(200, 23);
            txtSearch.PlaceholderText = "Number or name…";
            txtSearch.TextChanged    += (_, _) => ApplyFilter();
            Controls.Add(txtSearch);

            Controls.Add(new Label { Text = "Category:", Location = new Point(284, 52), AutoSize = true });
            cboCategory.Location      = new Point(350, 49);
            cboCategory.Size          = new Size(110, 23);
            cboCategory.DropDownStyle = ComboBoxStyle.DropDownList;
            cboCategory.Items.AddRange(new object[] { "All", "Parts", "Products" });
            cboCategory.SelectedIndex        = 0;
            cboCategory.SelectedIndexChanged += (_, _) => ApplyFilter();
            Controls.Add(cboCategory);

            var btnNewPart = new Button
            {
                Text     = "+ New Part",
                Location = new Point(472, 48),
                Size     = new Size(90, 26),
                UseVisualStyleBackColor = true
            };
            btnNewPart.Click += (_, _) =>
            {
                using var frm = new FormPartsManager();
                frm.ShowDialog(this);
                _parts = _partRepo.GetAll(includeInactive: false);
                ApplyFilter();
            };
            Controls.Add(btnNewPart);

            var btnNewProduct = new Button
            {
                Text     = "+ New Product",
                Location = new Point(570, 48),
                Size     = new Size(100, 26),
                UseVisualStyleBackColor = true
            };
            btnNewProduct.Click += (_, _) =>
            {
                using var frm = new FormAddProduct();
                frm.ShowDialog(this);
                _products = _productRepo.GetProducts().ToList();
                ApplyFilter();
            };
            Controls.Add(btnNewProduct);

            dgv.AutoGenerateColumns = false;
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCat",  HeaderText = "Type",      Width = 72, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSKU",  HeaderText = "SKU / Part#", Width = 120, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName", HeaderText = "Name",      AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCost", HeaderText = "Unit Cost",  Width = 90, ReadOnly = true });
            dgv.AllowUserToAddRows    = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.ReadOnly              = true;
            dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect           = true;
            dgv.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgv.Location = new Point(12, 80);
            dgv.Size     = new Size(656, 358);
            Controls.Add(dgv);

            btnAdd.Text     = "Add Selected";
            btnAdd.Size     = new Size(110, 28);
            btnAdd.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnAdd.Location = new Point(558, 442);
            btnAdd.UseVisualStyleBackColor = true;
            btnAdd.Click += BtnAdd_Click;
            Controls.Add(btnAdd);

            SizeChanged += (_, _) =>
                btnAdd.Location = new Point(ClientSize.Width - btnAdd.Width - 12, ClientSize.Height - btnAdd.Height - 10);
        }

        private void ApplyFilter()
        {
            var term = txtSearch.Text.Trim();
            var cat  = cboCategory.SelectedItem?.ToString() ?? "All";

            dgv.Rows.Clear();

            if (cat is "All" or "Parts")
            {
                foreach (var p in _parts.Where(p =>
                    string.IsNullOrEmpty(term) ||
                    p.PartNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    p.PartName.Contains(term, StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = dgv.Rows.Add();
                    var row = dgv.Rows[idx];
                    row.Cells["cCat"].Value  = "Part";
                    row.Cells["cSKU"].Value  = p.PartNumber;
                    row.Cells["cName"].Value = p.PartName;
                    row.Cells["cCost"].Value = $"${p.UnitCost:N2} CAD";
                    row.Tag = (object)p;
                }
            }

            if (cat is "All" or "Products")
            {
                foreach (var p in _products.Where(p =>
                    string.IsNullOrEmpty(term) ||
                    p.SKU.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    p.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = dgv.Rows.Add();
                    var row = dgv.Rows[idx];
                    row.Cells["cCat"].Value  = "Product";
                    row.Cells["cSKU"].Value  = p.SKU;
                    row.Cells["cName"].Value = p.ProductName;
                    row.Cells["cCost"].Value = $"${p.WholesalePrice:N2} CAD";
                    row.Tag = (object)p;
                }
            }
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dgv.SelectedRows)
            {
                if (row.Tag is Part part)
                {
                    SelectedItems.Add(new PurchaseOrderItem
                    {
                        PartID          = part.PartID,
                        SKU             = part.PartNumber,
                        ItemName        = part.PartName,
                        QuantityOrdered = 1,
                        UnitCost        = part.UnitCost
                    });
                }
                else if (row.Tag is Product prod)
                {
                    SelectedItems.Add(new PurchaseOrderItem
                    {
                        ProductID       = prod.ProductID,
                        SKU             = prod.SKU,
                        ItemName        = prod.ProductName,
                        QuantityOrdered = 1,
                        UnitCost        = prod.WholesalePrice
                    });
                }
            }

            if (SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Select at least one item.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
