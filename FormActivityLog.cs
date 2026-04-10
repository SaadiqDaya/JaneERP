using JaneERP.Data;

namespace JaneERP
{
    /// <summary>
    /// Shows the complete program activity log (all AuditLog entries).
    /// Includes Shopify syncs, product changes, inventory adjustments, and user changes.
    /// </summary>
    public class FormActivityLog : Form
    {
        private DataGridView dgv        = new();
        private ComboBox     cboFilter  = new();
        private DateTimePicker dtpFrom  = new();
        private DateTimePicker dtpTo    = new();
        private Button       btnRefresh = new();
        private Button       btnClose   = new();
        private Label        lblCount   = new();

        public FormActivityLog()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            LoadData();
        }

        private void BuildUI()
        {
            Text            = "Activity Log";
            ClientSize      = new Size(960, 560);
            MinimumSize     = new Size(960, 560);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;

            // Filter bar
            int y = 12;
            Controls.Add(new Label { Text = "Filter:", AutoSize = true, Location = new Point(12, y + 3) });

            cboFilter.Location      = new Point(55, y);
            cboFilter.Size          = new Size(160, 23);
            cboFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cboFilter.Items.AddRange(new object[] { "All", "Product", "Inventory", "User", "Shopify", "Login", "Logout", "WorkOrder" });
            cboFilter.SelectedIndex = 0;
            Controls.Add(cboFilter);

            Controls.Add(new Label { Text = "From:", AutoSize = true, Location = new Point(228, y + 3) });
            dtpFrom.Location = new Point(265, y);
            dtpFrom.Size     = new Size(130, 23);
            dtpFrom.Format   = DateTimePickerFormat.Short;
            dtpFrom.Value    = DateTime.Today.AddMonths(-1);
            Controls.Add(dtpFrom);

            Controls.Add(new Label { Text = "To:", AutoSize = true, Location = new Point(402, y + 3) });
            dtpTo.Location = new Point(420, y);
            dtpTo.Size     = new Size(130, 23);
            dtpTo.Format   = DateTimePickerFormat.Short;
            dtpTo.Value    = DateTime.Today.AddDays(1);
            Controls.Add(dtpTo);

            btnRefresh.Location = new Point(560, y);
            btnRefresh.Size     = new Size(90, 26);
            btnRefresh.Text     = "Refresh";
            btnRefresh.Click   += (_, _) => LoadData();
            Controls.Add(btnRefresh);

            y += 36;

            dgv.Location       = new Point(12, y);
            dgv.Size           = new Size(936, 460);
            dgv.Anchor         = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgv.ReadOnly       = true;
            dgv.AllowUserToAddRows    = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.SelectionMode  = DataGridViewSelectionMode.FullRowSelect;
            dgv.RowHeadersVisible = false;
            dgv.AutoGenerateColumns = false;
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cWhen",    HeaderText = "When",    DataPropertyName = "When",    Width = 160 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cUser",    HeaderText = "User",    DataPropertyName = "User",    Width = 120 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cAction",  HeaderText = "Action",  DataPropertyName = "Action",  Width = 160 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cDetails", HeaderText = "Details", DataPropertyName = "Details", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            Controls.Add(dgv);

            lblCount.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            lblCount.AutoSize = true;
            lblCount.Location = new Point(12, 524);
            Controls.Add(lblCount);

            btnClose.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(858, 520);
            btnClose.Size     = new Size(90, 30);
            btnClose.Text     = "Close";
            btnClose.Click   += (_, _) => Close();
            Controls.Add(btnClose);
        }

        private void LoadData()
        {
            try
            {
                using var db = new AppDbContext();
                var from   = dtpFrom.Value.Date;
                var to     = dtpTo.Value.Date.AddDays(1);
                var filter = cboFilter.SelectedItem?.ToString() ?? "All";

                var query = db.AuditLogs
                    .Where(a => a.When >= from && a.When < to)
                    .AsEnumerable();

                if (filter != "All")
                    query = query.Where(a => a.Action != null &&
                        a.Action.Contains(filter, StringComparison.OrdinalIgnoreCase));

                var results = query.OrderByDescending(a => a.When).ToList();
                dgv.DataSource = results;
                lblCount.Text  = $"{results.Count} event(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load log: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
