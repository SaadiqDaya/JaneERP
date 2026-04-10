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
                        var remaining = (int)Math.Ceiling((fresh.LockedUntil.Value - DateTime.Now).TotalMinutes);
                        MessageBox.Show(this,
                            $"Account locked due to too many failed attempts.\nTry again in {remaining} minute(s).",
                            "Account Locked", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        var attempts = fresh?.FailedLoginCount ?? 0;
                        MessageBox.Show(this,
                            $"Invalid username or password. ({attempts}/{UserRepository.MaxLoginAttempts} attempts)",
                            "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    txtPassword.Clear();
                    txtPassword.Focus();
                    return;
                }

                AppSession.SetUser(user);
                AppLogger.Audit(user.Username, "Login", $"Role={user.Role}");
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
                txtUsername.Focus();
                Show();
            }
            else
            {
                // User closed the main window — exit the app
                Application.Exit();
            }
        }
    }
}
