using Dapper;
using JaneERP.Data;
using Microsoft.Data.SqlClient;
using System.Data;

namespace JaneERP
{
    /// <summary>
    /// First-run setup wizard shown automatically when a brand-new database has
    /// no users yet.  Walks through four steps:
    ///   1. Welcome
    ///   2. Create admin account
    ///   3. Business settings (currency, labour rate)
    ///   4. Tax rates
    /// </summary>
    public class FormFirstRunSetup : Form
    {
        // ── pages ─────────────────────────────────────────────────────────────
        private readonly Panel[] _pages;
        private int _pageIndex;

        // ── navigation buttons ─────────────────────────────────────────────────
        private readonly Button _btnBack;
        private readonly Button _btnNext;
        private readonly Button _btnFinish;

        // ── step indicator labels ──────────────────────────────────────────────
        private readonly Label[] _stepLabels = new Label[4];

        // ── page 2 — admin account ─────────────────────────────────────────────
        private TextBox _txtUsername    = null!;
        private TextBox _txtPassword    = null!;
        private TextBox _txtConfirm     = null!;
        private TextBox _txtEmail       = null!;
        private Label   _lblUserError   = null!;

        // ── page 3 — business settings ─────────────────────────────────────────
        private ComboBox        _cboCurrency    = null!;
        private NumericUpDown   _numLabourRate  = null!;
        private TextBox         _txtAdminEmail  = null!;
        private TextBox         _txtAdminPhone  = null!;

        // ── page 4 — tax rates ─────────────────────────────────────────────────
        private DataGridView _gridTax     = null!;
        private Button       _btnAddTax   = null!;
        private Button       _btnRemoveTax= null!;

        // ── repos ──────────────────────────────────────────────────────────────
        private readonly UserRepository _userRepo = new();

        public FormFirstRunSetup()
        {
            Text          = "JaneERP — First-Run Setup";
            ClientSize    = new Size(700, 520);
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox   = false;
            MinimizeBox   = false;
            BackColor     = Theme.Background;

            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);

            // ── top header bar ─────────────────────────────────────────────────
            var topBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 52,
                BackColor = Color.FromArgb(30, 20, 50)
            };
            var lblHeader = new Label
            {
                Text      = "JaneERP  —  First-Run Setup",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(16, 0, 0, 0)
            };
            topBar.Controls.Add(lblHeader);
            Controls.Add(topBar);

            // ── left step sidebar ──────────────────────────────────────────────
            var sidebar = new Panel
            {
                Width     = 160,
                Dock      = DockStyle.Left,
                BackColor = Color.FromArgb(40, 30, 65),
                Padding   = new Padding(0, 20, 0, 0)
            };
            string[] stepTitles = { "Welcome", "Admin Account", "Settings", "Tax Rates" };
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var lbl = new Label
                {
                    Text      = $"  {i + 1}.  {stepTitles[i]}",
                    Font      = new Font("Segoe UI", 9F),
                    ForeColor = Color.FromArgb(140, 120, 180),
                    AutoSize  = false,
                    Size      = new Size(160, 36),
                    Location  = new Point(0, 20 + i * 44),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Tag       = idx
                };
                _stepLabels[i] = lbl;
                sidebar.Controls.Add(lbl);
            }
            Controls.Add(sidebar);

            // ── navigation row (bottom) ────────────────────────────────────────
            var navBar = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 54,
                BackColor = Color.FromArgb(38, 28, 60),
                Padding   = new Padding(12, 10, 12, 10)
            };

            _btnBack = new Button
            {
                Text      = "Back",
                Size      = new Size(90, 32),
                Location  = new Point(12, 11),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 55, 110),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand,
                Enabled   = false
            };
            _btnBack.FlatAppearance.BorderSize = 0;
            _btnBack.Click += (_, _) => Navigate(-1);

            _btnNext = new Button
            {
                Text      = "Next  →",
                Size      = new Size(110, 32),
                Location  = new Point(440, 11),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Teal,
                ForeColor = Color.White,
                Cursor    = Cursors.Hand
            };
            _btnNext.FlatAppearance.BorderSize = 0;
            _btnNext.Click += (_, _) => Navigate(+1);

            _btnFinish = new Button
            {
                Text      = "Finish  ✓",
                Size      = new Size(120, 32),
                Location  = new Point(555, 11),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 130, 70),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand,
                Visible   = false
            };
            _btnFinish.FlatAppearance.BorderSize = 0;
            _btnFinish.Click += BtnFinish_Click;

            navBar.Controls.AddRange(new Control[] { _btnBack, _btnNext, _btnFinish });
            Controls.Add(navBar);

            // ── build pages ────────────────────────────────────────────────────
            _pages = new[]
            {
                BuildPageWelcome(),
                BuildPageAdminAccount(),
                BuildPageSettings(),
                BuildPageTaxRates()
            };

            // host all pages in a content area that fills between sidebar and nav
            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 12) };
            foreach (var p in _pages)
            {
                p.Dock = DockStyle.Fill;
                content.Controls.Add(p);
            }
            Controls.Add(content);

            ShowPage(0);

            // Load tax grid once form is ready
            Load += (_, _) => LoadTaxGrid();
        }

        // ── page builders ──────────────────────────────────────────────────────

        private Panel BuildPageWelcome()
        {
            var p = new Panel { BackColor = Color.Transparent };

            p.Controls.Add(PageTitle("Welcome to JaneERP"));
            p.Controls.Add(new Label
            {
                Text = "Your database has been created and all tables are ready.\n\n" +
                       "This wizard will walk you through the essential first-run configuration:\n\n" +
                       "  •  Create your admin account\n" +
                       "  •  Set your home currency and default labour rate\n" +
                       "  •  Review and customise your tax rates\n\n" +
                       "You can change all of these settings later from the Settings screen.\n\n" +
                       "Click Next to begin.",
                Font      = new Font("Segoe UI", 10F),
                ForeColor = Theme.TextSecondary,
                AutoSize  = false,
                Size      = new Size(480, 280),
                Location  = new Point(0, 52)
            });
            return p;
        }

        private Panel BuildPageAdminAccount()
        {
            var p = new Panel { BackColor = Color.Transparent };
            p.Controls.Add(PageTitle("Create Admin Account"));
            p.Controls.Add(new Label
            {
                Text      = "This is the first account — it will have full Admin access.",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                AutoSize  = true,
                Location  = new Point(0, 50)
            });

            int y = 82;
            _txtUsername  = AddField(p, ref y, "Username *",          "", false);
            _txtPassword  = AddField(p, ref y, "Password *",          "", true);
            _txtConfirm   = AddField(p, ref y, "Confirm Password *",  "", true);
            _txtEmail     = AddField(p, ref y, "Email (optional)",    "", false);

            _lblUserError = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Theme.Danger,
                AutoSize  = true,
                Location  = new Point(0, y)
            };
            p.Controls.Add(_lblUserError);
            return p;
        }

        private Panel BuildPageSettings()
        {
            var p = new Panel { BackColor = Color.Transparent };
            p.Controls.Add(PageTitle("Business Settings"));
            p.Controls.Add(new Label
            {
                Text      = "Set your core defaults. All can be updated later from Settings.",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                AutoSize  = true,
                Location  = new Point(0, 50)
            });

            int y = 88;

            // Home currency
            p.Controls.Add(FieldLabel("Home Currency", y));
            _cboCurrency = new ComboBox
            {
                Location      = new Point(0, y + 20),
                Size          = new Size(240, 26),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (var c in new[] { "CAD", "USD", "GBP", "EUR", "AUD", "NZD", "ZAR", "MXN", "SGD", "AED" })
                _cboCurrency.Items.Add(c);
            _cboCurrency.SelectedItem = AppSettings.Current.HomeCurrency;
            if (_cboCurrency.SelectedIndex < 0) _cboCurrency.SelectedIndex = 0;
            p.Controls.Add(_cboCurrency);
            y += 60;

            // Default labour rate
            p.Controls.Add(FieldLabel("Default Labour Rate (per hour)", y));
            _numLabourRate = new NumericUpDown
            {
                Location      = new Point(0, y + 20),
                Size          = new Size(160, 26),
                DecimalPlaces = 2,
                Minimum       = 0,
                Maximum       = 9999,
                Value         = AppSettings.Current.DefaultLabourRate
            };
            p.Controls.Add(_numLabourRate);
            y += 60;

            // Admin contact email
            _txtAdminEmail = AddField(p, ref y, "Admin Contact Email (for lockout messages)", "", false);
            _txtAdminEmail.Size = new Size(320, 26);
            _txtAdminEmail.Text = AppSettings.Current.AdminEmail;

            // Admin contact phone
            _txtAdminPhone = AddField(p, ref y, "Admin Contact Phone (optional)", "", false);
            _txtAdminPhone.Text = AppSettings.Current.AdminPhone;

            return p;
        }

        private Panel BuildPageTaxRates()
        {
            var p = new Panel { BackColor = Color.Transparent };
            p.Controls.Add(PageTitle("Tax Rates"));
            p.Controls.Add(new Label
            {
                Text      = "Review and customise your tax rates. You can add, edit, or remove rows.",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                AutoSize  = true,
                Location  = new Point(0, 50)
            });

            _gridTax = new DataGridView
            {
                Location            = new Point(0, 76),
                Size                = new Size(470, 200),
                AllowUserToAddRows  = false,
                RowHeadersVisible   = false,
                BackgroundColor     = SystemColors.Window,
                BorderStyle         = BorderStyle.FixedSingle,
                SelectionMode       = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect         = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EditMode            = DataGridViewEditMode.EditOnEnter
            };

            _gridTax.Columns.Add(new DataGridViewTextBoxColumn { Name = "TaxRateID", HeaderText = "ID",     Visible = false });
            _gridTax.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name",      HeaderText = "Name",   FillWeight = 60 });
            _gridTax.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rate",      HeaderText = "Rate %", FillWeight = 30 });
            _gridTax.Columns.Add(new DataGridViewCheckBoxColumn { Name = "IsActive", HeaderText = "Active", FillWeight = 10 });

            _btnAddTax = new Button
            {
                Text      = "+ Add Row",
                Location  = new Point(0, 284),
                Size      = new Size(110, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Teal,
                ForeColor = Color.White,
                Cursor    = Cursors.Hand
            };
            _btnAddTax.FlatAppearance.BorderSize = 0;
            _btnAddTax.Click += (_, _) =>
            {
                _gridTax.Rows.Add(0, "New Tax", "0.00", true);
                _gridTax.CurrentCell = _gridTax.Rows[_gridTax.Rows.Count - 1].Cells["Name"];
            };

            _btnRemoveTax = new Button
            {
                Text      = "Remove",
                Location  = new Point(118, 284),
                Size      = new Size(90, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(140, 50, 50),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand
            };
            _btnRemoveTax.FlatAppearance.BorderSize = 0;
            _btnRemoveTax.Click += (_, _) =>
            {
                if (_gridTax.SelectedRows.Count > 0)
                    _gridTax.Rows.Remove(_gridTax.SelectedRows[0]);
            };

            p.Controls.AddRange(new Control[] { _gridTax, _btnAddTax, _btnRemoveTax });
            return p;
        }

        // ── helpers ────────────────────────────────────────────────────────────

        private static Label PageTitle(string text) => new Label
        {
            Text      = text,
            Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = Theme.Gold,
            AutoSize  = true,
            Location  = new Point(0, 4)
        };

        private static Label FieldLabel(string text, int y) => new Label
        {
            Text      = text,
            Font      = new Font("Segoe UI", 9F),
            ForeColor = Theme.TextSecondary,
            AutoSize  = true,
            Location  = new Point(0, y)
        };

        private static TextBox AddField(Panel p, ref int y, string label, string defaultText, bool isPassword)
        {
            p.Controls.Add(FieldLabel(label, y));
            var txt = new TextBox
            {
                Location              = new Point(0, y + 20),
                Size                  = new Size(320, 26),
                Text                  = defaultText,
                UseSystemPasswordChar = isPassword
            };
            p.Controls.Add(txt);
            y += 58;
            return txt;
        }

        // ── navigation ─────────────────────────────────────────────────────────

        private void ShowPage(int index)
        {
            _pageIndex = index;
            for (int i = 0; i < _pages.Length; i++)
                _pages[i].Visible = (i == index);

            // Update step highlights
            for (int i = 0; i < _stepLabels.Length; i++)
            {
                bool active = (i == index);
                bool done   = (i < index);
                _stepLabels[i].ForeColor = active ? Color.White
                                         : done   ? Theme.Teal
                                                  : Color.FromArgb(120, 100, 160);
                _stepLabels[i].Font      = new Font("Segoe UI", 9F,
                    active ? FontStyle.Bold : FontStyle.Regular);
            }

            _btnBack.Enabled  = index > 0;
            _btnNext.Visible  = index < _pages.Length - 1;
            _btnFinish.Visible = index == _pages.Length - 1;
        }

        private void Navigate(int direction)
        {
            if (direction > 0 && !ValidatePage(_pageIndex)) return;
            ShowPage(_pageIndex + direction);
        }

        // ── validation ─────────────────────────────────────────────────────────

        private bool ValidatePage(int index)
        {
            _lblUserError.Text = "";

            if (index == 1) // Admin Account
            {
                var user = _txtUsername.Text.Trim();
                var pwd  = _txtPassword.Text;
                var conf = _txtConfirm.Text;

                if (string.IsNullOrEmpty(user))
                { _lblUserError.Text = "Username is required."; return false; }
                if (pwd.Length < 6)
                { _lblUserError.Text = "Password must be at least 6 characters."; return false; }
                if (pwd != conf)
                { _lblUserError.Text = "Passwords do not match."; return false; }
            }
            return true;
        }

        // ── tax grid data ──────────────────────────────────────────────────────

        private void LoadTaxGrid()
        {
            try
            {
                using IDbConnection db = new SqlConnection(
                    System.Configuration.ConfigurationManager
                          .ConnectionStrings["MyERP"]?.ConnectionString);
                var rows = db.Query("SELECT TaxRateID, Name, Rate, IsActive FROM TaxRates ORDER BY Name").ToList();
                _gridTax.Rows.Clear();
                foreach (var r in rows)
                    _gridTax.Rows.Add((int)r.TaxRateID,
                                      (string)r.Name,
                                      ((decimal)r.Rate * 100m).ToString("0.##"),
                                      (bool)r.IsActive);
            }
            catch { /* leave grid empty if table not yet ready */ }
        }

        private void SaveTaxRates()
        {
            try
            {
                using IDbConnection db = new SqlConnection(
                    System.Configuration.ConfigurationManager
                          .ConnectionStrings["MyERP"]?.ConnectionString);

                // Delete all existing rows and re-insert from the grid for simplicity
                db.Execute("DELETE FROM TaxRates");
                foreach (DataGridViewRow row in _gridTax.Rows)
                {
                    var name = row.Cells["Name"].Value?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(name)) continue;

                    decimal.TryParse(row.Cells["Rate"].Value?.ToString(), out decimal ratePercent);
                    bool active = row.Cells["IsActive"].Value is bool b && b;

                    db.Execute("INSERT INTO TaxRates (Name, Rate, IsActive) VALUES (@Name, @Rate, @IsActive)",
                        new { Name = name, Rate = ratePercent / 100m, IsActive = active });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save tax rates:\n\n" + ex.Message,
                    "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ── finish ─────────────────────────────────────────────────────────────

        private void BtnFinish_Click(object? sender, EventArgs e)
        {
            if (!ValidatePage(_pageIndex)) return;

            try
            {
                // 1. Create admin account
                _userRepo.CreateUser(
                    _txtUsername.Text.Trim(),
                    _txtPassword.Text,
                    "Admin",
                    _txtEmail.Text.Trim());

                // 2. Save business settings
                var cfg = AppSettings.Current;
                cfg.HomeCurrency    = _cboCurrency.SelectedItem?.ToString() ?? "CAD";
                cfg.DefaultCurrency = cfg.HomeCurrency;
                cfg.DefaultLabourRate = _numLabourRate.Value;
                cfg.AdminEmail      = _txtAdminEmail.Text.Trim();
                cfg.AdminPhone      = _txtAdminPhone.Text.Trim();
                cfg.Save();

                // 3. Save tax rates
                SaveTaxRates();

                Logging.AppLogger.Audit(_txtUsername.Text.Trim(), "FirstRunSetup",
                    $"Admin account created; currency={cfg.HomeCurrency}; " +
                    $"labourRate={cfg.DefaultLabourRate}");

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Could not complete setup:\n\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
