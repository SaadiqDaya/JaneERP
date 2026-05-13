using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>
    /// Shows all auto-created products and parts that have not yet been reviewed.
    /// Users can edit records inline, then mark them as verified individually or in bulk.
    /// </summary>
    public class FormUnverifiedItems : Form
    {
        private readonly IProductRepository     _prodRepo     = AppServices.Get<IProductRepository>();
        private readonly IPartRepository        _partRepo     = AppServices.Get<IPartRepository>();
        private readonly IProductTypeRepository _typeRepo     = AppServices.Get<IProductTypeRepository>();

        private TabControl  tabCtrl       = new();
        private TabPage     tabProducts   = new();
        private TabPage     tabParts      = new();

        private DataGridView dgvProducts  = new();
        private DataGridView dgvParts     = new();

        private Label  lblProductCount   = new();
        private Label  lblPartCount      = new();

        public FormUnverifiedItems()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.MakeResizable(this);
            Theme.AddCloseButton(this);
            Load += (_, _) => { LoadProducts(); LoadParts(); };
        }

        private void BuildUI()
        {
            Text          = "Unverified Auto-Created Items";
            ClientSize    = new Size(1000, 640);
            MinimumSize   = new Size(800, 500);
            StartPosition = FormStartPosition.CenterParent;

            // ── Header ────────────────────────────────────────────────────────────
            var pnlHeader = new Panel { Tag = "header", Dock = DockStyle.Top, Height = 56 };
            pnlHeader.Controls.Add(new Label
            {
                Text      = "🔍  Unverified Items",
                Font      = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(20, 10),
                AutoSize  = true
            });
            pnlHeader.Controls.Add(new Label
            {
                Text      = "Auto-created products and parts awaiting review. Double-click to edit. Verify to clear from list.",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(22, 34),
                AutoSize  = true
            });
            Theme.MakeDraggable(this, pnlHeader);

            // ── Tab control ───────────────────────────────────────────────────────
            tabCtrl.Dock = DockStyle.Fill;
            tabProducts.Text = "Products";
            tabParts.Text    = "Parts";
            tabCtrl.TabPages.Add(tabProducts);
            tabCtrl.TabPages.Add(tabParts);

            // ── Products tab ──────────────────────────────────────────────────────
            BuildProductsTab();

            // ── Parts tab ─────────────────────────────────────────────────────────
            BuildPartsTab();

            Controls.Add(tabCtrl);
            Controls.Add(pnlHeader);
        }

        private void BuildProductsTab()
        {
            dgvProducts.Dock                  = DockStyle.Fill;
            dgvProducts.AutoGenerateColumns   = false;
            dgvProducts.AllowUserToAddRows    = false;
            dgvProducts.AllowUserToDeleteRows = false;
            dgvProducts.ReadOnly              = true;
            dgvProducts.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvProducts.MultiSelect           = true;
            dgvProducts.RowHeadersVisible     = false;

            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProductID", Visible = false });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSKU",       HeaderText = "SKU",         Width = 110 });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName",      HeaderText = "Product Name",AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colType",      HeaderText = "Type",        Width = 100 });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPrice",     HeaderText = "Retail",      Width = 80  });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStock",     HeaderText = "Stock",       Width = 65  });
            dgvProducts.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colVerify", HeaderText = "", Text = "Verify",
                UseColumnTextForButtonValue = true, Width = 70, ReadOnly = false
            });

            dgvProducts.CellDoubleClick += DgvProducts_CellDoubleClick;
            dgvProducts.CellClick       += (_, e) =>
            {
                if (e.RowIndex >= 0 && dgvProducts.Columns[e.ColumnIndex].Name == "colVerify")
                {
                    if (int.TryParse(dgvProducts.Rows[e.RowIndex].Cells["colProductID"].Value?.ToString(), out int id))
                        DoVerifyProducts(new List<int> { id });
                }
            };

            var pnlProductBottom = new Panel { Dock = DockStyle.Bottom, Height = 44 };

            lblProductCount.AutoSize  = true;
            lblProductCount.Location  = new Point(10, 14);
            lblProductCount.ForeColor = Theme.TextSecondary;
            pnlProductBottom.Controls.Add(lblProductCount);

            var btnApplyAttrs = new Button
            {
                Text   = "Apply Type/Attrs…",
                Size   = new Size(140, 30),
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            Theme.StyleSecondaryButton(btnApplyAttrs);
            btnApplyAttrs.Click += BtnApplyAttrs_Click;
            pnlProductBottom.Controls.Add(btnApplyAttrs);

            var btnVerifyAllProducts = new Button
            {
                Text   = "✔ Verify All",
                Size   = new Size(110, 30),
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            Theme.StyleButton(btnVerifyAllProducts);
            btnVerifyAllProducts.Click += (_, _) => VerifyProducts(getAllIds: true);
            pnlProductBottom.Controls.Add(btnVerifyAllProducts);

            var btnVerifySelected = new Button
            {
                Text   = "✔ Verify Selected",
                Size   = new Size(130, 30),
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            Theme.StyleSecondaryButton(btnVerifySelected);
            btnVerifySelected.Click += (_, _) => VerifyProducts(getAllIds: false);
            pnlProductBottom.Controls.Add(btnVerifySelected);

            pnlProductBottom.Resize += (_, _) =>
            {
                int right = pnlProductBottom.ClientSize.Width - 8;
                btnVerifySelected.Location    = new Point(right - btnVerifySelected.Width, 7);
                btnVerifyAllProducts.Location = new Point(btnVerifySelected.Left - btnVerifyAllProducts.Width - 8, 7);
                btnApplyAttrs.Location        = new Point(btnVerifyAllProducts.Left - btnApplyAttrs.Width - 8, 7);
            };

            tabProducts.Controls.Add(dgvProducts);
            tabProducts.Controls.Add(pnlProductBottom);
        }

        private void BuildPartsTab()
        {
            dgvParts.Dock                  = DockStyle.Fill;
            dgvParts.AutoGenerateColumns   = false;
            dgvParts.AllowUserToAddRows    = false;
            dgvParts.AllowUserToDeleteRows = false;
            dgvParts.ReadOnly              = true;
            dgvParts.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvParts.MultiSelect           = true;
            dgvParts.RowHeadersVisible     = false;

            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPartID",     Visible = false });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPartNumber", HeaderText = "Part #",    Width = 120 });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPartName",   HeaderText = "Part Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCost",       HeaderText = "Unit Cost", Width = 90  });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStock",      HeaderText = "Stock",     Width = 65  });
            dgvParts.Columns.Add(new DataGridViewButtonColumn
            {
                Name = "colVerify", HeaderText = "", Text = "Verify",
                UseColumnTextForButtonValue = true, Width = 70, ReadOnly = false
            });

            dgvParts.CellDoubleClick += DgvParts_CellDoubleClick;
            dgvParts.CellClick       += (_, e) =>
            {
                if (e.RowIndex >= 0 && dgvParts.Columns[e.ColumnIndex].Name == "colVerify")
                {
                    if (int.TryParse(dgvParts.Rows[e.RowIndex].Cells["colPartID"].Value?.ToString(), out int id))
                        DoVerifyParts(new List<int> { id });
                }
            };

            var pnlPartsBottom = new Panel { Dock = DockStyle.Bottom, Height = 44 };

            lblPartCount.AutoSize  = true;
            lblPartCount.Location  = new Point(10, 14);
            lblPartCount.ForeColor = Theme.TextSecondary;
            pnlPartsBottom.Controls.Add(lblPartCount);

            var btnVerifyAllParts = new Button
            {
                Text   = "✔ Verify All",
                Size   = new Size(110, 30),
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            Theme.StyleButton(btnVerifyAllParts);
            btnVerifyAllParts.Click += (_, _) => VerifyParts(getAllIds: true);
            pnlPartsBottom.Controls.Add(btnVerifyAllParts);

            var btnVerifySelected = new Button
            {
                Text   = "✔ Verify Selected",
                Size   = new Size(130, 30),
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            Theme.StyleSecondaryButton(btnVerifySelected);
            btnVerifySelected.Click += (_, _) => VerifyParts(getAllIds: false);
            pnlPartsBottom.Controls.Add(btnVerifySelected);

            pnlPartsBottom.Resize += (_, _) =>
            {
                int right = pnlPartsBottom.ClientSize.Width - 8;
                btnVerifySelected.Location = new Point(right - btnVerifySelected.Width, 7);
                btnVerifyAllParts.Location = new Point(btnVerifySelected.Left - btnVerifyAllParts.Width - 8, 7);
            };

            tabParts.Controls.Add(dgvParts);
            tabParts.Controls.Add(pnlPartsBottom);
        }

        // ── Data loading ──────────────────────────────────────────────────────────

        private void LoadProducts()
        {
            try
            {
                var rows = _prodRepo.GetUnverifiedProducts();
                dgvProducts.Rows.Clear();
                foreach (var r in rows)
                    dgvProducts.Rows.Add(r.ProductID, r.SKU, r.ProductName,
                        r.TypeName, r.RetailPrice.ToString("C"), r.CurrentStock.ToString());
                lblProductCount.Text = $"{rows.Count} unverified product{(rows.Count == 1 ? "" : "s")}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to load products: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadParts()
        {
            try
            {
                var rows = _partRepo.GetUnverifiedParts();
                dgvParts.Rows.Clear();
                foreach (var r in rows)
                    dgvParts.Rows.Add(r.PartID, r.PartNumber, r.PartName,
                        r.UnitCost.ToString("C"), r.CurrentStock.ToString());
                lblPartCount.Text = $"{rows.Count} unverified part{(rows.Count == 1 ? "" : "s")}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to load parts: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Double-click to edit ──────────────────────────────────────────────────

        private void DgvProducts_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (!int.TryParse(dgvProducts.Rows[e.RowIndex].Cells["colProductID"].Value?.ToString(), out int productId)) return;

            try
            {
                var product = _prodRepo.GetProductById(productId);
                if (product == null) return;
                product.Attributes = _prodRepo.GetAttributes(productId).ToList();

                using var frm = new FormAddProduct(product);
                if (frm.ShowDialog(this) == DialogResult.OK)
                    LoadProducts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvParts_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (!int.TryParse(dgvParts.Rows[e.RowIndex].Cells["colPartID"].Value?.ToString(), out int partId)) return;

            try
            {
                var part = _partRepo.GetById(partId);
                if (part == null) return;

                using var frm = new FormEditPart(part, _partRepo);
                if (frm.ShowDialog(this) == DialogResult.OK)
                    LoadParts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Verify actions ────────────────────────────────────────────────────────

        private void VerifyProducts(bool getAllIds)
        {
            var ids = getAllIds
                ? dgvProducts.Rows.Cast<DataGridViewRow>()
                    .Select(r => int.TryParse(r.Cells["colProductID"].Value?.ToString(), out int id) ? id : 0)
                    .Where(id => id > 0).ToList()
                : dgvProducts.SelectedRows.Cast<DataGridViewRow>()
                    .Select(r => int.TryParse(r.Cells["colProductID"].Value?.ToString(), out int id) ? id : 0)
                    .Where(id => id > 0).ToList();

            DoVerifyProducts(ids);
        }

        private void DoVerifyProducts(List<int> ids)
        {
            if (ids.Count == 0) return;
            try
            {
                _prodRepo.VerifyProducts(ids);
                LoadProducts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void VerifyParts(bool getAllIds)
        {
            var ids = getAllIds
                ? dgvParts.Rows.Cast<DataGridViewRow>()
                    .Select(r => int.TryParse(r.Cells["colPartID"].Value?.ToString(), out int id) ? id : 0)
                    .Where(id => id > 0).ToList()
                : dgvParts.SelectedRows.Cast<DataGridViewRow>()
                    .Select(r => int.TryParse(r.Cells["colPartID"].Value?.ToString(), out int id) ? id : 0)
                    .Where(id => id > 0).ToList();

            DoVerifyParts(ids);
        }

        private void DoVerifyParts(List<int> ids)
        {
            if (ids.Count == 0) return;
            try
            {
                _partRepo.VerifyParts(ids);
                LoadParts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Apply Type / Attributes to selected products ───────────────────────────

        private void BtnApplyAttrs_Click(object? sender, EventArgs e)
        {
            var selectedIds = dgvProducts.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => int.TryParse(r.Cells["colProductID"].Value?.ToString(), out int id) ? id : 0)
                .Where(id => id > 0).ToList();

            if (selectedIds.Count == 0)
            {
                MessageBox.Show(this, "Select one or more products first.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new FormApplyProductAttributes(selectedIds, _prodRepo, _typeRepo);
            if (dlg.ShowDialog(this) == DialogResult.OK)
                LoadProducts();
        }
    }

    // ── Lightweight part editor used from within FormUnverifiedItems ──────────────

    internal class FormEditPart : Form
    {
        private readonly Part            _part;
        private readonly IPartRepository _repo;

        public FormEditPart(Part part, IPartRepository repo)
        {
            _part = part;
            _repo = repo;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
        }

        private TextBox      txtPartNum = new();
        private TextBox      txtName    = new();
        private TextBox      txtDesc    = new();
        private TextBox      txtCost    = new();

        private void BuildUI()
        {
            Text          = $"Edit Part — {_part.PartNumber}";
            ClientSize    = new Size(420, 280);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;

            int y = 16, x = 14;
            void AddField(string label, TextBox txt, string value)
            {
                Controls.Add(new Label { AutoSize = true, Location = new Point(x, y), Text = label, ForeColor = Theme.TextSecondary });
                y += 20;
                txt.Location = new Point(x, y); txt.Size = new Size(380, 23); txt.Text = value;
                Controls.Add(txt);
                y += 32;
            }

            Controls.Add(new Label { Text = "Edit Part", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Theme.Gold, Location = new Point(x, y), AutoSize = true });
            y += 34;

            AddField("Part Number:", txtPartNum, _part.PartNumber);
            AddField("Part Name:",   txtName,    _part.PartName);
            AddField("Description:", txtDesc,    _part.Description ?? "");
            AddField("Unit Cost:",   txtCost,    _part.UnitCost.ToString("G"));

            var btnSave = new Button { Text = "Save", Size = new Size(90, 30), Location = new Point(x, y) };
            Theme.StyleButton(btnSave);
            btnSave.Click += (_, _) =>
            {
                if (!decimal.TryParse(txtCost.Text, out decimal cost)) cost = 0;
                _part.PartNumber  = txtPartNum.Text.Trim();
                _part.PartName    = txtName.Text.Trim();
                _part.Description = string.IsNullOrWhiteSpace(txtDesc.Text) ? null : txtDesc.Text.Trim();
                _part.UnitCost    = cost;
                _repo.Update(_part);
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(btnSave);

            var btnCancel = new Button { Text = "Cancel", Size = new Size(90, 30), Location = new Point(x + 100, y) };
            Theme.StyleSecondaryButton(btnCancel);
            btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);
        }
    }

    // ── Dialog: Apply Product Type + key attributes to multiple products at once ────

    internal class FormApplyProductAttributes : Form
    {
        private readonly List<int>              _productIds;
        private readonly IProductRepository     _prodRepo;
        private readonly IProductTypeRepository _typeRepo;

        private ComboBox     cboType  = new();
        private DataGridView dgvAttrs = new();

        private List<ProductType> _types = new();

        public FormApplyProductAttributes(List<int> productIds, IProductRepository prodRepo,
            IProductTypeRepository typeRepo)
        {
            _productIds = productIds;
            _prodRepo   = prodRepo;
            _typeRepo   = typeRepo;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            LoadTypes();
        }

        private void BuildUI()
        {
            Text          = "Apply Type / Attributes";
            ClientSize    = new Size(500, 440);
            MinimumSize   = new Size(420, 360);
            StartPosition = FormStartPosition.CenterParent;

            int y = 12, x = 12;
            Controls.Add(new Label
            {
                Text      = $"Applying to {_productIds.Count} product{(_productIds.Count == 1 ? "" : "s")}",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                AutoSize  = true, Location = new Point(x, y)
            });
            y += 34;

            Controls.Add(new Label { Text = "Product Type:", AutoSize = true, Location = new Point(x, y + 3), ForeColor = Theme.TextSecondary });
            cboType.Location      = new Point(x + 120, y);
            cboType.Size          = new Size(260, 24);
            cboType.DropDownStyle = ComboBoxStyle.DropDownList;
            cboType.DisplayMember = "TypeName";
            Controls.Add(cboType);
            y += 34;

            Controls.Add(new Label
            {
                Text      = "Attribute Values  (leave Value blank to skip that attribute)",
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Theme.TextSecondary,
                AutoSize  = true,
                Location  = new Point(x, y)
            });
            y += 20;

            dgvAttrs.Location          = new Point(x, y);
            dgvAttrs.Size              = new Size(470, 270);
            dgvAttrs.Anchor            = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvAttrs.AllowUserToAddRows    = true;
            dgvAttrs.AllowUserToDeleteRows = true;
            dgvAttrs.AutoGenerateColumns   = false;
            dgvAttrs.RowHeadersVisible     = false;
            dgvAttrs.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAttrName",  HeaderText = "Attribute Name",  AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvAttrs.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAttrValue", HeaderText = "Value",           Width = 160 });
            Controls.Add(dgvAttrs);
            y += 278;

            cboType.SelectedIndexChanged += CboType_SelectedIndexChanged;

            var btnApply = new Button { Text = "Apply", Size = new Size(100, 30) };
            Theme.StyleButton(btnApply);
            btnApply.Click += BtnApply_Click;
            Controls.Add(btnApply);

            var btnCancel = new Button { Text = "Cancel", Size = new Size(80, 30) };
            Theme.StyleSecondaryButton(btnCancel);
            btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);

            SizeChanged += (_, _) => PositionButtons(btnApply, btnCancel);
            Load        += (_, _) => PositionButtons(btnApply, btnCancel);
        }

        private void PositionButtons(Button btnApply, Button btnCancel)
        {
            int bottom = ClientSize.Height - 8;
            int right  = ClientSize.Width  - 12;
            btnCancel.Location = new Point(right - btnCancel.Width, bottom - btnCancel.Height);
            btnApply.Location  = new Point(btnCancel.Left - btnApply.Width - 8, bottom - btnApply.Height);
            dgvAttrs.Height    = Math.Max(80, btnApply.Top - dgvAttrs.Top - 8);
        }

        private void LoadTypes()
        {
            try
            {
                _types = _typeRepo.GetAll();
                cboType.Items.Clear();
                cboType.Items.Add(new ProductType { ProductTypeID = 0, TypeName = "(no change)" });
                foreach (var t in _types) cboType.Items.Add(t);
                cboType.SelectedIndex = 0;
            }
            catch { /* best effort */ }
        }

        private void CboType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            dgvAttrs.Rows.Clear();
            if (cboType.SelectedItem is not ProductType pt || pt.ProductTypeID == 0) return;
            try
            {
                var attrNames = _typeRepo.GetAttributeNamesForType(pt.ProductTypeID);
                foreach (var name in attrNames)
                    dgvAttrs.Rows.Add(name, "");
            }
            catch { /* best effort */ }
        }

        private void BtnApply_Click(object? sender, EventArgs e)
        {
            var selectedType = cboType.SelectedItem as ProductType;
            int? typeId      = (selectedType?.ProductTypeID > 0) ? selectedType.ProductTypeID : (int?)null;

            var attrs = new List<(string Name, string Value)>();
            foreach (DataGridViewRow row in dgvAttrs.Rows)
            {
                if (row.IsNewRow) continue;
                var name  = row.Cells["colAttrName"].Value?.ToString()?.Trim() ?? "";
                var value = row.Cells["colAttrValue"].Value?.ToString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
                    attrs.Add((name, value));
            }

            if (typeId == null && attrs.Count == 0)
            {
                MessageBox.Show(this, "Select a product type or enter at least one attribute value.",
                    "Nothing to Apply", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _prodRepo.BulkApplyTypeAndAttributes(_productIds, typeId, attrs);
                MessageBox.Show(this,
                    $"Applied to {_productIds.Count} product{(_productIds.Count == 1 ? "" : "s")}.",
                    "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
