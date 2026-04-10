using JaneERP.Security;
using Microsoft.Data.SqlClient;

namespace JaneERP
{
    /// <summary>
    /// Shown after first login: lets the user pick which company database to work in,
    /// or create a new one.
    /// </summary>
    public class FormCompanySelector : Form
    {
        private ListBox  lstCompanies = new();
        private Button   btnSelect    = new();
        private Button   btnNew       = new();
        private Button   btnRemove    = new();
        private Label    lblStatus    = new();

        private List<CompanyProfile> _companies = new();

        /// <summary>True when the user confirmed a selection (OK to proceed).</summary>
        public bool Confirmed { get; private set; }

        public FormCompanySelector()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            LoadCompanies();
        }

        private void BuildUI()
        {
            Text            = "Select Company";
            ClientSize      = new Size(480, 360);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterScreen;
            MaximizeBox     = false;

            var lbl = new Label { Text = "Select a Company Database", Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Theme.Gold, AutoSize = true, Location = new Point(16, 16) };
            Controls.Add(lbl);

            Controls.Add(new Label { Text = "Choose the company you want to work in:", AutoSize = true,
                ForeColor = Theme.TextSecondary, Location = new Point(16, 44) });

            lstCompanies.Location          = new Point(16, 66);
            lstCompanies.Size              = new Size(446, 180);
            lstCompanies.SelectionMode     = SelectionMode.One;
            lstCompanies.DoubleClick      += (_, _) => BtnSelect_Click(null, EventArgs.Empty);
            Controls.Add(lstCompanies);

            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(16, 254);
            Controls.Add(lblStatus);

            btnSelect.Location = new Point(16, 278);
            btnSelect.Size     = new Size(140, 32);
            btnSelect.Text     = "Open Selected";
            btnSelect.Click   += BtnSelect_Click;
            Controls.Add(btnSelect);

            btnNew.Location = new Point(164, 278);
            btnNew.Size     = new Size(140, 32);
            btnNew.Text     = "+ New Company";
            btnNew.Click   += BtnNew_Click;
            Controls.Add(btnNew);

            btnRemove.Location = new Point(322, 278);
            btnRemove.Size     = new Size(140, 32);
            btnRemove.Text     = "Remove";
            btnRemove.Click   += BtnRemove_Click;
            Controls.Add(btnRemove);

            var btnExit = new Button
            {
                Text      = "Exit App",
                Location  = new Point(16, 318),
                Size      = new Size(90, 28),
                BackColor = Color.FromArgb(80, 20, 20),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand
            };
            btnExit.FlatAppearance.BorderSize = 0;
            btnExit.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnExit);

            ClientSize = new Size(480, 358);
        }

        private void LoadCompanies()
        {
            _companies = CompanyManager.Load();
            lstCompanies.Items.Clear();
            foreach (var c in _companies)
                lstCompanies.Items.Add(c);

            if (lstCompanies.Items.Count > 0)
                lstCompanies.SelectedIndex = 0;
        }

        private void BtnSelect_Click(object? sender, EventArgs e)
        {
            if (lstCompanies.SelectedItem is not CompanyProfile company)
            {
                MessageBox.Show(this, "Please select a company first.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Test the connection
            lblStatus.Text = "Testing connection…";
            lblStatus.ForeColor = Theme.TextSecondary;
            Application.DoEvents();

            try
            {
                using var conn = new SqlConnection(company.ConnectionString);
                conn.Open();
                conn.Close();
                CompanyManager.SetActive(company);
                lblStatus.Text      = $"Connected to {company.Name}";
                lblStatus.ForeColor = Theme.Teal;
                Confirmed = true;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                lblStatus.Text      = "Connection failed: " + ex.Message;
                lblStatus.ForeColor = Theme.Danger;
            }
        }

        private void BtnNew_Click(object? sender, EventArgs e)
        {
            using var dlg = new FormNewCompany();
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.NewProfile != null)
            {
                CompanyManager.AddCompany(dlg.NewProfile);
                LoadCompanies();
            }
        }

        private void BtnRemove_Click(object? sender, EventArgs e)
        {
            if (lstCompanies.SelectedItem is not CompanyProfile company) return;
            if (_companies.Count <= 1)
            {
                MessageBox.Show(this, "You must keep at least one company.", "Cannot Remove",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show(this, $"Remove '{company.Name}' from the list?\n(This does not delete the database.)",
                    "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            _companies.Remove(company);
            CompanyManager.Save(_companies);
            LoadCompanies();
        }
    }

    // ── New Company Dialog ────────────────────────────────────────────────────────

    internal class FormNewCompany : Form
    {
        private TextBox txtName   = new();
        private TextBox txtServer = new();
        private TextBox txtDb     = new();
        private CheckBox chkIntegrated = new();
        private TextBox txtUser   = new();
        private TextBox txtPwd    = new();
        private Label   lblStatus = new();
        private Button  btnTest   = new();
        private Button  btnCreate = new();
        private Button  btnCancel = new();

        public CompanyProfile? NewProfile { get; private set; }

        public FormNewCompany()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
        }

        private void BuildUI()
        {
            Text            = "Add New Company";
            ClientSize      = new Size(480, 420);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;

            var lbl = new Label { Text = "New Company Database", Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Theme.Gold, AutoSize = true, Location = new Point(16, 14) };
            Controls.Add(lbl);

            int y = 50;
            AddField(ref y, "Display Name:", txtName);
            AddField(ref y, "SQL Server (e.g. localhost\\SQLEXPRESS):", txtServer);
            txtServer.Text = "localhost\\SQLEXPRESS";
            AddField(ref y, "Database Name:", txtDb);

            chkIntegrated.AutoSize = true;
            chkIntegrated.Text     = "Use Windows Authentication (Integrated Security)";
            chkIntegrated.Checked  = true;
            chkIntegrated.Location = new Point(16, y);
            chkIntegrated.CheckedChanged += (_, _) =>
            {
                txtUser.Enabled = !chkIntegrated.Checked;
                txtPwd.Enabled  = !chkIntegrated.Checked;
            };
            Controls.Add(chkIntegrated);
            y += 28;

            AddField(ref y, "Username (SQL auth):", txtUser);
            txtUser.Enabled = false;
            AddField(ref y, "Password (SQL auth):", txtPwd);
            txtPwd.UseSystemPasswordChar = true;
            txtPwd.Enabled = false;

            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(16, y);
            Controls.Add(lblStatus);
            y += 26;

            btnTest.Location = new Point(16, y);
            btnTest.Size     = new Size(100, 30);
            btnTest.Text     = "Test";
            btnTest.Click   += BtnTest_Click;
            Controls.Add(btnTest);

            btnCreate.Location = new Point(240, y);
            btnCreate.Size     = new Size(110, 30);
            btnCreate.Text     = "Create & Add";
            btnCreate.Click   += BtnCreate_Click;
            Controls.Add(btnCreate);

            btnCancel.Location = new Point(358, y);
            btnCancel.Size     = new Size(90, 30);
            btnCancel.Text     = "Cancel";
            btnCancel.Click   += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);
        }

        private void AddField(ref int y, string label, TextBox txt)
        {
            Controls.Add(new Label { AutoSize = true, Location = new Point(16, y), Text = label });
            y += 20;
            txt.Location = new Point(16, y);
            txt.Size     = new Size(446, 23);
            Controls.Add(txt);
            y += 32;
        }

        private string BuildConnectionString()
        {
            var server = txtServer.Text.Trim();
            var db     = txtDb.Text.Trim();
            if (chkIntegrated.Checked)
                return $"Server={server};Database={db};Integrated Security=True;TrustServerCertificate=True;";
            else
                return $"Server={server};Database={db};User Id={txtUser.Text.Trim()};Password={txtPwd.Text};TrustServerCertificate=True;";
        }

        private void BtnTest_Click(object? sender, EventArgs e)
        {
            try
            {
                lblStatus.ForeColor = Theme.TextSecondary;
                lblStatus.Text      = "Testing…";
                Application.DoEvents();
                using var conn = new SqlConnection(BuildConnectionString());
                conn.Open();
                lblStatus.Text      = "Connection successful!";
                lblStatus.ForeColor = Theme.Teal;
            }
            catch (Exception ex)
            {
                lblStatus.Text      = "Failed: " + ex.Message;
                lblStatus.ForeColor = Theme.Danger;
            }
        }

        private void BtnCreate_Click(object? sender, EventArgs e)
        {
            var name = txtName.Text.Trim();
            var db   = txtDb.Text.Trim();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(db))
            {
                MessageBox.Show(this, "Display name and database name are required.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var connStr = BuildConnectionString();
                // Test before saving
                using var conn = new SqlConnection(connStr);
                conn.Open();

                NewProfile   = new CompanyProfile { Name = name, ConnectionString = connStr };
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not connect: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
