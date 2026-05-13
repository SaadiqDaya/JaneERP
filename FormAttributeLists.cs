using JaneERP.Data;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>
    /// Manage attribute definitions: name, category (Manufacturing/Marketing/General),
    /// data type (Text/Number/List), optional unit, and the list of allowed values.
    ///
    /// Two import modes:
    ///   • "Import Definitions CSV" — full row per attribute (Name,Category,DataType,Unit,AllowedValues)
    ///   • "Import Values for Selected" — single-column list that becomes AllowedValues for the
    ///     currently selected attribute (e.g. paste your concentrates CSV).
    /// </summary>
    public class FormAttributeLists : Form
    {
        private readonly ProductTypeRepository _repo = new();

        // ── Left panel ────────────────────────────────────────────────────────────
        private DataGridView _dgv       = new();
        private TextBox      _txtSearch = new();

        // ── Right panel ──────────────────────────────────────────────────────────
        private TextBox   _txtName     = new();
        private ComboBox  _cboCategory = new();
        private ComboBox  _cboDataType = new();
        private TextBox   _txtUnit     = new();
        private TextBox   _txtValues   = new();

        private Button _btnSave       = new();
        private Button _btnDelete     = new();
        private Button _btnNew        = new();
        private Button _btnImportDefs = new();
        private Button _btnImportVals = new();

        private int _editingId = 0;

        public FormAttributeLists()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            try { _repo.EnsureSchema(); } catch { }
            SeedFromProductTypeAttributes();
            LoadData();
        }

        private void SeedFromProductTypeAttributes()
        {
            try
            {
                var existing = _repo.GetAttributeDefinitions();
                if (existing.Count > 0) return;
                var typeAttrNames = _repo.GetAll()
                    .SelectMany(t => t.AllAttributes)
                    .Select(a => a.AttributeName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n);
                foreach (var name in typeAttrNames)
                    _repo.UpsertAttributeDefinition(name, null);
            }
            catch { }
        }

        private void BuildUI()
        {
            Text          = "Attribute Lists";
            ClientSize    = new Size(960, 580);
            MinimumSize   = new Size(820, 500);
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
                Text      = "Define attribute names, categories, types, and allowed values.",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(12, 40),
                AutoSize  = true
            });

            // ── Search box ────────────────────────────────────────────────────
            _txtSearch.Location        = new Point(12, 66);
            _txtSearch.Size            = new Size(310, 23);
            _txtSearch.PlaceholderText = "Search attribute name…";
            _txtSearch.TextChanged    += (_, _) => LoadData(_txtSearch.Text);
            Controls.Add(_txtSearch);

            // ── Left: list grid ───────────────────────────────────────────────
            _dgv.Location          = new Point(12, 96);
            _dgv.Size              = new Size(340, 420);
            _dgv.Anchor            = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            _dgv.ReadOnly          = true;
            _dgv.AllowUserToAddRows    = false;
            _dgv.AllowUserToDeleteRows = false;
            _dgv.SelectionMode     = DataGridViewSelectionMode.FullRowSelect;
            _dgv.MultiSelect       = false;
            _dgv.AutoGenerateColumns = false;
            _dgv.RowHeadersVisible     = false;
            _dgv.AllowUserToResizeRows = false;
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colName",     HeaderText = "Attribute Name", Width = 180 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colCategory", HeaderText = "Category",       Width = 110 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colType",     HeaderText = "Type",           Width = 50, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _dgv.SelectionChanged += DgvSelectionChanged;
            Controls.Add(_dgv);

            // ── Right: edit panel ─────────────────────────────────────────────
            int ex = 370, y = 66;
            int lblW = 115, ctrlX = ex + lblW;

            Controls.Add(MakeLabel("Attribute Name:", ex, y + 3));
            _txtName.Location = new Point(ctrlX, y);
            _txtName.Size     = new Size(300, 24);
            Controls.Add(_txtName);
            y += 34;

            Controls.Add(MakeLabel("Category:", ex, y + 3));
            _cboCategory.Location      = new Point(ctrlX, y);
            _cboCategory.Size          = new Size(160, 24);
            _cboCategory.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboCategory.Items.AddRange(new object[] { "General", "Manufacturing", "Marketing" });
            _cboCategory.SelectedIndex = 0;
            Controls.Add(_cboCategory);
            Controls.Add(new Label
            {
                Text      = "Manufacturing = used for batch maths\nMarketing = labels & display",
                Font      = new Font("Segoe UI", 7.5F),
                ForeColor = Theme.TextMuted,
                Location  = new Point(ctrlX + 168, y),
                AutoSize  = true
            });
            y += 34;

            Controls.Add(MakeLabel("Data Type:", ex, y + 3));
            _cboDataType.Location      = new Point(ctrlX, y);
            _cboDataType.Size          = new Size(120, 24);
            _cboDataType.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboDataType.Items.AddRange(new object[] { "Text", "Number", "List" });
            _cboDataType.SelectedIndex = 0;
            Controls.Add(_cboDataType);
            y += 34;

            Controls.Add(MakeLabel("Unit (optional):", ex, y + 3));
            _txtUnit.Location        = new Point(ctrlX, y);
            _txtUnit.Size            = new Size(100, 24);
            _txtUnit.PlaceholderText = "ml, mg/ml, %…";
            Controls.Add(_txtUnit);
            Controls.Add(new Label
            {
                Text      = "Only relevant for Number type",
                Font      = new Font("Segoe UI", 7.5F),
                ForeColor = Theme.TextMuted,
                Location  = new Point(ctrlX + 108, y + 4),
                AutoSize  = true
            });
            y += 34;

            Controls.Add(MakeLabel("Allowed Values:", ex, y + 3));
            _txtValues.Location       = new Point(ctrlX, y);
            _txtValues.Size           = new Size(300, 100);
            _txtValues.Multiline      = true;
            _txtValues.ScrollBars     = ScrollBars.Vertical;
            _txtValues.PlaceholderText = "Comma-separated, e.g. FreeBase, Salt\nLeave blank to allow any value.";
            Controls.Add(_txtValues);
            y += 112;

            // Buttons
            _btnSave.Text     = "Save";
            _btnSave.Size     = new Size(80, 28);
            _btnSave.Location = new Point(ctrlX, y);
            _btnSave.Click   += BtnSave_Click;
            Theme.StyleButton(_btnSave);
            Controls.Add(_btnSave);

            _btnDelete.Text     = "Delete";
            _btnDelete.Size     = new Size(80, 28);
            _btnDelete.Location = new Point(ctrlX + 88, y);
            _btnDelete.Click   += BtnDelete_Click;
            Controls.Add(_btnDelete);

            _btnNew.Text     = "+ New";
            _btnNew.Size     = new Size(80, 28);
            _btnNew.Location = new Point(ctrlX + 176, y);
            _btnNew.Click   += (_, _) =>
            {
                _editingId = 0;
                _txtName.Text  = "";
                _txtValues.Text = "";
                _txtUnit.Text   = "";
                _cboCategory.SelectedIndex = 0;
                _cboDataType.SelectedIndex = 0;
                _dgv.ClearSelection();
                _txtName.Focus();
            };
            Controls.Add(_btnNew);
            y += 38;

            // Import buttons
            _btnImportDefs.Text     = "Import Definitions CSV";
            _btnImportDefs.Size     = new Size(190, 28);
            _btnImportDefs.Location = new Point(ctrlX, y);
            _btnImportDefs.Click   += BtnImportDefs_Click;
            Controls.Add(_btnImportDefs);

            _btnImportVals.Text     = "Import Values for Selected";
            _btnImportVals.Size     = new Size(210, 28);
            _btnImportVals.Location = new Point(ctrlX + 198, y);
            _btnImportVals.Click   += BtnImportVals_Click;
            Controls.Add(_btnImportVals);

            // Column hint
            Controls.Add(new Label
            {
                Text      = "Definitions CSV columns: AttributeName, Category, DataType, Unit, AllowedValues\n" +
                             "Values CSV: single column of values (one per line or comma-separated).",
                Font      = new Font("Segoe UI", 7.5F),
                ForeColor = Theme.TextMuted,
                Location  = new Point(ex, ClientSize.Height - 46),
                Size      = new Size(560, 32),
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left
            });

            SizeChanged += (_, _) => PositionGrid();
            Load        += (_, _) => PositionGrid();
        }

        private static Label MakeLabel(string text, int x, int y) =>
            new() { Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = Theme.TextSecondary };

        private void PositionGrid()
        {
            _dgv.Size = new Size(_dgv.Width, ClientSize.Height - _dgv.Top - 66);
        }

        private void LoadData(string? filter = null)
        {
            try
            {
                var defs = _repo.GetAttributeDefinitions();
                if (!string.IsNullOrWhiteSpace(filter))
                    defs = defs.Where(d => d.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

                _dgv.Rows.Clear();
                foreach (var d in defs)
                {
                    int idx = _dgv.Rows.Add(d.Name, d.Category, d.DataType);
                    _dgv.Rows[idx].Tag = d.Id;
                    // Colour-code by category
                    _dgv.Rows[idx].Cells["colCategory"].Style.ForeColor = d.Category switch
                    {
                        "Manufacturing" => Theme.Teal,
                        "Marketing"     => Theme.Gold,
                        _               => Theme.TextSecondary
                    };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load attribute definitions:\n\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvSelectionChanged(object? sender, EventArgs e)
        {
            if (_dgv.CurrentRow == null) return;
            _editingId = (int)(_dgv.CurrentRow.Tag ?? 0);
            var name = _dgv.CurrentRow.Cells["colName"].Value?.ToString() ?? "";
            _txtName.Text = name;

            var all   = _repo.GetAttributeDefinitions();
            var match = all.FirstOrDefault(d => d.Id == _editingId);
            if (match != null)
            {
                _txtValues.Text    = match.AllowedValues ?? "";
                _txtUnit.Text      = match.Unit ?? "";
                _cboCategory.SelectedItem = match.Category;
                _cboDataType.SelectedItem = match.DataType;
                if (_cboCategory.SelectedIndex < 0) _cboCategory.SelectedIndex = 0;
                if (_cboDataType.SelectedIndex < 0) _cboDataType.SelectedIndex = 0;
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            string name = _txtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Attribute name is required.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string? values   = string.IsNullOrWhiteSpace(_txtValues.Text) ? null : _txtValues.Text.Trim();
            string  category = _cboCategory.SelectedItem?.ToString() ?? "General";
            string  dataType = _cboDataType.SelectedItem?.ToString() ?? "Text";
            string? unit     = string.IsNullOrWhiteSpace(_txtUnit.Text) ? null : _txtUnit.Text.Trim();

            try
            {
                _repo.UpsertAttributeDefinition(name, values, category, dataType, unit);
                LoadData(_txtSearch.Text);
                SelectRowByName(name);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Save failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (_editingId == 0 || _dgv.CurrentRow == null) return;
            string name = _dgv.CurrentRow.Cells["colName"].Value?.ToString() ?? "";
            if (MessageBox.Show(this, $"Delete attribute definition '{name}'?", "Confirm Delete",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                _repo.DeleteAttributeDefinition(_editingId);
                _editingId = 0;
                _txtName.Text = _txtValues.Text = _txtUnit.Text = "";
                _cboCategory.SelectedIndex = 0;
                _cboDataType.SelectedIndex = 0;
                LoadData(_txtSearch.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Delete failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Import full attribute definitions from a CSV.
        /// Expected columns (header row required):
        ///   AttributeName, Category, DataType, Unit, AllowedValues
        /// Category, DataType, Unit, AllowedValues are optional — defaults used if blank.
        /// </summary>
        private void BtnImportDefs_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title  = "Import Attribute Definitions",
                Filter = "CSV Files (*.csv)|*.csv"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var lines    = File.ReadAllLines(dlg.FileName);
                if (lines.Length < 2)
                {
                    MessageBox.Show(this, "File appears empty or has no data rows.", "Import",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Parse header to find column indices (case-insensitive)
                var headers = lines[0].Split(',').Select(h => h.Trim().Trim('"').ToLowerInvariant()).ToArray();
                int iName   = Array.IndexOf(headers, "attributename");
                int iCat    = Array.IndexOf(headers, "category");
                int iType   = Array.IndexOf(headers, "datatype");
                int iUnit   = Array.IndexOf(headers, "unit");
                int iVals   = Array.IndexOf(headers, "allowedvalues");

                if (iName < 0)
                {
                    MessageBox.Show(this,
                        "Column 'AttributeName' not found in header.\n" +
                        "Required header: AttributeName, Category, DataType, Unit, AllowedValues",
                        "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                int imported = 0, skipped = 0;
                foreach (var raw in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(raw)) { skipped++; continue; }
                    var cols = SplitCsvLine(raw);
                    string name = GetCol(cols, iName);
                    if (string.IsNullOrWhiteSpace(name)) { skipped++; continue; }

                    string  cat    = GetCol(cols, iCat,  "General");
                    string  dtype  = GetCol(cols, iType, "Text");
                    string? unit   = GetCol(cols, iUnit);
                    string? vals   = GetCol(cols, iVals);
                    if (string.IsNullOrWhiteSpace(unit))  unit  = null;
                    if (string.IsNullOrWhiteSpace(vals))  vals  = null;
                    if (!new[] { "General", "Manufacturing", "Marketing" }.Contains(cat,  StringComparer.OrdinalIgnoreCase)) cat   = "General";
                    if (!new[] { "Text", "Number", "List" }.Contains(dtype, StringComparer.OrdinalIgnoreCase)) dtype = "Text";

                    _repo.UpsertAttributeDefinition(name, vals, cat, dtype, unit);
                    imported++;
                }

                LoadData(_txtSearch.Text);
                MessageBox.Show(this, $"Imported {imported} attribute definition(s). Skipped {skipped}.",
                    "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Import failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Import a simple value list for the currently selected attribute.
        /// CSV can be:
        ///   • Single column, one value per row (no header)
        ///   • OR a comma-separated list in a single cell
        /// The imported values replace the attribute's existing AllowedValues.
        /// </summary>
        private void BtnImportVals_Click(object? sender, EventArgs e)
        {
            if (_editingId == 0)
            {
                MessageBox.Show(this, "Select an attribute from the list first.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string attrName = _dgv.CurrentRow?.Cells["colName"].Value?.ToString() ?? "";

            using var dlg = new OpenFileDialog
            {
                Title  = $"Import Values for '{attrName}'",
                Filter = "CSV or Text Files (*.csv;*.txt)|*.csv;*.txt|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var lines  = File.ReadAllLines(dlg.FileName);
                var values = new List<string>();
                foreach (var line in lines)
                {
                    // Each line may be a single value or a comma-separated row
                    foreach (var cell in line.Split(','))
                    {
                        var v = cell.Trim().Trim('"');
                        if (!string.IsNullOrWhiteSpace(v))
                            values.Add(v);
                    }
                }

                if (values.Count == 0)
                {
                    MessageBox.Show(this, "No values found in file.", "Import",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Confirm replacement
                var res = MessageBox.Show(this,
                    $"Replace allowed values for '{attrName}' with {values.Count} imported value(s)?",
                    "Confirm Import", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res != DialogResult.Yes) return;

                // Preserve existing category/datatype/unit
                var existing = _repo.GetAttributeDefinitions().FirstOrDefault(d => d.Id == _editingId);
                string cat   = existing?.Category ?? "General";
                string dtype = existing?.DataType ?? "Text";
                string? unit = existing?.Unit;

                string joined = string.Join(",", values);
                _repo.UpsertAttributeDefinition(attrName, joined, cat, dtype, unit);
                _txtValues.Text = joined;

                LoadData(_txtSearch.Text);
                SelectRowByName(attrName);

                MessageBox.Show(this, $"Imported {values.Count} values for '{attrName}'.",
                    "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Import failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SelectRowByName(string name)
        {
            foreach (DataGridViewRow row in _dgv.Rows)
            {
                if (row.Cells["colName"].Value?.ToString()
                       ?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
                { row.Selected = true; break; }
            }
        }

        // ── CSV helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Splits a CSV line respecting double-quoted fields.
        /// Does not handle escaped quotes within quoted fields — sufficient for simple attribute CSVs.
        /// </summary>
        private static string[] SplitCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuote = false;
            var  current = new System.Text.StringBuilder();
            foreach (char c in line)
            {
                if (c == '"')      { inQuote = !inQuote; }
                else if (c == ',' && !inQuote) { result.Add(current.ToString().Trim()); current.Clear(); }
                else               { current.Append(c); }
            }
            result.Add(current.ToString().Trim());
            return result.ToArray();
        }

        private static string GetCol(string[] cols, int idx, string defaultVal = "")
        {
            if (idx < 0 || idx >= cols.Length) return defaultVal;
            var v = cols[idx].Trim('"').Trim();
            return string.IsNullOrWhiteSpace(v) ? defaultVal : v;
        }

        private static string? GetCol(string[] cols, int idx)
        {
            if (idx < 0 || idx >= cols.Length) return null;
            var v = cols[idx].Trim('"').Trim();
            return string.IsNullOrWhiteSpace(v) ? null : v;
        }
    }
}
