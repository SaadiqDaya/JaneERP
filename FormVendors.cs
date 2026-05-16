using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;

namespace JaneERP
{
    public class FormVendors : Form
    {
        private readonly IVendorRepository _repo = AppServices.Get<IVendorRepository>();

        private DataGridView _dgvVendors = new();
        private DataGridView _dgvParts   = new();
        private TextBox      _txtSearch  = new();
        private TextBox      _txtName    = new();
        private TextBox      _txtContact = new();
        private TextBox      _txtEmail   = new();
        private TextBox      _txtPhone   = new();
        private TextBox      _txtWebsite = new();
        private CheckBox     _chkActive  = new();
        private Button       _btnSave    = new();
        private Button       _btnNew     = new();
        private Button       _btnDeact   = new();
        private Label        _lblStatus  = new();

        private List<Vendor> _allVendors = [];
        private int          _editingId  = 0;

        public FormVendors()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            LoadVendors();
        }

        private void BuildUI()
        {
            Text          = "Vendors";
            ClientSize    = new Size(920, 600);
            MinimumSize   = new Size(800, 520);
            StartPosition = FormStartPosition.CenterParent;

            // Search
            Controls.Add(new Label { Text = "Search:", Location = new Point(12, 60), AutoSize = true });
            _txtSearch.Location = new Point(68, 56);
            _txtSearch.Size     = new Size(200, 23);
            _txtSearch.PlaceholderText = "Filter vendors...";
            _txtSearch.TextChanged += (_, _) => ApplyFilter();
            Controls.Add(_txtSearch);

            // Left: vendor grid
            _dgvVendors.Location        = new Point(12, 84);
            _dgvVendors.Size            = new Size(370, 480);
            _dgvVendors.Anchor          = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            _dgvVendors.ReadOnly        = true;
            _dgvVendors.AllowUserToAddRows    = false;
            _dgvVendors.AllowUserToDeleteRows = false;
            _dgvVendors.SelectionMode   = DataGridViewSelectionMode.FullRowSelect;
            _dgvVendors.MultiSelect     = false;
            _dgvVendors.AutoGenerateColumns = false;
            _dgvVendors.RowHeadersVisible   = false;
            _dgvVendors.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName",    HeaderText = "Vendor Name", Width = 170, ReadOnly = true });
            _dgvVendors.Columns.Add(new DataGridViewTextBoxColumn { Name = "colContact", HeaderText = "Contact",     AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            _dgvVendors.Columns.Add(new DataGridViewCheckBoxColumn { Name = "colActive", HeaderText = "Active",      Width = 56,  ReadOnly = true });
            _dgvVendors.SelectionChanged += DgvVendors_SelectionChanged;
            Controls.Add(_dgvVendors);

            // Right: edit panel
            int x = 400, y = 60;

            void AddRow(string label, Control ctl)
            {
                Controls.Add(new Label { Text = label, Location = new Point(x, y + 4), AutoSize = true });
                ctl.Location = new Point(x + 120, y);
                ctl.Size     = new Size(280, 23);
                Controls.Add(ctl);
                y += 32;
            }

            AddRow("Vendor Name:",  _txtName);
            AddRow("Contact Name:", _txtContact);
            AddRow("Email:",        _txtEmail);
            AddRow("Phone:",        _txtPhone);
            AddRow("Website:",      _txtWebsite);

            _chkActive.Text     = "Active";
            _chkActive.Location = new Point(x + 120, y);
            _chkActive.AutoSize = true;
            _chkActive.Checked  = true;
            Controls.Add(_chkActive);
            y += 32;

            _btnSave.Text     = "Save";
            _btnSave.Size     = new Size(80, 28);
            _btnSave.Location = new Point(x + 120, y);
            _btnSave.Click   += BtnSave_Click;
            Controls.Add(_btnSave);

            _btnNew.Text     = "+ New";
            _btnNew.Size     = new Size(80, 28);
            _btnNew.Location = new Point(x + 208, y);
            _btnNew.Click   += (_, _) => ClearForm();
            Controls.Add(_btnNew);

            _btnDeact.Text     = "Deactivate";
            _btnDeact.Size     = new Size(95, 28);
            _btnDeact.Location = new Point(x + 296, y);
            _btnDeact.Enabled  = false;
            _btnDeact.Click   += BtnDeact_Click;
            Controls.Add(_btnDeact);
            y += 40;

            // ── Import helper ─────────────────────────────────────────────────────
            var btnImport = new Button
            {
                Text     = "Import from PO Suppliers",
                Size     = new Size(200, 26),
                Location = new Point(x + 120, y),
                Font     = new Font("Segoe UI", 8.5F)
            };
            Theme.StyleSecondaryButton(btnImport);
            btnImport.Click += BtnImportFromSuppliers_Click;
            Controls.Add(btnImport);

            Controls.Add(new Label
            {
                Text      = "Copies new supplier names from Purchase Orders → Vendors",
                Font      = new Font("Segoe UI", 8F),
                ForeColor = Theme.TextMuted,
                Location  = new Point(x + 120, y + 28),
                Size      = new Size(380, 16),
                AutoSize  = false
            });
            y += 52;

            Controls.Add(new Label
            {
                Text      = "Parts using this vendor as default supplier:",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(x, y),
                AutoSize  = true
            });
            y += 22;

            _dgvParts.Location        = new Point(x, y);
            _dgvParts.Anchor          = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _dgvParts.Size            = new Size(500, 200);
            _dgvParts.ReadOnly        = true;
            _dgvParts.AllowUserToAddRows    = false;
            _dgvParts.AllowUserToDeleteRows = false;
            _dgvParts.AutoGenerateColumns   = false;
            _dgvParts.RowHeadersVisible     = false;
            _dgvParts.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPN",    HeaderText = "Part #",    Width = 110, ReadOnly = true });
            _dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPName", HeaderText = "Part Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            _dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStock", HeaderText = "Stock",     Width = 64, ReadOnly = true });
            _dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCost",  HeaderText = "Unit Cost", Width = 84, ReadOnly = true });
            Controls.Add(_dgvParts);

            _lblStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _lblStatus.Location = new Point(12, ClientSize.Height - 24);
            _lblStatus.AutoSize = true;
            Controls.Add(_lblStatus);

            SizeChanged += (_, _) => _lblStatus.Location = new Point(12, ClientSize.Height - 24);
            Theme.AddFormHeader(this, "🏢  Vendors");
        }

        private void LoadVendors()
        {
            try
            {
                _allVendors = _repo.GetAll(includeInactive: true).ToList();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load vendors: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyFilter()
        {
            string q = _txtSearch.Text.Trim();
            var filtered = string.IsNullOrEmpty(q)
                ? _allVendors
                : _allVendors.Where(v =>
                    (v.VendorName?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (v.ContactName?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) ||
                    (v.Email?.Contains(q, StringComparison.OrdinalIgnoreCase) == true)).ToList();

            _dgvVendors.Rows.Clear();
            foreach (var v in filtered)
            {
                int idx = _dgvVendors.Rows.Add();
                var row = _dgvVendors.Rows[idx];
                row.Cells["colName"].Value    = v.VendorName;
                row.Cells["colContact"].Value = v.ContactName ?? "";
                row.Cells["colActive"].Value  = v.IsActive;
                row.Tag = v;
            }

            _lblStatus.Text = $"{filtered.Count} vendor(s)";
        }

        private void DgvVendors_SelectionChanged(object? sender, EventArgs e)
        {
            if (_dgvVendors.SelectedRows.Count == 0) return;
            if (_dgvVendors.SelectedRows[0].Tag is not Vendor v) return;

            _editingId         = v.VendorID;
            _txtName.Text      = v.VendorName;
            _txtContact.Text   = v.ContactName ?? "";
            _txtEmail.Text     = v.Email       ?? "";
            _txtPhone.Text     = v.Phone       ?? "";
            _txtWebsite.Text   = v.Website     ?? "";
            _chkActive.Checked = v.IsActive;
            _btnDeact.Enabled  = v.IsActive;

            LoadPartsForVendor(v.VendorID);
        }

        private void LoadPartsForVendor(int vendorId)
        {
            _dgvParts.Rows.Clear();
            try
            {
                var parts = _repo.GetPartsByVendor(vendorId);
                foreach (var p in parts)
                {
                    int idx = _dgvParts.Rows.Add();
                    var r   = _dgvParts.Rows[idx];
                    r.Cells["colPN"].Value    = p.PartNumber;
                    r.Cells["colPName"].Value = p.PartName;
                    r.Cells["colStock"].Value = p.CurrentStock;
                    r.Cells["colCost"].Value  = p.UnitCost.ToString("N2");
                }
            }
            catch { /* best-effort */ }
        }

        private void ClearForm()
        {
            _editingId         = 0;
            _txtName.Text      = "";
            _txtContact.Text   = "";
            _txtEmail.Text     = "";
            _txtPhone.Text     = "";
            _txtWebsite.Text   = "";
            _chkActive.Checked = true;
            _btnDeact.Enabled  = false;
            _dgvParts.Rows.Clear();
            _dgvVendors.ClearSelection();
            _txtName.Focus();
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            string name = _txtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Vendor name is required.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var v = new Vendor
            {
                VendorID    = _editingId,
                VendorName  = name,
                ContactName = string.IsNullOrWhiteSpace(_txtContact.Text) ? null : _txtContact.Text.Trim(),
                Email       = string.IsNullOrWhiteSpace(_txtEmail.Text)   ? null : _txtEmail.Text.Trim(),
                Phone       = string.IsNullOrWhiteSpace(_txtPhone.Text)   ? null : _txtPhone.Text.Trim(),
                Website     = string.IsNullOrWhiteSpace(_txtWebsite.Text) ? null : _txtWebsite.Text.Trim(),
                IsActive    = _chkActive.Checked
            };

            try
            {
                if (_editingId == 0) _repo.Add(v);
                else                 _repo.Update(v);

                LoadVendors();
                _lblStatus.Text = _editingId == 0 ? "Vendor added." : "Vendor saved.";
                _editingId      = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Save failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDeact_Click(object? sender, EventArgs e)
        {
            if (_editingId == 0) return;
            string name = _txtName.Text;
            if (MessageBox.Show(this, $"Deactivate vendor '{name}'?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                _repo.Deactivate(_editingId);
                LoadVendors();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Deactivate failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnImportFromSuppliers_Click(object? sender, EventArgs e)
        {
            try
            {
                int imported = _repo.ImportFromSuppliers();
                if (imported == 0)
                    MessageBox.Show(this, "All PO suppliers are already in the vendor list.", "Import",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                {
                    LoadVendors();
                    MessageBox.Show(this, $"{imported} supplier(s) imported to the vendor list.", "Import Complete",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Import failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
