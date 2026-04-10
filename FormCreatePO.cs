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

        // Supplier search
        private TextBox   txtSupplierSearch = new();
        private Button    btnSearchSupplier = new();
        private Button    btnNewSupplier    = new();
        private Supplier? _selectedSupplier;

        private DateTimePicker  dtpExpected   = new();
        private CheckBox        chkNoExpected = new();
        private TextBox         txtNotes      = new();
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

        public FormCreatePO(SupplierRepository repo, PurchaseOrder? po = null)
        {
            _repo     = repo;
            _viewOnly = po;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            LoadData();
            if (_viewOnly != null) PopulateView();
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
            var lblTitle = new Label
            {
                Text      = readOnly ? $"PO: {_viewOnly!.PONumber}" : "Create Purchase Order",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                AutoSize  = false,
                Location  = new Point(x, y),
                Size      = new Size(600, 28)
            };
            Controls.Add(lblTitle);
            y += 40;

            // ── Supplier search ───────────────────────────────────────────────────
            AddLabel("Supplier:", x, y);
            txtSupplierSearch.Location        = new Point(cx, y - 3);
            txtSupplierSearch.Size            = new Size(260, 23);
            txtSupplierSearch.PlaceholderText = "Type supplier name…";
            txtSupplierSearch.ReadOnly        = readOnly;
            Controls.Add(txtSupplierSearch);

            if (!readOnly)
            {
                btnSearchSupplier.Text     = "Search →";
                btnSearchSupplier.Location = new Point(cx + 268, y - 4);
                btnSearchSupplier.Size     = new Size(84, 26);
                btnSearchSupplier.UseVisualStyleBackColor = true;
                btnSearchSupplier.Click   += BtnSearchSupplier_Click;
                Controls.Add(btnSearchSupplier);

                btnNewSupplier.Text     = "+ New";
                btnNewSupplier.Location = new Point(cx + 360, y - 4);
                btnNewSupplier.Size     = new Size(62, 26);
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

            // ── Line items ────────────────────────────────────────────────────────
            var lblItems = new Label
            {
                Text      = "Line Items",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Teal,
                AutoSize  = true,
                Location  = new Point(x, y)
            };
            Controls.Add(lblItems);

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

            // ── DataGridView ──────────────────────────────────────────────────────
            dgvItems.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvItems.Location = new Point(x, y);
            dgvItems.Size     = new Size(830, 360);
            dgvItems.AllowUserToAddRows    = false;
            dgvItems.AllowUserToDeleteRows = false;
            dgvItems.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvItems.MultiSelect           = false;
            dgvItems.AutoGenerateColumns   = false;
            dgvItems.RowHeadersVisible     = false;
            dgvItems.CellEndEdit          += (_, _) => RecalcTotal();

            // Type — readonly (set by picker)
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cType", HeaderText = "Type", Width = 72, ReadOnly = true
            });
            // SKU / Part number — readonly (set by picker)
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cSKU", HeaderText = "SKU / Part #", Width = 110, ReadOnly = true
            });
            // Item Name — readonly (set by picker)
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cName", HeaderText = "Item Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true
            });
            // Qty — editable
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cQty", HeaderText = "Qty", DataPropertyName = "QuantityOrdered",
                Width = 55, ReadOnly = readOnly
            });
            // Unit Cost — editable
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cCost", HeaderText = "Unit Cost", DataPropertyName = "UnitCost",
                Width = 90, ReadOnly = readOnly
            });
            // Line Total — always readonly
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cLineTotal", HeaderText = "Line Total", Width = 100, ReadOnly = true
            });
            // Qty Received — view-mode only
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cReceived", HeaderText = "Received", DataPropertyName = "QuantityReceived",
                Width = 70, ReadOnly = true, Visible = readOnly
            });

            dgvItems.CellFormatting += DgvItems_CellFormatting;
            dgvItems.DataError      += (_, e) => e.Cancel = true;
            Controls.Add(dgvItems);

            // ── Total ─────────────────────────────────────────────────────────────
            lblTotal.AutoSize  = false;
            lblTotal.Size      = new Size(250, 24);
            lblTotal.Font      = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblTotal.ForeColor = Theme.Gold;
            lblTotal.Anchor    = AnchorStyles.Bottom | AnchorStyles.Right;
            lblTotal.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(lblTotal);

            // ── Action buttons ────────────────────────────────────────────────────
            if (!readOnly)
            {
                btnSave.Text     = "Save PO";
                btnSave.Size     = new Size(100, 30);
                btnSave.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
                btnSave.UseVisualStyleBackColor = true;
                btnSave.Click   += BtnSave_Click;
                Controls.Add(btnSave);
            }

            btnCancel.Text     = readOnly ? "Close" : "Cancel";
            btnCancel.Size     = new Size(80, 30);
            btnCancel.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click   += (_, _) => Close();
            Controls.Add(btnCancel);

            SizeChanged += (_, _) => PositionBottomControls();
            Load        += (_, _) => PositionBottomControls();
        }

        private void PositionBottomControls()
        {
            int bottom = ClientSize.Height - 8;
            int right  = ClientSize.Width  - 8;

            btnCancel.Location = new Point(right - btnCancel.Width, bottom - btnCancel.Height);
            if (btnSave.Parent != null)
                btnSave.Location = new Point(right - btnCancel.Width - btnSave.Width - 8, bottom - btnSave.Height);

            lblTotal.Location = new Point(right - 260 - btnCancel.Width - 20, bottom - 24);
        }

        private void DgvItems_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _items.Count) return;
            if (dgvItems.Columns[e.ColumnIndex].Name == "cLineTotal")
            {
                var item = _items[e.RowIndex];
                e.Value = $"R {item.UnitCost * item.QuantityOrdered:N2}";
                e.FormattingApplied = true;
            }
        }

        private void LoadData()
        {
            _suppliers = _repo.GetAllSuppliers(includeInactive: false);
            _parts     = _partRepo.GetAll(includeInactive: false);
            _products  = _productRepo.GetProducts().ToList();

            var ac = new AutoCompleteStringCollection();
            foreach (var s in _suppliers) ac.Add(s.SupplierName);
            txtSupplierSearch.AutoCompleteMode         = AutoCompleteMode.SuggestAppend;
            txtSupplierSearch.AutoCompleteSource       = AutoCompleteSource.CustomSource;
            txtSupplierSearch.AutoCompleteCustomSource = ac;

            txtSupplierSearch.TextChanged += (_, _) =>
            {
                var term  = txtSupplierSearch.Text.Trim();
                var match = _suppliers.FirstOrDefault(s =>
                    s.SupplierName.Equals(term, StringComparison.OrdinalIgnoreCase));
                if (match != null) _selectedSupplier = match;
            };
        }

        private void PopulateView()
        {
            var po  = _viewOnly!;
            var sup = _suppliers.FirstOrDefault(s => s.SupplierID == po.SupplierID);
            if (sup != null)
            {
                _selectedSupplier      = sup;
                txtSupplierSearch.Text = sup.SupplierName;
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

            txtNotes.Text = po.Notes ?? "";
            _items        = po.Items.ToList();
            RefreshGrid();
        }

        private void BtnSearchSupplier_Click(object? sender, EventArgs e)
        {
            using var picker = new FormSupplierPicker(_suppliers);
            if (picker.ShowDialog(this) == DialogResult.OK && picker.SelectedSupplier != null)
            {
                _selectedSupplier      = picker.SelectedSupplier;
                txtSupplierSearch.Text = picker.SelectedSupplier.SupplierName;
            }
        }

        private void BtnNewSupplier_Click(object? sender, EventArgs e)
        {
            using var frm = new FormSupplierManager(_repo);
            frm.ShowDialog(this);
            // Reload and refresh autocomplete
            _suppliers = _repo.GetAllSuppliers(includeInactive: false);
            var ac = new AutoCompleteStringCollection();
            foreach (var s in _suppliers) ac.Add(s.SupplierName);
            txtSupplierSearch.AutoCompleteCustomSource = ac;
        }

        private void BtnAddLine_Click(object? sender, EventArgs e)
        {
            using var picker = new FormPOItemPicker(_parts, _products);
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            foreach (var item in picker.SelectedItems) _items.Add(item);
            RefreshGrid();
        }

        private void BtnRemoveLine_Click(object? sender, EventArgs e)
        {
            if (dgvItems.CurrentRow == null || dgvItems.CurrentRow.Index < 0 ||
                dgvItems.CurrentRow.Index >= _items.Count) return;
            _items.RemoveAt(dgvItems.CurrentRow.Index);
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            dgvItems.DataSource = null;
            dgvItems.DataSource = _items;

            for (int r = 0; r < _items.Count; r++)
            {
                var item = _items[r];
                var row  = dgvItems.Rows[r];
                row.Cells["cType"].Value = item.PartID.HasValue ? "Part" : "Product";
                row.Cells["cSKU"].Value  = item.SKU ?? "";
                row.Cells["cName"].Value = item.ItemName;
            }
            RecalcTotal();
        }

        private void RecalcTotal()
        {
            SyncGridToItems();
            decimal total = _items.Sum(i => i.UnitCost * i.QuantityOrdered);
            lblTotal.Text = $"Total Cost: R {total:N2}";
            dgvItems.Refresh();
        }

        private void SyncGridToItems()
        {
            for (int r = 0; r < dgvItems.Rows.Count && r < _items.Count; r++)
            {
                var row  = dgvItems.Rows[r];
                var item = _items[r];
                if (int.TryParse(row.Cells["cQty"].Value?.ToString(), out int qty))       item.QuantityOrdered = qty;
                if (decimal.TryParse(row.Cells["cCost"].Value?.ToString(), out decimal c)) item.UnitCost        = c;
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            SyncGridToItems();

            if (_selectedSupplier == null)
            {
                var term = txtSupplierSearch.Text.Trim();
                _selectedSupplier = _suppliers.FirstOrDefault(s =>
                    s.SupplierName.Equals(term, StringComparison.OrdinalIgnoreCase));
            }

            if (_selectedSupplier == null)
            {
                MessageBox.Show(this, "Please select a valid supplier.", "Validation",
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
                    SupplierID   = _selectedSupplier.SupplierID,
                    Status       = "Draft",
                    OrderDate    = DateTime.Now,
                    ExpectedDate = chkNoExpected.Checked ? (DateTime?)null : dtpExpected.Value.Date,
                    Notes        = string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim(),
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
        private readonly List<Part>    _parts;
        private readonly List<Product> _products;

        private TextBox      txtSearch   = new();
        private ComboBox     cboCategory = new();
        private DataGridView dgv         = new();
        private Button       btnAdd      = new();

        public List<PurchaseOrderItem> SelectedItems { get; } = new();

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
            ClientSize    = new Size(680, 480);
            MinimumSize   = new Size(560, 380);
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
                    row.Cells["cCost"].Value = $"R {p.UnitCost:N2}";
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
                    row.Cells["cCost"].Value = $"R {p.WholesalePrice:N2}";
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
