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

        private DataGridView             dgvTypes     = new();
        private TextBox                  txtName      = new();
        private DataGridView             dgvAttrs     = new();
        private DataGridViewComboBoxColumn _colAttr   = new();
        private Button                   btnSave      = new();
        private Button                   btnNew       = new();
        private Button                   btnDelete    = new();
        private Button                   btnAttrLists = new();
        private Button                   btnImport    = new();
        private Button                   btnClose     = new();
        private Label                    lblEdit   = new();

        private ProductType? _editing;

        public FormProductTypes()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            try { _repo.EnsureSchema(); } catch { /* already logged at startup */ }
            LoadAttributeNames();
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

            _colAttr = new DataGridViewComboBoxColumn
            {
                Name         = "colAttr",
                HeaderText   = "Attribute Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FlatStyle    = FlatStyle.Popup,
            };
            dgvAttrs.Columns.Add(_colAttr);

            // Allow typing new attribute names not yet in the list
            dgvAttrs.DataError             += (s, e) => e.ThrowException = false;
            dgvAttrs.EditingControlShowing += DgvAttrs_EditingControlShowing;
            dgvAttrs.CellValidating        += DgvAttrs_CellValidating;

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

            btnAttrLists.Location = new Point(x + 240, y);
            btnAttrLists.Size     = new Size(130, 30);
            btnAttrLists.Text     = "Attribute Lists →";
            btnAttrLists.Click   += (_, _) =>
            {
                using var f = new FormAttributeLists();
                f.ShowDialog(this);
                LoadAttributeNames();
            };
            Controls.Add(btnAttrLists);

            btnImport.Location = new Point(x + 378, y);
            btnImport.Size     = new Size(100, 30);
            btnImport.Text     = "Import CSV";
            btnImport.Click   += BtnImport_Click;
            Controls.Add(btnImport);

            btnClose.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(754, 498);
            btnClose.Size     = new Size(90, 30);
            btnClose.Text     = "Close";
            btnClose.Click   += (_, _) => Close();
            Controls.Add(btnClose);

            SizeChanged += (_, _) =>
                btnClose.Location = new Point(ClientSize.Width - btnClose.Width - 12, ClientSize.Height - btnClose.Height - 10);
        }

        private void LoadAttributeNames()
        {
            _colAttr.Items.Clear();
            try
            {
                foreach (var (_, name, _) in _repo.GetAttributeDefinitions())
                    _colAttr.Items.Add(name);
            }
            catch { /* best-effort */ }
        }

        private void DgvAttrs_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (dgvAttrs.CurrentCell?.OwningColumn?.Name != "colAttr") return;
            if (e.Control is not ComboBox cbo) return;
            cbo.DropDownStyle      = ComboBoxStyle.DropDown;
            cbo.AutoCompleteMode   = AutoCompleteMode.SuggestAppend;
            cbo.AutoCompleteSource = AutoCompleteSource.ListItems;
        }

        private void DgvAttrs_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.ColumnIndex != _colAttr.Index) return;
            string? val = e.FormattedValue?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(val) && !_colAttr.Items.Contains(val))
                _colAttr.Items.Add(val);
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
                // Ensure this name is available in the dropdown (handles legacy names)
                if (!_colAttr.Items.Contains(a.AttributeName))
                    _colAttr.Items.Add(a.AttributeName);

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

        /// <summary>
        /// Import product type → attribute assignments from a CSV.
        /// Required columns (header row): TypeName, AttributeName, IsRequired
        ///   • TypeName     — creates the type if it does not already exist
        ///   • AttributeName — the attribute to assign to the type
        ///   • IsRequired    — true/false/yes/no/1/0  (default: true if blank)
        /// Example:
        ///   TypeName,AttributeName,IsRequired
        ///   Juice,SizeML,true
        ///   Juice,VGPercent,true
        ///   Juice,Brand,false
        /// </summary>
        private void BtnImport_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title  = "Import Product Type Attribute Assignments",
                Filter = "CSV Files (*.csv)|*.csv"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var lines = File.ReadAllLines(dlg.FileName);
                if (lines.Length < 2)
                {
                    MessageBox.Show(this, "File appears empty or has no data rows.", "Import",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var headers = lines[0].Split(',')
                    .Select(h => h.Trim().Trim('"').ToLowerInvariant()).ToArray();
                int iType = Array.IndexOf(headers, "typename");
                int iAttr = Array.IndexOf(headers, "attributename");
                int iReq  = Array.IndexOf(headers, "isrequired");

                if (iType < 0 || iAttr < 0)
                {
                    MessageBox.Show(this,
                        "Columns 'TypeName' and 'AttributeName' are required in the header.\n" +
                        "Expected: TypeName, AttributeName, IsRequired",
                        "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Group rows by TypeName
                var byType = new Dictionary<string, List<Models.ProductTypeAttr>>(StringComparer.OrdinalIgnoreCase);
                int skipped = 0;
                foreach (var raw in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(raw)) { skipped++; continue; }
                    var cols     = raw.Split(',');
                    string tname = GetImportCol(cols, iType);
                    string aname = GetImportCol(cols, iAttr);
                    if (string.IsNullOrWhiteSpace(tname) || string.IsNullOrWhiteSpace(aname)) { skipped++; continue; }

                    string reqRaw = GetImportCol(cols, iReq, "true").ToLowerInvariant();
                    bool   isReq  = reqRaw is "true" or "yes" or "1";

                    if (!byType.TryGetValue(tname, out var list))
                        byType[tname] = list = new List<Models.ProductTypeAttr>();
                    list.Add(new Models.ProductTypeAttr(aname, isReq));
                }

                // Upsert each type
                var existingTypes = _repo.GetAll();
                int typesSaved = 0;
                foreach (var (typeName, attrs) in byType)
                {
                    var existing = existingTypes.FirstOrDefault(t =>
                        t.TypeName.Equals(typeName, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        _repo.Add(typeName, attrs);
                    }
                    else
                    {
                        // Merge: keep existing attrs + add new ones from CSV (no duplicates)
                        var merged = existing.AllAttributes.ToList();
                        foreach (var a in attrs)
                        {
                            if (!merged.Any(m => m.AttributeName.Equals(a.AttributeName, StringComparison.OrdinalIgnoreCase)))
                                merged.Add(a);
                        }
                        existing.AllAttributes = merged;
                        _repo.Update(existing);
                    }
                    typesSaved++;
                }

                LoadTypes();
                LoadAttributeNames();
                MessageBox.Show(this,
                    $"Import complete.\n{typesSaved} type(s) updated. {skipped} row(s) skipped.",
                    "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Import failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string GetImportCol(string[] cols, int idx, string defaultVal = "")
        {
            if (idx < 0 || idx >= cols.Length) return defaultVal;
            var v = cols[idx].Trim().Trim('"');
            return string.IsNullOrWhiteSpace(v) ? defaultVal : v;
        }
    }
}
