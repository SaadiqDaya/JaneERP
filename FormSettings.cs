using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>Admin-accessible settings form for customising the logo, phone numbers, and currencies.</summary>
    public class FormSettings : Form
    {
        private readonly AppSettings          _settings;
        private readonly IUomRepository       _uomRepo       = AppServices.Get<IUomRepository>();
        private readonly IAccountingRepository _accountingRepo = AppServices.Get<IAccountingRepository>();
        private DataGridView            _dgvUom         = new();
        private DataGridView            _dgvTaxRates    = new();
        private DataGridView            _dgvFlasks      = new();
        private DataGridView            _dgvLossPresets = new();
        private NumericUpDown           _nudLabourRate  = new();
        private TextBox    _txtBackupFolder    = new();
        private ComboBox   _cboBackupSchedule  = new();
        private Label      _lblLastBackup      = new();
        private PictureBox pbPreview       = new();
        private TextBox    txtLogoPath     = new();
        private Button     btnBrowse       = new();
        private TextBox    txtJanePhone    = new();
        private TextBox    txtOpheliaPhone = new();
        private ComboBox   cboHomeCurrency = new();
        private DataGridView dgvCurrencies   = new();
        private TextBox    txtNewCode        = new();
        private TextBox    txtNewRate        = new();
        private Button     btnAddCurrency    = new();
        private Button     btnRemoveCurrency = new();
        private ListBox    lstOrderTypes     = new();
        private TextBox    txtNewOrderType   = new();
        private Button     btnAddOrderType   = new();
        private Button     btnRemoveOrderType = new();
        private ListBox    lstShippingMethods      = new();
        private TextBox    txtNewShippingMethod     = new();
        private Button     btnAddShippingMethod     = new();
        private Button     btnRemoveShippingMethod  = new();
        private Button     btnSave           = new();
        private Button     btnCancel         = new();
        // Appearance tab
        private Panel      _pnlAccentPreview    = new();
        private Panel      _pnlHighlightPreview = new();
        private Button     _btnAccentColor      = new();
        private Button     _btnHighlightColor   = new();
        private TextBox      txtSmtpServer = new();
        private NumericUpDown nudSmtpPort  = new();
        private TextBox      txtSmtpUser   = new();
        private TextBox      txtSmtpPass   = new();
        private TextBox      txtFromEmail  = new();
        // Security settings
        private NumericUpDown nudMaxAttempts  = new();
        private NumericUpDown nudLockoutMins  = new();
        private TextBox      txtAdminPhone    = new();
        private TextBox      txtAdminEmail    = new();
        private CheckBox     chkRememberUser  = new();
        private TextBox      _txtDefaultExportPath = new();

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.Style |= 0x00040000; return cp; }
        }

        public FormSettings()
        {
            _settings = AppSettings.Load();
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.MakeResizable(this);
            Theme.AddCloseButton(this);
            pbPreview.BackColor = Color.White;
            RefreshPreview();
            LoadCurrencies();
            LoadOrderTypes();
            LoadShippingMethods();
            LoadManufacturingSettings();
            LoadUoms();
        }

        private void BuildUI()
        {
            Text          = "Settings";
            ClientSize    = new Size(820, 640);
            MinimumSize   = new Size(700, 520);
            StartPosition = FormStartPosition.CenterParent;

            // ── Header bar ────────────────────────────────────────────────────────
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Theme.Header };
            pnlHeader.Controls.Add(new Label
            {
                Text      = "Settings",
                AutoSize  = false,
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(14, 0, 0, 0)
            });
            // (pnlHeader added to Controls at end of BuildUI in correct dock order)
            Theme.MakeDraggable(this, pnlHeader);

            // ── Save / Cancel strip (bottom) ──────────────────────────────────────
            var pnlBottom = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 48,
                BackColor = Theme.Header,
                Padding   = new Padding(8, 8, 8, 8)
            };

            btnSave.Size     = new Size(88, 30);
            btnSave.Text     = "Save";
            btnSave.Anchor   = AnchorStyles.Right | AnchorStyles.Top;
            btnSave.Click   += BtnSave_Click;

            btnCancel.Size   = new Size(88, 30);
            btnCancel.Text   = "Cancel";
            btnCancel.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            btnCancel.Click += (_, _) => Close();

            void PositionBottomButtons()
            {
                btnCancel.Location = new Point(pnlBottom.ClientSize.Width - btnCancel.Width - 8, 9);
                btnSave.Location   = new Point(btnCancel.Left - btnSave.Width - 6, 9);
            }
            pnlBottom.Resize += (_, _) => PositionBottomButtons();
            pnlBottom.Controls.Add(btnSave);
            pnlBottom.Controls.Add(btnCancel);
            // (pnlBottom added to Controls at end of BuildUI in correct dock order)
            Load += (_, _) => PositionBottomButtons();

            // ── Left nav ─────────────────────────────────────────────────────────
            var pnlNav = new Panel { Dock = DockStyle.Left, Width = 168, BackColor = Theme.Header };

            var lstNav = new ListBox
            {
                Dock        = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font        = new Font("Segoe UI", 9.5F),
                BackColor   = Theme.Header,
                ForeColor   = Theme.TextPrimary
            };
            lstNav.Items.AddRange(new object[]
            {
                "Company", "Currencies", "Order Types", "Fulfillment",
                "Notifications", "Security", "System", "Units of Measure",
                "Backup", "Manufacturing", "Appearance", "Tax Rates"
            });
            pnlNav.Controls.Add(lstNav);
            // (pnlNav and divider added to Controls at end of BuildUI in correct dock order)
            var divider = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = Theme.Border };

            // ── Content area ──────────────────────────────────────────────────────
            var pnlContent = new Panel { Dock = DockStyle.Fill };
            // (pnlContent added to Controls at end of BuildUI in correct dock order)

            // ── Build all page panels ─────────────────────────────────────────────
            var pages = new Panel[]
            {
                BuildPageCompany(),
                BuildPageCurrencies(),
                BuildPageOrderTypes(),
                BuildPageFulfillment(),
                BuildPageNotifications(),
                BuildPageSecurity(),
                BuildPageSystem(),
                BuildPageUom(),
                BuildPageBackup(),
                BuildPageManufacturing(),
                BuildPageAppearance(),
                BuildPageTaxRates()
            };

            // Each page fills the content area; WinForms handles sizing via Dock.
            // Invisible Fill panels are excluded from layout — only the visible one fills.
            foreach (var p in pages)
            {
                p.Dock    = DockStyle.Fill;
                p.Visible = false;
                pnlContent.Controls.Add(p);
            }

            lstNav.SelectedIndexChanged += (_, _) =>
            {
                foreach (var p in pages) p.Visible = false;
                int idx = lstNav.SelectedIndex;
                if (idx >= 0 && idx < pages.Length)
                    pages[idx].Visible = true;
            };
            lstNav.SelectedIndex = 0;

            // ── Add to form in correct WinForms dock order ────────────────────────
            // Fill must be at the lowest index so it is processed last, getting
            // whatever space remains after Top/Bottom/Left panels have claimed theirs.
            Controls.Add(pnlContent);   // Fill  — processed last
            Controls.Add(pnlNav);       // Left
            Controls.Add(divider);      // Left (1 px border)
            Controls.Add(pnlBottom);    // Bottom
            Controls.Add(pnlHeader);    // Top   — processed first
        }

        // ── Page builders ─────────────────────────────────────────────────────────

        private Panel BuildPageCompany()
        {
            var pnl = new Panel { AutoScroll = true };
            int y = 16;
            pnl.Controls.Add(new Label { Text = "Company Logo (PNG or JPG):", Location = new Point(16, y), AutoSize = true });
            y += 22;

            txtLogoPath.Location     = new Point(16, y);
            txtLogoPath.Size         = new Size(400, 23);
            txtLogoPath.Text         = _settings.LogoPath;
            txtLogoPath.TextChanged += (_, _) => RefreshPreview();
            pnl.Controls.Add(txtLogoPath);

            btnBrowse.Location = new Point(424, y - 1);
            btnBrowse.Size     = new Size(80, 25);
            btnBrowse.Text     = "Browse…";
            btnBrowse.Click   += BtnBrowse_Click;
            pnl.Controls.Add(btnBrowse);
            y += 30;

            pbPreview.Location    = new Point(16, y);
            pbPreview.Size        = new Size(200, 60);
            pbPreview.SizeMode    = PictureBoxSizeMode.Zoom;
            pbPreview.BackColor   = Color.White;
            pbPreview.BorderStyle = BorderStyle.FixedSingle;
            pnl.Controls.Add(pbPreview);
            y += 76;

            pnl.Controls.Add(new Label
            {
                Text = "Phone Numbers", Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(16, y), AutoSize = true
            });
            y += 28;

            pnl.Controls.Add(new Label { Text = "Jane:", Location = new Point(16, y + 3), AutoSize = true });
            txtJanePhone.Location        = new Point(104, y);
            txtJanePhone.Size            = new Size(200, 23);
            txtJanePhone.Text            = _settings.JanePhone;
            txtJanePhone.PlaceholderText = "e.g. 6042274507";
            pnl.Controls.Add(txtJanePhone);
            y += 30;

            pnl.Controls.Add(new Label { Text = "Ophelia:", Location = new Point(16, y + 3), AutoSize = true });
            txtOpheliaPhone.Location = new Point(104, y);
            txtOpheliaPhone.Size     = new Size(200, 23);
            txtOpheliaPhone.Text     = _settings.OpheliaPhone;
            pnl.Controls.Add(txtOpheliaPhone);

            return pnl;
        }

        private Panel BuildPageCurrencies()
        {
            var pnl = new Panel { AutoScroll = true };
            int y = 16;

            pnl.Controls.Add(new Label { Text = "Home Currency:", Location = new Point(16, y + 3), AutoSize = true });
            cboHomeCurrency.Location      = new Point(134, y);
            cboHomeCurrency.Size          = new Size(100, 23);
            cboHomeCurrency.DropDownStyle = ComboBoxStyle.DropDownList;
            cboHomeCurrency.Items.Add(_settings.HomeCurrency);
            foreach (var k in _settings.CurrencyRates.Keys) cboHomeCurrency.Items.Add(k);
            cboHomeCurrency.SelectedItem = _settings.HomeCurrency;
            pnl.Controls.Add(cboHomeCurrency);
            y += 36;

            pnl.Controls.Add(new Label
            {
                Text = "Exchange Rates  (units of home currency per 1 foreign unit)",
                Location = new Point(16, y), AutoSize = true, ForeColor = Theme.TextSecondary
            });
            y += 22;

            dgvCurrencies.AutoGenerateColumns = false;
            dgvCurrencies.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCode", HeaderText = "Code", Width = 80, ReadOnly = true });
            dgvCurrencies.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRate", HeaderText = "Rate to Home", Width = 140 });
            dgvCurrencies.AllowUserToAddRows    = false;
            dgvCurrencies.AllowUserToDeleteRows = false;
            dgvCurrencies.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvCurrencies.Location = new Point(16, y);
            dgvCurrencies.Size     = new Size(300, 150);
            dgvCurrencies.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex < 0 || dgvCurrencies.Columns["colRate"] is not DataGridViewColumn colRateChk || e.ColumnIndex != colRateChk.Index) return;
                var code = dgvCurrencies.Rows[e.RowIndex].Cells["colCode"]?.Value?.ToString() ?? "";
                if (decimal.TryParse(dgvCurrencies.Rows[e.RowIndex].Cells["colRate"]?.Value?.ToString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var rate) && !string.IsNullOrEmpty(code))
                    _settings.CurrencyRates[code] = rate;
            };
            pnl.Controls.Add(dgvCurrencies);

            txtNewCode.Location        = new Point(324, y);
            txtNewCode.Size            = new Size(60, 23);
            txtNewCode.PlaceholderText = "USD";
            txtNewCode.CharacterCasing = CharacterCasing.Upper;
            pnl.Controls.Add(txtNewCode);

            txtNewRate.Location        = new Point(392, y);
            txtNewRate.Size            = new Size(80, 23);
            txtNewRate.PlaceholderText = "1.45";
            pnl.Controls.Add(txtNewRate);

            btnAddCurrency.Text     = "+ Add";
            btnAddCurrency.Size     = new Size(70, 23);
            btnAddCurrency.Location = new Point(480, y);
            btnAddCurrency.Click   += BtnAddCurrency_Click;
            pnl.Controls.Add(btnAddCurrency);
            y += 30;

            btnRemoveCurrency.Text     = "Remove Selected";
            btnRemoveCurrency.Size     = new Size(130, 23);
            btnRemoveCurrency.Location = new Point(324, y);
            btnRemoveCurrency.Click   += BtnRemoveCurrency_Click;
            pnl.Controls.Add(btnRemoveCurrency);

            return pnl;
        }

        private Panel BuildPageOrderTypes()
        {
            var pnl = new Panel { AutoScroll = true };
            int y = 16;

            pnl.Controls.Add(new Label
            {
                Text      = "Order Types  (used when creating manual orders)",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(16, y), AutoSize = true
            });
            y += 28;

            pnl.Controls.Add(new Label
            {
                Text      = "\"Shopify\" is a reserved system type and cannot be removed.",
                ForeColor = Theme.TextSecondary, Location = new Point(16, y), AutoSize = true
            });
            y += 24;

            lstOrderTypes.Location      = new Point(16, y);
            lstOrderTypes.Size          = new Size(200, 140);
            lstOrderTypes.SelectionMode = SelectionMode.One;
            pnl.Controls.Add(lstOrderTypes);

            txtNewOrderType.Location        = new Point(228, y);
            txtNewOrderType.Size            = new Size(140, 23);
            txtNewOrderType.PlaceholderText = "e.g. Phone";
            pnl.Controls.Add(txtNewOrderType);

            btnAddOrderType.Text     = "+ Add";
            btnAddOrderType.Size     = new Size(70, 23);
            btnAddOrderType.Location = new Point(376, y);
            btnAddOrderType.Click   += BtnAddOrderType_Click;
            pnl.Controls.Add(btnAddOrderType);
            y += 30;

            btnRemoveOrderType.Text     = "Remove Selected";
            btnRemoveOrderType.Size     = new Size(130, 23);
            btnRemoveOrderType.Location = new Point(228, y);
            btnRemoveOrderType.Click   += BtnRemoveOrderType_Click;
            pnl.Controls.Add(btnRemoveOrderType);

            return pnl;
        }

        private Panel BuildPageFulfillment()
        {
            var pnl = new Panel { AutoScroll = true };
            int y = 16;

            pnl.Controls.Add(new Label
            {
                Text      = "Shipping Methods  (shown in the picking and quick-fulfil screens)",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(16, y), AutoSize = true
            });
            y += 28;

            pnl.Controls.Add(new Label
            {
                Text      = "\"Local Pickup\" and other non-carrier methods are valid entries.",
                ForeColor = Theme.TextSecondary, Location = new Point(16, y), AutoSize = true
            });
            y += 24;

            lstShippingMethods.Location      = new Point(16, y);
            lstShippingMethods.Size          = new Size(200, 140);
            lstShippingMethods.SelectionMode = SelectionMode.One;
            pnl.Controls.Add(lstShippingMethods);

            txtNewShippingMethod.Location        = new Point(228, y);
            txtNewShippingMethod.Size            = new Size(160, 23);
            txtNewShippingMethod.PlaceholderText = "e.g. Same Day";
            pnl.Controls.Add(txtNewShippingMethod);

            btnAddShippingMethod.Text     = "+ Add";
            btnAddShippingMethod.Size     = new Size(70, 23);
            btnAddShippingMethod.Location = new Point(396, y);
            btnAddShippingMethod.Click   += BtnAddShippingMethod_Click;
            pnl.Controls.Add(btnAddShippingMethod);
            y += 30;

            btnRemoveShippingMethod.Text     = "Remove Selected";
            btnRemoveShippingMethod.Size     = new Size(130, 23);
            btnRemoveShippingMethod.Location = new Point(228, y);
            btnRemoveShippingMethod.Click   += BtnRemoveShippingMethod_Click;
            pnl.Controls.Add(btnRemoveShippingMethod);

            return pnl;
        }

        private Panel BuildPageNotifications()
        {
            var pnl = new Panel { AutoScroll = true };
            int y = 16;

            pnl.Controls.Add(new Label
            {
                Text      = "Email / SMTP  (for @mention notifications)",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(16, y), AutoSize = true
            });
            y += 32;

            pnl.Controls.Add(new Label { Text = "SMTP Server:", Location = new Point(16, y + 3), AutoSize = true });
            txtSmtpServer.Location        = new Point(134, y);
            txtSmtpServer.Size            = new Size(200, 23);
            txtSmtpServer.PlaceholderText = "smtp.gmail.com";
            txtSmtpServer.Text            = _settings.SmtpServer;
            pnl.Controls.Add(txtSmtpServer);

            pnl.Controls.Add(new Label { Text = "Port:", Location = new Point(348, y + 3), AutoSize = true });
            nudSmtpPort.Location = new Point(384, y);
            nudSmtpPort.Size     = new Size(70, 23);
            nudSmtpPort.Minimum  = 1;
            nudSmtpPort.Maximum  = 65535;
            nudSmtpPort.Value    = _settings.SmtpPort;
            pnl.Controls.Add(nudSmtpPort);
            y += 32;

            pnl.Controls.Add(new Label { Text = "Username:", Location = new Point(16, y + 3), AutoSize = true });
            txtSmtpUser.Location        = new Point(134, y);
            txtSmtpUser.Size            = new Size(200, 23);
            txtSmtpUser.PlaceholderText = "user@gmail.com";
            txtSmtpUser.Text            = _settings.SmtpUser;
            pnl.Controls.Add(txtSmtpUser);
            y += 32;

            pnl.Controls.Add(new Label { Text = "Password:", Location = new Point(16, y + 3), AutoSize = true });
            txtSmtpPass.Location              = new Point(134, y);
            txtSmtpPass.Size                  = new Size(200, 23);
            txtSmtpPass.UseSystemPasswordChar = true;
            txtSmtpPass.PlaceholderText       = "app password";
            txtSmtpPass.Text                  = _settings.SmtpPasswordPlain;
            pnl.Controls.Add(txtSmtpPass);
            y += 32;

            pnl.Controls.Add(new Label { Text = "From Email:", Location = new Point(16, y + 3), AutoSize = true });
            txtFromEmail.Location        = new Point(134, y);
            txtFromEmail.Size            = new Size(200, 23);
            txtFromEmail.PlaceholderText = "noreply@company.com";
            txtFromEmail.Text            = _settings.FromEmail;
            pnl.Controls.Add(txtFromEmail);

            return pnl;
        }

        private Panel BuildPageSecurity()
        {
            var pnl = new Panel { AutoScroll = true };
            int y = 16;

            pnl.Controls.Add(new Label
            {
                Text = "Login & Lockout Policy", Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(16, y), AutoSize = true
            });
            y += 28;

            pnl.Controls.Add(new Label { Text = "Max failed attempts before lockout:", AutoSize = true, Location = new Point(16, y + 3) });
            nudMaxAttempts.Location = new Point(264, y);
            nudMaxAttempts.Size     = new Size(70, 23);
            nudMaxAttempts.Minimum  = 1;
            nudMaxAttempts.Maximum  = 20;
            nudMaxAttempts.Value    = _settings.MaxLoginAttempts > 0 ? _settings.MaxLoginAttempts : 5;
            pnl.Controls.Add(nudMaxAttempts);
            y += 32;

            pnl.Controls.Add(new Label { Text = "Lockout duration (minutes):", AutoSize = true, Location = new Point(16, y + 3) });
            nudLockoutMins.Location = new Point(264, y);
            nudLockoutMins.Size     = new Size(70, 23);
            nudLockoutMins.Minimum  = 1;
            nudLockoutMins.Maximum  = 1440;
            nudLockoutMins.Value    = _settings.LockoutMinutes > 0 ? _settings.LockoutMinutes : 15;
            pnl.Controls.Add(nudLockoutMins);
            y += 36;

            pnl.Controls.Add(new Label
            {
                Text = "Admin Contact  (shown in lockout messages)", Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(16, y), AutoSize = true
            });
            y += 28;

            pnl.Controls.Add(new Label { Text = "Admin Phone:", AutoSize = true, Location = new Point(16, y + 3) });
            txtAdminPhone.Location        = new Point(134, y);
            txtAdminPhone.Size            = new Size(200, 23);
            txtAdminPhone.PlaceholderText = "e.g. 604-555-0100";
            txtAdminPhone.Text            = _settings.AdminPhone;
            pnl.Controls.Add(txtAdminPhone);
            y += 32;

            pnl.Controls.Add(new Label { Text = "Admin Email:", AutoSize = true, Location = new Point(16, y + 3) });
            txtAdminEmail.Location        = new Point(134, y);
            txtAdminEmail.Size            = new Size(200, 23);
            txtAdminEmail.PlaceholderText = "admin@company.com";
            txtAdminEmail.Text            = _settings.AdminEmail;
            pnl.Controls.Add(txtAdminEmail);
            y += 36;

            pnl.Controls.Add(new Label
            {
                Text = "Login Options", Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(16, y), AutoSize = true
            });
            y += 28;

            chkRememberUser.Text     = "Remember last username on login screen";
            chkRememberUser.AutoSize = true;
            chkRememberUser.Checked  = _settings.RememberLastUsername;
            chkRememberUser.Location = new Point(16, y);
            pnl.Controls.Add(chkRememberUser);

            return pnl;
        }

        private Panel BuildPageSystem()
        {
            var pnl = new Panel { AutoScroll = true };
            int y = 16;

            var grpDiscountTiers = new GroupBox
            {
                Text      = "Discount Tiers",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(16, y),
                Size      = new Size(580, 90)
            };
            grpDiscountTiers.Controls.Add(new Label
            {
                Text = "Assign discount tiers to customers for automatic order discounts.",
                ForeColor = Theme.TextSecondary, Location = new Point(10, 24), Size = new Size(440, 18)
            });
            var btnDiscountTiers = new Button { Text = "Manage Discount Tiers →", Location = new Point(10, 50), Size = new Size(200, 28) };
            btnDiscountTiers.Click += (_, _) => { using var frm = new FormDiscountTiers(); frm.ShowDialog(this); };
            grpDiscountTiers.Controls.Add(btnDiscountTiers);
            var btnCustomerTiers = new Button { Text = "Assign Tiers to Customers →", Location = new Point(218, 50), Size = new Size(220, 28) };
            btnCustomerTiers.Click += (_, _) => { using var frm = new FormCustomerTiers(); frm.ShowDialog(this); };
            grpDiscountTiers.Controls.Add(btnCustomerTiers);
            pnl.Controls.Add(grpDiscountTiers);
            y += 104;

            var grpProductSearch = new GroupBox
            {
                Text      = "Product Explorer",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(16, y),
                Size      = new Size(580, 90)
            };
            grpProductSearch.Controls.Add(new Label
            {
                Text = "Set up attribute filter buttons for the Product Explorer screen.",
                ForeColor = Theme.TextSecondary, Location = new Point(10, 24), Size = new Size(440, 18)
            });
            var btnConfigureSearch = new Button { Text = "Configure Search Filters →", Location = new Point(10, 50), Size = new Size(200, 28) };
            btnConfigureSearch.Click += (_, _) => { using var frm = new FormProductSearch(); frm.ShowDialog(this); };
            grpProductSearch.Controls.Add(btnConfigureSearch);
            pnl.Controls.Add(grpProductSearch);
            y += 104;

            var grpExports = new GroupBox
            {
                Text      = "Exports",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(16, y),
                Size      = new Size(580, 90)
            };
            grpExports.Controls.Add(new Label
            {
                Text = "Default folder for CSV exports. Leave blank to always ask.",
                ForeColor = Theme.TextSecondary, Location = new Point(10, 24), Size = new Size(440, 18)
            });
            _txtDefaultExportPath.Location        = new Point(10, 50);
            _txtDefaultExportPath.Size            = new Size(420, 23);
            _txtDefaultExportPath.PlaceholderText = "e.g. C:\\Exports";
            _txtDefaultExportPath.Text            = _settings.DefaultExportPath;
            grpExports.Controls.Add(_txtDefaultExportPath);
            var btnBrowseExport = new Button { Text = "Browse…", Location = new Point(438, 49), Size = new Size(80, 25) };
            btnBrowseExport.Click += (_, _) =>
            {
                using var dlg = new FolderBrowserDialog { Description = "Select default export folder" };
                if (!string.IsNullOrEmpty(_txtDefaultExportPath.Text)) dlg.SelectedPath = _txtDefaultExportPath.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK) _txtDefaultExportPath.Text = dlg.SelectedPath;
            };
            grpExports.Controls.Add(btnBrowseExport);
            pnl.Controls.Add(grpExports);

            return pnl;
        }

        private Panel BuildPageUom()
        {
            var pnl = new Panel { AutoScroll = true };
            int y = 16;

            pnl.Controls.Add(new Label
            {
                Text = "Units of Measure", Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(16, y), AutoSize = true
            });
            y += 28;

            pnl.Controls.Add(new Label
            {
                Text      = "ConversionFactor = how many base units equal 1 of this unit  (e.g. kg → g: factor = 1000)",
                ForeColor = Theme.TextSecondary, Location = new Point(16, y), AutoSize = true
            });
            y += 22;

            _dgvUom.AutoGenerateColumns = false;
            _dgvUom.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colUomID",    Visible = false });
            _dgvUom.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colName",     HeaderText = "Name",              Width = 110 });
            _dgvUom.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colAbbr",     HeaderText = "Abbreviation",      Width = 90  });
            _dgvUom.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colBase",     HeaderText = "Base Unit",         Width = 80  });
            _dgvUom.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colFactor",   HeaderText = "Conversion Factor", Width = 110 });
            _dgvUom.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colOrder",    HeaderText = "Display Order",     Width = 90  });
            _dgvUom.Columns.Add(new DataGridViewCheckBoxColumn { Name = "colUomActive",HeaderText = "Active",            Width = 58  });
            _dgvUom.AllowUserToAddRows    = false;
            _dgvUom.AllowUserToDeleteRows = false;
            _dgvUom.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgvUom.Location              = new Point(16, y);
            _dgvUom.Size                  = new Size(600, 280);
            _dgvUom.Anchor                = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnl.Controls.Add(_dgvUom);
            y += 290;

            var btnAddUomRow = new Button { Text = "+ Add Row",       Size = new Size(90, 28),  Location = new Point(16, y) };
            var btnDelUomRow = new Button { Text = "Delete Selected", Size = new Size(120, 28), Location = new Point(114, y) };
            var btnSaveUoms  = new Button { Text = "Save Changes",    Size = new Size(120, 28), Location = new Point(496, y) };
            btnAddUomRow.Click += (_, _) =>
            {
                int nextOrder = _dgvUom.Rows.Count * 10;
                int idx = _dgvUom.Rows.Add(0, "New Unit", "unit", "unit", "1", nextOrder.ToString(), true);
                _dgvUom.ClearSelection();
                _dgvUom.Rows[idx].Selected = true;
                _dgvUom.CurrentCell = _dgvUom.Rows[idx].Cells["colName"];
                _dgvUom.BeginEdit(true);
            };
            btnDelUomRow.Click += BtnDelUomRow_Click;
            btnSaveUoms.Click  += BtnSaveUoms_Click;
            Theme.StyleButton(btnSaveUoms);
            pnl.Controls.Add(btnAddUomRow);
            pnl.Controls.Add(btnDelUomRow);
            pnl.Controls.Add(btnSaveUoms);
            y += 38;

            // ── Conversion calculator ─────────────────────────────────────────────
            pnl.Controls.Add(new Label
            {
                Text = "Conversion Calculator", Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(16, y), AutoSize = true
            });
            y += 26;
            pnl.Controls.Add(new Label
            {
                Text = "Units sharing the same Base Unit can be converted. Conversion = quantity × FromFactor ÷ ToFactor.",
                ForeColor = Theme.TextSecondary, Location = new Point(16, y), AutoSize = true
            });
            y += 22;

            var nudConvQty  = new NumericUpDown { Location = new Point(16, y), Size = new Size(90, 23), DecimalPlaces = 4, Minimum = 0, Maximum = 999999, Value = 1 };
            var cboConvFrom = new ComboBox { Location = new Point(114, y), Size = new Size(90, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            pnl.Controls.Add(new Label { Text = "→", AutoSize = true, Location = new Point(210, y + 3), ForeColor = Theme.TextPrimary });
            var cboConvTo   = new ComboBox { Location = new Point(228, y), Size = new Size(90, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            var lblConvResult = new Label { Location = new Point(326, y + 3), AutoSize = true, ForeColor = Theme.Teal, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            var btnConvert  = new Button  { Text = "Convert", Size = new Size(76, 23), Location = new Point(480, y) };

            // Populate combos when page becomes visible (after UOMs are loaded)
            pnl.VisibleChanged += (_, _) =>
            {
                if (!pnl.Visible) return;
                var abbrs = _uomRepo.GetAbbreviations();
                cboConvFrom.Items.Clear(); cboConvTo.Items.Clear();
                foreach (var a in abbrs) { cboConvFrom.Items.Add(a); cboConvTo.Items.Add(a); }
                if (cboConvFrom.Items.Count > 1) { cboConvFrom.SelectedIndex = 0; cboConvTo.SelectedIndex = 1; }
            };

            btnConvert.Click += (_, _) =>
            {
                string? from = cboConvFrom.SelectedItem?.ToString();
                string? to   = cboConvTo.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;
                if (_uomRepo.TryConvert(from, to, nudConvQty.Value, out decimal r))
                    lblConvResult.Text = $"= {r:G} {to}";
                else
                    lblConvResult.Text = "Cannot convert (incompatible base units)";
            };

            pnl.Controls.Add(nudConvQty);
            pnl.Controls.Add(cboConvFrom);
            pnl.Controls.Add(cboConvTo);
            pnl.Controls.Add(lblConvResult);
            pnl.Controls.Add(btnConvert);

            return pnl;
        }

        private Panel BuildPageBackup()
        {
            var pnl = new Panel { AutoScroll = true };
            int y = 16;

            pnl.Controls.Add(new Label
            {
                Text = "Database Backup", Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(16, y), AutoSize = true
            });
            y += 30;

            pnl.Controls.Add(new Label { Text = "Backup Folder:", AutoSize = true, Location = new Point(16, y + 3) });
            _txtBackupFolder          = new TextBox();
            _txtBackupFolder.Location = new Point(134, y);
            _txtBackupFolder.Size     = new Size(320, 23);
            _txtBackupFolder.Text     = _settings.BackupFolder;
            pnl.Controls.Add(_txtBackupFolder);

            var btnBrowseBackup = new Button { Text = "Browse…", Location = new Point(462, y - 1), Size = new Size(80, 25) };
            btnBrowseBackup.Click += (_, _) =>
            {
                using var dlg = new FolderBrowserDialog { Description = "Select backup folder" };
                if (!string.IsNullOrEmpty(_txtBackupFolder.Text)) dlg.SelectedPath = _txtBackupFolder.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK) _txtBackupFolder.Text = dlg.SelectedPath;
            };
            pnl.Controls.Add(btnBrowseBackup);
            y += 36;

            pnl.Controls.Add(new Label { Text = "Auto Backup:", AutoSize = true, Location = new Point(16, y + 3) });
            _cboBackupSchedule = new ComboBox
            {
                Location      = new Point(134, y),
                Size          = new Size(120, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cboBackupSchedule.Items.AddRange(new object[] { "None", "Daily", "Weekly" });
            _cboBackupSchedule.SelectedItem = _settings.BackupSchedule ?? "None";
            pnl.Controls.Add(_cboBackupSchedule);
            y += 36;

            _lblLastBackup = new Label
            {
                AutoSize  = true,
                Location  = new Point(16, y),
                ForeColor = Theme.TextSecondary,
                Text      = _settings.LastBackupAt.HasValue
                    ? $"Last backup: {_settings.LastBackupAt.Value.ToLocalTime():yyyy-MM-dd HH:mm}"
                    : "Last backup: never"
            };
            pnl.Controls.Add(_lblLastBackup);
            y += 30;

            var btnBackupNow = new Button { Text = "Backup Now", Location = new Point(16, y), Size = new Size(130, 32) };
            btnBackupNow.Click += BtnBackupNow_Click;
            pnl.Controls.Add(btnBackupNow);

            pnl.Controls.Add(new Label
            {
                Text      = "Note: The backup folder must be accessible by the SQL Server service account\n" +
                             "(usually MSSQLSERVER or SQLServerMSSQLUser). For localhost, any local folder works.",
                ForeColor = Theme.TextSecondary,
                Location  = new Point(16, y + 42),
                Size      = new Size(560, 40)
            });

            return pnl;
        }

        private Panel BuildPageManufacturing()
        {
            var pnl = new Panel { AutoScroll = true };
            int y = 16;

            // ── Default Labour Rate ───────────────────────────────────────────────
            pnl.Controls.Add(new Label
            {
                Text = "Default Labour Rate", Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(16, y), AutoSize = true
            });
            y += 28;

            pnl.Controls.Add(new Label { Text = "Default hourly rate ($/hr):", AutoSize = true, Location = new Point(16, y + 3) });
            _nudLabourRate.Location      = new Point(200, y);
            _nudLabourRate.Size          = new Size(90, 23);
            _nudLabourRate.DecimalPlaces = 2;
            _nudLabourRate.Minimum       = 0;
            _nudLabourRate.Maximum       = 9999;
            _nudLabourRate.Value         = _settings.DefaultLabourRate;
            pnl.Controls.Add(_nudLabourRate);

            pnl.Controls.Add(new Label
            {
                Text = "Pre-fills the Rate/hr field when you add a new labour row in the BOM editor.",
                ForeColor = Theme.TextSecondary, Location = new Point(16, y + 28), AutoSize = true
            });
            y += 62;

            // ── Flask / Vessel Configuration ──────────────────────────────────────
            pnl.Controls.Add(new Label
            {
                Text = "Flask / Vessel Configuration", Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(16, y), AutoSize = true
            });
            pnl.Controls.Add(new Label
            {
                Text = "First flask where Batch ML ≤ Max Batch ML is assigned. Listed in ascending order.",
                ForeColor = Theme.TextSecondary, Location = new Point(16, y + 22), AutoSize = true
            });
            y += 46;

            _dgvFlasks.AutoGenerateColumns   = false;
            _dgvFlasks.AllowUserToAddRows    = true;
            _dgvFlasks.AllowUserToDeleteRows = true;
            _dgvFlasks.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgvFlasks.Location = new Point(16, y);
            _dgvFlasks.Size     = new Size(450, 140);
            _dgvFlasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFlaskName",  HeaderText = "Flask Name",     Width = 200 });
            _dgvFlasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFlaskMaxMl", HeaderText = "Max Batch (ml)", Width = 140 });
            pnl.Controls.Add(_dgvFlasks);
            y += 150;

            var btnSaveFlasks = new Button { Text = "Save Flasks", Size = new Size(120, 28), Location = new Point(16, y) };
            btnSaveFlasks.Click += BtnSaveFlasks_Click;
            Theme.StyleButton(btnSaveFlasks);
            pnl.Controls.Add(btnSaveFlasks);
            y += 44;

            // ── Batch Loss Presets ────────────────────────────────────────────────
            pnl.Controls.Add(new Label
            {
                Text = "Batch Loss Presets", Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(16, y), AutoSize = true
            });
            pnl.Controls.Add(new Label
            {
                Text = "Quick-select options shown in the Batch Cooking launcher.",
                ForeColor = Theme.TextSecondary, Location = new Point(16, y + 22), AutoSize = true
            });
            y += 46;

            _dgvLossPresets.AutoGenerateColumns   = false;
            _dgvLossPresets.AllowUserToAddRows    = true;
            _dgvLossPresets.AllowUserToDeleteRows = true;
            _dgvLossPresets.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgvLossPresets.Location = new Point(16, y);
            _dgvLossPresets.Size     = new Size(380, 120);
            _dgvLossPresets.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPresetLabel",   HeaderText = "Label",  Width = 220 });
            _dgvLossPresets.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPresetPercent", HeaderText = "Loss %", Width = 80  });
            pnl.Controls.Add(_dgvLossPresets);
            y += 130;

            var btnSavePresets = new Button { Text = "Save Presets", Size = new Size(120, 28), Location = new Point(16, y) };
            btnSavePresets.Click += BtnSavePresets_Click;
            Theme.StyleButton(btnSavePresets);
            pnl.Controls.Add(btnSavePresets);

            return pnl;
        }

        private Panel BuildPageAppearance()
        {
            var pnl = new Panel { AutoScroll = true };
            int y = 16;

            pnl.Controls.Add(new Label
            {
                Text = "Colour Scheme", Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(16, y), AutoSize = true
            });
            y += 34;

            pnl.Controls.Add(new Label { Text = "Primary Accent:", AutoSize = true, Location = new Point(16, y + 5) });
            _pnlAccentPreview.Location    = new Point(134, y);
            _pnlAccentPreview.Size        = new Size(36, 26);
            _pnlAccentPreview.BackColor   = TryParseColor(_settings.AccentColor, Theme.Gold);
            _pnlAccentPreview.BorderStyle = BorderStyle.FixedSingle;
            pnl.Controls.Add(_pnlAccentPreview);

            _btnAccentColor.Text     = "Pick…";
            _btnAccentColor.Size     = new Size(60, 26);
            _btnAccentColor.Location = new Point(178, y);
            _btnAccentColor.Click   += (_, _) =>
            {
                using var dlg = new ColorDialog { FullOpen = true, Color = _pnlAccentPreview.BackColor };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _pnlAccentPreview.BackColor = dlg.Color;
                    _settings.AccentColor       = ColorTranslator.ToHtml(dlg.Color);
                }
            };
            pnl.Controls.Add(_btnAccentColor);

            var btnResetAccent = new Button { Text = "Reset", Size = new Size(55, 26), Location = new Point(246, y) };
            btnResetAccent.Click += (_, _) => { _settings.AccentColor = ""; _pnlAccentPreview.BackColor = Color.FromArgb(155, 55, 220); };
            pnl.Controls.Add(btnResetAccent);
            pnl.Controls.Add(new Label
            {
                Text = "Main accent (buttons, borders, tile glow)", ForeColor = Theme.TextSecondary,
                Location = new Point(310, y + 5), AutoSize = true
            });
            y += 44;

            pnl.Controls.Add(new Label { Text = "Highlight:", AutoSize = true, Location = new Point(16, y + 5) });
            _pnlHighlightPreview.Location    = new Point(134, y);
            _pnlHighlightPreview.Size        = new Size(36, 26);
            _pnlHighlightPreview.BackColor   = TryParseColor(_settings.HighlightColor, Theme.Teal);
            _pnlHighlightPreview.BorderStyle = BorderStyle.FixedSingle;
            pnl.Controls.Add(_pnlHighlightPreview);

            _btnHighlightColor.Text     = "Pick…";
            _btnHighlightColor.Size     = new Size(60, 26);
            _btnHighlightColor.Location = new Point(178, y);
            _btnHighlightColor.Click   += (_, _) =>
            {
                using var dlg = new ColorDialog { FullOpen = true, Color = _pnlHighlightPreview.BackColor };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _pnlHighlightPreview.BackColor = dlg.Color;
                    _settings.HighlightColor       = ColorTranslator.ToHtml(dlg.Color);
                }
            };
            pnl.Controls.Add(_btnHighlightColor);

            var btnResetHighlight = new Button { Text = "Reset", Size = new Size(55, 26), Location = new Point(246, y) };
            btnResetHighlight.Click += (_, _) => { _settings.HighlightColor = ""; _pnlHighlightPreview.BackColor = Color.FromArgb(32, 184, 204); };
            pnl.Controls.Add(btnResetHighlight);
            pnl.Controls.Add(new Label
            {
                Text = "Secondary highlight (grid headers, status indicators)", ForeColor = Theme.TextSecondary,
                Location = new Point(310, y + 5), AutoSize = true
            });

            return pnl;
        }

        private void LoadCurrencies()
        {
            dgvCurrencies.Rows.Clear();
            foreach (var kv in _settings.CurrencyRates)
            {
                int idx = dgvCurrencies.Rows.Add();
                dgvCurrencies.Rows[idx].Cells["colCode"].Value = kv.Key;
                dgvCurrencies.Rows[idx].Cells["colRate"].Value = kv.Value.ToString("G");
            }
        }

        private void RefreshPreview()
        {
            pbPreview.Image?.Dispose();
            pbPreview.Image = null;
            try { if (File.Exists(txtLogoPath.Text.Trim())) pbPreview.Image = Image.FromFile(txtLogoPath.Text.Trim()); }
            catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormSettings.RefreshPreview]: {ex.Message}"); }
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title  = "Select Logo File",
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp"
            };
            if (File.Exists(txtLogoPath.Text)) dlg.InitialDirectory = Path.GetDirectoryName(txtLogoPath.Text);
            if (dlg.ShowDialog(this) == DialogResult.OK) txtLogoPath.Text = dlg.FileName;
        }

        private void BtnAddCurrency_Click(object? sender, EventArgs e)
        {
            var code = txtNewCode.Text.Trim().ToUpper();
            if (string.IsNullOrWhiteSpace(code) || code.Length < 2)
            {
                MessageBox.Show(this, "Enter a valid currency code (e.g. USD).", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!decimal.TryParse(txtNewRate.Text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var rate) || rate <= 0)
            {
                MessageBox.Show(this, "Enter a valid positive exchange rate.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _settings.CurrencyRates[code] = rate;
            txtNewCode.Clear(); txtNewRate.Clear();
            LoadCurrencies();
        }

        private void BtnRemoveCurrency_Click(object? sender, EventArgs e)
        {
            if (dgvCurrencies.SelectedRows.Count == 0) return;
            var code = dgvCurrencies.SelectedRows[0].Cells["colCode"].Value?.ToString() ?? "";
            if (!string.IsNullOrEmpty(code)) { _settings.CurrencyRates.Remove(code); LoadCurrencies(); }
        }

        private void LoadOrderTypes()
        {
            lstOrderTypes.Items.Clear();
            foreach (var t in _settings.OrderTypes) lstOrderTypes.Items.Add(t);
        }

        private void BtnAddOrderType_Click(object? sender, EventArgs e)
        {
            var name = txtNewOrderType.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;
            if (name.Equals("Shopify", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "\"Shopify\" is a reserved system type.", "Reserved",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!_settings.OrderTypes.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                _settings.OrderTypes.Add(name);
                LoadOrderTypes();
            }
            txtNewOrderType.Clear();
        }

        private void BtnRemoveOrderType_Click(object? sender, EventArgs e)
        {
            if (lstOrderTypes.SelectedItem is not string selected) return;
            _settings.OrderTypes.Remove(selected);
            LoadOrderTypes();
        }

        private void LoadShippingMethods()
        {
            lstShippingMethods.Items.Clear();
            foreach (var m in _settings.ShippingMethods) lstShippingMethods.Items.Add(m);
        }

        private void BtnAddShippingMethod_Click(object? sender, EventArgs e)
        {
            var name = txtNewShippingMethod.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;
            if (!_settings.ShippingMethods.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                _settings.ShippingMethods.Add(name);
                LoadShippingMethods();
            }
            txtNewShippingMethod.Clear();
        }

        private void BtnRemoveShippingMethod_Click(object? sender, EventArgs e)
        {
            if (lstShippingMethods.SelectedItem is not string selected) return;
            _settings.ShippingMethods.Remove(selected);
            LoadShippingMethods();
        }

        // ── UOM helpers ───────────────────────────────────────────────────────────

        private void LoadUoms()
        {
            _dgvUom.Rows.Clear();
            try
            {
                var uoms = _uomRepo.GetAll(includeInactive: true);
                foreach (var u in uoms)
                {
                    int idx = _dgvUom.Rows.Add(
                        u.UOMID, u.Name, u.Abbreviation, u.BaseUnit ?? "",
                        u.ConversionFactor.ToString("G"), u.DisplayOrder, u.IsActive);
                }
            }
            catch (Exception ex)
            {
                // Non-fatal on first boot — table may not exist yet
                System.Diagnostics.Debug.WriteLine($"[FormSettings.LoadUoms] {ex.Message}");
            }
        }

        private void BtnDelUomRow_Click(object? sender, EventArgs e)
        {
            if (_dgvUom.SelectedRows.Count == 0) return;
            var row = _dgvUom.SelectedRows[0];
            if (!int.TryParse(row.Cells["colUomID"].Value?.ToString(), out int id) || id <= 0)
            {
                // Unsaved new row — just remove from grid
                _dgvUom.Rows.Remove(row);
                return;
            }
            if (MessageBox.Show(this, $"Delete '{row.Cells["colName"].Value}'?", "Confirm Delete",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                _uomRepo.Delete(id);
                _dgvUom.Rows.Remove(row);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Delete failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSaveUoms_Click(object? sender, EventArgs e)
        {
            int saved = 0, errors = 0;
            foreach (DataGridViewRow row in _dgvUom.Rows)
            {
                if (row.IsNewRow) continue;
                var name  = row.Cells["colName"].Value?.ToString()?.Trim() ?? "";
                var abbr  = row.Cells["colAbbr"].Value?.ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(abbr)) { errors++; continue; }

                int.TryParse(row.Cells["colUomID"].Value?.ToString(), out int id);
                decimal.TryParse(row.Cells["colFactor"].Value?.ToString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal factor);
                if (factor <= 0) factor = 1;
                int.TryParse(row.Cells["colOrder"].Value?.ToString(), out int order);
                bool active = row.Cells["colUomActive"].Value is true;
                string? baseUnit = row.Cells["colBase"].Value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(baseUnit)) baseUnit = abbr;

                var uom = new UnitOfMeasure
                {
                    UOMID            = id,
                    Name             = name,
                    Abbreviation     = abbr,
                    BaseUnit         = baseUnit,
                    ConversionFactor = factor,
                    DisplayOrder     = order,
                    IsActive         = active
                };
                try
                {
                    if (id == 0) _uomRepo.Add(uom);
                    else         _uomRepo.Update(uom);
                    saved++;
                }
                catch { errors++; }
            }

            if (errors > 0)
                MessageBox.Show(this, $"{saved} saved, {errors} skipped (missing name/abbreviation or DB error).", "UOM Save",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else
                MessageBox.Show(this, $"{saved} unit(s) of measure saved.", "UOM Save",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

            LoadUoms();
        }

        // ── Manufacturing settings helpers ────────────────────────────────────────

        private void LoadManufacturingSettings()
        {
            _nudLabourRate.Value = Math.Min(_nudLabourRate.Maximum, Math.Max(_nudLabourRate.Minimum, _settings.DefaultLabourRate));

            _dgvFlasks.Rows.Clear();
            foreach (var fc in _settings.FlaskConfigs.OrderBy(f => f.MaxBatchMl))
                _dgvFlasks.Rows.Add(fc.Name, fc.MaxBatchMl.ToString("G"));

            _dgvLossPresets.Rows.Clear();
            foreach (var p in _settings.BatchLossPresets)
                _dgvLossPresets.Rows.Add(p.Label, p.Percent.ToString("G"));
        }

        private void BtnSaveFlasks_Click(object? sender, EventArgs e)
        {
            var list = new List<FlaskConfig>();
            foreach (DataGridViewRow row in _dgvFlasks.Rows)
            {
                if (row.IsNewRow) continue;
                string name = row.Cells["colFlaskName"].Value?.ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!decimal.TryParse(row.Cells["colFlaskMaxMl"].Value?.ToString(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal maxMl) || maxMl <= 0)
                    continue;
                list.Add(new FlaskConfig { Name = name, MaxBatchMl = maxMl });
            }
            if (list.Count == 0)
            {
                MessageBox.Show(this, "Add at least one flask configuration.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _settings.FlaskConfigs = list.OrderBy(f => f.MaxBatchMl).ToList();
            _settings.Save();
            MessageBox.Show(this, "Flask configurations saved.", "Saved",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnSavePresets_Click(object? sender, EventArgs e)
        {
            var list = new List<BatchLossPreset>();
            foreach (DataGridViewRow row in _dgvLossPresets.Rows)
            {
                if (row.IsNewRow) continue;
                string label = row.Cells["colPresetLabel"].Value?.ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(label)) continue;
                if (!decimal.TryParse(row.Cells["colPresetPercent"].Value?.ToString(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal pct) || pct < 0)
                    continue;
                list.Add(new BatchLossPreset { Label = label, Percent = pct });
            }
            _settings.BatchLossPresets = list;
            _settings.Save();
            MessageBox.Show(this, "Batch loss presets saved.", "Saved",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            _settings.LogoPath      = txtLogoPath.Text.Trim();
            _settings.JanePhone     = txtJanePhone.Text.Trim();
            _settings.OpheliaPhone  = txtOpheliaPhone.Text.Trim();
            _settings.HomeCurrency  = cboHomeCurrency.SelectedItem?.ToString() ?? "CAD";
            _settings.DefaultCurrency = _settings.HomeCurrency;
            _settings.SmtpServer   = txtSmtpServer.Text.Trim();
            _settings.SmtpPort     = (int)nudSmtpPort.Value;
            _settings.SmtpUser     = txtSmtpUser.Text.Trim();
            _settings.SmtpPasswordPlain = txtSmtpPass.Text.Trim();
            _settings.FromEmail    = txtFromEmail.Text.Trim();
            _settings.MaxLoginAttempts     = (int)nudMaxAttempts.Value;
            _settings.LockoutMinutes       = (int)nudLockoutMins.Value;
            _settings.AdminPhone           = txtAdminPhone.Text.Trim();
            _settings.AdminEmail           = txtAdminEmail.Text.Trim();
            _settings.RememberLastUsername = chkRememberUser.Checked;
            _settings.BackupFolder        = _txtBackupFolder.Text.Trim();
            _settings.BackupSchedule      = _cboBackupSchedule.SelectedItem?.ToString() ?? "None";
            _settings.DefaultExportPath   = _txtDefaultExportPath.Text.Trim();
            _settings.DefaultLabourRate   = _nudLabourRate.Value;
            _settings.Save();

            // Reload the AppSettings singleton and re-apply theme colours to all open forms immediately
            var fresh = AppSettings.Load();
            Theme.ApplyCustomColors(fresh);
            foreach (Form f in Application.OpenForms)
            {
                try { f.Invalidate(true); }
                catch { /* best-effort */ }
            }

            MessageBox.Show(this, "Settings saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }

        private void BtnBackupNow_Click(object? sender, EventArgs e)
        {
            var folder = _txtBackupFolder.Text.Trim();
            if (string.IsNullOrWhiteSpace(folder))
            {
                MessageBox.Show(this, "Choose a backup folder first.", "Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Save current backup folder before running
            _settings.BackupFolder   = folder;
            _settings.BackupSchedule = _cboBackupSchedule.SelectedItem?.ToString() ?? "None";
            _settings.Save();

            Cursor = Cursors.WaitCursor;
            try
            {
                Services.BackupService.Backup(folder);
                _lblLastBackup.Text = $"Last backup: {DateTime.Now:yyyy-MM-dd HH:mm}";
                MessageBox.Show(this, $"Backup completed successfully.\n\nFolder: {folder}", "Backup Complete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    $"Backup failed:\n\n{ex.Message}\n\n" +
                    "Tip: Make sure the SQL Server service account has write access to the backup folder.\n" +
                    "For a local SQL Express install, try a folder under C:\\Users or C:\\Backups.",
                    "Backup Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private static Color TryParseColor(string? hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            try { return ColorTranslator.FromHtml(hex); }
            catch { return fallback; }
        }

        // ── Tax Rates page ────────────────────────────────────────────────────────

        private Panel BuildPageTaxRates()
        {
            var pnl = new Panel { AutoScroll = true };
            int y = 16;

            pnl.Controls.Add(new Label
            {
                Text = "Tax Rates", Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(16, y), AutoSize = true
            });
            y += 28;
            pnl.Controls.Add(new Label
            {
                Text = "Define reusable tax rates. Select one when creating a Purchase Order to auto-calculate tax.",
                ForeColor = Theme.TextSecondary, Location = new Point(16, y), AutoSize = true
            });
            y += 22;

            _dgvTaxRates.AutoGenerateColumns   = false;
            _dgvTaxRates.AllowUserToAddRows    = false;
            _dgvTaxRates.AllowUserToDeleteRows = false;
            _dgvTaxRates.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgvTaxRates.Location              = new Point(16, y);
            _dgvTaxRates.Size                  = new Size(450, 200);
            _dgvTaxRates.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colTaxID",     Visible = false });
            _dgvTaxRates.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colTaxName",   HeaderText = "Name",     Width = 180 });
            _dgvTaxRates.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colTaxRate",   HeaderText = "Rate (%)", Width = 100 });
            _dgvTaxRates.Columns.Add(new DataGridViewCheckBoxColumn { Name = "colTaxActive", HeaderText = "Active",   Width = 64 });
            pnl.Controls.Add(_dgvTaxRates);
            y += 210;

            var txtNewTaxName = new TextBox { Location = new Point(16, y), Size = new Size(160, 23), PlaceholderText = "e.g. PST BC" };
            var txtNewTaxRate = new TextBox { Location = new Point(184, y), Size = new Size(80, 23), PlaceholderText = "7" };
            pnl.Controls.Add(new Label { Text = "Name:", AutoSize = true, Location = new Point(16, y - 20), ForeColor = Theme.TextSecondary });
            pnl.Controls.Add(new Label { Text = "Rate %:", AutoSize = true, Location = new Point(184, y - 20), ForeColor = Theme.TextSecondary });
            pnl.Controls.Add(txtNewTaxName);
            pnl.Controls.Add(txtNewTaxRate);

            var btnAddTax = new Button { Text = "+ Add", Size = new Size(70, 23), Location = new Point(272, y) };
            btnAddTax.Click += (_, _) =>
            {
                var name = txtNewTaxName.Text.Trim();
                if (string.IsNullOrWhiteSpace(name)) return;
                if (!decimal.TryParse(txtNewTaxRate.Text.Replace("%", "").Trim(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal pct) || pct < 0)
                {
                    MessageBox.Show(this, "Enter a valid rate (e.g. 5 for 5%).", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
                }
                try
                {
                    _accountingRepo.AddTaxRate(name, pct / 100m);
                    txtNewTaxName.Clear(); txtNewTaxRate.Clear();
                    LoadTaxRates();
                }
                catch (Exception ex) { MessageBox.Show(this, "Save failed: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            pnl.Controls.Add(btnAddTax);
            y += 36;

            var btnToggleTax = new Button { Text = "Toggle Active", Size = new Size(120, 26), Location = new Point(16, y) };
            btnToggleTax.Click += (_, _) =>
            {
                if (_dgvTaxRates.SelectedRows.Count == 0) return;
                if (!int.TryParse(_dgvTaxRates.SelectedRows[0].Cells["colTaxID"].Value?.ToString(), out int id)) return;
                try { _accountingRepo.ToggleTaxRate(id); LoadTaxRates(); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            pnl.Controls.Add(btnToggleTax);

            // Defer loading because DB table might not exist yet on first launch
            pnl.VisibleChanged += (_, _) => { if (pnl.Visible) LoadTaxRates(); };

            return pnl;
        }

        private void LoadTaxRates()
        {
            _dgvTaxRates.Rows.Clear();
            try
            {
                foreach (var r in _accountingRepo.GetAllTaxRates())
                    _dgvTaxRates.Rows.Add(r.TaxRateID, r.Name, (r.Rate * 100m).ToString("G"), r.IsActive);
            }
            catch { /* table may not exist yet */ }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            pbPreview.Image?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
