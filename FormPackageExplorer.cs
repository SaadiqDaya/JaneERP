using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;
using JaneERP.Logging;
using JaneERP.Security;

namespace JaneERP
{
    /// <summary>
    /// Package Explorer — view, create, and edit product bundles.
    ///
    /// Left panel  : list of all packages (products that have package components).
    /// Right panel : editable component grid for the selected package.
    ///
    /// "New Package" opens FormAddProduct pre-set to Package Bundle mode.
    /// "Edit Package" opens FormAddProduct for the selected package.
    /// "Remove Package" clears all components (the product itself is kept).
    /// "Save Components" persists component edits made directly in the right grid.
    /// </summary>
    public class FormPackageExplorer : Form
    {
        private readonly IPackageRepository _pkgRepo  = AppServices.Get<IPackageRepository>();
        private readonly IProductRepository _prodRepo = AppServices.Get<IProductRepository>();

        private readonly Panel          _pnlHeader  = new();
        private readonly SplitContainer _split      = new();
        private readonly DataGridView   _dgvPkgs    = new();
        private readonly Label          _lblPkgHdr  = new();
        private readonly DataGridView   _dgvComps   = new();
        private readonly Label          _lblCompHdr = new();

        private readonly Button _btnNew     = new() { Text = "+ New Package"     };
        private readonly Button _btnEdit    = new() { Text = "\u270F\u0020 Edit Package"    };
        private readonly Button _btnRemove  = new() { Text = "\U0001F5D1\u0020 Remove"         };
        private readonly Button _btnSave    = new() { Text = "\U0001F4BE\u0020 Save Components" };
        private readonly Button _btnRefresh = new() { Text = "\u21BA\u0020 Refresh"           };

        private PackageProductRow? _selected;
        private List<Product>      _allProducts = new();

        public FormPackageExplorer()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            Load += (_, _) => { LoadAllProducts(); RefreshPackages(); };
        }

        // ── Layout ────────────────────────────────────────────────────────────────

        private void BuildUI()
        {
            Text          = "Package Explorer";
            ClientSize    = new Size(1100, 660);
            MinimumSize   = new Size(820, 500);
            StartPosition = FormStartPosition.CenterParent;

            // Header
            _pnlHeader.Dock      = DockStyle.Top;
            _pnlHeader.Height    = 52;
            _pnlHeader.BackColor = Theme.Header;
            _pnlHeader.Controls.Add(new Label
            {
                Text      = "\U0001F4E6  Package Explorer",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(14, 13),
                AutoSize  = true
            });
            Theme.MakeDraggable(this, _pnlHeader);

            // Bottom action bar
            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = Theme.Header };

            StyleBtn(_btnRefresh, Theme.StyleSecondaryButton, 100);
            StyleBtn(_btnNew,     Theme.StyleButton,          130);
            StyleBtn(_btnEdit,    Theme.StyleSecondaryButton, 120);
            StyleBtn(_btnRemove,  Theme.StyleSecondaryButton, 110);
            StyleBtn(_btnSave,    Theme.StyleButton,          160);

            _btnRefresh.Click += (_, _) => { LoadAllProducts(); RefreshPackages(); };
            _btnNew.Click     += BtnNew_Click;
            _btnEdit.Click    += BtnEdit_Click;
            _btnRemove.Click  += BtnRemove_Click;
            _btnSave.Click    += BtnSave_Click;

            int bx = 12;
            foreach (var btn in new[] { _btnRefresh, _btnNew, _btnEdit, _btnRemove })
            {
                btn.Location = new Point(bx, 10);
                pnlBottom.Controls.Add(btn);
                bx += btn.Width + 8;
            }

            pnlBottom.Resize += (_, _) =>
                _btnSave.Location = new Point(pnlBottom.Width - _btnSave.Width - 12, 10);
            pnlBottom.Controls.Add(_btnSave);

            // Split
            _split.Dock             = DockStyle.Fill;
            _split.Orientation      = Orientation.Vertical;
            _split.SplitterDistance = 360;
            _split.SplitterWidth    = 6;

            BuildPackagesPanel();
            BuildComponentsPanel();

            Controls.Add(_split);
            Controls.Add(pnlBottom);
            Controls.Add(_pnlHeader);

            UpdateButtons();
        }

        private void BuildPackagesPanel()
        {
            _dgvPkgs.Dock                  = DockStyle.Fill;
            _dgvPkgs.AutoGenerateColumns   = false;
            _dgvPkgs.AllowUserToAddRows    = false;
            _dgvPkgs.AllowUserToDeleteRows = false;
            _dgvPkgs.ReadOnly              = true;
            _dgvPkgs.RowHeadersVisible     = false;
            _dgvPkgs.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgvPkgs.MultiSelect           = false;
            _dgvPkgs.Columns.AddRange(
                new DataGridViewTextBoxColumn { Name = "colPkgSKU",   HeaderText = "SKU",          Width = 110 },
                new DataGridViewTextBoxColumn { Name = "colPkgName",  HeaderText = "Package Name",  AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill },
                new DataGridViewTextBoxColumn { Name = "colPkgCount", HeaderText = "Items",         Width = 50  }
            );
            _dgvPkgs.SelectionChanged += DgvPkgs_SelectionChanged;
            Theme.StyleGrid(_dgvPkgs);

            _lblPkgHdr.Dock      = DockStyle.Top;
            _lblPkgHdr.Height    = 26;
            _lblPkgHdr.ForeColor = Theme.Gold;
            _lblPkgHdr.Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _lblPkgHdr.Padding   = new Padding(4, 5, 0, 0);
            _lblPkgHdr.Text      = "All Packages";

            var pnl = new Panel { Dock = DockStyle.Fill };
            pnl.Controls.Add(_dgvPkgs);
            pnl.Controls.Add(_lblPkgHdr);
            _split.Panel1.Controls.Add(pnl);
        }

        private void BuildComponentsPanel()
        {
            var skuCol = new DataGridViewComboBoxColumn
            {
                Name         = "colCompSKU",
                HeaderText   = "SKU",
                Width        = 130,
                FlatStyle    = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing
            };

            _dgvComps.Dock                  = DockStyle.Fill;
            _dgvComps.AutoGenerateColumns   = false;
            _dgvComps.AllowUserToAddRows    = true;
            _dgvComps.AllowUserToDeleteRows = true;
            _dgvComps.RowHeadersVisible     = false;
            _dgvComps.EditMode              = DataGridViewEditMode.EditOnKeystrokeOrF2;
            _dgvComps.Columns.AddRange(
                skuCol,
                new DataGridViewTextBoxColumn { Name = "colCompName",  HeaderText = "Product Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true },
                new DataGridViewTextBoxColumn { Name = "colCompQty",   HeaderText = "Qty",   Width = 60  },
                new DataGridViewTextBoxColumn { Name = "colCompNotes", HeaderText = "Notes", Width = 160 }
            );
            _dgvComps.CellEndEdit += DgvComps_CellEndEdit;
            _dgvComps.DataError   += (_, e) => e.Cancel = true;
            Theme.StyleGrid(_dgvComps);

            _lblCompHdr.Dock      = DockStyle.Top;
            _lblCompHdr.Height    = 26;
            _lblCompHdr.ForeColor = Theme.Gold;
            _lblCompHdr.Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _lblCompHdr.Padding   = new Padding(4, 5, 0, 0);
            _lblCompHdr.Text      = "Select a package to view components";

            var pnl = new Panel { Dock = DockStyle.Fill };
            pnl.Controls.Add(_dgvComps);
            pnl.Controls.Add(_lblCompHdr);
            _split.Panel2.Controls.Add(pnl);
        }

        // ── Data loading ──────────────────────────────────────────────────────────

        private void LoadAllProducts()
        {
            try
            {
                _allProducts = _prodRepo.GetProducts().ToList();
                var skuCol = _dgvComps.Columns["colCompSKU"] as DataGridViewComboBoxColumn;
                if (skuCol == null) return;
                skuCol.Items.Clear();
                foreach (var p in _allProducts)
                    skuCol.Items.Add(p.SKU);
            }
            catch (Exception ex) { AppLogger.Info($"[FormPackageExplorer.LoadAllProducts]: {ex.Message}"); }
        }

        private void RefreshPackages()
        {
            try
            {
                int? prevId = _selected?.ProductID;
                var rows = _pkgRepo.GetAllPackageProducts();

                _dgvPkgs.SuspendLayout();
                _dgvPkgs.Rows.Clear();
                foreach (var r in rows)
                {
                    int i = _dgvPkgs.Rows.Add(r.SKU, r.ProductName, r.ComponentCount);
                    _dgvPkgs.Rows[i].Tag = r;
                }
                _dgvPkgs.ResumeLayout();
                _lblPkgHdr.Text = $"All Packages  ({rows.Count})";

                // Restore selection
                if (prevId.HasValue)
                {
                    foreach (DataGridViewRow row in _dgvPkgs.Rows)
                    {
                        if (row.Tag is PackageProductRow r && r.ProductID == prevId)
                        {
                            row.Selected = true;
                            return;
                        }
                    }
                }
                ClearComponents();
                UpdateButtons();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not load packages:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadComponents(int packageProductID)
        {
            try
            {
                var comps = _pkgRepo.GetComponents(packageProductID);
                _dgvComps.SuspendLayout();
                _dgvComps.Rows.Clear();
                foreach (var c in comps)
                {
                    var skuCol = _dgvComps.Columns["colCompSKU"] as DataGridViewComboBoxColumn;
                    if (skuCol != null && !skuCol.Items.Contains(c.ComponentSKU))
                        skuCol.Items.Add(c.ComponentSKU);
                    _dgvComps.Rows.Add(c.ComponentSKU, c.ComponentName, c.Quantity, c.Notes ?? "");
                }
                _dgvComps.ResumeLayout();
            }
            catch (Exception ex) { AppLogger.Info($"[FormPackageExplorer.LoadComponents]: {ex.Message}"); }
        }

        private void ClearComponents()
        {
            _selected = null;
            _dgvComps.Rows.Clear();
            _lblCompHdr.Text = "Select a package to view components";
        }

        // ── Grid events ───────────────────────────────────────────────────────────

        private void DgvPkgs_SelectionChanged(object? sender, EventArgs e)
        {
            if (_dgvPkgs.SelectedRows.Count == 0) { ClearComponents(); UpdateButtons(); return; }
            _selected = _dgvPkgs.SelectedRows[0].Tag as PackageProductRow;
            if (_selected != null)
            {
                _lblCompHdr.Text = $"{_selected.ProductName}  —  Components";
                LoadComponents(_selected.ProductID);
            }
            UpdateButtons();
        }

        private void DgvComps_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = _dgvComps.Rows[e.RowIndex];
            if (row.IsNewRow) return;
            if (_dgvComps.Columns[e.ColumnIndex].Name != "colCompSKU") return;

            string? sku = row.Cells["colCompSKU"].Value?.ToString();
            if (string.IsNullOrEmpty(sku)) return;

            var product = _allProducts.FirstOrDefault(
                p => p.SKU.Equals(sku, StringComparison.OrdinalIgnoreCase));
            if (product != null)
                row.Cells["colCompName"].Value = product.ProductName;
        }

        // ── Button actions ────────────────────────────────────────────────────────

        private void BtnNew_Click(object? sender, EventArgs e)
        {
            using var frm = new FormAddProduct(null, presetPackage: true);
            if (frm.ShowDialog(this) == DialogResult.OK)
            {
                LoadAllProducts();
                RefreshPackages();
            }
        }

        private void BtnEdit_Click(object? sender, EventArgs e)
        {
            if (_selected == null) return;
            try
            {
                var product = _prodRepo.GetProductById(_selected.ProductID);
                if (product == null) return;

                using var frm = new FormAddProduct(product);
                if (frm.ShowDialog(this) == DialogResult.OK)
                {
                    LoadAllProducts();
                    RefreshPackages();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not open product:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRemove_Click(object? sender, EventArgs e)
        {
            if (_selected == null) return;
            var confirm = MessageBox.Show(this,
                $"Remove all components from package '{_selected.ProductName}'?\n\n" +
                "The product itself will not be deleted — only its package components will be cleared.",
                "Remove Package", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            try
            {
                _pkgRepo.SetComponents(_selected.ProductID, Array.Empty<PackageComponent>());
                AppLogger.Audit(AppSession.CurrentUser?.Username ?? "system",
                    "PackageRemoved", $"ProductID={_selected.ProductID} SKU={_selected.SKU}");
                RefreshPackages();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Remove failed:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (_selected == null) return;

            _dgvComps.CommitEdit(DataGridViewDataErrorContexts.Commit);
            _dgvComps.EndEdit();

            var components = new List<PackageComponent>();
            foreach (DataGridViewRow row in _dgvComps.Rows)
            {
                if (row.IsNewRow) continue;
                string sku = row.Cells["colCompSKU"].Value?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(sku)) continue;

                var product = _allProducts.FirstOrDefault(
                    p => p.SKU.Equals(sku, StringComparison.OrdinalIgnoreCase));
                if (product == null) continue;

                int.TryParse(row.Cells["colCompQty"].Value?.ToString(), out int qty);
                if (qty <= 0) qty = 1;

                components.Add(new PackageComponent
                {
                    PackageProductID   = _selected.ProductID,
                    ComponentProductID = product.ProductID,
                    Quantity           = qty,
                    Notes              = row.Cells["colCompNotes"].Value?.ToString()
                });
            }

            if (components.Count == 0)
            {
                MessageBox.Show(this,
                    "No valid component rows to save. Add at least one component product.",
                    "No Components", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _pkgRepo.SetComponents(_selected.ProductID, components);
                AppLogger.Audit(AppSession.CurrentUser?.Username ?? "system",
                    "PackageSaved",
                    $"ProductID={_selected.ProductID} SKU={_selected.SKU} Components={components.Count}");
                RefreshPackages();
                MessageBox.Show(this, $"Saved {components.Count} component(s).",
                    "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Save failed:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private void UpdateButtons()
        {
            bool hasPkg = _selected != null;
            _btnEdit.Enabled   = hasPkg;
            _btnRemove.Enabled = hasPkg;
            _btnSave.Enabled   = hasPkg;
        }

        private static void StyleBtn(Button btn, Action<Button> style, int width)
        {
            btn.Size = new Size(width, 32);
            style(btn);
        }
    }
}
