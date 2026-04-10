using JaneERP.Data;
using JaneERP.Logging;
using JaneERP.Models;
using JaneERP.Security;
using System.Diagnostics;

namespace JaneERP
{
    /// <summary>Admin-only screen for viewing and managing user accounts.</summary>
    public class FormUserManagement : Form
    {
        private readonly UserRepository _repo = new();

        private DataGridView dgvUsers   = new();
        private Button       btnAddUser = new();
        private Label        lblUsername = new();
        private Label        lblUsernameLbl = new();
        private TextBox      txtUsername    = new();  // only visible in add-user mode
        private Label        lblEmailLbl = new();
        private TextBox      txtEmail    = new();
        private Label        lblRoleLbl  = new();
        private ComboBox     cboRole     = new();
        private GroupBox     grpPerms    = new();
        private CheckBox     chkInventory      = new();
        private CheckBox     chkSalesOrders    = new();
        private CheckBox     chkLog            = new();
        private CheckBox     chkManufacturing  = new();
        private CheckBox     chkParts          = new();
        private CheckBox     chkTasks          = new();
        private CheckBox     chkCycleCount     = new();
        private Label        lblPwdLbl      = new();
        private TextBox      txtNewPassword = new();
        private Label        lblConfirmLbl  = new();
        private TextBox      txtConfirmPassword = new();
        private Button       btnSave          = new();
        private Button       btnResetPassword = new();
        private Button       btnDeactivate    = new();
        private Button       btnEmailUser     = new();

        private CheckBox chkShowInactive = new();
        private AppUser? _selected;
        private bool     _addMode = false;

        public FormUserManagement()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            LoadUsers();
        }

        private void BuildUI()
        {
            Text            = "Manage Users";
            ClientSize      = new Size(900, 520);
            MinimumSize     = new Size(900, 520);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;

            // ── Left: user grid ──────────────────────────────────────────────────────
            dgvUsers.Anchor            = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            dgvUsers.Location          = new Point(12, 12);
            dgvUsers.Size              = new Size(430, 460);
            dgvUsers.ReadOnly          = true;
            dgvUsers.AllowUserToAddRows    = false;
            dgvUsers.AllowUserToDeleteRows = false;
            dgvUsers.SelectionMode     = DataGridViewSelectionMode.FullRowSelect;
            dgvUsers.MultiSelect       = false;
            dgvUsers.AutoGenerateColumns = false;
            dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUsername", HeaderText = "Username", DataPropertyName = "Username", Width = 150 });
            dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "colEmail",    HeaderText = "Email",    DataPropertyName = "Email",    Width = 160 });
            dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRole",     HeaderText = "Role",     DataPropertyName = "Role",     Width = 70  });
            dgvUsers.Columns.Add(new DataGridViewCheckBoxColumn { Name = "colActive",  HeaderText = "Active",   DataPropertyName = "IsActive", Width = 50  });
            dgvUsers.SelectionChanged += DgvUsers_SelectionChanged;
            Controls.Add(dgvUsers);

            // Show inactive users checkbox
            chkShowInactive.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            chkShowInactive.Location = new Point(160, 485);
            chkShowInactive.AutoSize = true;
            chkShowInactive.Text     = "Show Inactive Users";
            chkShowInactive.CheckedChanged += (_, _) => LoadUsers();
            Controls.Add(chkShowInactive);

            // Add User button below the grid
            btnAddUser.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnAddUser.Location = new Point(12, 480);
            btnAddUser.Size     = new Size(140, 30);
            btnAddUser.Text     = "+ Add New User";
            btnAddUser.Click   += BtnAddUser_Click;
            Controls.Add(btnAddUser);

            // ── Right: edit panel ────────────────────────────────────────────────────
            int x = 458, y = 12;

            lblUsername.AutoSize = false;
            lblUsername.Font     = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblUsername.Location = new Point(x, y);
            lblUsername.Size     = new Size(420, 26);
            lblUsername.Text     = "(select a user)";
            Controls.Add(lblUsername);
            y += 34;

            // Username field — only visible in add mode
            lblUsernameLbl.AutoSize = true;
            lblUsernameLbl.Location = new Point(x, y);
            lblUsernameLbl.Text     = "Username:";
            lblUsernameLbl.Visible  = false;
            Controls.Add(lblUsernameLbl);
            y += 20;

            txtUsername.Location = new Point(x, y);
            txtUsername.Size     = new Size(420, 23);
            txtUsername.Visible  = false;
            Controls.Add(txtUsername);
            y += 34;

            // Email
            lblEmailLbl.AutoSize = true;
            lblEmailLbl.Location = new Point(x, y);
            lblEmailLbl.Text     = "Email:";
            Controls.Add(lblEmailLbl);
            y += 20;

            txtEmail.Location = new Point(x, y);
            txtEmail.Size     = new Size(420, 23);
            Controls.Add(txtEmail);
            y += 34;

            // Role
            lblRoleLbl.AutoSize = true;
            lblRoleLbl.Location = new Point(x, y);
            lblRoleLbl.Text     = "Role:";
            Controls.Add(lblRoleLbl);
            y += 20;

            cboRole.Location      = new Point(x, y);
            cboRole.Size          = new Size(180, 23);
            cboRole.DropDownStyle = ComboBoxStyle.DropDownList;
            cboRole.Items.AddRange(new object[] { "Admin", "Editor", "Viewer" });
            cboRole.SelectedIndexChanged += CboRole_SelectedIndexChanged;
            Controls.Add(cboRole);
            y += 36;

            // Permissions group (Editor only)
            grpPerms.Location = new Point(x, y);
            grpPerms.Size     = new Size(420, 160);
            grpPerms.Text     = "Editor Permissions (select areas)";
            grpPerms.Visible  = false;

            void AddPerm(CheckBox chk, string label, int col, int row) {
                chk.Location = new Point(10 + col * 130, 22 + row * 24);
                chk.AutoSize = true; chk.Text = label;
                grpPerms.Controls.Add(chk);
            }
            AddPerm(chkInventory,     "Inventory",     0, 0);
            AddPerm(chkSalesOrders,   "Sales Orders",  0, 1);
            AddPerm(chkLog,           "Login Log",     0, 2);
            AddPerm(chkManufacturing, "Manufacturing", 1, 0);
            AddPerm(chkParts,         "Parts & BOM",   1, 1);
            AddPerm(chkTasks,         "Tasks",         1, 2);
            AddPerm(chkCycleCount,    "Cycle Count",   2, 0);

            Controls.Add(grpPerms);
            y += 170;

            // New password
            lblPwdLbl.AutoSize = true;
            lblPwdLbl.Location = new Point(x, y);
            lblPwdLbl.Text     = "New Password (leave blank to keep current):";
            Controls.Add(lblPwdLbl);
            y += 20;

            txtNewPassword.Location     = new Point(x, y);
            txtNewPassword.Size         = new Size(420, 23);
            txtNewPassword.UseSystemPasswordChar = true;
            Controls.Add(txtNewPassword);
            y += 32;

            lblConfirmLbl.AutoSize = true;
            lblConfirmLbl.Location = new Point(x, y);
            lblConfirmLbl.Text     = "Confirm Password:";
            Controls.Add(lblConfirmLbl);
            y += 20;

            txtConfirmPassword.Location     = new Point(x, y);
            txtConfirmPassword.Size         = new Size(420, 23);
            txtConfirmPassword.UseSystemPasswordChar = true;
            Controls.Add(txtConfirmPassword);
            y += 40;

            // Buttons
            btnSave.Location = new Point(x, y);
            btnSave.Size     = new Size(100, 30);
            btnSave.Text     = "Save";
            btnSave.Click   += BtnSave_Click;
            Controls.Add(btnSave);

            btnResetPassword.Location = new Point(x + 110, y);
            btnResetPassword.Size     = new Size(120, 30);
            btnResetPassword.Text     = "Reset Password";
            btnResetPassword.Click   += BtnResetPassword_Click;
            Controls.Add(btnResetPassword);

            btnDeactivate.Location = new Point(x, y + 38);
            btnDeactivate.Size     = new Size(120, 30);
            btnDeactivate.Text     = "Deactivate";
            btnDeactivate.Click   += BtnDeactivate_Click;
            Controls.Add(btnDeactivate);

            btnEmailUser.Location = new Point(x + 130, y + 38);
            btnEmailUser.Size     = new Size(100, 30);
            btnEmailUser.Text     = "Email User";
            btnEmailUser.Click   += BtnEmailUser_Click;
            Controls.Add(btnEmailUser);

            SetEditPanelEnabled(false);
        }

        private void SetEditPanelEnabled(bool enabled)
        {
            txtEmail.Enabled           = enabled;
            cboRole.Enabled            = enabled;
            grpPerms.Enabled           = enabled;
            txtNewPassword.Enabled     = enabled;
            txtConfirmPassword.Enabled = enabled;
            btnSave.Enabled            = enabled;
            btnResetPassword.Enabled   = enabled && !_addMode;
            btnDeactivate.Enabled      = enabled && !_addMode;
            btnEmailUser.Enabled       = enabled && !_addMode;
        }

        private void BtnAddUser_Click(object? sender, EventArgs e)
        {
            _addMode  = true;
            _selected = null;
            dgvUsers.ClearSelection();

            lblUsername.Text     = "New User";
            lblUsernameLbl.Visible = true;
            txtUsername.Visible  = true;
            txtUsername.Clear();

            lblPwdLbl.Text = "Password (required):";
            txtEmail.Clear();
            cboRole.SelectedItem = "Viewer";
            chkInventory.Checked   = false;
            chkSalesOrders.Checked = false;
            chkLog.Checked         = false;
            txtNewPassword.Clear();
            txtConfirmPassword.Clear();
            grpPerms.Visible = false;
            btnSave.Text     = "Create User";

            SetEditPanelEnabled(true);
            txtUsername.Focus();
        }

        private void LoadUsers()
        {
            try
            {
                var users = _repo.GetAll(includeInactive: true);
                dgvUsers.DataSource = users;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load users: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DgvUsers_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvUsers.SelectedRows.Count == 0) { SetEditPanelEnabled(false); return; }
            if (dgvUsers.SelectedRows[0].DataBoundItem is not AppUser user) return;

            // Leave add mode when user clicks a grid row
            _addMode = false;
            lblUsernameLbl.Visible = false;
            txtUsername.Visible    = false;
            lblPwdLbl.Text         = "New Password (leave blank to keep current):";
            btnSave.Text           = "Save";

            _selected = user;
            lblUsername.Text      = user.Username;
            txtEmail.Text         = user.Email ?? "";
            cboRole.SelectedItem  = user.Role;

            var parts = (user.Permissions ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            bool Has(string area) => parts.Any(p => p.Equals(area, StringComparison.OrdinalIgnoreCase));
            chkInventory.Checked     = Has("Inventory");
            chkSalesOrders.Checked   = Has("SalesOrders");
            chkLog.Checked           = Has("Log");
            chkManufacturing.Checked = Has("Manufacturing");
            chkParts.Checked         = Has("Parts");
            chkTasks.Checked         = Has("Tasks");
            chkCycleCount.Checked    = Has("CycleCount");

            txtNewPassword.Clear();
            txtConfirmPassword.Clear();

            grpPerms.Visible     = user.Role == "Editor";
            btnDeactivate.Text   = user.IsActive ? "Deactivate" : "Re-activate";

            SetEditPanelEnabled(true);
        }

        private void CboRole_SelectedIndexChanged(object? sender, EventArgs e)
        {
            grpPerms.Visible = cboRole.SelectedItem?.ToString() == "Editor";
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (_addMode)
            {
                // ── Create new user ──────────────────────────────────────────────
                var username = txtUsername.Text.Trim();
                var pwd      = txtNewPassword.Text;
                var confirm  = txtConfirmPassword.Text;

                if (string.IsNullOrWhiteSpace(username))
                {
                    MessageBox.Show(this, "Username is required.", "Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(pwd))
                {
                    MessageBox.Show(this, "Password is required for new users.", "Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (pwd != confirm)
                {
                    MessageBox.Show(this, "Passwords do not match.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    var role = cboRole.SelectedItem?.ToString() ?? "Viewer";
                    _repo.CreateUser(username, pwd, role: role, email: txtEmail.Text.Trim(),
                        permissions: BuildPermissionsString());
                    AppLogger.Audit(AppSession.CurrentUser?.Username, "UserCreate",
                        $"NewUser={username} Role={role}");
                    _addMode = false;
                    lblUsernameLbl.Visible = false;
                    txtUsername.Visible    = false;
                    lblPwdLbl.Text         = "New Password (leave blank to keep current):";
                    btnSave.Text           = "Save";
                    LoadUsers();
                    MessageBox.Show(this, $"User '{username}' created.", "Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Create failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            // ── Update existing user ─────────────────────────────────────────────
            if (_selected == null) return;

            _selected.Email       = txtEmail.Text.Trim();
            _selected.Role        = cboRole.SelectedItem?.ToString() ?? "Viewer";
            _selected.Permissions = BuildPermissionsString();

            try
            {
                _repo.UpdateUser(_selected);
                LoadUsers();
                MessageBox.Show(this, "User saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Save failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnResetPassword_Click(object? sender, EventArgs e)
        {
            if (_selected == null) return;

            var pwd     = txtNewPassword.Text;
            var confirm = txtConfirmPassword.Text;

            if (string.IsNullOrWhiteSpace(pwd))
            {
                MessageBox.Show(this, "Enter a new password.", "Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (pwd != confirm)
            {
                MessageBox.Show(this, "Passwords do not match.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _repo.SetPassword(_selected.UserId, pwd);
                txtNewPassword.Clear();
                txtConfirmPassword.Clear();
                MessageBox.Show(this, "Password updated.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Password reset failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDeactivate_Click(object? sender, EventArgs e)
        {
            if (_selected == null) return;

            bool willDeactivate = _selected.IsActive;
            string action       = willDeactivate ? "deactivate" : "re-activate";

            if (MessageBox.Show(this, $"Are you sure you want to {action} '{_selected.Username}'?",
                    "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _selected.IsActive = !_selected.IsActive;
            try
            {
                _repo.UpdateUser(_selected);
                LoadUsers();
                btnDeactivate.Text = _selected.IsActive ? "Deactivate" : "Re-activate";
            }
            catch (Exception ex)
            {
                _selected.IsActive = !_selected.IsActive; // revert
                MessageBox.Show(this, "Failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnEmailUser_Click(object? sender, EventArgs e)
        {
            var email = _selected?.Email;
            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show(this, "No email address on file for this user.", "No Email",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo($"mailto:{email}") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open email client: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string BuildPermissionsString()
        {
            if (cboRole.SelectedItem?.ToString() != "Editor") return "";

            var parts = new List<string>();
            if (chkInventory.Checked)     parts.Add("Inventory");
            if (chkSalesOrders.Checked)   parts.Add("SalesOrders");
            if (chkLog.Checked)           parts.Add("Log");
            if (chkManufacturing.Checked) parts.Add("Manufacturing");
            if (chkParts.Checked)         parts.Add("Parts");
            if (chkTasks.Checked)         parts.Add("Tasks");
            if (chkCycleCount.Checked)    parts.Add("CycleCount");
            return string.Join(",", parts);
        }
    }
}
