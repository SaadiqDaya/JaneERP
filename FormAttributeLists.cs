using JaneERP.Data;

namespace JaneERP
{
    /// <summary>
    /// Manage allowed values for each product attribute name.
    /// E.g. "Nicotine" → "0, 3, 6, 12, 18"
    /// Values are shown as autocomplete suggestions when editing product attributes.
    /// </summary>
    public class FormAttributeLists : Form
    {
        private readonly ProductTypeRepository _repo = new();

        private DataGridView dgv        = new();
        private TextBox      txtName    = new();
        private TextBox      txtValues  = new();
        private Label        lblInfo    = new();
        private Button       btnSave    = new();
        private Button       btnDelete  = new();
        private Button       btnNew     = new();
        private Button       btnImport  = new();

        private int _editingId = 0;  // 0 = new

        public FormAttributeLists()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            LoadData();
        }

        private void BuildUI()
        {
            Text          = "Attribute Lists";
            ClientSize    = new Size(820, 520);
            MinimumSize   = new Size(720, 460);
            StartPosition = FormStartPosition.CenterParent;

            // ── Title ─────────────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = "Attribute Lists",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            });
            Controls.Add(new Label
            {
                Text      = "Define the valid values for each product attribute name.",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(12, 40),
                AutoSize  = true
            });

            // ── Left: list of attribute names ─────────────────────────────────
            dgv.Location          = new Point(12, 66);
            dgv.Size              = new Size(300, 390);
            dgv.Anchor            = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            dgv.ReadOnly          = true;
            dgv.AllowUserToAddRows    = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.SelectionMode     = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect       = false;
            dgv.AutoGenerateColumns = false;
            dgv.RowHeadersVisible = false;
            dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colName", HeaderText = "Attribute Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });
            dgv.SelectionChanged += DgvSelectionChanged;
            Controls.Add(dgv);

            // ── Right: edit panel ─────────────────────────────────────────────
            int ex = 330, y = 66;

            Controls.Add(new Label
            {
                Text      = "Attribute Name:",
                Location  = new Point(ex, y + 3),
                AutoSize  = true,
                ForeColor = Theme.TextSecondary
            });
            txtName.Location = new Point(ex + 130, y);
            txtName.Size     = new Size(260, 24);
            Controls.Add(txtName);
            y += 34;

            Controls.Add(new Label
            {
                Text      = "Allowed Values:",
                Location  = new Point(ex, y + 3),
                AutoSize  = true,
                ForeColor = Theme.TextSecondary
            });
            txtValues.Location   = new Point(ex + 130, y);
            txtValues.Size       = new Size(260, 80);
            txtValues.Multiline  = true;
            txtValues.ScrollBars = ScrollBars.Vertical;
            txtValues.PlaceholderText = "Comma-separated, e.g. 0, 3, 6, 12, 18";
            Controls.Add(txtValues);
            y += 92;

            lblInfo.Location = new Point(ex + 130, y);
            lblInfo.Size     = new Size(260, 36);
            lblInfo.Font     = new Font("Segoe UI", 8F);
            lblInfo.ForeColor = Theme.TextMuted;
            lblInfo.Text     = "Enter values separated by commas.\nLeave blank to allow any value.";
            Controls.Add(lblInfo);
            y += 48;

            btnSave.Text     = "Save";
            btnSave.Size     = new Size(80, 28);
            btnSave.Location = new Point(ex + 130, y);
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click   += BtnSave_Click;
            Controls.Add(btnSave);

            btnDelete.Text     = "Delete";
            btnDelete.Size     = new Size(80, 28);
            btnDelete.Location = new Point(ex + 220, y);
            btnDelete.UseVisualStyleBackColor = true;
            btnDelete.Click   += BtnDelete_Click;
            Controls.Add(btnDelete);
            y += 36;

            btnNew.Text     = "+ New";
            btnNew.Size     = new Size(80, 28);
            btnNew.Location = new Point(ex + 130, y);
            btnNew.UseVisualStyleBackColor = true;
            btnNew.Click   += (_, _) =>
            {
                _editingId    = 0;
                txtName.Text  = "";
                txtValues.Text = "";
                dgv.ClearSelection();
                txtName.Focus();
            };
            Controls.Add(btnNew);

            btnImport.Text     = "Import CSV";
            btnImport.Size     = new Size(90, 28);
            btnImport.Location = new Point(ex + 220, y);
            btnImport.UseVisualStyleBackColor = true;
            btnImport.Click   += BtnImport_Click;
            Controls.Add(btnImport);

            // ── Close button row ──────────────────────────────────────────────
            SizeChanged += (_, _) => PositionGrid();
            Load        += (_, _) => PositionGrid();
        }

        private void PositionGrid()
        {
            dgv.Size = new Size(300, ClientSize.Height - dgv.Top - 66);
        }

        private void LoadData()
        {
            var defs = _repo.GetAttributeDefinitions();
            dgv.Rows.Clear();
            foreach (var (id, name, _) in defs)
            {
                int idx = dgv.Rows.Add(name);
                dgv.Rows[idx].Tag = id;
            }
        }

        private void DgvSelectionChanged(object? sender, EventArgs e)
        {
            if (dgv.CurrentRow == null) return;
            var name = dgv.CurrentRow.Cells["colName"].Value?.ToString() ?? "";
            _editingId    = (int)(dgv.CurrentRow.Tag ?? 0);
            txtName.Text  = name;
            var defs = _repo.GetAttributeDefinitions();
            var match = defs.FirstOrDefault(d => d.Id == _editingId);
            txtValues.Text = match.Values ?? "";
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            string name = txtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Attribute name is required.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string? values = string.IsNullOrWhiteSpace(txtValues.Text) ? null : txtValues.Text.Trim();

            try
            {
                _repo.UpsertAttributeDefinition(name, values);
                LoadData();
                // Select the just-saved row
                foreach (DataGridViewRow row in dgv.Rows)
                {
                    if (row.Cells["colName"].Value?.ToString() == name)
                    {
                        row.Selected = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Save failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (_editingId == 0 || dgv.CurrentRow == null) return;
            string name = dgv.CurrentRow.Cells["colName"].Value?.ToString() ?? "";
            if (MessageBox.Show(this, $"Delete attribute list '{name}'?", "Confirm Delete",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                _repo.DeleteAttributeDefinition(_editingId);
                _editingId     = 0;
                txtName.Text   = "";
                txtValues.Text = "";
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Delete failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnImport_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title  = "Import Attribute Lists from CSV",
                Filter = "CSV Files (*.csv)|*.csv"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                // Expect columns: AttributeName, AllowedValues
                var lines   = File.ReadAllLines(dlg.FileName);
                int imported = 0, skipped = 0;

                foreach (var line in lines.Skip(1)) // skip header
                {
                    var parts = line.Split(',', 2);
                    if (parts.Length < 1 || string.IsNullOrWhiteSpace(parts[0])) { skipped++; continue; }
                    string attrName = parts[0].Trim().Trim('"');
                    string? values  = parts.Length > 1 ? parts[1].Trim().Trim('"') : null;
                    _repo.UpsertAttributeDefinition(attrName, string.IsNullOrWhiteSpace(values) ? null : values);
                    imported++;
                }

                LoadData();
                MessageBox.Show(this, $"Imported {imported} attribute(s). Skipped {skipped}.",
                    "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Import failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
