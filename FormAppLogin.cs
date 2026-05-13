using JaneERP.Data;
using JaneERP.Logging;
using JaneERP.Models;
using JaneERP.Security;
using System.IO;

namespace JaneERP
{
    public partial class FormAppLogin : Form
    {
        private readonly UserRepository _repo = new();
        private bool _isFirstRun;

        public FormAppLogin()
        {
            InitializeComponent();
        }

        private void FormAppLogin_Load(object sender, EventArgs e)
        {
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeDraggable(this, pnlRight);

            // Pre-fill last username if the setting is on
            var cfg = AppSettings.Current;
            chkRememberUsername.Checked = cfg.RememberLastUsername;
            if (cfg.RememberLastUsername && !string.IsNullOrWhiteSpace(cfg.LastUsername))
            {
                txtUsername.Text = cfg.LastUsername;
                txtPassword.Focus();
            }
            // Load images
            try
            {
                if (File.Exists(Theme.MascotImagePath))
                    pbMascot.Image = Image.FromFile(Theme.MascotImagePath);
            }
            catch (Exception ex) { AppLogger.Info($"[FormAppLogin.FormAppLogin_Load]: {ex.Message}"); }

            try
            {
                pbLogo.Image = AppSettings.Current.LoadLogoImage();
            }
            catch (Exception ex) { AppLogger.Info($"[FormAppLogin.FormAppLogin_Load]: {ex.Message}"); }

            // First-run check
            try
            {
                _isFirstRun = !_repo.HasAnyUsers();

                if (_isFirstRun)
                {
                    lblSubtitle.Text           = "First run \u2014 create your admin account to get started";
                    btnLogin.Text              = "Create Account & Sign In";
                    lblConfirmPassword.Visible = true;
                    txtConfirmPassword.Visible = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Could not connect to the database.\n\n" + ex.Message,
                    "Database Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            var username = txtUsername.Text.Trim();
            var password = txtPassword.Text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show(this, "Username and password are required.", "Missing fields",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (_isFirstRun)
                {
                    if (password != txtConfirmPassword.Text)
                    {
                        MessageBox.Show(this, "Passwords do not match.", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    _repo.CreateUser(username, password, "Admin");
                    AppLogger.Audit(username, "FirstRunSetup", "Admin account created");

                    _isFirstRun                = false;
                    lblConfirmPassword.Visible  = false;
                    txtConfirmPassword.Visible  = false;
                    btnLogin.Text              = "Sign In";
                    lblSubtitle.Text           = "Sign in to your account";
                }

                var user = _repo.Authenticate(username, password);
                if (user == null)
                {
                    // Check whether the account is now locked (or was already locked)
                    var fresh = _repo.GetByUsername(username);
                    if (fresh?.LockedUntil.HasValue == true && fresh.LockedUntil.Value > DateTime.Now)
                    {
                        var unlockAt  = fresh.LockedUntil.Value;
                        var remaining = (int)Math.Ceiling((unlockAt - DateTime.Now).TotalMinutes);
                        var cfg2      = AppSettings.Current;
                        var contact   = new System.Text.StringBuilder();
                        if (!string.IsNullOrWhiteSpace(cfg2.AdminPhone))
                            contact.Append($"Phone: {cfg2.AdminPhone}");
                        if (!string.IsNullOrWhiteSpace(cfg2.AdminEmail))
                        {
                            if (contact.Length > 0) contact.Append("  |  ");
                            contact.Append($"Email: {cfg2.AdminEmail}");
                        }
                        var adminLine = contact.Length > 0
                            ? $"\n\nAdmin contact: {contact}"
                            : "\n\nContact an administrator if you need immediate access.";
                        MessageBox.Show(this,
                            $"This account has been locked after too many failed attempts.\n\n" +
                            $"Locked until: {unlockAt:h:mm tt} ({remaining} minute{(remaining == 1 ? "" : "s")} remaining)" +
                            adminLine,
                            "Account Locked", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        var attempts  = fresh?.FailedLoginCount ?? 0;
                        var remaining2 = _repo.MaxLoginAttempts - attempts;
                        var msg = attempts == 0
                            ? "Incorrect username or password."
                            : $"Incorrect password — {remaining2} attempt{(remaining2 == 1 ? "" : "s")} remaining before lockout.";
                        MessageBox.Show(this, msg, "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    txtPassword.Clear();
                    txtPassword.Focus();
                    return;
                }

                AppSession.SetUser(user);
                AppLogger.Audit(user.Username, "Login", $"Role={user.Role}");

                // Persist remember-username preference from the checkbox
                var cfgSave = AppSettings.Current;
                cfgSave.RememberLastUsername = chkRememberUsername.Checked;
                if (chkRememberUsername.Checked)
                    cfgSave.LastUsername = user.Username;
                cfgSave.Save();

                LaunchMainMenu(user);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "An error occurred: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LaunchMainMenu(AppUser user)
        {
            Hide();
            using var menu = new FormMainMenu(user);
            menu.ShowDialog();

            if (menu.LoggedOut)
            {
                // Return to login screen
                txtUsername.Text = "";
                txtPassword.Text = "";
                Show();
                if (menu.SessionExpired)
                    MessageBox.Show(this,
                        "Your session has expired due to inactivity.\nPlease sign in again.",
                        "Session Expired", MessageBoxButtons.OK, MessageBoxIcon.Information);
                txtUsername.Focus();
            }
            else
            {
                // User closed the main window — exit the app
                Application.Exit();
            }
        }
    }
}
