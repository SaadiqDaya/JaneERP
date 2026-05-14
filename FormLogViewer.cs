using JaneERP.Data;

namespace JaneERP
{
    /// <summary>
    /// Log viewer with two modes:
    ///   • File Log tab  — reads the rolling app.log files from disk
    ///   • Activity tabs — queries the SQLite AuditLogs table, filtered by category
    /// </summary>
    internal class FormLogViewer : Form
    {
        private static string LogFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "JaneERP", "logs");
        private static string LogFile => Path.Combine(LogFolder, "app.log");

        // ── File-log tab controls ────────────────────────────────────────────────
        private ComboBox    cboFile    = new();
        private ComboBox    cboLevel   = new();
        private TextBox     txtSearch  = new();
        private Button      btnRefresh = new();
        private Button      btnOpenFolder = new();
        private RichTextBox rtb        = new();
        private Label       lblCount   = new();
        private List<(string Level, string Text)> _lines = new();

        // ── SQL activity tab controls ────────────────────────────────────────────
        // One DataGridView per SQL tab (WO, SO, Exports/Imports, Payments)
        private record SqlTab(string Title, string ActionFilter, DataGridView Grid, DateTimePicker DtpFrom, DateTimePicker DtpTo);
        private List<SqlTab> _sqlTabs = new();

        public FormLogViewer()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            LoadFileList();
            Refresh_Click(null, EventArgs.Empty);
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.Style |= 0x00040000; return cp; }
        }

        private void BuildUI()
        {
            Text          = "Application Logs";
            ClientSize    = new Size(960, 660);
            MinimumSize   = new Size(780, 520);
            StartPosition = FormStartPosition.CenterParent;

            // ── Header strip ──────────────────────────────────────────────────────
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Theme.Header, Padding = new Padding(8, 8, 8, 0) };
            pnlHeader.Controls.Add(new Label
            {
                Text      = "Application Logs",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                AutoSize  = false,
                Location  = new Point(12, 10),
                Size      = new Size(300, 28)
            });
            Theme.MakeDraggable(this, pnlHeader);
            Controls.Add(pnlHeader);

            // ── TabControl ────────────────────────────────────────────────────────
            var tabs = new TabControl { Dock = DockStyle.Fill };
            Controls.Add(tabs);

            tabs.TabPages.Add(BuildFileLogTab());
            foreach (var (title, filter) in new[]
            {
                ("Work Orders",       "WorkOrder"),
                ("Sales Orders",      "SalesOrder|Order|Shopify"),
                ("Exports / Imports", "Export|Import"),
                ("Payments",          "Payment|Invoice|Credit")
            })
            {
                var tab = BuildSqlTab(title, filter);
                tabs.TabPages.Add(tab.tabPage);
                _sqlTabs.Add(tab.sqlTab);
            }

            // Load SQL data when switching to those tabs
            tabs.SelectedIndexChanged += (_, _) =>
            {
                int idx = tabs.SelectedIndex - 1;   // tab 0 is file log
                if (idx >= 0 && idx < _sqlTabs.Count)
                    LoadSqlTab(_sqlTabs[idx]);
            };
        }

        // ── File log tab ──────────────────────────────────────────────────────────

        private TabPage BuildFileLogTab()
        {
            var page = new TabPage("File Log");

            var pnlBar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Theme.Surface, Padding = new Padding(8, 6, 8, 4) };

            var lblFile = new Label { Text = "File:", AutoSize = true, Location = new Point(8, 11), ForeColor = Theme.TextSecondary };
            pnlBar.Controls.Add(lblFile);

            cboFile.Location      = new Point(42, 7);
            cboFile.Size          = new Size(200, 24);
            cboFile.DropDownStyle = ComboBoxStyle.DropDownList;
            cboFile.SelectedIndexChanged += (_, _) => Refresh_Click(null, EventArgs.Empty);
            pnlBar.Controls.Add(cboFile);

            var lblLevel = new Label { Text = "Level:", AutoSize = true, Location = new Point(254, 11), ForeColor = Theme.TextSecondary };
            pnlBar.Controls.Add(lblLevel);

            cboLevel.Location      = new Point(295, 7);
            cboLevel.Size          = new Size(110, 24);
            cboLevel.DropDownStyle = ComboBoxStyle.DropDownList;
            cboLevel.Items.AddRange(new object[] { "All", "Info", "Error", "Audit" });
            cboLevel.SelectedIndex = 0;
            cboLevel.SelectedIndexChanged += (_, _) => ApplyFilter();
            pnlBar.Controls.Add(cboLevel);

            var lblSearch = new Label { Text = "Search:", AutoSize = true, Location = new Point(418, 11), ForeColor = Theme.TextSecondary };
            pnlBar.Controls.Add(lblSearch);

            txtSearch.Location        = new Point(470, 7);
            txtSearch.Size            = new Size(180, 24);
            txtSearch.PlaceholderText = "Filter text…";
            txtSearch.TextChanged    += (_, _) => ApplyFilter();
            pnlBar.Controls.Add(txtSearch);

            btnRefresh.Text     = "↻ Refresh";
            btnRefresh.Location = new Point(660, 6);
            btnRefresh.Size     = new Size(90, 26);
            btnRefresh.UseVisualStyleBackColor = true;
            btnRefresh.Click   += Refresh_Click;
            pnlBar.Controls.Add(btnRefresh);

            btnOpenFolder.Text     = "Open Folder";
            btnOpenFolder.Location = new Point(758, 6);
            btnOpenFolder.Size     = new Size(100, 26);
            btnOpenFolder.UseVisualStyleBackColor = true;
            btnOpenFolder.Click += (_, _) =>
            {
                try { System.Diagnostics.Process.Start("explorer.exe", LogFolder); }
                catch { }
            };
            pnlBar.Controls.Add(btnOpenFolder);

            // Status bar
            var pnlStatus = new Panel { Dock = DockStyle.Bottom, Height = 24, BackColor = Theme.Header };
            lblCount.AutoSize  = true;
            lblCount.ForeColor = Theme.TextSecondary;
            lblCount.Font      = new Font("Segoe UI", 8F);
            lblCount.Location  = new Point(8, 5);
            pnlStatus.Controls.Add(lblCount);

            rtb.Dock          = DockStyle.Fill;
            rtb.ReadOnly      = true;
            rtb.Font          = new Font("Consolas", 9F);
            rtb.BackColor     = Color.FromArgb(10, 10, 20);
            rtb.ForeColor     = Color.FromArgb(200, 200, 210);
            rtb.BorderStyle   = BorderStyle.None;
            rtb.ScrollBars    = RichTextBoxScrollBars.Both;
            rtb.WordWrap      = false;

            page.Controls.Add(rtb);
            page.Controls.Add(pnlBar);
            page.Controls.Add(pnlStatus);
            return page;
        }

        private void LoadFileList()
        {
            cboFile.Items.Clear();
            cboFile.Items.Add("Today (app.log)");
            if (Directory.Exists(LogFolder))
            {
                var archives = Directory.GetFiles(LogFolder, "app_????.??.??.log")
                    .OrderByDescending(f => f)
                    .Select(f => Path.GetFileName(f))
                    .ToArray();
                cboFile.Items.AddRange(archives);
            }
            cboFile.SelectedIndex = 0;
        }

        private void Refresh_Click(object? sender, EventArgs? e)
        {
            _lines.Clear();
            try
            {
                string path = cboFile.SelectedIndex == 0
                    ? LogFile
                    : Path.Combine(LogFolder, cboFile.SelectedItem?.ToString() ?? "app.log");

                if (!File.Exists(path))
                {
                    rtb.Clear();
                    rtb.AppendText("Log file not found: " + path);
                    lblCount.Text = "0 lines";
                    return;
                }

                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                string content = sr.ReadToEnd();

                foreach (var line in content.Split('\n'))
                {
                    var trimmed = line.TrimEnd('\r');
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    string level = "Info";
                    if (trimmed.Contains("[Error]"))  level = "Error";
                    else if (trimmed.Contains("[Audit]") || trimmed.Contains("[AuditError]")) level = "Audit";
                    _lines.Add((level, trimmed));
                }
            }
            catch (Exception ex)
            {
                rtb.Clear();
                rtb.AppendText($"Error reading log: {ex.Message}");
                lblCount.Text = "Error";
                return;
            }
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string levelFilter  = cboLevel.SelectedItem?.ToString() ?? "All";
            string searchFilter = txtSearch.Text.Trim();

            var filtered = _lines.Where(l =>
                (levelFilter == "All" || l.Level.Equals(levelFilter, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrEmpty(searchFilter) || l.Text.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            rtb.Clear();
            rtb.SuspendLayout();
            foreach (var (level, text) in filtered)
            {
                Color col = level switch
                {
                    "Error" => Color.FromArgb(255, 100, 100),
                    "Audit" => Color.FromArgb(255, 210, 100),
                    _       => Color.FromArgb(160, 160, 180)
                };
                rtb.SelectionStart  = rtb.TextLength;
                rtb.SelectionLength = 0;
                rtb.SelectionColor  = col;
                rtb.AppendText(text + "\n");
            }
            rtb.ResumeLayout();
            rtb.SelectionStart = rtb.TextLength;
            rtb.ScrollToCaret();
            lblCount.Text = $"{filtered.Count:N0} lines shown  ({_lines.Count:N0} total)";
        }

        // ── SQL activity tabs ─────────────────────────────────────────────────────

        private (TabPage tabPage, SqlTab sqlTab) BuildSqlTab(string title, string actionFilter)
        {
            var page = new TabPage(title);

            // Date range toolbar
            var pnlBar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Theme.Surface };
            var dtpFrom = new DateTimePicker { Location = new Point(80, 7), Size = new Size(130, 24), Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(-30) };
            var dtpTo   = new DateTimePicker { Location = new Point(240, 7), Size = new Size(130, 24), Format = DateTimePickerFormat.Short, Value = DateTime.Today };
            pnlBar.Controls.Add(new Label { Text = "From:", AutoSize = true, Location = new Point(8, 11), ForeColor = Theme.TextSecondary });
            pnlBar.Controls.Add(dtpFrom);
            pnlBar.Controls.Add(new Label { Text = "To:", AutoSize = true, Location = new Point(380, 11), ForeColor = Theme.TextSecondary });
            pnlBar.Controls.Add(dtpTo);

            var dgv = new DataGridView
            {
                Dock                    = DockStyle.Fill,
                AutoGenerateColumns     = true,
                ReadOnly                = true,
                AllowUserToAddRows      = false,
                AllowUserToDeleteRows   = false,
                SelectionMode           = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible       = false,
                AutoSizeColumnsMode     = DataGridViewAutoSizeColumnsMode.Fill
            };

            var sqlTab = new SqlTab(title, actionFilter, dgv, dtpFrom, dtpTo);

            var btnLoad = new Button { Text = "Load", Location = new Point(440, 6), Size = new Size(80, 26), UseVisualStyleBackColor = true };
            btnLoad.Click += (_, _) => LoadSqlTab(sqlTab);
            pnlBar.Controls.Add(btnLoad);

            page.Controls.Add(dgv);
            page.Controls.Add(pnlBar);
            return (page, sqlTab);
        }

        private void LoadSqlTab(SqlTab tab)
        {
            try
            {
                using var db = new AppDbContext();
                var from = tab.DtpFrom.Value.Date;
                var to   = tab.DtpTo.Value.Date.AddDays(1);

                // actionFilter may be a pipe-separated list of keywords (OR match)
                var keywords = tab.ActionFilter.Split('|');

                var query = db.AuditLogs
                    .Where(a => a.When >= from && a.When < to)
                    .AsEnumerable()
                    .Where(a => a.Action != null &&
                        keywords.Any(kw => a.Action.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(a => a.When)
                    .Select(a => new { When = a.When.ToString("yyyy-MM-dd HH:mm:ss"), a.User, a.Action, a.Details })
                    .ToList();

                tab.Grid.DataSource = query;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load log: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
