using JaneERP.Data;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>CRUD for Parts.</summary>
    public class FormPartsManager : Form
    {
        private readonly PartRepository   _repo        = new();
        private readonly VendorRepository _vendorRepo  = new();

        private DataGridView  dgvParts   = new();
        private Label         lblEdit    = new();
        private TextBox       txtPartNum = new();
        private TextBox       txtName    = new();
        private TextBox       txtDesc    = new();
        private TextBox       txtCost    = new();
        private NumericUpDown nudStock   = new();
        private ComboBox      cboVendor  = new();
        private CheckBox      chkActive  = new();
        private Button        btnSave    = new();
        private Button        btnNew     = new();
        private Button        btnClose   = new();
        private Label         lblCount   = new();

        private Part? _editing;

        public FormPartsManager()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            LoadParts();
        }

        private void BuildUI()
        {
            Text            = "Parts Manager";
            ClientSize      = new Size(900, 560);
            MinimumSize     = new Size(900, 560);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;

            dgvParts.Anchor          = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            dgvParts.Location        = new Point(12, 12);
            dgvParts.Size            = new Size(500, 500);
            dgvParts.ReadOnly        = true;
            dgvParts.AllowUserToAddRows    = false;
            dgvParts.AllowUserToDeleteRows = false;
            dgvParts.SelectionMode   = DataGridViewSelectionMode.FullRowSelect;
            dgvParts.MultiSelect     = false;
            dgvParts.AutoGenerateColumns = false;
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cNum",   HeaderText = "Part #",  DataPropertyName = "PartNumber",  Width = 100 });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",  HeaderText = "Name",    DataPropertyName = "PartName",    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCost",  HeaderText = "Cost",    DataPropertyName = "UnitCost",    Width = 70  });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cStock", HeaderText = "Stock",   DataPropertyName = "CurrentStock",Width = 60  });
            dgvParts.Columns.Add(new DataGridViewCheckBoxColumn { Name = "cAct",  HeaderText = "Active",  DataPropertyName = "IsActive",    Width = 55  });
            dgvParts.SelectionChanged += DgvParts_SelectionChanged;
            Controls.Add(dgvParts);

            lblCount.AutoSize = true;
            lblCount.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            lblCount.Location = new Point(12, 520);
            Controls.Add(lblCount);

            int x = 528, y = 12;

            lblEdit.AutoSize  = false;
            lblEdit.Font      = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblEdit.ForeColor = Theme.Gold;
            lblEdit.Location  = new Point(x, y);
            lblEdit.Size      = new Size(350, 26);
            lblEdit.Text      = "Select a part to edit";
            Controls.Add(lblEdit);
            y += 36;

            AddField(ref y, x, "Part Number:", txtPartNum);
            AddField(ref y, x, "Part Name:", txtName);
            AddField(ref y, x, "Description:", txtDesc);
            AddField(ref y, x, "Unit Cost:", txtCost);

            Controls.Add(new Label { AutoSize = true, Location = new Point(x, y), Text = "Opening Stock:" });
            y += 20;
            nudStock.Location = new Point(x, y);
            nudStock.Size     = new Size(100, 23);
            nudStock.Maximum  = 99999;
            Controls.Add(nudStock);
            y += 34;

            Controls.Add(new Label { AutoSize = true, Location = new Point(x, y), Text = "Default Vendor:" });
            y += 20;
            cboVendor.Location     = new Point(x, y);
            cboVendor.Size         = new Size(350, 23);
            cboVendor.DropDownStyle = ComboBoxStyle.DropDownList;
            Controls.Add(cboVendor);
            y += 34;

            chkActive.AutoSize = true;
            chkActive.Location = new Point(x, y);
            chkActive.Text     = "Active";
            chkActive.Checked  = true;
            Controls.Add(chkActive);
            y += 34;

            btnSave.Location = new Point(x, y);
            btnSave.Size     = new Size(120, 30);
            btnSave.Text     = "Save";
            btnSave.Enabled  = false;
            btnSave.Click   += BtnSave_Click;
            Controls.Add(btnSave);
            y += 40;

            var sep = new Label { Location = new Point(x, y), Size = new Size(350, 1), BorderStyle = BorderStyle.Fixed3D };
            Controls.Add(sep);
            y += 14;

            btnNew.Location = new Point(x, y);
            btnNew.Size     = new Size(140, 32);
            btnNew.Text     = "+ Add New Part";
            btnNew.Click   += BtnNew_Click;
            Controls.Add(btnNew);

            btnClose.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(796, 516);
            btnClose.Size     = new Size(90, 30);
            btnClose.Text     = "Close";
            btnClose.Click   += (_, _) => Close();
            Controls.Add(btnClose);

            SetEditEnabled(false);
        }

        private void AddField(ref int y, int x, string label, TextBox txt)
        {
            Controls.Add(new Label { AutoSize = true, Location = new Point(x, y), Text = label });
            y += 20;
            txt.Location = new Point(x, y);
            txt.Size     = new Size(350, 23);
            Controls.Add(txt);
            y += 34;
        }

        private void SetEditEnabled(bool enabled)
        {
            txtPartNum.Enabled = enabled;
            txtName.Enabled    = enabled;
            txtDesc.Enabled    = enabled;
            txtCost.Enabled    = enabled;
            nudStock.Enabled   = enabled;
            cboVendor.Enabled  = enabled;
            chkActive.Enabled  = enabled;
            btnSave.Enabled    = enabled;
        }

        private void LoadParts()
        {
            try
            {
                // Populate vendor dropdown
                var vendors = _vendorRepo.GetAll().ToList();
                cboVendor.DataSource    = new[] { new Models.Vendor { VendorID = 0, VendorName = "(none)" } }
                    .Concat(vendors).ToList();
                cboVendor.DisplayMember = "VendorName";
                cboVendor.ValueMember   = "VendorID";

                var parts = _repo.GetAll(includeInactive: true);
                dgvParts.DataSource = parts.ToList();
                lblCount.Text = $"{parts.Count()} part(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load parts: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvParts_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvParts.SelectedRows.Count == 0) { SetEditEnabled(false); return; }
            if (dgvParts.SelectedRows[0].DataBoundItem is not Part part) return;

            _editing          = part;
            lblEdit.Text      = part.PartName;
            txtPartNum.Text   = part.PartNumber;
            txtName.Text      = part.PartName;
            txtDesc.Text      = part.Description ?? "";
            txtCost.Text      = part.UnitCost.ToString("G");
            nudStock.Value    = Math.Min(part.CurrentStock, (int)nudStock.Maximum);
            cboVendor.SelectedValue = part.DefaultVendorID ?? 0;
            chkActive.Checked = part.IsActive;
            SetEditEnabled(true);
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtPartNum.Text) || string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show(this, "Part Number and Name are required.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!decimal.TryParse(txtCost.Text, out decimal cost)) cost = 0;
            int? vendorId = cboVendor.SelectedValue is int v && v != 0 ? v : null;

            try
            {
                if (_editing == null)
                {
                    _repo.Add(new Part
                    {
                        PartNumber      = txtPartNum.Text.Trim(),
                        PartName        = txtName.Text.Trim(),
                        Description     = string.IsNullOrWhiteSpace(txtDesc.Text) ? null : txtDesc.Text.Trim(),
                        UnitCost        = cost,
                        CurrentStock    = (int)nudStock.Value,
                        IsActive        = chkActive.Checked,
                        DefaultVendorID = vendorId
                    });
                }
                else
                {
                    _editing.PartNumber      = txtPartNum.Text.Trim();
                    _editing.PartName        = txtName.Text.Trim();
                    _editing.Description     = string.IsNullOrWhiteSpace(txtDesc.Text) ? null : txtDesc.Text.Trim();
                    _editing.UnitCost        = cost;
                    _editing.IsActive        = chkActive.Checked;
                    _editing.DefaultVendorID = vendorId;
                    _repo.Update(_editing);
                }

                _editing = null;
                SetEditEnabled(false);
                lblEdit.Text = "Select a part to edit";
                LoadParts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Save failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnNew_Click(object? sender, EventArgs e)
        {
            _editing      = null;
            lblEdit.Text  = "New Part";
            txtPartNum.Clear();
            txtName.Clear();
            txtDesc.Clear();
            txtCost.Clear();
            nudStock.Value    = 0;
            chkActive.Checked = true;
            dgvParts.ClearSelection();
            SetEditEnabled(true);
            txtPartNum.Focus();
        }

    }

    // ── BOM Editor dialog ─────────────────────────────────────────────────────────

    internal class FormBomEditor : Form
    {
        private readonly Product        _product;
        private readonly PartRepository _repo;
        private DataGridView dgv        = new();
        private DataGridView dgvLabour  = new();
        private Button btnSave          = new();
        private Button btnCancel        = new();
        private Button btnAddPart       = new();
        private Button btnAddLabour     = new();

        public FormBomEditor(Product product, PartRepository repo)
        {
            _product = product;
            _repo    = repo;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            LoadBom();
        }

        private void BuildUI()
        {
            Text            = $"BOM — {_product.ProductName}";
            ClientSize      = new Size(580, 620);
            MinimumSize     = new Size(560, 580);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.CenterParent;

            var lbl = new Label { Text = $"Bill of Materials: {_product.ProductName}", Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, AutoSize = true, Location = new Point(12, 12) };
            Controls.Add(lbl);

            // ── Parts section ─────────────────────────────────────────────────
            Controls.Add(new Label { Text = "PARTS", Location = new Point(12, 38), AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Theme.TextMuted });

            dgv.Location          = new Point(12, 56);
            dgv.Size              = new Size(556, 220);
            dgv.Anchor            = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            dgv.AllowUserToAddRows    = false;
            dgv.AllowUserToDeleteRows = true;
            dgv.SelectionMode     = DataGridViewSelectionMode.FullRowSelect;
            dgv.AutoGenerateColumns = false;
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPartID",  Visible     = false });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPartNum", HeaderText  = "Part #",   Width = 110 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPartName",HeaderText  = "Part Name",AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colQty",     HeaderText  = "Qty",      Width = 60  });
            Controls.Add(dgv);

            btnAddPart.Text     = "+ Add Part";
            btnAddPart.Location = new Point(12, 284);
            btnAddPart.Size     = new Size(110, 28);
            btnAddPart.Click   += BtnAddPart_Click;
            Controls.Add(btnAddPart);

            // ── Labour section ────────────────────────────────────────────────
            Controls.Add(new Label { Text = "LABOUR COSTS", Location = new Point(12, 322), AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Theme.TextMuted });

            dgvLabour.Location          = new Point(12, 340);
            dgvLabour.Size              = new Size(556, 180);
            dgvLabour.Anchor            = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            dgvLabour.AllowUserToAddRows    = false;
            dgvLabour.AllowUserToDeleteRows = true;
            dgvLabour.AllowUserToResizeRows = false;
            dgvLabour.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvLabour.AutoGenerateColumns   = false;
            dgvLabour.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDesc",   HeaderText = "Description", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvLabour.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRate",   HeaderText = "Rate/hr ($)", Width = 100 });
            dgvLabour.Columns.Add(new DataGridViewTextBoxColumn { Name = "colHours",  HeaderText = "Hours",       Width = 70  });
            dgvLabour.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTotal",  HeaderText = "Total ($)",   Width = 90, ReadOnly = true });
            dgvLabour.CellEndEdit += (_, _) => RefreshLabourTotals();
            Controls.Add(dgvLabour);

            btnAddLabour.Text     = "+ Add Labour";
            btnAddLabour.Location = new Point(12, 528);
            btnAddLabour.Size     = new Size(120, 28);
            btnAddLabour.Click   += (_, _) =>
            {
                int idx = dgvLabour.Rows.Add("Labour", "0", "1", "$0.00");
                dgvLabour.Rows[idx].Tag = 0; // LabourCostID = 0 (new)
            };
            Controls.Add(btnAddLabour);

            btnSave.Text     = "Save BOM";
            btnSave.Location = new Point(380, 576);
            btnSave.Size     = new Size(90, 30);
            btnSave.Click   += BtnSave_Click;
            Controls.Add(btnSave);

            btnCancel.Text     = "Cancel";
            btnCancel.Location = new Point(478, 576);
            btnCancel.Size     = new Size(90, 30);
            btnCancel.Click   += (_, _) => Close();
            Controls.Add(btnCancel);

            SizeChanged += (_, _) => RepositionBottom();
            Load        += (_, _) => RepositionBottom();
        }

        private void RepositionBottom()
        {
            int bottom = ClientSize.Height - 8;
            btnCancel.Location   = new Point(ClientSize.Width - 8 - btnCancel.Width, bottom - btnCancel.Height);
            btnSave.Location     = new Point(btnCancel.Left - btnSave.Width - 8, bottom - btnSave.Height);
            btnAddLabour.Location = new Point(12, bottom - btnSave.Height - dgvLabour.Height - 32);
            dgvLabour.Location   = new Point(12, btnAddLabour.Bottom + 4);
        }

        private void LoadBom()
        {
            dgv.Rows.Clear();
            var bom = _repo.GetBom(_product.ProductID);
            foreach (var e in bom)
                dgv.Rows.Add(e.PartID, e.PartNumber, e.PartName, e.Quantity);

            dgvLabour.Rows.Clear();
            var labour = _repo.GetLabourCosts(_product.ProductID);
            foreach (var lc in labour)
            {
                int idx = dgvLabour.Rows.Add(lc.Description, lc.HourlyRate.ToString("F2"), lc.Hours.ToString("F2"), $"${lc.TotalCost:F2}");
                dgvLabour.Rows[idx].Tag = lc.LabourCostID;
            }
        }

        private void RefreshLabourTotals()
        {
            foreach (DataGridViewRow row in dgvLabour.Rows)
            {
                if (row.IsNewRow) continue;
                decimal.TryParse(row.Cells["colRate"].Value?.ToString(), out decimal rate);
                decimal.TryParse(row.Cells["colHours"].Value?.ToString(), out decimal hours);
                row.Cells["colTotal"].Value = $"${rate * hours:F2}";
            }
        }

        private void BtnAddPart_Click(object? sender, EventArgs e)
        {
            var allParts = _repo.GetAll();
            using var dlg = new FormPartPicker(allParts);
            if (dlg.ShowDialog(this) != DialogResult.OK || dlg.SelectedPart == null) return;

            var p = dlg.SelectedPart;
            foreach (DataGridViewRow row in dgv.Rows)
                if (row.Cells["colPartID"].Value?.ToString() == p.PartID.ToString()) return;

            dgv.Rows.Add(p.PartID, p.PartNumber, p.PartName, 1);
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            var entries = new List<(int partId, int qty)>();
            foreach (DataGridViewRow row in dgv.Rows)
            {
                if (row.IsNewRow) continue;
                if (!int.TryParse(row.Cells["colPartID"].Value?.ToString(), out int pid)) continue;
                int.TryParse(row.Cells["colQty"].Value?.ToString(), out int qty);
                entries.Add((pid, Math.Max(1, qty)));
            }

            var labourCosts = new List<BomLabourCost>();
            foreach (DataGridViewRow row in dgvLabour.Rows)
            {
                if (row.IsNewRow) continue;
                string desc = row.Cells["colDesc"].Value?.ToString() ?? "Labour";
                decimal.TryParse(row.Cells["colRate"].Value?.ToString(), out decimal rate);
                decimal.TryParse(row.Cells["colHours"].Value?.ToString(), out decimal hours);
                labourCosts.Add(new BomLabourCost
                {
                    ProductID   = _product.ProductID,
                    Description = string.IsNullOrWhiteSpace(desc) ? "Labour" : desc,
                    HourlyRate  = rate,
                    Hours       = hours > 0 ? hours : 1
                });
            }

            try
            {
                _repo.SetBom(_product.ProductID, entries);
                _repo.SetLabourCosts(_product.ProductID, labourCosts);
                MessageBox.Show(this, "BOM saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Save failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    internal class FormPartPicker : Form
    {
        private DataGridView dgv = new();
        public Part? SelectedPart { get; private set; }

        public FormPartPicker(IEnumerable<Part> parts)
        {
            Text            = "Select Part";
            ClientSize      = new Size(500, 380);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;

            dgv.Location       = new Point(12, 12);
            dgv.Size           = new Size(476, 320);
            dgv.ReadOnly       = true;
            dgv.AllowUserToAddRows    = false;
            dgv.SelectionMode  = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect    = false;
            dgv.AutoGenerateColumns = true;
            dgv.DataSource     = parts.ToList();
            Controls.Add(dgv);

            var btnOk = new Button { Text = "Select", Location = new Point(298, 340), Size = new Size(90, 30) };
            btnOk.Click += (_, _) =>
            {
                if (dgv.SelectedRows.Count > 0 && dgv.SelectedRows[0].DataBoundItem is Part p)
                { SelectedPart = p; DialogResult = DialogResult.OK; Close(); }
            };
            var btnC = new Button { Text = "Cancel", Location = new Point(396, 340), Size = new Size(90, 30) };
            btnC.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnOk);
            Controls.Add(btnC);

            Theme.Apply(this);
            Theme.MakeBorderless(this);
        }
    }

    /// <summary>
    /// Combined product browser + inline BOM editor.
    /// Replaces the two-step picker→editor flow: select a product on the left, edit its BOM on the right.
    /// </summary>
    internal class FormBomExplorer : Form
    {
        private readonly ProductRepository _productRepo = new();
        private readonly PartRepository    _partRepo    = new();
        private Product? _current;
        private List<Product> _all = new();

        // Header
        private Panel pnlHeader = new();

        // Left panel
        private TextBox      txtSearch = new();
        private DataGridView dgvProd   = new();

        // Right panel BOM controls
        private Panel        pnlBom           = new();
        private Label        lblHint          = new();
        private Label        lblTitle         = new();
        private Label        lblPartsSection  = new();
        private Label        lblLabourSection = new();
        private DataGridView dgvParts         = new();
        private DataGridView dgvLabour        = new();
        private Button       btnAddPart       = new();
        private Button       btnAddLabour     = new();
        private Button       btnSaveBom       = new();
        private Label        lblCostSummary   = new();

        public FormBomExplorer()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.MakeResizable(this);
            Theme.MakeDraggable(this, pnlHeader);
            Theme.AddCloseButton(this);
            LoadProducts();
        }

        private void BuildUI()
        {
            Text          = "BOM Explorer";
            ClientSize    = new Size(1280, 720);
            MinimumSize   = new Size(960, 560);
            StartPosition = FormStartPosition.CenterParent;

            // ── Header ──────────────────────────────────────────────────────────
            pnlHeader.Dock      = DockStyle.Top;
            pnlHeader.Height    = 36;
            pnlHeader.BackColor = Theme.Header;
            pnlHeader.Controls.Add(new Label
            {
                Text      = "BOM Explorer — Bill of Materials",
                AutoSize  = false,
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(12, 0, 0, 0)
            });
            Controls.Add(pnlHeader);

            // ── Left panel ───────────────────────────────────────────────────────
            var pnlLeft = new Panel { Width = 280, Dock = DockStyle.Left };

            pnlLeft.Controls.Add(new Label
            {
                Text      = "PRODUCTS",
                Font      = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Theme.TextMuted,
                Location  = new Point(8, 8),
                AutoSize  = true
            });

            txtSearch.Location        = new Point(8, 30);
            txtSearch.Size            = new Size(262, 26);
            txtSearch.Anchor          = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtSearch.PlaceholderText = "Search…";
            txtSearch.TextChanged    += (_, _) => FilterProducts();
            pnlLeft.Controls.Add(txtSearch);

            dgvProd.Location          = new Point(8, 62);
            dgvProd.Size              = new Size(262, 560);
            dgvProd.Anchor            = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvProd.ReadOnly              = true;
            dgvProd.AllowUserToAddRows    = false;
            dgvProd.AllowUserToResizeRows = false;
            dgvProd.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvProd.MultiSelect       = false;
            dgvProd.AutoGenerateColumns = false;
            dgvProd.RowHeadersVisible = false;
            dgvProd.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPID",  Visible = false });
            dgvProd.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName", HeaderText = "Product", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvProd.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSKU",  HeaderText = "SKU", Width = 80 });
            dgvProd.SelectionChanged += DgvProd_SelectionChanged;
            pnlLeft.Controls.Add(dgvProd);
            Controls.Add(pnlLeft);

            // ── Divider ──────────────────────────────────────────────────────────
            Controls.Add(new Panel { Width = 1, Dock = DockStyle.Left, BackColor = Theme.Border });

            // ── Right panel (fill) ───────────────────────────────────────────────
            var pnlRight = new Panel { Dock = DockStyle.Fill };

            lblHint.Text      = "← Select a product from the list to view or edit its BOM";
            lblHint.Font      = new Font("Segoe UI", 11F, FontStyle.Italic);
            lblHint.ForeColor = Theme.TextMuted;
            lblHint.Dock      = DockStyle.Fill;
            lblHint.TextAlign = ContentAlignment.MiddleCenter;
            pnlRight.Controls.Add(lblHint);

            // ── BOM pane (hidden until product is selected) ───────────────────
            pnlBom.Dock    = DockStyle.Fill;
            pnlBom.Visible = false;
            pnlBom.Padding = new Padding(12, 8, 12, 8);

            lblTitle.Font      = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblTitle.ForeColor = Theme.Gold;
            lblTitle.AutoSize  = false;

            lblPartsSection.Text      = "PARTS";
            lblPartsSection.Font      = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblPartsSection.ForeColor = Theme.TextMuted;
            lblPartsSection.AutoSize  = true;

            dgvParts.AllowUserToAddRows      = false;
            dgvParts.AllowUserToDeleteRows   = true;
            dgvParts.AllowUserToResizeRows   = false;
            dgvParts.SelectionMode           = DataGridViewSelectionMode.FullRowSelect;
            dgvParts.AutoGenerateColumns   = false;
            dgvParts.RowHeadersVisible     = false;
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPartID",   Visible = false });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPartNum",  HeaderText = "Part #", Width = 110 });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPartName", HeaderText = "Part Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colQty",      HeaderText = "Qty", Width = 60 });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUnitCost", HeaderText = "Unit Cost", Width = 84, ReadOnly = true });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLineCost", HeaderText = "Line Cost", Width = 84, ReadOnly = true });
            dgvParts.CellEndEdit += (_, _) => RefreshPartLineCosts();

            btnAddPart.Text   = "+ Add Part";
            btnAddPart.Size   = new Size(110, 26);
            btnAddPart.Click += BtnAddPart_Click;

            lblLabourSection.Text      = "LABOUR COSTS";
            lblLabourSection.Font      = new Font("Segoe UI", 8F, FontStyle.Bold);
            lblLabourSection.ForeColor = Theme.TextMuted;
            lblLabourSection.AutoSize  = true;

            dgvLabour.AllowUserToAddRows    = false;
            dgvLabour.AllowUserToDeleteRows = true;
            dgvLabour.AllowUserToResizeRows = false;
            dgvLabour.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvLabour.AutoGenerateColumns   = false;
            dgvLabour.RowHeadersVisible     = false;
            dgvLabour.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDesc",  HeaderText = "Description", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvLabour.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRate",  HeaderText = "Rate/hr ($)", Width = 100 });
            dgvLabour.Columns.Add(new DataGridViewTextBoxColumn { Name = "colHours", HeaderText = "Hours",       Width = 70 });
            dgvLabour.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTotal", HeaderText = "Total ($)",   Width = 90, ReadOnly = true });
            dgvLabour.CellEndEdit += (_, _) => RefreshLabourTotals();

            btnAddLabour.Text   = "+ Add Labour";
            btnAddLabour.Size   = new Size(120, 26);
            btnAddLabour.Click += (_, _) =>
            {
                int idx = dgvLabour.Rows.Add("Labour", "0", "1", "$0.00");
                dgvLabour.Rows[idx].Tag = 0;
            };

            btnSaveBom.Text   = "Save BOM";
            btnSaveBom.Size   = new Size(110, 32);
            btnSaveBom.Click += BtnSave_Click;

            lblCostSummary.AutoSize  = false;
            lblCostSummary.Font      = new Font("Segoe UI", 8.5F);
            lblCostSummary.ForeColor = Theme.TextSecondary;
            lblCostSummary.TextAlign = ContentAlignment.MiddleLeft;

            pnlBom.Controls.AddRange(new Control[]
            {
                lblTitle, lblPartsSection, dgvParts, btnAddPart,
                lblLabourSection, dgvLabour, btnAddLabour, btnSaveBom,
                lblCostSummary
            });
            pnlBom.SizeChanged += (_, _) => LayoutBom();

            pnlRight.Controls.Add(pnlBom);
            Controls.Add(pnlRight);
        }

        /// <summary>Recalculates absolute positions of BOM pane controls on resize.</summary>
        private void LayoutBom()
        {
            if (pnlBom.ClientSize.Width <= 0) return;
            int pad = pnlBom.Padding.Left;
            int x   = pad;
            int w   = pnlBom.ClientSize.Width - pad * 2;
            int h   = pnlBom.ClientSize.Height;
            int y   = pnlBom.Padding.Top;

            lblTitle.SetBounds(x, y, w, 26);               y += 30;
            lblPartsSection.Location = new Point(x, y);    y += 22;

            // fixedBelow: addPart(26+4) + labourLabel(22) + labourGrid(80 min) + costSummary(20) + buttonRow(36+4)
            int fixedBelow = 30 + 22 + 80 + 20 + 40;
            int partsH     = Math.Max(80, h - y - fixedBelow);
            dgvParts.SetBounds(x, y, w, partsH);           y += partsH + 4;
            btnAddPart.SetBounds(x, y, 110, 26);           y += 32;

            lblLabourSection.Location = new Point(x, y);   y += 22;
            // labourH: remaining space above cost summary + button row
            int labourH = Math.Max(60, h - y - 20 - 40);
            dgvLabour.SetBounds(x, y, w, labourH);         y += labourH + 4;

            lblCostSummary.SetBounds(x, y, w, 18);

            btnAddLabour.Location = new Point(x, h - 36);
            btnSaveBom.Location   = new Point(pnlBom.ClientSize.Width - pad - btnSaveBom.Width, h - 36);
        }

        private void LoadProducts()
        {
            _all = _productRepo.GetProducts().ToList();
            PopulateProductGrid(_all);
        }

        private void FilterProducts()
        {
            var q = txtSearch.Text.Trim();
            var filtered = string.IsNullOrEmpty(q)
                ? _all
                : _all.FindAll(p =>
                    (p.ProductName?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (p.SKU?.Contains(q, StringComparison.OrdinalIgnoreCase) == true));
            PopulateProductGrid(filtered);
        }

        private void PopulateProductGrid(IEnumerable<Product> products)
        {
            dgvProd.Rows.Clear();
            foreach (var p in products)
                dgvProd.Rows.Add(p.ProductID, p.ProductName, p.SKU);
        }

        private void DgvProd_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvProd.SelectedRows.Count == 0) return;
            if (!int.TryParse(dgvProd.SelectedRows[0].Cells["colPID"].Value?.ToString(), out int pid)) return;
            _current = _all.Find(p => p.ProductID == pid);
            if (_current == null) return;

            lblTitle.Text   = $"Bill of Materials: {_current.ProductName}";
            lblHint.Visible = false;
            pnlBom.Visible  = true;
            LoadBom();
            // Defer layout so pnlBom has its actual size after becoming visible
            BeginInvoke(() => LayoutBom());
        }

        private void LoadBom()
        {
            if (_current == null) return;
            dgvParts.Rows.Clear();
            foreach (var entry in _partRepo.GetBom(_current.ProductID))
            {
                int idx = dgvParts.Rows.Add(entry.PartID, entry.PartNumber, entry.PartName, entry.Quantity,
                    $"${entry.UnitCost:N2}", $"${entry.LineCost:N2}");
                dgvParts.Rows[idx].Tag = entry.UnitCost;  // store unit cost for recalc
            }

            dgvLabour.Rows.Clear();
            foreach (var lc in _partRepo.GetLabourCosts(_current.ProductID))
            {
                int idx = dgvLabour.Rows.Add(lc.Description, lc.HourlyRate.ToString("F2"), lc.Hours.ToString("F2"), $"${lc.TotalCost:F2}");
                dgvLabour.Rows[idx].Tag = lc.LabourCostID;
            }

            RefreshCostSummary();
        }

        private void RefreshPartLineCosts()
        {
            foreach (DataGridViewRow row in dgvParts.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Tag is not decimal unitCost) continue;
                int.TryParse(row.Cells["colQty"].Value?.ToString(), out int qty);
                row.Cells["colLineCost"].Value = $"${unitCost * qty:N2}";
            }
            RefreshCostSummary();
        }

        private void RefreshCostSummary()
        {
            decimal partsCost = 0m;
            foreach (DataGridViewRow row in dgvParts.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Tag is decimal unitCost)
                {
                    int.TryParse(row.Cells["colQty"].Value?.ToString(), out int qty);
                    partsCost += unitCost * qty;
                }
            }

            decimal labourCost = 0m;
            foreach (DataGridViewRow row in dgvLabour.Rows)
            {
                if (row.IsNewRow) continue;
                decimal.TryParse(row.Cells["colRate"].Value?.ToString(),  out decimal rate);
                decimal.TryParse(row.Cells["colHours"].Value?.ToString(), out decimal hours);
                labourCost += rate * hours;
            }

            decimal total = partsCost + labourCost;
            lblCostSummary.Text = $"Parts: ${partsCost:N2}   Labour: ${labourCost:N2}   Total BOM Cost: ${total:N2}";
        }

        private void RefreshLabourTotals()
        {
            foreach (DataGridViewRow row in dgvLabour.Rows)
            {
                if (row.IsNewRow) continue;
                decimal.TryParse(row.Cells["colRate"].Value?.ToString(),  out decimal rate);
                decimal.TryParse(row.Cells["colHours"].Value?.ToString(), out decimal hours);
                row.Cells["colTotal"].Value = $"${rate * hours:F2}";
            }
            RefreshCostSummary();
        }

        private void BtnAddPart_Click(object? sender, EventArgs e)
        {
            var allParts = _partRepo.GetAll();
            using var dlg = new FormPartPicker(allParts);
            if (dlg.ShowDialog(this) != DialogResult.OK || dlg.SelectedPart == null) return;
            var p = dlg.SelectedPart;
            foreach (DataGridViewRow row in dgvParts.Rows)
                if (row.Cells["colPartID"].Value?.ToString() == p.PartID.ToString()) return;
            dgvParts.Rows.Add(p.PartID, p.PartNumber, p.PartName, 1);
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (_current == null) return;

            var entries = new List<(int partId, int qty)>();
            foreach (DataGridViewRow row in dgvParts.Rows)
            {
                if (row.IsNewRow) continue;
                if (!int.TryParse(row.Cells["colPartID"].Value?.ToString(), out int pid)) continue;
                int.TryParse(row.Cells["colQty"].Value?.ToString(), out int qty);
                entries.Add((pid, Math.Max(1, qty)));
            }

            var labourCosts = new List<BomLabourCost>();
            foreach (DataGridViewRow row in dgvLabour.Rows)
            {
                if (row.IsNewRow) continue;
                string desc = row.Cells["colDesc"].Value?.ToString() ?? "Labour";
                decimal.TryParse(row.Cells["colRate"].Value?.ToString(),  out decimal rate);
                decimal.TryParse(row.Cells["colHours"].Value?.ToString(), out decimal hours);
                labourCosts.Add(new BomLabourCost
                {
                    ProductID   = _current.ProductID,
                    Description = string.IsNullOrWhiteSpace(desc) ? "Labour" : desc,
                    HourlyRate  = rate,
                    Hours       = hours > 0 ? hours : 1
                });
            }

            try
            {
                _partRepo.SetBom(_current.ProductID, entries);
                _partRepo.SetLabourCosts(_current.ProductID, labourCosts);
                MessageBox.Show(this, $"BOM saved for {_current.ProductName}.", "Saved",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Save failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
