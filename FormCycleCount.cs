using System.Configuration;
using Dapper;
using JaneERP.Data;
using JaneERP.Security;
using Microsoft.Data.SqlClient;

namespace JaneERP
{
    /// <summary>Cycle count screen: select location, verify physical quantities, adjust on discrepancy.</summary>
    public class FormCycleCount : Form
    {
        private readonly string _cs =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        private readonly LocationRepository   _locRepo = new();
        private readonly CycleCountRepository _ccRepo  = new();

        private ComboBox     cboLocation      = new();
        private DataGridView dgvItems         = new();
        private Button       btnVerify        = new();
        private Button       btnVerifyAll     = new();
        private Button       btnClose         = new();
        private Label        lblStatus        = new();
        private Label        lblUncounted     = new();
        private CheckBox     chkUncountedOnly = new();

        // Full loaded entries before any filter
        private List<CycleCountEntry> _allEntries = [];

        // Editable "Actual Qty" column index
        private int _colActual = -1;

        public FormCycleCount()
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
            Text          = "Cycle Count";
            ClientSize    = new Size(860, 560);
            MinimumSize   = new Size(740, 480);
            StartPosition = FormStartPosition.CenterParent;

            var lblTitle = new Label
            {
                Text     = "Cycle Count",
                Font     = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location = new Point(12, 12),
                AutoSize = true
            };
            Controls.Add(lblTitle);

            // Uncounted count status label
            lblUncounted.Location  = new Point(220, 14);
            lblUncounted.AutoSize  = true;
            lblUncounted.Font      = new Font("Segoe UI", 9F, FontStyle.Italic);
            lblUncounted.ForeColor = Color.Orange;
            lblUncounted.Text      = "";
            Controls.Add(lblUncounted);

            Controls.Add(new Label { Text = "Location:", Location = new Point(12, 52), AutoSize = true });
            cboLocation.DropDownStyle = ComboBoxStyle.DropDownList;
            cboLocation.Location      = new Point(80, 48);
            cboLocation.Size          = new Size(220, 23);
            cboLocation.SelectedIndexChanged += (_, _) => LoadItems();
            Controls.Add(cboLocation);

            // Filter: uncounted only checkbox
            chkUncountedOnly.Text     = "Filter: Show Uncounted Only";
            chkUncountedOnly.AutoSize = true;
            chkUncountedOnly.Location = new Point(320, 51);
            chkUncountedOnly.CheckedChanged += (_, _) => ApplyFilter();
            Controls.Add(chkUncountedOnly);

            // ── Grid ─────────────────────────────────────────────────────────────
            dgvItems.AutoGenerateColumns = false;
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSKU",     HeaderText = "SKU",          DataPropertyName = "SKU",           Width = 120, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName",    HeaderText = "Product",      DataPropertyName = "ProductName",    Width = 200, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSystem",  HeaderText = "System Qty",   DataPropertyName = "SystemQty",      Width = 90,  ReadOnly = true });

            var colActual = new DataGridViewTextBoxColumn { Name = "colActual", HeaderText = "Actual Qty", Width = 90, ReadOnly = false };
            dgvItems.Columns.Add(colActual);
            _colActual = dgvItems.Columns["colActual"]?.Index ?? -1;

            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDiff",       HeaderText = "Difference",   Width = 80,  ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colVerifiedAt", HeaderText = "Last Verified",DataPropertyName = "LastVerifiedAt", Width = 130, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colVerifiedBy", HeaderText = "Verified By",  DataPropertyName = "LastVerifiedBy", Width = 110, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStatus",     HeaderText = "Status",       Width = 80,  ReadOnly = true });

            dgvItems.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvItems.Location = new Point(12, 80);
            dgvItems.Size     = new Size(836, 410);
            dgvItems.AllowUserToAddRows    = false;
            dgvItems.AllowUserToDeleteRows = false;
            dgvItems.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvItems.MultiSelect           = true;

            // Compute difference when actual qty changes
            dgvItems.CellValueChanged += DgvItems_CellValueChanged;
            dgvItems.CellFormatting   += (s, e) =>
            {
                if (e.ColumnIndex == (dgvItems.Columns["colVerifiedAt"]?.Index ?? -2) && e.Value is DateTime dt)
                    e.Value = dt.ToString("yyyy-MM-dd HH:mm");
            };

            Controls.Add(dgvItems);

            // ── Bottom buttons ────────────────────────────────────────────────────
            lblStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            lblStatus.Location = new Point(12, 502);
            lblStatus.AutoSize = true;
            Controls.Add(lblStatus);

            btnVerify.Text     = "Verify Selected";
            btnVerify.Size     = new Size(130, 30);
            btnVerify.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnVerify.Location = new Point(560, 500);
            btnVerify.Click   += BtnVerify_Click;
            Controls.Add(btnVerify);

            btnVerifyAll.Text     = "Verify All";
            btnVerifyAll.Size     = new Size(100, 30);
            btnVerifyAll.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnVerifyAll.Location = new Point(668, 500);
            btnVerifyAll.Click   += BtnVerifyAll_Click;
            Controls.Add(btnVerifyAll);

            btnClose.Text     = "Close";
            btnClose.Size     = new Size(80, 30);
            btnClose.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(776, 500);
            btnClose.Click   += (_, _) => Close();
            Controls.Add(btnClose);
        }

        private void LoadLocations()
        {
            try
            {
                var locs = _locRepo.GetAll().ToList();
                cboLocation.Items.Clear();
                cboLocation.Items.Add(new Models.Location { LocationID = 0, LocationName = "(All Locations)" });
                foreach (var l in locs) cboLocation.Items.Add(l);
                cboLocation.DisplayMember = "LocationName";
                cboLocation.ValueMember   = "LocationID";
                cboLocation.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load locations: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadItems()
        {
            int? locId = null;
            if (cboLocation.SelectedItem is Models.Location loc && loc.LocationID != 0)
                locId = loc.LocationID;

            try
            {
                _allEntries = _ccRepo.GetEntries(locId);
                ApplyFilter();
                RefreshUncountedLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load items: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshUncountedLabel()
        {
            try
            {
                using var db = new SqlConnection(_cs);
                int count = db.ExecuteScalar<int>(@"
                    SELECT COUNT(1) FROM Products
                    WHERE  IsActive = 1
                      AND  (LastVerifiedAt IS NULL OR LastVerifiedAt < DATEADD(day, -30, GETDATE()))");

                lblUncounted.Text = count > 0
                    ? $"  \u26A0 {count} product(s) not counted in 30+ days"
                    : "";
            }
            catch
            {
                lblUncounted.Text = "";
            }
        }

        private void ApplyFilter()
        {
            var source = chkUncountedOnly.Checked
                ? _allEntries.Where(e =>
                    e.LastVerifiedAt == null ||
                    e.LastVerifiedAt < DateTime.Now.AddDays(-30)).ToList()
                : _allEntries;

            dgvItems.DataSource = null;
            dgvItems.Rows.Clear();

            foreach (var e in source)
            {
                int idx = dgvItems.Rows.Add();
                var row = dgvItems.Rows[idx];
                row.Cells["colSKU"].Value        = e.SKU;
                row.Cells["colName"].Value       = e.ProductName;
                row.Cells["colSystem"].Value     = e.SystemQty;
                row.Cells["colActual"].Value     = "";
                row.Cells["colDiff"].Value       = "";
                row.Cells["colStatus"].Value     = "";
                row.Cells["colVerifiedAt"].Value = e.LastVerifiedAt;
                row.Cells["colVerifiedBy"].Value = e.LastVerifiedBy;
                row.Tag = e;
            }

            lblStatus.Text = $"{source.Count()} product(s) loaded.";
        }

        private void DgvItems_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != _colActual) return;
            var row = dgvItems.Rows[e.RowIndex];
            if (int.TryParse(row.Cells["colSystem"].Value?.ToString(), out int sys) &&
                int.TryParse(row.Cells["colActual"]?.Value?.ToString(), out int actual))
            {
                row.Cells["colDiff"].Value   = (actual - sys).ToString("+0;-0;0");
                row.Cells["colStatus"].Value = "Pending";
                // Yellow if different, light amber if same
                row.DefaultCellStyle.BackColor = actual != sys
                    ? Color.FromArgb(80, 60, 10)
                    : Color.FromArgb(40, 60, 20);
            }
            else if (string.IsNullOrWhiteSpace(row.Cells["colActual"]?.Value?.ToString()))
            {
                row.Cells["colDiff"].Value   = "";
                row.Cells["colStatus"].Value = "";
                row.DefaultCellStyle.BackColor = Color.Empty;
            }
        }

        private void BtnVerify_Click(object? sender, EventArgs e)
        {
            var rows = dgvItems.SelectedRows.Cast<DataGridViewRow>().ToList();
            if (rows.Count == 0) { lblStatus.Text = "Select rows to verify."; return; }
            SaveVerifications(rows);
        }

        private void BtnVerifyAll_Click(object? sender, EventArgs e)
        {
            var rows = dgvItems.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow).ToList();
            SaveVerifications(rows);
        }

        private void SaveVerifications(List<DataGridViewRow> rows)
        {
            int? locId = null;
            if (cboLocation.SelectedItem is Models.Location loc && loc.LocationID != 0)
                locId = loc.LocationID;

            if (locId == null)
            {
                MessageBox.Show(this, "Please select a specific location before verifying.", "Location Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Only process rows with actual qty entered
            var pending = rows.Where(r =>
                !r.IsNewRow &&
                r.Tag is CycleCountEntry &&
                !string.IsNullOrWhiteSpace(r.Cells["colActual"]?.Value?.ToString())).ToList();

            if (pending.Count == 0) { lblStatus.Text = "No rows with actual qty entered."; return; }

            int done = 0, errors = 0;
            string verifiedBy = AppSession.CurrentUser?.Username ?? "system";

            foreach (var row in pending)
            {
                if (row.Tag is not CycleCountEntry entry) continue;
                if (!int.TryParse(row.Cells["colActual"]?.Value?.ToString(), out int actual)) continue;
                try
                {
                    _ccRepo.RecordVerification(entry.ProductID, locId.Value, entry.SystemQty, actual, verifiedBy);
                    // Mark as verified (green)
                    row.Cells["colStatus"].Value   = "Verified";
                    row.Cells["colVerifiedBy"].Value = verifiedBy;
                    row.Cells["colVerifiedAt"].Value = DateTime.Now;
                    row.DefaultCellStyle.BackColor = Color.FromArgb(20, 70, 20);
                    done++;
                }
                catch { errors++; }
            }

            lblStatus.Text = $"Verified {done} item(s){(errors > 0 ? $", {errors} errors" : "")}.";
            // Don't reload — keep verified state visible
        }
    }
}
