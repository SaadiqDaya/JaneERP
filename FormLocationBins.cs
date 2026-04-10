using JaneERP.Data;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>Manage bins for a single inventory location.</summary>
    public class FormLocationBins : Form
    {
        private readonly LocationRepository _repo = new();
        private readonly Location           _location;

        private DataGridView  dgv       = new();
        private Button        btnAdd    = new();
        private Button        btnEdit   = new();
        private Button        btnDelete = new();
        private Label         lblStatus = new();

        public FormLocationBins(Location location)
        {
            _location = location;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            LoadBins();
        }

        private void BuildUI()
        {
            Text          = $"Bins — {_location.LocationName}";
            ClientSize    = new Size(640, 500);
            MinimumSize   = new Size(500, 380);
            StartPosition = FormStartPosition.CenterParent;

            var lblTitle = new Label
            {
                Text      = $"Bins for: {_location.LocationName}",
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            };
            Controls.Add(lblTitle);

            var lblHint = new Label
            {
                Text      = "Bins are storage spots within a location (e.g. A1, Shelf-3). Double-click to edit.",
                ForeColor = Theme.TextSecondary,
                AutoSize  = true,
                Location  = new Point(12, 38)
            };
            Controls.Add(lblHint);

            // ── Grid ────────────────────────────────────────────────────────────
            dgv.AutoGenerateColumns = false;
            dgv.Columns.Add(new DataGridViewTextBoxColumn  { HeaderText = "Bin Code",    DataPropertyName = "BinCode",     Width = 110, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn  { HeaderText = "Description", DataPropertyName = "Description", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn  { HeaderText = "Capacity",    DataPropertyName = "Capacity",    Width = 80,  ReadOnly = true });
            dgv.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Active",      DataPropertyName = "IsActive",    Width = 60 });
            dgv.AllowUserToAddRows    = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.ReadOnly              = true;
            dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect           = false;
            dgv.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgv.Location = new Point(12, 62);
            dgv.Size     = new Size(616, 390);
            dgv.DoubleClick += (_, _) => EditSelected();
            Controls.Add(dgv);

            // ── Buttons (bottom bar) ─────────────────────────────────────────────
            btnAdd.Text     = "+ Add Bin";
            btnAdd.Size     = new Size(100, 30);
            btnAdd.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnAdd.Location = new Point(12, 460);
            btnAdd.Click   += (_, _) => AddBin();
            Controls.Add(btnAdd);

            btnEdit.Text     = "Edit Bin";
            btnEdit.Size     = new Size(100, 30);
            btnEdit.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnEdit.Location = new Point(122, 460);
            btnEdit.Click   += (_, _) => EditSelected();
            Controls.Add(btnEdit);

            btnDelete.Text     = "Delete Bin";
            btnDelete.Size     = new Size(100, 30);
            btnDelete.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnDelete.Location = new Point(232, 460);
            btnDelete.Click   += (_, _) => DeleteSelected();
            Controls.Add(btnDelete);

            lblStatus.AutoSize = true;
            lblStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            lblStatus.Location = new Point(450, 468);
            Controls.Add(lblStatus);
        }

        private void LoadBins()
        {
            try
            {
                var bins = _repo.GetBinsForLocation(_location.LocationID, includeInactive: true).ToList();
                dgv.DataSource = bins;
                lblStatus.Text = $"{bins.Count(b => b.IsActive)} active / {bins.Count} total bin(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load bins: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private LocationBin? SelectedBin()
        {
            if (dgv.SelectedRows.Count == 0) return null;
            return dgv.SelectedRows[0].DataBoundItem as LocationBin;
        }

        private void AddBin()
        {
            using var dlg = new FormBinEdit(new LocationBin { LocationID = _location.LocationID, IsActive = true });
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                _repo.AddBin(dlg.Result!);
                LoadBins();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not add bin: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EditSelected()
        {
            var bin = SelectedBin();
            if (bin == null)
            {
                MessageBox.Show(this, "Select a bin to edit.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new FormBinEdit(bin);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                _repo.UpdateBin(dlg.Result!);
                LoadBins();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not update bin: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteSelected()
        {
            var bin = SelectedBin();
            if (bin == null)
            {
                MessageBox.Show(this, "Select a bin to delete.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(this,
                    $"Deactivate bin '{bin.BinCode}'?\n\nThe bin will be marked inactive.",
                    "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                _repo.DeleteBin(bin.BinID);
                LoadBins();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not delete bin: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ── Inline bin edit dialog ────────────────────────────────────────────────────

    internal class FormBinEdit : Form
    {
        private readonly LocationBin _original;

        private TextBox       txtCode        = new();
        private TextBox       txtDescription = new();
        private NumericUpDown nudCapacity    = new();
        private CheckBox      chkActive      = new();
        private Button        btnOK          = new();
        private Button        btnCancel      = new();

        public LocationBin? Result { get; private set; }

        public FormBinEdit(LocationBin bin)
        {
            _original = bin;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
        }

        private void BuildUI()
        {
            Text          = _original.BinID == 0 ? "Add Bin" : "Edit Bin";
            ClientSize    = new Size(380, 300);
            StartPosition = FormStartPosition.CenterParent;

            int y = 12;

            var lblTitle = new Label
            {
                Text      = _original.BinID == 0 ? "New Bin" : $"Edit: {_original.BinCode}",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, y),
                AutoSize  = true
            };
            Controls.Add(lblTitle);
            y += 38;

            Controls.Add(new Label { Text = "Bin Code:", AutoSize = true, Location = new Point(12, y) });
            y += 20;
            txtCode.Location = new Point(12, y);
            txtCode.Size     = new Size(200, 23);
            txtCode.Text     = _original.BinCode;
            Controls.Add(txtCode);
            y += 36;

            Controls.Add(new Label { Text = "Description (optional):", AutoSize = true, Location = new Point(12, y) });
            y += 20;
            txtDescription.Location = new Point(12, y);
            txtDescription.Size     = new Size(350, 23);
            txtDescription.Text     = _original.Description ?? "";
            Controls.Add(txtDescription);
            y += 36;

            Controls.Add(new Label { Text = "Capacity (0 = unlimited):", AutoSize = true, Location = new Point(12, y) });
            y += 20;
            nudCapacity.Location = new Point(12, y);
            nudCapacity.Size     = new Size(100, 23);
            nudCapacity.Minimum  = 0;
            nudCapacity.Maximum  = 99999;
            nudCapacity.Value    = _original.Capacity ?? 0;
            Controls.Add(nudCapacity);
            y += 40;

            chkActive.Text     = "Active";
            chkActive.Checked  = _original.IsActive;
            chkActive.AutoSize = true;
            chkActive.Location = new Point(12, y);
            Controls.Add(chkActive);
            y += 38;

            btnOK.Text     = "Save";
            btnOK.Size     = new Size(90, 30);
            btnOK.Location = new Point(12, y);
            btnOK.Click   += BtnOK_Click;
            Controls.Add(btnOK);

            btnCancel.Text     = "Cancel";
            btnCancel.Size     = new Size(90, 30);
            btnCancel.Location = new Point(112, y);
            btnCancel.Click   += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            var code = txtCode.Text.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show(this, "Bin Code is required.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtCode.Focus();
                return;
            }

            Result = new LocationBin
            {
                BinID       = _original.BinID,
                LocationID  = _original.LocationID,
                BinCode     = code,
                Description = string.IsNullOrWhiteSpace(txtDescription.Text) ? null : txtDescription.Text.Trim(),
                Capacity    = nudCapacity.Value > 0 ? (int?)nudCapacity.Value : null,
                IsActive    = chkActive.Checked
            };

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
