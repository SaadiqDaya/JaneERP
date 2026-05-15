namespace JaneERP
{
    partial class FormMainMenu
    {
        private System.ComponentModel.IContainer components = null;

        // ── Header ────────────────────────────────────────────────────────────
        private Panel      pnlHeader;
        private PictureBox pbLogo;
        private Label      lblAppName;
        private Label      lblWelcome;
        private Button     btnLogout;
        private Button     btnSettings;

        // ── Sidebar ───────────────────────────────────────────────────────────
        private Panel pnlSidebar;

        // ── Content grid ──────────────────────────────────────────────────────
        private FlowLayoutPanel pnlGrid;

        // ── Section header refs (for sidebar scroll) ──────────────────────────
        private Panel _hdrSales, _hdrProducts, _hdrMfg, _hdrAnalytics, _hdrData, _hdrAdmin;

        // ── Button fields (kept for handler compatibility) ─────────────────────
        private Button btnInventory;
        private Button btnParts;
        private Button btnBOM;
        private Button btnSales;
        private Button btnPickingDash;
        private Button btnPackingDash;
        private Button btnPurchaseOrders;
        private Button btnManufacturing;
        private Button btnWorkOrders;
        private Button btnBatchCooking;
        private Button btnProductSearch;
        private Button btnTaskManager;
        private Button btnCycleCount;
        private Button btnLocations;
        private Button btnProductTypes;
        private Button btnAttributeLists;
        private Button btnManageUsers;
        private Button btnLoginLog;
        private Button btnActivityLog;
        private Button btnAppLogs;
        private Button btnDashboard;
        private Button btnReports;
        private Button btnReorderReport;
        private Button btnInventoryDash;
        private Button btnUnverified;
        private Button btnExport;
        private Button btnImports;
        private Button btnBreakeven;
        private Button btnAccounting;
        private Button btnJane;
        private Button btnOphelia;
        private Button btnShopifyStores;
        private Button btnCustomers;
        private Button btnVendors;
        private Button btnExitApp;
        private Button btnExpiryTracker;
        private Button btnBackorders;
        private Button btnReturnsManager;
        private Button btnReturnsReport;
        private Button btnInventoryMoveImport;
        private Button btnPackageExplorer;

        // ── Misc ──────────────────────────────────────────────────────────────
        private Label   lblSelectDept;
        private ToolTip toolTip1;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components    = new System.ComponentModel.Container();
            toolTip1      = new ToolTip(components);
            pnlHeader     = new Panel();
            pbLogo        = new PictureBox();
            lblAppName    = new Label();
            lblWelcome    = new Label();
            btnSettings   = new Button();
            btnLogout     = new Button();
            pnlSidebar    = new Panel();
            pnlGrid       = new FlowLayoutPanel();
            lblSelectDept = new Label { Visible = false };
            btnExitApp    = new Button { Visible = false, Size = new Size(1, 1) };
            btnExitApp.Click += (_, _) => Application.Exit();

            // ── Build icon buttons ─────────────────────────────────────────────
            btnInventory      = MakeIconButton("\U0001F4E6", "Stock Browser",      btnInventory_Click);
            btnParts          = MakeIconButton("\U0001F527", "Parts",              btnParts_Click);
            btnBOM            = MakeIconButton("\U0001F4CB", "BOM",                btnBOM_Click);
            btnSales          = MakeIconButton("\U0001F6D2", "Sales",              btnSales_Click);
            btnPickingDash    = MakeIconButton("\U0001F4CB", "Picking",            btnPickingDash_Click);
            btnPackingDash    = MakeIconButton("\U0001F4E6", "Packing",            btnPackingDash_Click);
            btnPurchaseOrders = MakeIconButton("\U0001F69B", "Purchase",           btnPurchaseOrders_Click);
            btnManufacturing  = MakeIconButton("\U0001F3ED", "Mfg",               btnManufacturing_Click);
            btnWorkOrders     = MakeIconButton("\U0001F6E0", "Work Orders",        btnWorkOrders_Click);
            btnBatchCooking   = MakeIconButton("\U0001F9EA", "Batch Cooking",      btnBatchCooking_Click);
            btnProductSearch  = MakeIconButton("\U0001F50D", "Product Explorer",   btnProductSearch_Click);
            btnTaskManager    = MakeIconButton("\u2705",     "Tasks",              btnTaskManager_Click);
            btnCycleCount     = MakeIconButton("\U0001F504", "Cycle Count",        btnCycleCount_Click);
            btnLocations      = MakeIconButton("\U0001F4CD", "Locations",          btnLocations_Click);
            btnProductTypes   = MakeIconButton("\U0001F3F7", "Types",              btnProductTypes_Click);
            btnAttributeLists = MakeIconButton("\U0001F4CB", "Attr Lists",         btnAttributeLists_Click);
            btnManageUsers    = MakeIconButton("\U0001F464", "Users",              btnManageUsers_Click);
            btnLoginLog       = MakeIconButton("\U0001F4DD", "Login Log",          btnLoginLog_Click);
            btnActivityLog    = MakeIconButton("\U0001F4DC", "Audit Log",          btnActivityLog_Click);
            btnAppLogs        = MakeIconButton("\U0001F5C2", "App Logs",           btnAppLogs_Click);
            btnDashboard      = MakeIconButton("\U0001F4C9", "KPI",                btnDashboard_Click);
            btnReports        = MakeIconButton("\U0001F4C8", "Reports",            btnReports_Click);
            btnReorderReport  = MakeIconButton("\u26A0",     "Reorder",            btnReorderReport_Click);
            btnInventoryDash  = MakeIconButton("\U0001F4CA", "Inv. Snapshot",      btnInventoryDash_Click);
            btnUnverified     = MakeIconButton("\u26A0",     "Unverified",         btnUnverified_Click);
            btnExport         = MakeIconButton("\U0001F4BE", "Exports",            btnExport_Click);
            btnImports        = MakeIconButton("\U0001F4E5", "Imports",            btnImports_Click);
            btnBreakeven      = MakeIconButton("\u2696",     "Breakeven",          btnBreakeven_Click);
            btnAccounting     = MakeIconButton("\U0001F4B0", "Accounting",         btnAccounting_Click);
            btnShopifyStores  = MakeIconButton("\U0001F6D2", "Shopify Stores",     btnShopifyStores_Click);
            btnCustomers      = MakeIconButton("\U0001F465", "Customers",          btnCustomers_Click);
            btnVendors        = MakeIconButton("\U0001F3ED", "Vendors",            btnVendors_Click);
            btnExpiryTracker  = MakeIconButton("\u23F0",     "Expiry Tracker",     btnExpiryTracker_Click);
            btnBackorders     = MakeIconButton("\U0001F4E6", "Backorders",         btnBackorders_Click);
            btnReturnsManager = MakeIconButton("\U0001F504", "Returns",            btnReturnsManager_Click);
            btnReturnsReport       = MakeIconButton("\U0001F4CB", "Returns Report",     btnReturnsReport_Click);
            btnInventoryMoveImport = MakeIconButton("\U0001F69A", "Move Import",         btnInventoryMoveImport_Click);
            btnPackageExplorer     = MakeIconButton("\U0001F381", "Packages",             btnPackageExplorer_Click);

            // Quick-dial buttons in header — always on dark background
            btnJane    = new Button { Text = "\U0001F4DE Jane",    UseVisualStyleBackColor = false };
            btnOphelia = new Button { Text = "\U0001F4F1 Ophelia", UseVisualStyleBackColor = false };
            btnJane.Click   += btnJane_Click;
            btnOphelia.Click += btnOphelia_Click;

            // ── Tooltips ──────────────────────────────────────────────────────
            toolTip1.SetToolTip(btnInventory,      "Stock Browser — browse and manage all products and stock levels");
            toolTip1.SetToolTip(btnParts,          "Manage raw material parts and BOM components");
            toolTip1.SetToolTip(btnBOM,            "Edit Bill of Materials for products");
            toolTip1.SetToolTip(btnSales,          "View and manage Shopify & manual sales orders");
            toolTip1.SetToolTip(btnPickingDash,    "Pick inventory for Live orders — mark items collected");
            toolTip1.SetToolTip(btnPackingDash,    "Pack and ship orders — enter tracking and mark complete");
            toolTip1.SetToolTip(btnPurchaseOrders, "Create and manage supplier purchase orders");
            toolTip1.SetToolTip(btnManufacturing,  "Work orders and production management");
            toolTip1.SetToolTip(btnWorkOrders,     "Process open work orders directly");
            toolTip1.SetToolTip(btnBatchCooking,   "Batch cooking — ingredient-first session tracking for juice production");
            toolTip1.SetToolTip(btnTaskManager,    "Create tasks, assign to team members");
            toolTip1.SetToolTip(btnCycleCount,     "Schedule and record stock cycle counts");
            toolTip1.SetToolTip(btnLocations,      "Manage warehouse storage locations");
            toolTip1.SetToolTip(btnProductTypes,   "Define product categories and required attributes");
            toolTip1.SetToolTip(btnAttributeLists, "Define allowed values for product attribute names");
            toolTip1.SetToolTip(btnManageUsers,    "Add, edit and manage user accounts");
            toolTip1.SetToolTip(btnLoginLog,       "View login history and failed attempts");
            toolTip1.SetToolTip(btnActivityLog,    "Full audit trail of all system changes");
            toolTip1.SetToolTip(btnAppLogs,        "View application log files");
            toolTip1.SetToolTip(btnDashboard,      "Live KPI dashboard — orders, stock, revenue");
            toolTip1.SetToolTip(btnReports,        "Stock, sales, COGS and cycle count reports");
            toolTip1.SetToolTip(btnReorderReport,  "Products and parts that need reordering");
            toolTip1.SetToolTip(btnInventoryDash,  "Inventory Snapshot — health overview: stock alerts, low stock, and value summary");
            toolTip1.SetToolTip(btnUnverified,     "Review auto-created products and parts");
            toolTip1.SetToolTip(btnSettings,       "Configure theme, logo, colors and app settings");
            toolTip1.SetToolTip(btnExport,         "Export ERP data to CSV files");
            toolTip1.SetToolTip(btnImports,        "Import data from CSV files into the ERP");
            toolTip1.SetToolTip(btnShopifyStores,  "Add, edit, and test Shopify store connections");
            toolTip1.SetToolTip(btnCustomers,      "Browse customers and their order history");
            toolTip1.SetToolTip(btnVendors,        "Manage vendors and the parts they supply");
            toolTip1.SetToolTip(btnBreakeven,      "Breakeven & margin calculator");
            toolTip1.SetToolTip(btnAccounting,     "Accounting — revenue, COGS, expenses and net profit");
            toolTip1.SetToolTip(btnJane,           "Dial Jane directly from this screen");
            toolTip1.SetToolTip(btnOphelia,        "Dial Ophelia directly from this screen");
            toolTip1.SetToolTip(btnProductSearch,  "Explore products with custom attribute filters");
            toolTip1.SetToolTip(btnExpiryTracker,  "View lot stock by expiry date — expired, critical, and upcoming");
            toolTip1.SetToolTip(btnBackorders,     "View and fulfill open backorder lines");
            toolTip1.SetToolTip(btnReturnsManager, "Review, approve and reject customer returns");
            toolTip1.SetToolTip(btnReturnsReport,       "Date-range report: returns by condition and credit value");
            toolTip1.SetToolTip(btnInventoryMoveImport, "Bulk-move stock between locations via CSV (SKU, FromLocation, ToLocation)");
            toolTip1.SetToolTip(btnPackageExplorer,     "View, create, and manage product bundles and their components");

            // ════════════════════════════════════════════════════════════════
            // BEGIN INIT
            // ════════════════════════════════════════════════════════════════
            ((System.ComponentModel.ISupportInitialize)pbLogo).BeginInit();
            pnlHeader.SuspendLayout();
            pnlSidebar.SuspendLayout();
            pnlGrid.SuspendLayout();
            SuspendLayout();

            // ── Header ────────────────────────────────────────────────────────
            pnlHeader.Tag       = "header";
            pnlHeader.BackColor = Theme.Header;
            pnlHeader.Dock      = DockStyle.Top;
            pnlHeader.Height    = 62;

            // Logo badge
            pbLogo.Location    = new Point(12, 7);
            pbLogo.Size        = new Size(120, 46);
            pbLogo.SizeMode    = PictureBoxSizeMode.Zoom;
            pbLogo.BackColor   = Color.White;
            pbLogo.BorderStyle = BorderStyle.None;

            // App name
            lblAppName.AutoSize  = false;
            lblAppName.Font      = new Font("Segoe UI", 16F, FontStyle.Bold);
            lblAppName.ForeColor = Color.White;
            lblAppName.Location  = new Point(144, 9);
            lblAppName.Size      = new Size(180, 28);
            lblAppName.Text      = "JaneERP";

            // Welcome subtitle
            lblWelcome.AutoSize  = false;
            lblWelcome.Font      = new Font("Segoe UI", 9F);
            lblWelcome.ForeColor = Color.FromArgb(160, 200, 210);
            lblWelcome.Location  = new Point(144, 36);
            lblWelcome.Size      = new Size(340, 17);
            lblWelcome.Text      = "Welcome";

            // Settings button
            btnSettings.FlatStyle = FlatStyle.Flat;
            btnSettings.FlatAppearance.BorderColor        = Color.FromArgb(30, 255, 255, 255);
            btnSettings.FlatAppearance.BorderSize         = 1;
            btnSettings.FlatAppearance.MouseOverBackColor = Color.FromArgb(24, 56, 66);
            btnSettings.Font      = new Font("Segoe UI", 9F);
            btnSettings.ForeColor = Color.FromArgb(176, 213, 220);
            btnSettings.BackColor = Color.Transparent;
            btnSettings.Size      = new Size(96, 28);
            btnSettings.Anchor    = AnchorStyles.Top | AnchorStyles.Right;
            btnSettings.Text      = "\u2699 Settings";
            btnSettings.Click    += btnSettings_Click;
            btnSettings.Cursor    = Cursors.Hand;
            btnSettings.UseVisualStyleBackColor = false;

            // Logout button
            btnLogout.FlatStyle = FlatStyle.Flat;
            btnLogout.FlatAppearance.BorderColor        = Color.FromArgb(30, 255, 255, 255);
            btnLogout.FlatAppearance.BorderSize         = 1;
            btnLogout.FlatAppearance.MouseOverBackColor = Color.FromArgb(24, 56, 66);
            btnLogout.Font      = new Font("Segoe UI", 9F);
            btnLogout.ForeColor = Color.FromArgb(176, 213, 220);
            btnLogout.BackColor = Color.Transparent;
            btnLogout.Size      = new Size(74, 28);
            btnLogout.Anchor    = AnchorStyles.Top | AnchorStyles.Right;
            btnLogout.Text      = "Logout";
            btnLogout.Click    += btnLogout_Click;
            btnLogout.Cursor    = Cursors.Hand;
            btnLogout.UseVisualStyleBackColor = false;

            // Quick dial
            StyleHeaderLinkButton(btnJane,    "\U0001F4DE Jane",    82);
            StyleHeaderLinkButton(btnOphelia, "\U0001F4F1 Ophelia", 96);

            pnlHeader.Controls.Add(pbLogo);
            pnlHeader.Controls.Add(lblAppName);
            pnlHeader.Controls.Add(lblWelcome);
            pnlHeader.Controls.Add(btnSettings);
            pnlHeader.Controls.Add(btnLogout);
            pnlHeader.Controls.Add(btnJane);
            pnlHeader.Controls.Add(btnOphelia);

            // Header right-side button positioning
            void PositionHeaderButtons()
            {
                btnLogout.Location   = new Point(pnlHeader.Width - btnLogout.Width   - 12, 17);
                btnSettings.Location = new Point(btnLogout.Left  - btnSettings.Width -  8, 17);
                btnOphelia.Location  = new Point(btnSettings.Left - btnOphelia.Width - 10, 19);
                btnJane.Location     = new Point(btnOphelia.Left  - btnJane.Width    -  4, 19);
            }
            pnlHeader.Resize += (_, _) => PositionHeaderButtons();
            Load             += (_, _) => PositionHeaderButtons();

            // Bottom accent line
            var headerLine = new Panel
            {
                Height    = 2,
                Dock      = DockStyle.Bottom,
                BackColor = Theme.Gold
            };
            pnlHeader.Controls.Add(headerLine);

            // ── Sidebar ───────────────────────────────────────────────────────
            pnlSidebar.Tag       = "sidebar";
            pnlSidebar.BackColor = Theme.Header;
            pnlSidebar.Dock      = DockStyle.Left;
            pnlSidebar.Width     = 192;

            var sidebarFlow = new FlowLayoutPanel
            {
                Tag           = "sidebar",
                BackColor     = Color.Transparent,
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents  = false,
                AutoScroll    = false,
                Padding       = new Padding(0, 8, 0, 8)
            };

            // Sidebar nav label
            var lblNavTitle = new Label
            {
                Text      = "NAVIGATION",
                Font      = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 160, 170),
                BackColor = Color.Transparent,
                AutoSize  = false,
                Width     = 192,
                Height    = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(18, 0, 0, 0)
            };
            sidebarFlow.Controls.Add(lblNavTitle);

            // Sidebar nav buttons — scroll content to each section
            var navSales    = MakeSidebarNavButton("\U0001F6D2", "Sales & Purchasing");
            var navProducts = MakeSidebarNavButton("\U0001F4E6", "Products & Inventory");
            var navMfg      = MakeSidebarNavButton("\U0001F3ED", "Manufacturing");
            var navAnalytics= MakeSidebarNavButton("\U0001F4C9", "Analytics & Reports");
            var navData     = MakeSidebarNavButton("\U0001F4BE", "Data");
            var navAdmin    = MakeSidebarNavButton("\U0001F464", "Team & Admin");

            navSales.Click     += (_, _) => ScrollToSection(_hdrSales);
            navProducts.Click  += (_, _) => ScrollToSection(_hdrProducts);
            navMfg.Click       += (_, _) => ScrollToSection(_hdrMfg);
            navAnalytics.Click += (_, _) => ScrollToSection(_hdrAnalytics);
            navData.Click      += (_, _) => ScrollToSection(_hdrData);
            navAdmin.Click     += (_, _) => ScrollToSection(_hdrAdmin);

            sidebarFlow.Controls.Add(navSales);
            sidebarFlow.Controls.Add(navProducts);
            sidebarFlow.Controls.Add(navMfg);
            sidebarFlow.Controls.Add(navAnalytics);
            sidebarFlow.Controls.Add(navData);
            sidebarFlow.Controls.Add(navAdmin);

            // Vertical separator line on the right edge of the sidebar
            var sidebarBorder = new Panel
            {
                Width     = 1,
                Dock      = DockStyle.Right,
                BackColor = Color.FromArgb(30, 255, 255, 255),
                Tag       = "sidebar"
            };
            pnlSidebar.Controls.Add(sidebarFlow);
            pnlSidebar.Controls.Add(sidebarBorder);

            // ── Grid Panel ────────────────────────────────────────────────────
            pnlGrid.Dock              = DockStyle.Fill;
            pnlGrid.FlowDirection     = FlowDirection.LeftToRight;
            pnlGrid.WrapContents      = true;
            pnlGrid.AutoScroll        = true;
            pnlGrid.AutoScrollMargin  = new Size(0, 20);
            pnlGrid.Padding           = new Padding(20, 16, 20, 16);
            pnlGrid.BackColor         = Theme.Background;

            // ── Group header helper ──────────────────────────────────────────
            var _grpHeaderList = new List<Control>();
            Panel GrpHdr(string title)
            {
                var pnl = new Panel
                {
                    Height    = 38,
                    Width     = 800,
                    BackColor = Theme.Background,
                    Margin    = new Padding(0, 18, 0, 6)
                };

                pnl.Controls.Add(new Label
                {
                    Text      = title.ToUpper(),
                    Font      = new Font("Segoe UI", 8F, FontStyle.Bold),
                    ForeColor = Theme.Gold,
                    Location  = new Point(2, 4),
                    AutoSize  = true
                });

                pnl.Controls.Add(new Panel
                {
                    Location  = new Point(0, 28),
                    Height    = 2,
                    Width     = 500,
                    BackColor = Theme.Border,
                    Anchor    = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
                });

                _grpHeaderList.Add(pnl);
                return pnl;
            }
            Panel GrpSpacer() => new Panel { Size = new Size(1, 1), Margin = new Padding(0) };

            // ── Section: Sales & Purchasing ──────────────────────────────────
            _hdrSales = GrpHdr("Sales & Purchasing");
            pnlGrid.Controls.Add(_hdrSales);
            pnlGrid.SetFlowBreak(_hdrSales, true);
            pnlGrid.Controls.Add(btnSales);
            pnlGrid.Controls.Add(btnPickingDash);
            pnlGrid.Controls.Add(btnPackingDash);
            pnlGrid.Controls.Add(btnPurchaseOrders);
            pnlGrid.Controls.Add(btnCustomers);
            pnlGrid.Controls.Add(btnVendors);
            pnlGrid.Controls.Add(btnShopifyStores);
            pnlGrid.Controls.Add(btnBackorders);
            pnlGrid.Controls.Add(btnReturnsManager);
            var sp1 = GrpSpacer(); pnlGrid.Controls.Add(sp1); pnlGrid.SetFlowBreak(sp1, true);

            // ── Section: Products & Inventory ────────────────────────────────
            _hdrProducts = GrpHdr("Products & Inventory");
            pnlGrid.Controls.Add(_hdrProducts);
            pnlGrid.SetFlowBreak(_hdrProducts, true);
            pnlGrid.Controls.Add(btnInventory);
            pnlGrid.Controls.Add(btnParts);
            pnlGrid.Controls.Add(btnBOM);
            pnlGrid.Controls.Add(btnPackageExplorer);
            pnlGrid.Controls.Add(btnProductSearch);
            pnlGrid.Controls.Add(btnLocations);
            pnlGrid.Controls.Add(btnProductTypes);
            pnlGrid.Controls.Add(btnAttributeLists);
            pnlGrid.Controls.Add(btnCycleCount);
            pnlGrid.Controls.Add(btnInventoryDash);
            pnlGrid.Controls.Add(btnReorderReport);
            pnlGrid.Controls.Add(btnUnverified);
            pnlGrid.Controls.Add(btnExpiryTracker);
            var sp2 = GrpSpacer(); pnlGrid.Controls.Add(sp2); pnlGrid.SetFlowBreak(sp2, true);

            // ── Section: Manufacturing ───────────────────────────────────────
            _hdrMfg = GrpHdr("Manufacturing");
            pnlGrid.Controls.Add(_hdrMfg);
            pnlGrid.SetFlowBreak(_hdrMfg, true);
            pnlGrid.Controls.Add(btnManufacturing);
            pnlGrid.Controls.Add(btnWorkOrders);
            pnlGrid.Controls.Add(btnBatchCooking);
            var sp3 = GrpSpacer(); pnlGrid.Controls.Add(sp3); pnlGrid.SetFlowBreak(sp3, true);

            // ── Section: Analytics & Reports ─────────────────────────────────
            _hdrAnalytics = GrpHdr("Analytics & Reports");
            pnlGrid.Controls.Add(_hdrAnalytics);
            pnlGrid.SetFlowBreak(_hdrAnalytics, true);
            pnlGrid.Controls.Add(btnDashboard);
            pnlGrid.Controls.Add(btnReports);
            pnlGrid.Controls.Add(btnBreakeven);
            pnlGrid.Controls.Add(btnAccounting);
            pnlGrid.Controls.Add(btnReturnsReport);
            var sp4 = GrpSpacer(); pnlGrid.Controls.Add(sp4); pnlGrid.SetFlowBreak(sp4, true);

            // ── Section: Data ─────────────────────────────────────────────────
            _hdrData = GrpHdr("Data");
            pnlGrid.Controls.Add(_hdrData);
            pnlGrid.SetFlowBreak(_hdrData, true);
            pnlGrid.Controls.Add(btnExport);
            pnlGrid.Controls.Add(btnImports);
            pnlGrid.Controls.Add(btnInventoryMoveImport);
            var sp4b = GrpSpacer(); pnlGrid.Controls.Add(sp4b); pnlGrid.SetFlowBreak(sp4b, true);

            // ── Section: Team & Administration ───────────────────────────────
            _hdrAdmin = GrpHdr("Team & Administration");
            pnlGrid.Controls.Add(_hdrAdmin);
            pnlGrid.SetFlowBreak(_hdrAdmin, true);
            pnlGrid.Controls.Add(btnTaskManager);
            pnlGrid.Controls.Add(btnManageUsers);
            pnlGrid.Controls.Add(btnLoginLog);
            pnlGrid.Controls.Add(btnActivityLog);
            pnlGrid.Controls.Add(btnAppLogs);
            var sp5 = GrpSpacer(); pnlGrid.Controls.Add(sp5); pnlGrid.SetFlowBreak(sp5, true);

            // Bottom scroll spacer
            var bottomSpacer = new Panel { Width = 1, Height = 180, Margin = new Padding(0), BackColor = Theme.Background };
            pnlGrid.Controls.Add(bottomSpacer);
            pnlGrid.SetFlowBreak(bottomSpacer, true);

            pnlGrid.Controls.Add(btnExitApp);
            pnlGrid.Controls.Add(lblSelectDept);

            // Keep group headers full-width
            void UpdateHeaders()
            {
                int w = Math.Max(400, pnlGrid.ClientSize.Width
                                    - pnlGrid.Padding.Left - pnlGrid.Padding.Right);
                foreach (var h in _grpHeaderList) h.Width = w;
            }
            pnlGrid.Resize += (_, _) => UpdateHeaders();
            Load           += (_, _) => UpdateHeaders();

            // ── Form ──────────────────────────────────────────────────────────
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode       = AutoScaleMode.Font;
            ClientSize          = new Size(1080, 800);
            MinimumSize         = new Size(780, 600);
            WindowState         = FormWindowState.Maximized;
            FormBorderStyle     = FormBorderStyle.None;
            MaximizeBox         = false;
            Name                = "FormMainMenu";
            StartPosition       = FormStartPosition.CenterScreen;
            Text                = "JaneERP";

            // Add order: Fill first, then Left, then Top (reverse z-order docking)
            Controls.Add(pnlGrid);
            Controls.Add(pnlSidebar);
            Controls.Add(pnlHeader);

            ((System.ComponentModel.ISupportInitialize)pbLogo).EndInit();
            pnlGrid.ResumeLayout(false);
            pnlSidebar.ResumeLayout(false);
            pnlHeader.ResumeLayout(false);
            ResumeLayout(false);
        }

        // ── Scroll helper ─────────────────────────────────────────────────────
        private void ScrollToSection(Panel sectionHeader)
        {
            if (sectionHeader == null) return;
            int y = Math.Max(0, sectionHeader.Top - pnlGrid.Padding.Top + 4);
            pnlGrid.AutoScrollPosition = new Point(0, y);
        }

        // ── Header link-button helper ─────────────────────────────────────────
        private static void StyleHeaderLinkButton(Button btn, string text, int width)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor        = Color.FromArgb(30, 255, 255, 255);
            btn.FlatAppearance.BorderSize         = 1;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(24, 56, 66);
            btn.Font      = new Font("Segoe UI", 8.5F);
            btn.ForeColor = Color.FromArgb(176, 213, 220);
            btn.BackColor = Color.Transparent;
            btn.Size      = new Size(width, 24);
            btn.Text      = text;
            btn.Cursor    = Cursors.Hand;
            btn.UseVisualStyleBackColor = false;
        }

        // ── Sidebar nav button factory ────────────────────────────────────────
        private static Button MakeSidebarNavButton(string icon, string label)
        {
            var btn = new Button
            {
                Width     = 192,
                Height    = 46,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(176, 213, 220),
                Font      = new Font("Segoe UI", 9F),
                Cursor    = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Text      = $"  {icon}  {label}",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(14, 0, 0, 0),
                Margin    = new Padding(0, 0, 0, 2),
                TabStop   = false,
            };
            btn.FlatAppearance.BorderSize         = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(22, 60, 70);

            // Left accent bar on hover via paint
            bool _hov = false;
            btn.MouseEnter += (_, _) => { _hov = true;  btn.Invalidate(); };
            btn.MouseLeave += (_, _) => { _hov = false; btn.Invalidate(); };
            btn.Paint += (s, e) =>
            {
                if (!_hov) return;
                using var b = new SolidBrush(Theme.Gold);
                e.Graphics.FillRectangle(b, 0, 10, 3, btn.Height - 20);
            };
            return btn;
        }

        // ── Card-style icon button factory ────────────────────────────────────
        private Button MakeIconButton(string icon, string label, EventHandler handler)
        {
            var btn = new Button
            {
                Size      = new Size(110, 98),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Theme.TextPrimary,
                Font      = new Font("Segoe UI", 9F),
                Cursor    = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Margin    = new Padding(6),
                Text      = "",
                TabStop   = false,
            };
            btn.FlatAppearance.BorderSize         = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.White;
            btn.Click += handler;

            bool _hovered = false;
            btn.MouseEnter += (_, _) => { _hovered = true;  btn.Invalidate(); };
            btn.MouseLeave += (_, _) => { _hovered = false; btn.Invalidate(); };

            btn.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                var bounds = new Rectangle(2, 2, btn.Width - 4, btn.Height - 4);

                // ── Card background ───────────────────────────────────────────
                using var bgPath  = RoundedRect(bounds, 10);
                var cardBg = _hovered ? Color.FromArgb(240, 252, 255) : Color.White;
                using var bgBrush = new SolidBrush(cardBg);
                g.FillPath(bgBrush, bgPath);

                // ── Card border ───────────────────────────────────────────────
                var borderColor = _hovered
                    ? Theme.Gold
                    : Color.FromArgb(218, 228, 238);
                using var borderPen = new Pen(borderColor, _hovered ? 2f : 1f);
                g.DrawPath(borderPen, bgPath);

                // ── Icon circle ───────────────────────────────────────────────
                const int csz = 42;
                int cx = (btn.Width - csz) / 2;
                int cy = 10;
                var circleRect = new RectangleF(cx, cy, csz, csz);
                var circleColor = _hovered
                    ? Color.FromArgb(200, 244, 250)   // teal tint on hover
                    : Color.FromArgb(224, 248, 252);   // light teal tint
                using var circleBrush = new SolidBrush(circleColor);
                g.FillEllipse(circleBrush, circleRect);

                // ── Emoji ─────────────────────────────────────────────────────
                using var iconFont  = new Font("Segoe UI Emoji", 16F, GraphicsUnit.Point);
                using var iconBrush = new SolidBrush(Theme.Gold);
                var iconFmt = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(icon, iconFont, iconBrush, circleRect, iconFmt);

                // ── Label ─────────────────────────────────────────────────────
                using var labelFont  = new Font("Segoe UI", 8F, GraphicsUnit.Point);
                var labelColor = _hovered ? Theme.Gold : Color.FromArgb(71, 85, 105);
                using var labelBrush = new SolidBrush(labelColor);
                var labelFmt = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming      = StringTrimming.EllipsisCharacter
                };
                g.DrawString(label, labelFont, labelBrush,
                    new RectangleF(4, 56, btn.Width - 8, 36), labelFmt);
            };

            return btn;
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X,          bounds.Y,           d, d, 180, 90);
            path.AddArc(bounds.Right - d,  bounds.Y,           d, d, 270, 90);
            path.AddArc(bounds.Right - d,  bounds.Bottom - d,  d, d,   0, 90);
            path.AddArc(bounds.X,          bounds.Bottom - d,  d, d,  90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
