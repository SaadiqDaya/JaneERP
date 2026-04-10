using JaneERP.Data;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>
    /// CRUD for Product Types and their custom attributes.
    /// Each attribute can be marked Required (default) or Optional.
    /// </summary>
    public class FormProductTypes : Form
    {
        private readonly ProductTypeRepository _repo = new();

        private DataGridView dgvTypes  = new();
        private TextBox      txtName   = new();
        private DataGridView dgvAttrs  = new();
        private Button       btnSave   = new();
        private Button       btnNew    = new();
        private Button       btnDelete = new();
        private Button       btnClose  = new();
        private Label        lblEdit   = new();

        private ProductType? _editing;

        public FormProductTypes()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            LoadTypes();
        }

        private void BuildUI()
        {
            Text          = "Product Types";
            ClientSize    = new Size(860, 540);
            MinimumSize   = new Size(820, 520);
            StartPosition = FormStartPosition.CenterParent;

            dgvTypes.Location        = new Point(12, 12);
            dgvTypes.Size            = new Size(320, 480);
            dgvTypes.Anchor          = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            dgvTypes.ReadOnly        = true;
            dgvTypes.AllowUserToAddRows    = false;
            dgvTypes.AllowUserToDeleteRows = false;
            dgvTypes.SelectionMode   = DataGridViewSelectionMode.FullRowSelect;
            dgvTypes.MultiSelect     = false;
            dgvTypes.AutoGenerateColumns = false;
            dgvTypes.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "Type Name", DataPropertyName = "TypeName", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvTypes.SelectionChanged += DgvTypes_SelectionChanged;
            Controls.Add(dgvTypes);

            int x = 348, y = 12;

            lblEdit.AutoSize  = false;
            lblEdit.Font      = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblEdit.ForeColor = Theme.Gold;
            lblEdit.Location  = new Point(x, y);
            lblEdit.Size      = new Size(490, 26);
            lblEdit.Text      = "Select a type to edit";
            Controls.Add(lblEdit);
            y += 34;

            Controls.Add(new Label { AutoSize = true, Location = new Point(x, y), Text = "Type Name:" });
            y += 20;
            txtName.Location = new Point(x, y);
            txtName.Size     = new Size(300, 23);
            Controls.Add(txtName);
            y += 34;

            Controls.Add(new Label
            {
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location  = new Point(x, y),
                Text      = "Attributes:"
            });
            Controls.Add(new Label
            {
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8F),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(x + 90, y + 3),
                Text      = "(uncheck = optional — shown but not validated on save)"
            });
            y += 22;

            dgvAttrs.Location        = new Point(x, y);
            dgvAttrs.Size            = new Size(490, 280);
            dgvAttrs.Anchor          = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            dgvAttrs.AllowUserToAddRows    = true;
            dgvAttrs.AllowUserToDeleteRows = true;
            dgvAttrs.SelectionMode   = DataGridViewSelectionMode.FullRowSelect;
            dgvAttrs.AutoGenerateColumns = false;

            dgvAttrs.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colAttr", HeaderText = "Attribute Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            var chkCol = new DataGridViewCheckBoxColumn
            {
                Name        = "colRequired",
                HeaderText  = "Required",
                Width       = 72,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            };
            dgvAttrs.Columns.Add(chkCol);

            Controls.Add(dgvAttrs);
            y += 290;

            btnSave.Location = new Point(x, y);
            btnSave.Size     = new Size(100, 30);
            btnSave.Text     = "Save";
            btnSave.Enabled  = false;
            btnSave.Click   += BtnSave_Click;
            Controls.Add(btnSave);

            btnNew.Location = new Point(x + 110, y);
            btnNew.Size     = new Size(120, 30);
            btnNew.Text     = "+ New Type";
            btnNew.Click   += BtnNew_Click;
            Controls.Add(btnNew);

            btnDelete.Location = new Point(x + 240, y);
            btnDelete.Size     = new Size(100, 30);
            btnDelete.Text     = "Delete Type";
            btnDelete.Enabled  = false;
            btnDelete.Click   += BtnDelete_Click;
            Controls.Add(btnDelete);

            btnClose.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(754, 498);
            btnClose.Size     = new Size(90, 30);
            btnClose.Text     = "Close";
            btnClose.Click   += (_, _) => Close();
            Controls.Add(btnClose);

            SizeChanged += (_, _) =>
                btnClose.Location = new Point(ClientSize.Width - btnClose.Width - 12, ClientSize.Height - btnClose.Height - 10);
        }

        private void LoadTypes()
        {
            try { dgvTypes.DataSource = _repo.GetAll(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load types: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvTypes_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvTypes.SelectedRows.Count == 0) { btnSave.Enabled = false; btnDelete.Enabled = false; return; }
            if (dgvTypes.SelectedRows[0].DataBoundItem is not ProductType pt) return;

            _editing     = pt;
            lblEdit.Text = pt.TypeName;
            txtName.Text = pt.TypeName;
            dgvAttrs.Rows.Clear();
            foreach (var a in pt.AllAttributes)
            {
                int idx = dgvAttrs.Rows.Add(a.AttributeName, a.IsRequired);
                // Ensure checkbox default is true if the value wasn't set
                if (dgvAttrs.Rows[idx].Cells["colRequired"].Value == null)
                    dgvAttrs.Rows[idx].Cells["colRequired"].Value = true;
            }

            btnSave.Enabled   = true;
            btnDelete.Enabled = true;
        }

        private void BtnNew_Click(object? sender, EventArgs e)
        {
            _editing     = null;
            lblEdit.Text = "New Type";
            txtName.Clear();
            dgvAttrs.Rows.Clear();
            dgvTypes.ClearSelection();
            btnSave.Enabled   = true;
            btnDelete.Enabled = false;
            txtName.Focus();
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            var name = txtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Type name is required.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var attrs = dgvAttrs.Rows.Cast<DataGridViewRow>()
                .Where(r => !r.IsNewRow)
                .Select(r =>
                {
                    var attrName   = r.Cells["colAttr"].Value?.ToString()?.Trim() ?? "";
                    var isRequired = r.Cells["colRequired"].Value is true || r.Cells["colRequired"].Value == null;
                    return new ProductTypeAttr(attrName, isRequired);
                })
                .Where(a => !string.IsNullOrEmpty(a.AttributeName))
                .DistinctBy(a => a.AttributeName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            try
            {
                if (_editing == null)
                {
                    _repo.Add(name, attrs);
                }
                else
                {
                    _editing.TypeName     = name;
                    _editing.AllAttributes = attrs;
                    _repo.Update(_editing);
                }

                _editing          = null;
                btnSave.Enabled   = false;
                btnDelete.Enabled = false;
                LoadTypes();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Save failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (_editing == null) return;
            if (MessageBox.Show(this, $"Delete type '{_editing.TypeName}'? Products using it will have their type cleared.",
                    "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            try
            {
                _repo.Delete(_editing.ProductTypeID);
                _editing = null;
                LoadTypes();
                txtName.Clear();
                dgvAttrs.Rows.Clear();
                btnSave.Enabled   = false;
                btnDelete.Enabled = false;
                lblEdit.Text      = "Select a type to edit";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Delete failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
