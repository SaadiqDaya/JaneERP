using JaneERP.Data;
using Microsoft.EntityFrameworkCore;

namespace JaneERP
{
    public class FormLoginLog : Form
    {
        private DataGridView grid = null!;
        private Button btnRefresh = null!;
        private Button btnClose = null!;

        public FormLoginLog()
        {
            Text            = "Login / Logout Log";
            ClientSize      = new Size(620, 440);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;

            grid = new DataGridView
            {
                Location          = new Point(12, 12),
                Size              = new Size(596, 370),
                ReadOnly          = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                SelectionMode     = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor   = SystemColors.Window
            };

            btnRefresh = new Button
            {
                Text     = "Refresh",
                Location = new Point(12, 400),
                Size     = new Size(90, 28)
            };
            btnRefresh.Click += (_, _) => LoadData();

            btnClose = new Button
            {
                Text     = "Close",
                Location = new Point(518, 400),
                Size     = new Size(90, 28)
            };
            btnClose.Click += (_, _) => Close();

            Controls.Add(grid);
            Controls.Add(btnRefresh);
            Controls.Add(btnClose);

            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Load += (_, _) => LoadData();
        }

        private void LoadData()
        {
            try
            {
                using var db = new AppDbContext();
                var rows = db.AuditLogs
                    .Where(a => a.Action == "Login" || a.Action == "Logout")
                    .OrderByDescending(a => a.When)
                    .Select(a => new
                    {
                        Time     = a.When.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                        User     = a.User,
                        Action   = a.Action,
                        Details  = a.Details
                    })
                    .ToList();

                grid.DataSource = rows;

                if (grid.Columns.Count > 0)
                {
                    if (grid.Columns["Time"]    != null) grid.Columns["Time"]!.FillWeight    = 20;
                    if (grid.Columns["User"]    != null) grid.Columns["User"]!.FillWeight    = 20;
                    if (grid.Columns["Action"]  != null) grid.Columns["Action"]!.FillWeight  = 15;
                    if (grid.Columns["Details"] != null) grid.Columns["Details"]!.FillWeight = 45;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load log: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
