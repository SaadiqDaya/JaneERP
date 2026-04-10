using JaneERP.Data;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>
    /// Attribute-filtered product search screen.
    /// Pinned attribute buttons expand a chip row for value selection;
    /// chips + text search filter the product grid in real time.
    /// </summary>
    public class FormProductSearch : Form
    {
        // ── Repositories ──────────────────────────────────────────────────────────
        private readonly ProductRepository _repo = new();

        // ── Data ──────────────────────────────────────────────────────────────────
        private List<Product>          _allProducts   = new();
        private List<ProductAttribute> _allAttributes = new();

        // ── Active filter state ───────────────────────────────────────────────────
        private string?       _expandedAttr;                 // attribute whose chips are shown
        private readonly HashSet<string> _selectedChips = new(); // selected chip values

        // ── UI controls ───────────────────────────────────────────────────────────
        private TextBox      txtSearch       = new();
        private Panel        pnlHeader       = new();
        private Panel        pnlFilterBtns   = new();  // row of attribute toggle buttons
        private Panel        pnlChips        = new();  // row of value chips (collapsible)
        private DataGridView dgv             = new();
        private Label        lblCount        = new();
        private Button       btnConfigure    = new();

        // ── Chip button tracking ──────────────────────────────────────────────────
        private readonly List<Button> _chipButtons  = new();
        private readonly List<Button> _attrButtons  = new();

        public FormProductSearch()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.MakeResizable(this);
            Theme.AddCloseButton(this);

            Load += (_, _) =>
            {
                LoadData();
                RebuildFilterButtons();
                ApplyFilter();
            };
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // UI construction
        // ═══════════════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            Text          = "Product Search";
            ClientSize    = new Size(1100, 700);
            MinimumSize   = new Size(800, 500);
            StartPosition = FormStartPosition.CenterParent;

            // ── Header panel ──────────────────────────────────────────────────────
            pnlHeader.Tag      = "header";
            pnlHeader.Dock     = DockStyle.Top;
            pnlHeader.Height   = 44;
            pnlHeader.Padding  = new Padding(12, 0, 0, 0);

            var lblTitle = new Label
            {
                Text      = "Product Search",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                AutoSize  = true,
                Location  = new Point(12, 10)
            };
            pnlHeader.Controls.Add(lblTitle);
            Theme.MakeDraggable(this, pnlHeader);

            btnConfigure.Text     = "⚙ Configure Filters";
            btnConfigure.Size     = new Size(148, 28);
            btnConfigure.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnConfigure.Location = new Point(pnlHeader.Width - 196, 8);
            btnConfigure.Click   += (_, _) => ShowConfigureDialog();
            pnlHeader.Controls.Add(btnConfigure);
            pnlHeader.Resize += (_, _) =>
                btnConfigure.Location = new Point(pnlHeader.Width - 196, 8);

            Controls.Add(pnlHeader);

            // ── Search bar ────────────────────────────────────────────────────────
            var pnlSearch = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(10, 8, 10, 0) };

            var lblSearch = new Label { Text = "Search:", AutoSize = true, Location = new Point(10, 12) };
            pnlSearch.Controls.Add(lblSearch);

            txtSearch.Location        = new Point(68, 8);
            txtSearch.Size            = new Size(300, 24);
            txtSearch.PlaceholderText = "SKU or product name…";
            txtSearch.TextChanged    += (_, _) => ApplyFilter();
            pnlSearch.Controls.Add(txtSearch);

            var btnClear = new Button { Text = "Clear", Size = new Size(60, 24), Location = new Point(374, 8) };
            btnClear.Click += (_, _) => { txtSearch.Clear(); };
            pnlSearch.Controls.Add(btnClear);

            Controls.Add(pnlSearch);

            // ── Attribute filter-button row ───────────────────────────────────────
            pnlFilterBtns.Dock      = DockStyle.Top;
            pnlFilterBtns.Height    = 40;
            pnlFilterBtns.Padding   = new Padding(10, 6, 10, 2);
            pnlFilterBtns.AutoScroll = false;

            var sepFilter = new Label
            {
                Text      = "",
                Dock      = DockStyle.Top,
                Height    = 1,
                BackColor = Theme.Border
            };

            Controls.Add(pnlFilterBtns);
            Controls.Add(sepFilter);

            // ── Chip row (hidden when no attribute is expanded) ───────────────────
            pnlChips.Dock      = DockStyle.Top;
            pnlChips.Height    = 0;      // collapsed initially
            pnlChips.Padding   = new Padding(10, 4, 10, 4);
            pnlChips.BackColor = Theme.Background;

            Controls.Add(pnlChips);

            var sepChips = new Label
            {
                Text      = "",
                Dock      = DockStyle.Top,
                Height    = 1,
                BackColor = Theme.Border
            };
            Controls.Add(sepChips);

            // ── Count label ───────────────────────────────────────────────────────
            lblCount.Dock      = DockStyle.Bottom;
            lblCount.Height    = 22;
            lblCount.TextAlign = ContentAlignment.MiddleLeft;
            lblCount.Padding   = new Padding(10, 0, 0, 0);
            Controls.Add(lblCount);

            // ── Product grid ──────────────────────────────────────────────────────
            dgv.Dock                  = DockStyle.Fill;
            dgv.AutoGenerateColumns   = false;
            dgv.AllowUserToAddRows    = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.ReadOnly              = true;
            dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect           = false;
            dgv.RowHeadersVisible     = false;

            dgv.Columns.Add(Col("colSKU",       "SKU",        80));
            dgv.Columns.Add(Col("colName",       "Product",    220, fill: true));
            dgv.Columns.Add(Col("colStock",      "Stock",      65,  right: true));
            dgv.Columns.Add(Col("colReorder",    "Reorder At", 80,  right: true));
            dgv.Columns.Add(Col("colType",       "Type",       100));
            dgv.Columns.Add(Col("colLocation",   "Location",   110));
            dgv.Columns.Add(Col("colRetail",     "Retail",     80,  right: true));
            dgv.Columns.Add(Col("colWholesale",  "Wholesale",  90,  right: true));

            dgv.CellDoubleClick += Dgv_CellDoubleClick;

            Controls.Add(dgv);

            // Re-order the docking layers (DockStyle.Top panels stack bottom-up in Controls)
            // Ensure correct visual order by adding in reverse: grid (Fill), count (Bottom),
            // then tops stack correctly because they were added last.
        }

        private static DataGridViewTextBoxColumn Col(
            string name, string header, int width,
            bool fill = false, bool right = false)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name       = name,
                HeaderText = header,
                ReadOnly   = true
            };
            if (fill)
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            else
                col.Width = width;

            if (right)
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            return col;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Data loading
        // ═══════════════════════════════════════════════════════════════════════════
        private void LoadData()
        {
            try
            {
                _allProducts = _repo.GetProducts().ToList();
                var ids = _allProducts.Select(p => p.ProductID);
                _allAttributes = _repo.GetProductAttributes(ids);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load products:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Filter button row — rebuild whenever pinned attributes change
        // ═══════════════════════════════════════════════════════════════════════════
        private void RebuildFilterButtons()
        {
            pnlFilterBtns.Controls.Clear();
            _attrButtons.Clear();

            int x = 0;
            foreach (var attr in AppSettings.Current.ProductSearchPinnedAttributes)
            {
                var btn = new Button
                {
                    Text      = attr + " ▼",
                    Tag       = attr,
                    Size      = new Size(110, 28),
                    Location  = new Point(x, 4),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Theme.Surface,
                    ForeColor = Theme.Teal,
                    Cursor    = Cursors.Hand,
                    TabStop   = false
                };
                btn.FlatAppearance.BorderColor = Theme.Teal;
                btn.FlatAppearance.BorderSize  = 1;
                btn.Click += AttrButton_Click;
                pnlFilterBtns.Controls.Add(btn);
                _attrButtons.Add(btn);
                x += 118;
            }

            // If the expanded attribute is no longer pinned, collapse
            if (_expandedAttr != null &&
                !AppSettings.Current.ProductSearchPinnedAttributes.Contains(_expandedAttr))
            {
                _expandedAttr = null;
                CollapseChips();
            }
        }

        private void AttrButton_Click(object? sender, EventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string attr) return;

            if (_expandedAttr == attr)
            {
                // Collapse
                _expandedAttr = null;
                CollapseChips();
            }
            else
            {
                _expandedAttr = attr;
                ExpandChips(attr);
            }

            RefreshAttrButtonStyles();
        }

        private void RefreshAttrButtonStyles()
        {
            foreach (var btn in _attrButtons)
            {
                bool active = btn.Tag as string == _expandedAttr;
                btn.BackColor = active ? Theme.Teal    : Theme.Surface;
                btn.ForeColor = active ? Theme.Background : Theme.Teal;
                btn.FlatAppearance.BorderColor = Theme.Teal;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Chip row management
        // ═══════════════════════════════════════════════════════════════════════════
        private void ExpandChips(string attributeName)
        {
            pnlChips.Controls.Clear();
            _chipButtons.Clear();

            List<string> values;
            try
            {
                values = _repo.GetDistinctAttributeValues(attributeName).ToList();
            }
            catch
            {
                values = new List<string>();
            }

            int x = 0;
            foreach (var val in values)
            {
                bool active = _selectedChips.Contains(val);
                var chip = new Button
                {
                    Text      = val,
                    Tag       = val,
                    AutoSize  = false,
                    Size      = new Size(TextRenderer.MeasureText(val, new Font("Segoe UI", 9F)).Width + 20, 26),
                    Location  = new Point(x, 4),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = active ? Theme.Teal : Theme.Background,
                    ForeColor = active ? Theme.Background : Theme.Teal,
                    Cursor    = Cursors.Hand,
                    TabStop   = false,
                    Font      = new Font("Segoe UI", 9F)
                };
                chip.FlatAppearance.BorderColor = Theme.Teal;
                chip.FlatAppearance.BorderSize  = 1;
                chip.Click += Chip_Click;
                pnlChips.Controls.Add(chip);
                _chipButtons.Add(chip);
                x += chip.Width + 6;
            }

            pnlChips.Height = values.Count > 0 ? 36 : 0;
        }

        private void CollapseChips()
        {
            pnlChips.Controls.Clear();
            _chipButtons.Clear();
            pnlChips.Height = 0;
        }

        private void Chip_Click(object? sender, EventArgs e)
        {
            if (sender is not Button chip || chip.Tag is not string val) return;

            if (_selectedChips.Contains(val))
            {
                _selectedChips.Remove(val);
                chip.BackColor = Theme.Background;
                chip.ForeColor = Theme.Teal;
            }
            else
            {
                _selectedChips.Add(val);
                chip.BackColor = Theme.Teal;
                chip.ForeColor = Theme.Background;
            }

            ApplyFilter();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Filtering & grid population
        // ═══════════════════════════════════════════════════════════════════════════
        private void ApplyFilter()
        {
            var results = _allProducts.AsEnumerable();

            // Text filter
            var txt = txtSearch.Text.Trim();
            if (!string.IsNullOrEmpty(txt))
            {
                results = results.Where(p =>
                    p.SKU.Contains(txt, StringComparison.OrdinalIgnoreCase) ||
                    p.ProductName.Contains(txt, StringComparison.OrdinalIgnoreCase));
            }

            // Attribute chip filter
            if (_expandedAttr != null && _selectedChips.Count > 0)
            {
                var attrName = _expandedAttr;
                var selected = _selectedChips.ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Build a lookup: productId → set of values for this attribute
                var matching = _allAttributes
                    .Where(a => a.AttributeName.Equals(attrName, StringComparison.OrdinalIgnoreCase)
                             && selected.Contains(a.AttributeValue))
                    .Select(a => a.ProductID)
                    .ToHashSet();

                results = results.Where(p => matching.Contains(p.ProductID));
            }

            PopulateGrid(results.ToList());
        }

        private void PopulateGrid(List<Product> products)
        {
            dgv.SuspendLayout();
            dgv.Rows.Clear();

            foreach (var p in products)
            {
                dgv.Rows.Add(
                    p.SKU,
                    p.ProductName,
                    p.CurrentStock,
                    p.ReorderPoint,
                    p.ProductTypeName ?? "",
                    p.DefaultLocationName ?? "",
                    p.RetailPrice.ToString("C"),
                    p.WholesalePrice.ToString("C"));

                dgv.Rows[dgv.Rows.Count - 1].Tag = p;
            }

            dgv.ResumeLayout();
            lblCount.Text = $"  {products.Count} product{(products.Count == 1 ? "" : "s")}";
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Grid double-click → open FormAddProduct in edit mode
        // ═══════════════════════════════════════════════════════════════════════════
        private void Dgv_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dgv.Rows[e.RowIndex].Tag is not Product product) return;

            // Load full attributes for this product before opening edit form
            try
            {
                product.Attributes = _repo.GetAttributes(product.ProductID).ToList();
            }
            catch { /* non-fatal */ }

            using var frm = new FormAddProduct(product);
            if (frm.ShowDialog(this) == DialogResult.OK)
            {
                // Refresh data so the grid reflects any edits
                LoadData();
                ApplyFilter();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Configure Filters overlay dialog
        // ═══════════════════════════════════════════════════════════════════════════
        private void ShowConfigureDialog()
        {
            var dlg = new Form
            {
                Text            = "Configure Filter Buttons",
                FormBorderStyle = FormBorderStyle.None,
                StartPosition   = FormStartPosition.CenterParent,
                ClientSize      = new Size(380, 420),
                TopMost         = true
            };
            Theme.Apply(dlg);

            // Title
            dlg.Controls.Add(new Label
            {
                Text      = "Configure Filter Buttons",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                AutoSize  = true,
                Location  = new Point(12, 12)
            });

            // Border line
            dlg.Controls.Add(new Label
            {
                Text      = "",
                Location  = new Point(0, 38),
                Size      = new Size(380, 1),
                BackColor = Theme.Border
            });

            // "Currently pinned" label
            dlg.Controls.Add(new Label
            {
                Text     = "Pinned attributes:",
                AutoSize = true,
                Location = new Point(12, 46)
            });

            // ListBox showing pinned items
            var lstPinned = new ListBox
            {
                Location = new Point(12, 66),
                Size     = new Size(260, 130)
            };
            lstPinned.BackColor = Theme.InputBg;
            lstPinned.ForeColor = Theme.TextPrimary;
            foreach (var a in AppSettings.Current.ProductSearchPinnedAttributes)
                lstPinned.Items.Add(a);
            dlg.Controls.Add(lstPinned);

            // Remove button
            var btnRemove = new Button { Text = "✕ Remove", Location = new Point(280, 66), Size = new Size(88, 28) };
            Theme.StyleSecondaryButton(btnRemove);
            btnRemove.Click += (_, _) =>
            {
                if (lstPinned.SelectedItem is string sel)
                    lstPinned.Items.Remove(sel);
            };
            dlg.Controls.Add(btnRemove);

            // Separator
            dlg.Controls.Add(new Label
            {
                Text      = "",
                Location  = new Point(0, 206),
                Size      = new Size(380, 1),
                BackColor = Theme.Border
            });

            // "Add attribute" label
            dlg.Controls.Add(new Label
            {
                Text     = "Add attribute:",
                AutoSize = true,
                Location = new Point(12, 214)
            });

            // ComboBox of all available attribute names
            var cboAvail = new ComboBox
            {
                Location  = new Point(12, 234),
                Size      = new Size(260, 24),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboAvail.BackColor = Theme.InputBg;
            cboAvail.ForeColor = Theme.TextPrimary;
            cboAvail.FlatStyle = FlatStyle.Flat;

            try
            {
                foreach (var name in _repo.GetAllAttributeNames())
                    cboAvail.Items.Add(name);
            }
            catch { /* non-fatal */ }

            dlg.Controls.Add(cboAvail);

            var btnAdd = new Button { Text = "+ Add", Location = new Point(280, 234), Size = new Size(88, 28) };
            Theme.StyleButton(btnAdd);
            btnAdd.Click += (_, _) =>
            {
                if (cboAvail.SelectedItem is string sel &&
                    !lstPinned.Items.Cast<string>().Contains(sel, StringComparer.OrdinalIgnoreCase))
                {
                    lstPinned.Items.Add(sel);
                }
            };
            dlg.Controls.Add(btnAdd);

            // Save / Cancel
            var btnSave = new Button
            {
                Text     = "Save",
                Location = new Point(192, 374),
                Size     = new Size(80, 30)
            };
            Theme.StyleButton(btnSave);
            btnSave.Click += (_, _) =>
            {
                AppSettings.Current.ProductSearchPinnedAttributes =
                    lstPinned.Items.Cast<string>().ToList();
                AppSettings.Current.Save();
                RebuildFilterButtons();

                // Clear any chips that relate to a now-removed attribute
                _selectedChips.Clear();
                if (_expandedAttr != null &&
                    !AppSettings.Current.ProductSearchPinnedAttributes.Contains(_expandedAttr))
                {
                    _expandedAttr = null;
                    CollapseChips();
                }
                ApplyFilter();
                dlg.Close();
            };
            dlg.Controls.Add(btnSave);

            var btnCancel = new Button
            {
                Text     = "Cancel",
                Location = new Point(280, 374),
                Size     = new Size(80, 30)
            };
            Theme.StyleSecondaryButton(btnCancel);
            btnCancel.Click += (_, _) => dlg.Close();
            dlg.Controls.Add(btnCancel);

            // Close button on dialog
            Theme.AddCloseButton(dlg);

            dlg.ShowDialog(this);
        }
    }
}
