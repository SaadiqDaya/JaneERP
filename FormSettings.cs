namespace JaneERP
{
    /// <summary>Admin-accessible settings form for customising the logo, phone numbers, and currencies.</summary>
    public class FormSettings : Form
    {
        private readonly AppSettings _settings;
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

        public FormSettings()
        {
            _settings = AppSettings.Load();
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            pbPreview.BackColor = Color.White;
            RefreshPreview();
            LoadCurrencies();
            LoadOrderTypes();
            LoadShippingMethods();
        }

        private void BuildUI()
        {
            Text          = "Settings";
            ClientSize    = new Size(600, 560);
            MinimumSize   = new Size(560, 480);
            StartPosition = FormStartPosition.CenterParent;

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
            Controls.Add(pnlBottom);
            Load += (_, _) => PositionBottomButtons();

            // ── Tab control ───────────────────────────────────────────────────────
            var tabs = new TabControl { Dock = DockStyle.Fill };
            Controls.Add(tabs);

            // ──────────────────────────────────────────────────────────────────────
            // TAB 1: Company
            // ──────────────────────────────────────────────────────────────────────
            var tabCompany = new TabPage("Company") { Padding = new Padding(12) };
            tabs.TabPages.Add(tabCompany);

            int y = 12;
            tabCompany.Controls.Add(new Label { Text = "Company Logo (PNG or JPG):", Location = new Point(12, y), AutoSize = true });
            y += 22;

            txtLogoPath.Location     = new Point(12, y);
            txtLogoPath.Size         = new Size(400, 23);
            txtLogoPath.Text         = _settings.LogoPath;
            txtLogoPath.TextChanged += (_, _) => RefreshPreview();
            tabCompany.Controls.Add(txtLogoPath);

            btnBrowse.Location = new Point(420, y - 1);
            btnBrowse.Size     = new Size(80, 25);
            btnBrowse.Text     = "Browse…";
            btnBrowse.Click   += BtnBrowse_Click;
            tabCompany.Controls.Add(btnBrowse);
            y += 30;

            pbPreview.Location    = new Point(12, y);
            pbPreview.Size        = new Size(200, 60);
            pbPreview.SizeMode    = PictureBoxSizeMode.Zoom;
            pbPreview.BackColor   = Color.White;
            pbPreview.BorderStyle = BorderStyle.FixedSingle;
            tabCompany.Controls.Add(pbPreview);
            y += 76;

            tabCompany.Controls.Add(new Label
            {
                Text = "Phone Numbers", Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(12, y), AutoSize = true
            });
            y += 28;

            tabCompany.Controls.Add(new Label { Text = "Jane:", Location = new Point(12, y + 3), AutoSize = true });
            txtJanePhone.Location        = new Point(100, y);
            txtJanePhone.Size            = new Size(200, 23);
            txtJanePhone.Text            = _settings.JanePhone;
            txtJanePhone.PlaceholderText = "e.g. 6042274507";
            tabCompany.Controls.Add(txtJanePhone);
            y += 30;

            tabCompany.Controls.Add(new Label { Text = "Ophelia:", Location = new Point(12, y + 3), AutoSize = true });
            txtOpheliaPhone.Location = new Point(100, y);
            txtOpheliaPhone.Size     = new Size(200, 23);
            txtOpheliaPhone.Text     = _settings.OpheliaPhone;
            tabCompany.Controls.Add(txtOpheliaPhone);

            // ──────────────────────────────────────────────────────────────────────
            // TAB 2: Currencies
            // ──────────────────────────────────────────────────────────────────────
            var tabCurrencies = new TabPage("Currencies") { Padding = new Padding(12) };
            tabs.TabPages.Add(tabCurrencies);
            y = 12;

            tabCurrencies.Controls.Add(new Label { Text = "Home Currency:", Location = new Point(12, y + 3), AutoSize = true });
            cboHomeCurrency.Location      = new Point(130, y);
            cboHomeCurrency.Size          = new Size(100, 23);
            cboHomeCurrency.DropDownStyle = ComboBoxStyle.DropDownList;
            cboHomeCurrency.Items.Add(_settings.HomeCurrency);
            foreach (var k in _settings.CurrencyRates.Keys) cboHomeCurrency.Items.Add(k);
            cboHomeCurrency.SelectedItem = _settings.HomeCurrency;
            tabCurrencies.Controls.Add(cboHomeCurrency);
            y += 36;

            tabCurrencies.Controls.Add(new Label
            {
                Text = "Exchange Rates  (units of home currency per 1 foreign unit)",
                Location = new Point(12, y), AutoSize = true, ForeColor = Theme.TextSecondary
            });
            y += 22;

            dgvCurrencies.AutoGenerateColumns = false;
            dgvCurrencies.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCode", HeaderText = "Code", Width = 80, ReadOnly = true });
            dgvCurrencies.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRate", HeaderText = "Rate to Home", Width = 140 });
            dgvCurrencies.AllowUserToAddRows    = false;
            dgvCurrencies.AllowUserToDeleteRows = false;
            dgvCurrencies.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvCurrencies.Location = new Point(12, y);
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
            tabCurrencies.Controls.Add(dgvCurrencies);

            txtNewCode.Location        = new Point(320, y);
            txtNewCode.Size            = new Size(60, 23);
            txtNewCode.PlaceholderText = "USD";
            txtNewCode.CharacterCasing = CharacterCasing.Upper;
            tabCurrencies.Controls.Add(txtNewCode);

            txtNewRate.Location        = new Point(388, y);
            txtNewRate.Size            = new Size(80, 23);
            txtNewRate.PlaceholderText = "1.45";
            tabCurrencies.Controls.Add(txtNewRate);

            btnAddCurrency.Text     = "+ Add";
            btnAddCurrency.Size     = new Size(70, 23);
            btnAddCurrency.Location = new Point(476, y);
            btnAddCurrency.Click   += BtnAddCurrency_Click;
            tabCurrencies.Controls.Add(btnAddCurrency);
            y += 30;

            btnRemoveCurrency.Text     = "Remove Selected";
            btnRemoveCurrency.Size     = new Size(130, 23);
            btnRemoveCurrency.Location = new Point(320, y);
            btnRemoveCurrency.Click   += BtnRemoveCurrency_Click;
            tabCurrencies.Controls.Add(btnRemoveCurrency);

            // ──────────────────────────────────────────────────────────────────────
            // TAB 3: Order Types
            // ──────────────────────────────────────────────────────────────────────
            var tabOrders = new TabPage("Order Types") { Padding = new Padding(12) };
            tabs.TabPages.Add(tabOrders);
            y = 12;

            tabOrders.Controls.Add(new Label
            {
                Text      = "Order Types  (used when creating manual orders)",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(12, y), AutoSize = true
            });
            y += 28;

            tabOrders.Controls.Add(new Label
            {
                Text      = "\"Shopify\" is a reserved system type and cannot be removed.",
                ForeColor = Theme.TextSecondary, Location = new Point(12, y), AutoSize = true
            });
            y += 24;

            lstOrderTypes.Location      = new Point(12, y);
            lstOrderTypes.Size          = new Size(200, 140);
            lstOrderTypes.SelectionMode = SelectionMode.One;
            tabOrders.Controls.Add(lstOrderTypes);

            txtNewOrderType.Location        = new Point(224, y);
            txtNewOrderType.Size            = new Size(140, 23);
            txtNewOrderType.PlaceholderText = "e.g. Phone";
            tabOrders.Controls.Add(txtNewOrderType);

            btnAddOrderType.Text     = "+ Add";
            btnAddOrderType.Size     = new Size(70, 23);
            btnAddOrderType.Location = new Point(372, y);
            btnAddOrderType.Click   += BtnAddOrderType_Click;
            tabOrders.Controls.Add(btnAddOrderType);
            y += 30;

            btnRemoveOrderType.Text     = "Remove Selected";
            btnRemoveOrderType.Size     = new Size(130, 23);
            btnRemoveOrderType.Location = new Point(224, y);
            btnRemoveOrderType.Click   += BtnRemoveOrderType_Click;
            tabOrders.Controls.Add(btnRemoveOrderType);

            // ──────────────────────────────────────────────────────────────────────
            // TAB 4: Fulfillment
            // ──────────────────────────────────────────────────────────────────────
            var tabFulfillment = new TabPage("Fulfillment") { Padding = new Padding(12) };
            tabs.TabPages.Add(tabFulfillment);
            y = 12;

            tabFulfillment.Controls.Add(new Label
            {
                Text      = "Shipping Methods  (shown in the picking and quick-fulfil screens)",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(12, y), AutoSize = true
            });
            y += 28;

            tabFulfillment.Controls.Add(new Label
            {
                Text      = "\"Local Pickup\" and other non-carrier methods are valid entries.",
                ForeColor = Theme.TextSecondary, Location = new Point(12, y), AutoSize = true
            });
            y += 24;

            lstShippingMethods.Location      = new Point(12, y);
            lstShippingMethods.Size          = new Size(200, 140);
            lstShippingMethods.SelectionMode = SelectionMode.One;
            tabFulfillment.Controls.Add(lstShippingMethods);

            txtNewShippingMethod.Location        = new Point(224, y);
            txtNewShippingMethod.Size            = new Size(160, 23);
            txtNewShippingMethod.PlaceholderText = "e.g. Same Day";
            tabFulfillment.Controls.Add(txtNewShippingMethod);

            btnAddShippingMethod.Text     = "+ Add";
            btnAddShippingMethod.Size     = new Size(70, 23);
            btnAddShippingMethod.Location = new Point(392, y);
            btnAddShippingMethod.Click   += BtnAddShippingMethod_Click;
            tabFulfillment.Controls.Add(btnAddShippingMethod);
            y += 30;

            btnRemoveShippingMethod.Text     = "Remove Selected";
            btnRemoveShippingMethod.Size     = new Size(130, 23);
            btnRemoveShippingMethod.Location = new Point(224, y);
            btnRemoveShippingMethod.Click   += BtnRemoveShippingMethod_Click;
            tabFulfillment.Controls.Add(btnRemoveShippingMethod);

            // ──────────────────────────────────────────────────────────────────────
            // TAB 5: Notifications (SMTP)
            // ──────────────────────────────────────────────────────────────────────
            var tabEmail = new TabPage("Notifications") { Padding = new Padding(12) };
            tabs.TabPages.Add(tabEmail);
            y = 12;

            tabEmail.Controls.Add(new Label
            {
                Text      = "Email / SMTP  (for @mention notifications)",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(12, y), AutoSize = true
            });
            y += 32;

            tabEmail.Controls.Add(new Label { Text = "SMTP Server:", Location = new Point(12, y + 3), AutoSize = true });
            txtSmtpServer.Location        = new Point(130, y);
            txtSmtpServer.Size            = new Size(200, 23);
            txtSmtpServer.PlaceholderText = "smtp.gmail.com";
            txtSmtpServer.Text            = _settings.SmtpServer;
            tabEmail.Controls.Add(txtSmtpServer);

            tabEmail.Controls.Add(new Label { Text = "Port:", Location = new Point(344, y + 3), AutoSize = true });
            nudSmtpPort.Location = new Point(380, y);
            nudSmtpPort.Size     = new Size(70, 23);
            nudSmtpPort.Minimum  = 1;
            nudSmtpPort.Maximum  = 65535;
            nudSmtpPort.Value    = _settings.SmtpPort;
            tabEmail.Controls.Add(nudSmtpPort);
            y += 32;

            tabEmail.Controls.Add(new Label { Text = "Username:", Location = new Point(12, y + 3), AutoSize = true });
            txtSmtpUser.Location        = new Point(130, y);
            txtSmtpUser.Size            = new Size(200, 23);
            txtSmtpUser.PlaceholderText = "user@gmail.com";
            txtSmtpUser.Text            = _settings.SmtpUser;
            tabEmail.Controls.Add(txtSmtpUser);
            y += 32;

            tabEmail.Controls.Add(new Label { Text = "Password:", Location = new Point(12, y + 3), AutoSize = true });
            txtSmtpPass.Location              = new Point(130, y);
            txtSmtpPass.Size                  = new Size(200, 23);
            txtSmtpPass.UseSystemPasswordChar = true;
            txtSmtpPass.PlaceholderText       = "app password";
            txtSmtpPass.Text                  = _settings.SmtpPasswordPlain;
            tabEmail.Controls.Add(txtSmtpPass);
            y += 32;

            tabEmail.Controls.Add(new Label { Text = "From Email:", Location = new Point(12, y + 3), AutoSize = true });
            txtFromEmail.Location        = new Point(130, y);
            txtFromEmail.Size            = new Size(200, 23);
            txtFromEmail.PlaceholderText = "noreply@company.com";
            txtFromEmail.Text            = _settings.FromEmail;
            tabEmail.Controls.Add(txtFromEmail);

            // ──────────────────────────────────────────────────────────────────────
            // TAB 5: Security
            // ──────────────────────────────────────────────────────────────────────
            var tabSecurity = new TabPage("Security") { Padding = new Padding(12) };
            tabs.TabPages.Add(tabSecurity);
            y = 12;

            tabSecurity.Controls.Add(new Label
            {
                Text = "Login & Lockout Policy", Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(12, y), AutoSize = true
            });
            y += 28;

            tabSecurity.Controls.Add(new Label { Text = "Max failed attempts before lockout:", AutoSize = true, Location = new Point(12, y + 3) });
            nudMaxAttempts.Location = new Point(260, y);
            nudMaxAttempts.Size     = new Size(70, 23);
            nudMaxAttempts.Minimum  = 1;
            nudMaxAttempts.Maximum  = 20;
            nudMaxAttempts.Value    = _settings.MaxLoginAttempts > 0 ? _settings.MaxLoginAttempts : 5;
            tabSecurity.Controls.Add(nudMaxAttempts);
            y += 32;

            tabSecurity.Controls.Add(new Label { Text = "Lockout duration (minutes):", AutoSize = true, Location = new Point(12, y + 3) });
            nudLockoutMins.Location = new Point(260, y);
            nudLockoutMins.Size     = new Size(70, 23);
            nudLockoutMins.Minimum  = 1;
            nudLockoutMins.Maximum  = 1440;
            nudLockoutMins.Value    = _settings.LockoutMinutes > 0 ? _settings.LockoutMinutes : 15;
            tabSecurity.Controls.Add(nudLockoutMins);
            y += 36;

            tabSecurity.Controls.Add(new Label
            {
                Text = "Admin Contact  (shown in lockout messages)", Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(12, y), AutoSize = true
            });
            y += 28;

            tabSecurity.Controls.Add(new Label { Text = "Admin Phone:", AutoSize = true, Location = new Point(12, y + 3) });
            txtAdminPhone.Location        = new Point(130, y);
            txtAdminPhone.Size            = new Size(200, 23);
            txtAdminPhone.PlaceholderText = "e.g. 604-555-0100";
            txtAdminPhone.Text            = _settings.AdminPhone;
            tabSecurity.Controls.Add(txtAdminPhone);
            y += 32;

            tabSecurity.Controls.Add(new Label { Text = "Admin Email:", AutoSize = true, Location = new Point(12, y + 3) });
            txtAdminEmail.Location        = new Point(130, y);
            txtAdminEmail.Size            = new Size(200, 23);
            txtAdminEmail.PlaceholderText = "admin@company.com";
            txtAdminEmail.Text            = _settings.AdminEmail;
            tabSecurity.Controls.Add(txtAdminEmail);
            y += 36;

            tabSecurity.Controls.Add(new Label
            {
                Text = "Login Options", Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold, Location = new Point(12, y), AutoSize = true
            });
            y += 28;

            chkRememberUser.Text     = "Remember last username on login screen";
            chkRememberUser.AutoSize = true;
            chkRememberUser.Checked  = _settings.RememberLastUsername;
            chkRememberUser.Location = new Point(12, y);
            tabSecurity.Controls.Add(chkRememberUser);

            // ──────────────────────────────────────────────────────────────────────
            // TAB 6: System
            // ──────────────────────────────────────────────────────────────────────
            var tabSystem = new TabPage("System") { Padding = new Padding(12) };
            tabs.TabPages.Add(tabSystem);
            y = 12;

            var grpDiscountTiers = new GroupBox
            {
                Text      = "Discount Tiers",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, y),
                Size      = new Size(530, 90)
            };
            grpDiscountTiers.Controls.Add(new Label
            {
                Text      = "Assign discount tiers to customers for automatic order discounts.",
                ForeColor = Theme.TextSecondary,
                Location  = new Point(10, 24),
                Size      = new Size(400, 18)
            });
            var btnDiscountTiers = new Button
            {
                Text = "Manage Discount Tiers →", Location = new Point(10, 50), Size = new Size(200, 28)
            };
            btnDiscountTiers.Click += (_, _) => { using var frm = new FormDiscountTiers(); frm.ShowDialog(this); };
            grpDiscountTiers.Controls.Add(btnDiscountTiers);

            var btnCustomerTiers = new Button
            {
                Text = "Assign Tiers to Customers →", Location = new Point(218, 50), Size = new Size(220, 28)
            };
            btnCustomerTiers.Click += (_, _) => { using var frm = new FormCustomerTiers(); frm.ShowDialog(this); };
            grpDiscountTiers.Controls.Add(btnCustomerTiers);

            tabSystem.Controls.Add(grpDiscountTiers);
            y += 104;

            var grpProductSearch = new GroupBox
            {
                Text      = "Product Explorer",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, y),
                Size      = new Size(530, 90)
            };
            grpProductSearch.Controls.Add(new Label
            {
                Text      = "Set up attribute filter buttons for the Product Explorer screen.",
                ForeColor = Theme.TextSecondary,
                Location  = new Point(10, 24),
                Size      = new Size(400, 18)
            });
            var btnConfigureSearch = new Button
            {
                Text = "Configure Search Filters →", Location = new Point(10, 50), Size = new Size(200, 28)
            };
            btnConfigureSearch.Click += (_, _) => { using var frm = new FormProductSearch(); frm.ShowDialog(this); };
            grpProductSearch.Controls.Add(btnConfigureSearch);
            tabSystem.Controls.Add(grpProductSearch);
            y += 104;

            var grpExports = new GroupBox
            {
                Text      = "Exports",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, y),
                Size      = new Size(530, 90)
            };
            grpExports.Controls.Add(new Label
            {
                Text      = "Default folder for CSV exports. Leave blank to always ask.",
                ForeColor = Theme.TextSecondary,
                Location  = new Point(10, 24),
                Size      = new Size(400, 18)
            });
            _txtDefaultExportPath.Location        = new Point(10, 50);
            _txtDefaultExportPath.Size            = new Size(380, 23);
            _txtDefaultExportPath.PlaceholderText = "e.g. C:\\Exports";
            _txtDefaultExportPath.Text            = _settings.DefaultExportPath;
            grpExports.Controls.Add(_txtDefaultExportPath);
            var btnBrowseExport = new Button { Text = "Browse…", Location = new Point(398, 49), Size = new Size(80, 25) };
            btnBrowseExport.Click += (_, _) =>
            {
                using var dlg = new FolderBrowserDialog { Description = "Select default export folder" };
                if (!string.IsNullOrEmpty(_txtDefaultExportPath.Text))
                    dlg.SelectedPath = _txtDefaultExportPath.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    _txtDefaultExportPath.Text = dlg.SelectedPath;
            };
            grpExports.Controls.Add(btnBrowseExport);
            tabSystem.Controls.Add(grpExports);

            // ──────────────────────────────────────────────────────────────────────
            // TAB 7: Backup
            // ──────────────────────────────────────────────────────────────────────
            var tabBackup = new TabPage("Backup") { Padding = new Padding(12) };
            tabs.TabPages.Add(tabBackup);
            y = 12;

            tabBackup.Controls.Add(new Label
            {
                Text      = "Database Backup",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, y),
                AutoSize  = true
            });
            y += 30;

            tabBackup.Controls.Add(new Label
            {
                Text      = "Backup Folder:",
                AutoSize  = true,
                Location  = new Point(12, y + 3)
            });
            _txtBackupFolder           = new TextBox();
            _txtBackupFolder.Location  = new Point(130, y);
            _txtBackupFolder.Size      = new Size(320, 23);
            _txtBackupFolder.Text      = _settings.BackupFolder;
            tabBackup.Controls.Add(_txtBackupFolder);

            var btnBrowseBackup = new Button { Text = "Browse…", Location = new Point(458, y - 1), Size = new Size(80, 25) };
            btnBrowseBackup.Click += (_, _) =>
            {
                using var dlg = new FolderBrowserDialog { Description = "Select backup folder" };
                if (!string.IsNullOrEmpty(_txtBackupFolder.Text))
                    dlg.SelectedPath = _txtBackupFolder.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    _txtBackupFolder.Text = dlg.SelectedPath;
            };
            tabBackup.Controls.Add(btnBrowseBackup);
            y += 36;

            tabBackup.Controls.Add(new Label { Text = "Auto Backup:", AutoSize = true, Location = new Point(12, y + 3) });
            _cboBackupSchedule = new ComboBox
            {
                Location      = new Point(130, y),
                Size          = new Size(120, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cboBackupSchedule.Items.AddRange(new object[] { "None", "Daily", "Weekly" });
            _cboBackupSchedule.SelectedItem = _settings.BackupSchedule ?? "None";
            tabBackup.Controls.Add(_cboBackupSchedule);
            y += 36;

            // Last backup label
            _lblLastBackup = new Label
            {
                AutoSize  = true,
                Location  = new Point(12, y),
                ForeColor = Theme.TextSecondary,
                Text      = _settings.LastBackupAt.HasValue
                    ? $"Last backup: {_settings.LastBackupAt.Value.ToLocalTime():yyyy-MM-dd HH:mm}"
                    : "Last backup: never"
            };
            tabBackup.Controls.Add(_lblLastBackup);
            y += 30;

            var btnBackupNow = new Button { Text = "Backup Now", Location = new Point(12, y), Size = new Size(130, 32) };
            btnBackupNow.Click += BtnBackupNow_Click;
            tabBackup.Controls.Add(btnBackupNow);

            tabBackup.Controls.Add(new Label
            {
                Text      = "Note: The backup folder must be accessible by the SQL Server service account\n" +
                             "(usually MSSQLSERVER or SQLServerMSSQLUser). For localhost, any local folder works.",
                ForeColor = Theme.TextSecondary,
                Location  = new Point(12, y + 42),
                Size      = new Size(520, 40)
            });

            // ──────────────────────────────────────────────────────────────────────
            // TAB 8: Appearance
            // ──────────────────────────────────────────────────────────────────────
            var tabAppearance = new TabPage("Appearance") { Padding = new Padding(12) };
            tabs.TabPages.Add(tabAppearance);
            y = 12;

            tabAppearance.Controls.Add(new Label
            {
                Text      = "Colour Scheme",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, y),
                AutoSize  = true
            });
            y += 30;

            y += 4;

            // Primary accent
            tabAppearance.Controls.Add(new Label { Text = "Primary Accent:", AutoSize = true, Location = new Point(12, y + 5) });
            _pnlAccentPreview.Location    = new Point(130, y);
            _pnlAccentPreview.Size        = new Size(36, 26);
            _pnlAccentPreview.BackColor   = TryParseColor(_settings.AccentColor, Theme.Gold);
            _pnlAccentPreview.BorderStyle = BorderStyle.FixedSingle;
            tabAppearance.Controls.Add(_pnlAccentPreview);

            _btnAccentColor.Text     = "Pick…";
            _btnAccentColor.Size     = new Size(60, 26);
            _btnAccentColor.Location = new Point(174, y);
            _btnAccentColor.Click   += (_, _) =>
            {
                using var dlg = new ColorDialog { FullOpen = true, Color = _pnlAccentPreview.BackColor };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _pnlAccentPreview.BackColor = dlg.Color;
                    _settings.AccentColor       = ColorTranslator.ToHtml(dlg.Color);
                }
            };
            tabAppearance.Controls.Add(_btnAccentColor);

            var btnResetAccent = new Button { Text = "Reset", Size = new Size(55, 26), Location = new Point(242, y) };
            btnResetAccent.Click += (_, _) =>
            {
                _settings.AccentColor       = "";
                _pnlAccentPreview.BackColor = Color.FromArgb(155, 55, 220);
            };
            tabAppearance.Controls.Add(btnResetAccent);
            tabAppearance.Controls.Add(new Label
            {
                Text      = "Main accent (buttons, borders, tile glow)",
                ForeColor = Theme.TextSecondary,
                Location  = new Point(306, y + 5),
                AutoSize  = true
            });
            y += 44;

            // Secondary highlight
            tabAppearance.Controls.Add(new Label { Text = "Highlight:", AutoSize = true, Location = new Point(12, y + 5) });
            _pnlHighlightPreview.Location    = new Point(130, y);
            _pnlHighlightPreview.Size        = new Size(36, 26);
            _pnlHighlightPreview.BackColor   = TryParseColor(_settings.HighlightColor, Theme.Teal);
            _pnlHighlightPreview.BorderStyle = BorderStyle.FixedSingle;
            tabAppearance.Controls.Add(_pnlHighlightPreview);

            _btnHighlightColor.Text     = "Pick…";
            _btnHighlightColor.Size     = new Size(60, 26);
            _btnHighlightColor.Location = new Point(174, y);
            _btnHighlightColor.Click   += (_, _) =>
            {
                using var dlg = new ColorDialog { FullOpen = true, Color = _pnlHighlightPreview.BackColor };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _pnlHighlightPreview.BackColor = dlg.Color;
                    _settings.HighlightColor       = ColorTranslator.ToHtml(dlg.Color);
                }
            };
            tabAppearance.Controls.Add(_btnHighlightColor);

            var btnResetHighlight = new Button { Text = "Reset", Size = new Size(55, 26), Location = new Point(242, y) };
            btnResetHighlight.Click += (_, _) =>
            {
                _settings.HighlightColor       = "";
                _pnlHighlightPreview.BackColor = Color.FromArgb(32, 184, 204);
            };
            tabAppearance.Controls.Add(btnResetHighlight);
            tabAppearance.Controls.Add(new Label
            {
                Text      = "Secondary highlight (grid headers, status indicators)",
                ForeColor = Theme.TextSecondary,
                Location  = new Point(306, y + 5),
                AutoSize  = true
            });
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            pbPreview.Image?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
