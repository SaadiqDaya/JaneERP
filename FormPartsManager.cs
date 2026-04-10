using JaneERP.Data;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>CRUD for Parts and BOM management.</summary>
    public class FormPartsManager : Form
    {
        private readonly PartRepository _repo = new();

        private DataGridView dgvParts   = new();
        private Label        lblEdit    = new();
        private TextBox      txtPartNum = new();
        private TextBox      txtName    = new();
        private TextBox      txtDesc    = new();
        private TextBox      txtCost    = new();
        private NumericUpDown nudStock  = new();
        private CheckBox     chkActive  = new();
        private Button       btnSave    = new();
        private Button       btnNew     = new();
        private Button       btnBOM     = new();
        private Button       btnClose   = new();
        private Label        lblCount   = new();

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
            y += 42;

            btnBOM.Location = new Point(x, y);
            btnBOM.Size     = new Size(200, 32);
            btnBOM.Text     = "Edit BOM for Selected Product…";
            btnBOM.Click   += BtnBOM_Click;
            Controls.Add(btnBOM);

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
            chkActive.Enabled  = enabled;
            btnSave.Enabled    = enabled;
        }

        private void LoadParts()
        {
            try
            {
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

            try
            {
                if (_editing == null)
                {
                    _repo.Add(new Part
                    {
                        PartNumber   = txtPartNum.Text.Trim(),
                        PartName     = txtName.Text.Trim(),
                        Description  = string.IsNullOrWhiteSpace(txtDesc.Text) ? null : txtDesc.Text.Trim(),
                        UnitCost     = cost,
                        CurrentStock = (int)nudStock.Value,
                        IsActive     = chkActive.Checked
                    });
                }
                else
                {
                    _editing.PartNumber  = txtPartNum.Text.Trim();
                    _editing.PartName    = txtName.Text.Trim();
                    _editing.Description = string.IsNullOrWhiteSpace(txtDesc.Text) ? null : txtDesc.Text.Trim();
                    _editing.UnitCost    = cost;
                    _editing.IsActive    = chkActive.Checked;
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

        private void BtnBOM_Click(object? sender, EventArgs e)
        {
            // Open a BOM editor for a chosen product
            var pRepo  = new ProductRepository();
            var picker = new FormProductPicker(pRepo);
            if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedProduct == null) return;

            using var bom = new FormBomEditor(picker.SelectedProduct, _repo);
            bom.ShowDialog(this);
        }
    }

    // ── BOM Editor dialog ─────────────────────────────────────────────────────────

    internal class FormBomEditor : Form
    {
        private readonly Product        _product;
        private readonly PartRepository _repo;
        private DataGridView dgv     = new();
        private Button btnSave       = new();
        private Button btnCancel     = new();
        private Button btnAddPart    = new();

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
            ClientSize      = new Size(560, 480);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;

            var lbl = new Label { Text = $"Bill of Materials: {_product.ProductName}", Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, AutoSize = true, Location = new Point(12, 12) };
            Controls.Add(lbl);

            dgv.Location          = new Point(12, 40);
            dgv.Size              = new Size(536, 360);
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
            btnAddPart.Location = new Point(12, 408);
            btnAddPart.Size     = new Size(110, 30);
            btnAddPart.Click   += BtnAddPart_Click;
            Controls.Add(btnAddPart);

            btnSave.Text     = "Save BOM";
            btnSave.Location = new Point(360, 408);
            btnSave.Size     = new Size(90, 30);
            btnSave.Click   += BtnSave_Click;
            Controls.Add(btnSave);

            btnCancel.Text     = "Cancel";
            btnCancel.Location = new Point(458, 408);
            btnCancel.Size     = new Size(90, 30);
            btnCancel.Click   += (_, _) => Close();
            Controls.Add(btnCancel);
        }

        private void LoadBom()
        {
            dgv.Rows.Clear();
            var bom = _repo.GetBom(_product.ProductID);
            foreach (var e in bom)
                dgv.Rows.Add(e.PartID, e.PartNumber, e.PartName, e.Quantity);
        }

        private void BtnAddPart_Click(object? sender, EventArgs e)
        {
            // Simple picker from all parts
            var allParts = _repo.GetAll();
            using var dlg = new FormPartPicker(allParts);
            if (dlg.ShowDialog(this) != DialogResult.OK || dlg.SelectedPart == null) return;

            var p = dlg.SelectedPart;
            // Check if already added
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

            try
            {
                _repo.SetBom(_product.ProductID, entries);
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
}
