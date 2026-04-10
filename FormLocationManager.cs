using JaneERP.Data;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>View, edit, deactivate, and bulk-generate inventory locations.</summary>
    public class FormLocationManager : Form
    {
        private readonly LocationRepository _repo = new();

        private DataGridView dgvLocations = new();
        private Label        lblEditTitle  = new();
        private Label        lblNameLbl    = new();
        private TextBox      txtName       = new();
        private Label        lblNotesLbl   = new();
        private TextBox      txtNotes      = new();
        private CheckBox     chkActive     = new();
        private Button       btnSaveEdit   = new();
        private Button       btnAddNew     = new();
        private Button       btnToggle     = new();
        private Button       btnBulkAdd    = new();
        private Label        lblCount      = new();

        private Location? _editing;

        public FormLocationManager()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            LoadLocations();
        }

        private void BuildUI()
        {
            Text            = "Inventory Locations";
            ClientSize      = new Size(740, 640);
            MinimumSize     = new Size(740, 640);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;

            // ── Left: grid ───────────────────────────────────────────────────────
            dgvLocations.Anchor            = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            dgvLocations.Location          = new Point(12, 12);
            dgvLocations.Size              = new Size(380, 580);
            dgvLocations.ReadOnly          = true;
            dgvLocations.AllowUserToAddRows    = false;
            dgvLocations.AllowUserToDeleteRows = false;
            dgvLocations.SelectionMode     = DataGridViewSelectionMode.FullRowSelect;
            dgvLocations.MultiSelect       = false;
            dgvLocations.AutoGenerateColumns = false;
            dgvLocations.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colName",   HeaderText = "Location Name", DataPropertyName = "LocationName", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvLocations.Columns.Add(new DataGridViewCheckBoxColumn
                { Name = "colActive", HeaderText = "Active",        DataPropertyName = "IsActive",     Width = 60 });
            dgvLocations.SelectionChanged += Grid_SelectionChanged;
            Controls.Add(dgvLocations);

            lblCount.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            lblCount.Location = new Point(12, 598);
            lblCount.AutoSize = true;
            Controls.Add(lblCount);

            // ── Right: edit panel ────────────────────────────────────────────────
            int x = 408, y = 12;

            lblEditTitle.AutoSize = false;
            lblEditTitle.Font     = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblEditTitle.ForeColor = Theme.Gold;
            lblEditTitle.Location = new Point(x, y);
            lblEditTitle.Size     = new Size(320, 26);
            lblEditTitle.Text     = "Select a location to edit";
            Controls.Add(lblEditTitle);
            y += 38;

            lblNameLbl.AutoSize = true;
            lblNameLbl.Location = new Point(x, y);
            lblNameLbl.Text     = "Location Name:";
            Controls.Add(lblNameLbl);
            y += 20;

            txtName.Location = new Point(x, y);
            txtName.Size     = new Size(320, 23);
            Controls.Add(txtName);
            y += 36;

            lblNotesLbl.AutoSize = true;
            lblNotesLbl.Location = new Point(x, y);
            lblNotesLbl.Text     = "Notes:";
            Controls.Add(lblNotesLbl);
            y += 20;

            txtNotes.Location   = new Point(x, y);
            txtNotes.Size       = new Size(320, 60);
            txtNotes.Multiline  = true;
            txtNotes.ScrollBars = ScrollBars.Vertical;
            Controls.Add(txtNotes);
            y += 68;

            chkActive.AutoSize = true;
            chkActive.Location = new Point(x, y);
            chkActive.Text     = "Active";
            chkActive.Checked  = true;
            Controls.Add(chkActive);
            y += 34;

            btnSaveEdit.Location = new Point(x, y);
            btnSaveEdit.Size     = new Size(150, 30);
            btnSaveEdit.Text     = "Save Changes";
            btnSaveEdit.Enabled  = false;
            btnSaveEdit.Click   += BtnSaveEdit_Click;
            Controls.Add(btnSaveEdit);

            btnToggle.Location = new Point(x + 160, y);
            btnToggle.Size     = new Size(150, 30);
            btnToggle.Text     = "Deactivate";
            btnToggle.Enabled  = false;
            btnToggle.Click   += BtnToggle_Click;
            Controls.Add(btnToggle);
            y += 50;

            var sep = new Label
            {
                Location  = new Point(x, y),
                Size      = new Size(320, 1),
                BorderStyle = BorderStyle.Fixed3D
            };
            Controls.Add(sep);
            y += 16;

            btnAddNew.Location = new Point(x, y);
            btnAddNew.Size     = new Size(150, 32);
            btnAddNew.Text     = "+ Add New Location";
            btnAddNew.Click   += BtnAddNew_Click;
            Controls.Add(btnAddNew);
            y += 50;

            // Bulk add separator
            var sep2 = new Label
            {
                AutoSize  = true,
                Location  = new Point(x, y),
                Text      = "── Bulk Generation ─────────────────",
                ForeColor = Theme.TextMuted
            };
            Controls.Add(sep2);
            y += 24;

            btnBulkAdd.Location = new Point(x, y);
            btnBulkAdd.Size     = new Size(320, 40);
            btnBulkAdd.Text     = "Bulk Add Locations (Rooms / Units / Shelves / Bins)…";
            btnBulkAdd.Click   += BtnBulkAdd_Click;
            Controls.Add(btnBulkAdd);
            y += 54;

            var sep3 = new Label
            {
                AutoSize  = true,
                Location  = new Point(x, y),
                Text      = "── Bins ─────────────────────────────",
                ForeColor = Theme.TextMuted
            };
            Controls.Add(sep3);
            y += 24;

            var btnManageBins = new Button
            {
                Location = new Point(x, y),
                Size     = new Size(320, 36),
                Text     = "Manage Bins for Selected Location…"
            };
            btnManageBins.Click += BtnManageBins_Click;
            Controls.Add(btnManageBins);

            SetEditEnabled(false);
        }

        private void BtnManageBins_Click(object? sender, EventArgs e)
        {
            if (_editing == null)
            {
                MessageBox.Show(this, "Select a location first.", "No Location Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var frm = new FormLocationBins(_editing!);
            frm.ShowDialog(this);
        }

        private void SetEditEnabled(bool enabled)
        {
            txtName.Enabled     = enabled;
            txtNotes.Enabled    = enabled;
            chkActive.Enabled   = enabled;
            btnSaveEdit.Enabled = enabled;
            btnToggle.Enabled   = enabled;
        }

        private void LoadLocations()
        {
            try
            {
                var all = _repo.GetAll(includeInactive: true).ToList();
                dgvLocations.DataSource = all;
                lblCount.Text = $"{all.Count} location(s)  |  {all.Count(l => l.IsActive)} active";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load locations: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Grid_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvLocations.SelectedRows.Count == 0) { SetEditEnabled(false); return; }
            if (dgvLocations.SelectedRows[0].DataBoundItem is not Location loc) return;

            _editing = loc;
            lblEditTitle.Text  = loc.LocationName;
            txtName.Text       = loc.LocationName;
            txtNotes.Text      = loc.Notes ?? "";
            chkActive.Checked  = loc.IsActive;
            btnToggle.Text     = loc.IsActive ? "Deactivate" : "Re-activate";
            SetEditEnabled(true);
        }

        private void BtnSaveEdit_Click(object? sender, EventArgs e)
        {
            if (_editing == null) return;
            var name = txtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Location name cannot be empty.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _editing.LocationName = name;
                _editing.Notes        = string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim();
                _editing.IsActive     = chkActive.Checked;
                _repo.UpdateLocation(_editing);
                LoadLocations();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Save failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnToggle_Click(object? sender, EventArgs e)
        {
            if (_editing == null) return;
            bool newState = !_editing.IsActive;
            string action = newState ? "re-activate" : "deactivate";

            if (MessageBox.Show(this, $"Are you sure you want to {action} '{_editing.LocationName}'?",
                    "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                _repo.SetActive(_editing.LocationID, newState);
                _editing.IsActive = newState;
                LoadLocations();
                // Re-select so panel refreshes
                foreach (DataGridViewRow row in dgvLocations.Rows)
                    if (row.DataBoundItem is Location l && l.LocationID == _editing.LocationID)
                    { row.Selected = true; break; }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAddNew_Click(object? sender, EventArgs e)
        {
            var name = txtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                // Clear selection and let user type a new name
                dgvLocations.ClearSelection();
                _editing = null;
                lblEditTitle.Text = "New Location";
                txtName.Clear();
                txtNotes.Clear();
                chkActive.Checked = true;
                SetEditEnabled(true);
                txtName.Focus();
                btnSaveEdit.Click -= BtnSaveEdit_Click;
                btnSaveEdit.Click += SaveNew_Click;
                return;
            }

            try
            {
                _repo.AddLocation(name, string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim());
                txtName.Clear();
                txtNotes.Clear();
                LoadLocations();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not add: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveNew_Click(object? sender, EventArgs e)
        {
            var name = txtName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Enter a location name.", "Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                string? notes = string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim();
                _repo.AddLocation(name, notes);
                txtName.Clear();
                txtNotes.Clear();
                lblEditTitle.Text = "Select a location to edit";
                SetEditEnabled(false);
                btnSaveEdit.Click -= SaveNew_Click;
                btnSaveEdit.Click += BtnSaveEdit_Click;
                LoadLocations();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not add: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnBulkAdd_Click(object? sender, EventArgs e)
        {
            using var frm = new FormBulkAddLocations();
            if (frm.ShowDialog(this) == DialogResult.OK)
                LoadLocations();
        }
    }
}
