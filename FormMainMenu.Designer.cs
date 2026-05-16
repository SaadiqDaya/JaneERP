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

        // ── Content host (fills remaining space after header+sidebar) ─────────
        private Panel pnlContentHost;

        // ── Content grid (High Level view) ───────────────────────────────────
        private FlowLayoutPanel pnlGrid;

        // ── Section panels (focused views) ────────────────────────────────────
        private Panel pnlSectionSales;
        private Panel pnlSectionProducts;
        private Panel pnlSectionMfg;
        private Panel pnlSectionAnalytics;
        private Panel pnlSectionData;
        private Panel pnlSectionAdmin;
        private Panel pnlSectionPurchasing;
        private Panel pnlSectionTasks;

        // ── Section header refs (for sidebar scroll / backward compat) ────────
        private Panel _hdrSales, _hdrProducts, _hdrMfg, _hdrAnalytics, _hdrData, _hdrAdmin;

        // ── Nav button refs for active-state highlighting ─────────────────────
        internal Button _navHighLevel, _navSales, _navProducts, _navMfg, _navAnalytics, _navData, _navAdmin;
        internal Button _navPurchasing, _navTasks;

        // ── KPI value labels — updated async when each section loads ──────────
        internal Label _kpiS0, _kpiS1, _kpiS2;        // Sales: Pending Orders, Revenue Today, Orders Today
        internal Label _kpiP0, _kpiP1, _kpiP2;        // Products: In Stock, Low Stock, Inventory Value
        internal Label _kpiM0;                          // Mfg: Open Work Orders
        internal Label _kpiA0, _kpiA1;                 // Analytics: Revenue Today, Inventory Value
        internal Label _kpiA2, _kpiA3, _kpiA4;        // Analytics: Inventory Value, Low Stock, Under Reorder Pt
        internal Label _kpiD0, _kpiD1;                 // Data: Total Products, Total Parts
        internal Label _kpiAd0, _kpiAd1;               // Admin: Tasks Overdue, Low Stock Alerts
        internal Label _kpiPu0, _kpiPu1;               // Purchasing: Pending POs, Outstanding Amount
        internal Label _kpiT0, _kpiT1, _kpiT2;        // Tasks: Overdue, Open Total, Due This Week
        // Time-range button arrays for sections that support it
        internal Button[] _salesTimeBtns = Array.Empty<Button>();
        internal Button[] _analyticsTimeBtns = Array.Empty<Button>();

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
        private Button btnBoxTypes;

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
            pnlContentHost = new Panel();
            lblSelectDept = new Label { Visible = false };
            btnExitApp    = new Button { Visible = false, Size = new Size(1, 1) };
            btnExitApp.Click += (_, _) => Application.Exit();

            // ── Build icon buttons (pnlGrid / High Level view) ─────────────────
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
            btnBoxTypes            = MakeIconButton("\U0001F4E6", "Box Types",            btnBoxTypes_Click);

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
            toolTip1.SetToolTip(btnBoxTypes,            "Define box and package types used when packing orders");

            // ════════════════════════════════════════════════════════════════
            // BEGIN INIT
            // ════════════════════════════════════════════════════════════════
            ((System.ComponentModel.ISupportInitialize)pbLogo).BeginInit();
            pnlHeader.SuspendLayout();
            pnlSidebar.SuspendLayout();
            pnlGrid.SuspendLayout();
            pnlContentHost.SuspendLayout();
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

            // Bottom accent line — "accent" tag tells Theme.Apply() to leave BackColor intact
            var headerLine = new Panel
            {
                Height    = 2,
                Dock      = DockStyle.Bottom,
                BackColor = Theme.Gold,
                Tag       = "accent"
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

            // ── Nav buttons ───────────────────────────────────────────────────
            _navHighLevel = MakeSidebarNavButton("\U0001F3E0", "High Level");
            sidebarFlow.Controls.Add(_navHighLevel);
            _navHighLevel.Click += (_, _) => ShowSection("HighLevel");

            // Separator line
            var navSep = new Panel
            {
                Height    = 1,
                Width     = 192,
                BackColor = Color.FromArgb(22, 58, 68),
                Margin    = new Padding(0, 6, 0, 6),
                Tag       = "accent"
            };
            sidebarFlow.Controls.Add(navSep);

            _navSales = MakeSidebarNavButton("\U0001F6D2", "Sales & Purchasing");
            sidebarFlow.Controls.Add(_navSales);
            _navSales.Click += (_, _) => ShowSection("Sales");

            _navPurchasing = MakeSidebarNavButton("\U0001F69B", "Purchasing");
            sidebarFlow.Controls.Add(_navPurchasing);
            _navPurchasing.Click += (_, _) => ShowSection("Purchasing");

            _navProducts = MakeSidebarNavButton("\U0001F4E6", "Products & Inventory");
            sidebarFlow.Controls.Add(_navProducts);
            _navProducts.Click += (_, _) => ShowSection("Products");

            _navMfg = MakeSidebarNavButton("\U0001F3ED", "Manufacturing");
            sidebarFlow.Controls.Add(_navMfg);
            _navMfg.Click += (_, _) => ShowSection("Mfg");

            _navAnalytics = MakeSidebarNavButton("\U0001F4CA", "Analytics & Reports");
            sidebarFlow.Controls.Add(_navAnalytics);
            _navAnalytics.Click += (_, _) => ShowSection("Analytics");

            _navTasks = MakeSidebarNavButton("\u2705", "Tasks");
            sidebarFlow.Controls.Add(_navTasks);
            _navTasks.Click += (_, _) => ShowSection("Tasks");

            _navData = MakeSidebarNavButton("\U0001F4BE", "Data");
            sidebarFlow.Controls.Add(_navData);
            _navData.Click += (_, _) => ShowSection("Data");

            _navAdmin = MakeSidebarNavButton("\U0001F464", "Team & Admin");
            sidebarFlow.Controls.Add(_navAdmin);
            _navAdmin.Click += (_, _) => ShowSection("Admin");

            // Vertical separator line on the right edge of the sidebar
            var sidebarBorder = new Panel
            {
                Width     = 1,
                Dock      = DockStyle.Right,
                BackColor = Color.FromArgb(22, 58, 68),
                Tag       = "accent"
            };
            pnlSidebar.Controls.Add(sidebarFlow);
            pnlSidebar.Controls.Add(sidebarBorder);

            // ── Grid Panel (High Level view) ──────────────────────────────────
            pnlGrid.Dock              = DockStyle.Fill;
            pnlGrid.FlowDirection     = FlowDirection.LeftToRight;
            pnlGrid.WrapContents      = true;
            pnlGrid.AutoScroll        = true;
            pnlGrid.AutoScrollMargin  = new Size(0, 20);
            pnlGrid.Padding           = new Padding(20, 16, 20, 16);
            pnlGrid.BackColor         = Theme.Background;
            pnlGrid.Visible           = true;

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
            pnlGrid.Controls.Add(btnBoxTypes);
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

            // ── Section panels (focused views) ────────────────────────────────

            // ── Sales section ─────────────────────────────────────────────────────────
            pnlSectionSales = BuildSectionView(
                "Sales",
                new (string, Color, EventHandler?)[]
                {
                    ("Pending Orders",  Theme.Gold, (_, _) => btnSales_Click(null!, EventArgs.Empty)),
                    ("Revenue",         Theme.Gold, null),
                    ("Orders Today",    Theme.Gold, null),
                },
                out var kpisSales,
                hasTimeRange: true,
                out var salesTimeBtns,
                rows: new (string, bool, (string, string, EventHandler)[])[]
                {
                    ("Workflow", true, new (string, string, EventHandler)[]
                    {
                        ("\U0001F6D2", "Sales",    btnSales_Click),
                        ("\U0001F4CB", "Picking",  btnPickingDash_Click),
                        ("\U0001F4E6", "Packing",  btnPackingDash_Click),
                    }),
                    ("Operations", false, new (string, string, EventHandler)[]
                    {
                        ("\U0001F4E6", "Backorders",  btnBackorders_Click),
                        ("\u21A9",     "Returns",     btnReturnsManager_Click),
                        ("\U0001F504", "Cycle Count", btnCycleCount_Click),
                        ("\u26A0",     "Unverified",  btnUnverified_Click),
                    }),
                    ("Products & Data", false, new (string, string, EventHandler)[]
                    {
                        ("\U0001F50D", "Product Explorer",  btnProductSearch_Click),
                        ("\U0001F4CA", "Inv. Snapshot",     btnInventoryDash_Click),
                        ("\U0001F381", "Packages",          btnPackageExplorer_Click),
                        ("\U0001F4E6", "Stock Browser",     btnInventory_Click),
                        ("\U0001F3F7", "Types",             btnProductTypes_Click),
                        ("\U0001F4CB", "Attr Lists",        btnAttributeLists_Click),
                        ("\U0001F6D2", "Shopify Stores",    btnShopifyStores_Click),
                        ("\U0001F4CD", "Locations",         btnLocations_Click),
                    }),
                },
                quickActions: new (string, EventHandler)[]
                {
                    ("New Order",         (_, _) => btnSales_Click(null!, EventArgs.Empty)),
                    ("New Customer",      btnCustomers_Click),
                    ("Customer Note",     btnCustomers_Click),
                    ("Inventory Move",    btnInventoryMoveImport_Click),
                    ("New Product",       btnInventory_Click),
                });
            _kpiS0 = kpisSales[0]; _kpiS1 = kpisSales[1]; _kpiS2 = kpisSales[2];
            _salesTimeBtns = salesTimeBtns;

            // ── Purchasing section ────────────────────────────────────────────────────
            pnlSectionPurchasing = BuildSectionView(
                "Purchasing",
                new (string, Color, EventHandler?)[]
                {
                    ("Pending POs",        Theme.Gold, (_, _) => btnPurchaseOrders_Click(null!, EventArgs.Empty)),
                    ("Outstanding Amount", Theme.Gold, null),
                },
                out var kpisPurchasing,
                hasTimeRange: false,
                out _,
                rows: new (string, bool, (string, string, EventHandler)[])[]
                {
                    ("Workflow", true, new (string, string, EventHandler)[]
                    {
                        ("\U0001F69B", "Purchase Orders", btnPurchaseOrders_Click),
                        ("\U0001F4E5", "Receive / Manage", btnPurchaseOrders_Click),
                        ("\u26A0",     "Reorder Report",  btnReorderReport_Click),
                    }),
                },
                quickActions: new (string, EventHandler)[]
                {
                    ("New PO",   btnPurchaseOrders_Click),
                    ("Vendors",  btnVendors_Click),
                });
            _kpiPu0 = kpisPurchasing[0]; _kpiPu1 = kpisPurchasing[1];

            // ── Products & Inventory section ──────────────────────────────────────────
            pnlSectionProducts = BuildSectionView(
                "Products & Inventory",
                new (string, Color, EventHandler?)[]
                {
                    ("In Stock",       Theme.Gold, (_, _) => btnInventory_Click(null!, EventArgs.Empty)),
                    ("Low Stock",      Theme.Gold, (_, _) => btnReorderReport_Click(null!, EventArgs.Empty)),
                    ("Inv. Value",     Theme.Gold, null),
                },
                out var kpisProducts,
                hasTimeRange: false,
                out _,
                rows: new (string, bool, (string, string, EventHandler)[])[]
                {
                    ("Catalogue", false, new (string, string, EventHandler)[]
                    {
                        ("\U0001F4E6", "Stock Browser",    btnInventory_Click),
                        ("\U0001F527", "Parts",            btnParts_Click),
                        ("\U0001F4CB", "BOM",              btnBOM_Click),
                        ("\U0001F381", "Packages",         btnPackageExplorer_Click),
                        ("\U0001F4E6", "Box Types",        btnBoxTypes_Click),
                        ("\U0001F50D", "Product Explorer", btnProductSearch_Click),
                        ("\U0001F4CD", "Locations",        btnLocations_Click),
                        ("\U0001F3F7", "Types",            btnProductTypes_Click),
                        ("\U0001F4CB", "Attr Lists",       btnAttributeLists_Click),
                    }),
                    ("Operations", false, new (string, string, EventHandler)[]
                    {
                        ("\U0001F504", "Cycle Count",      btnCycleCount_Click),
                        ("\U0001F4CA", "Inv. Snapshot",    btnInventoryDash_Click),
                        ("\u26A0",     "Reorder",          btnReorderReport_Click),
                        ("\u26A0",     "Unverified",       btnUnverified_Click),
                        ("\u23F0",     "Expiry",           btnExpiryTracker_Click),
                    }),
                },
                quickActions: null);
            _kpiP0 = kpisProducts[0]; _kpiP1 = kpisProducts[1]; _kpiP2 = kpisProducts[2];

            // ── Manufacturing section ─────────────────────────────────────────────────
            pnlSectionMfg = BuildSectionView(
                "Manufacturing",
                new (string, Color, EventHandler?)[]
                {
                    ("Open Work Orders", Theme.Gold, (_, _) => btnManufacturing_Click(null!, EventArgs.Empty)),
                },
                out var kpisMfg,
                hasTimeRange: false,
                out _,
                rows: new (string, bool, (string, string, EventHandler)[])[]
                {
                    ("Workflow", true, new (string, string, EventHandler)[]
                    {
                        ("\U0001F3ED", "Mfg Dash",     btnManufacturing_Click),
                        ("\U0001F6E0", "Work Orders",  btnWorkOrders_Click),
                        ("\U0001F9EA", "Batch Cooking", btnBatchCooking_Click),
                    }),
                    ("Reference", false, new (string, string, EventHandler)[]
                    {
                        ("\U0001F4CB", "BOM Explorer",  btnBOM_Click),
                        ("\u23F0",     "Expiry Tracker", btnExpiryTracker_Click),
                    }),
                },
                quickActions: new (string, EventHandler)[]
                {
                    ("New Work Order",  btnManufacturing_Click),
                    ("New Part",        btnParts_Click),
                });
            _kpiM0 = kpisMfg[0];

            // ── Analytics section ─────────────────────────────────────────────────────
            pnlSectionAnalytics = BuildSectionView(
                "Analytics & Reports",
                new (string, Color, EventHandler?)[]
                {
                    ("Expenses (7d)",    Theme.Gold, (_, _) => btnAccounting_Click(null!, EventArgs.Empty)),
                    ("Revenue",          Theme.Gold, (_, _) => btnDashboard_Click(null!, EventArgs.Empty)),
                    ("Inventory Value",  Theme.Gold, (_, _) => btnInventoryDash_Click(null!, EventArgs.Empty)),
                    ("Low Stock",        Theme.Gold, (_, _) => btnReorderReport_Click(null!, EventArgs.Empty)),
                    ("Under Reorder Pt", Theme.Gold, (_, _) => btnReorderReport_Click(null!, EventArgs.Empty)),
                },
                out var kpisAnalytics,
                hasTimeRange: true,
                out var analyticsTimeBtns,
                rows: new (string, bool, (string, string, EventHandler)[])[]
                {
                    ("Dashboards", true, new (string, string, EventHandler)[]
                    {
                        ("\U0001F4C9", "KPI Dash",   btnDashboard_Click),
                        ("\U0001F4C8", "Reports",    btnReports_Click),
                        ("\U0001F4B0", "Accounting", btnAccounting_Click),
                    }),
                    ("Tools", false, new (string, string, EventHandler)[]
                    {
                        ("\u2696",     "Breakeven",       btnBreakeven_Click),
                        ("\U0001F4CB", "Returns Report",  btnReturnsReport_Click),
                    }),
                },
                quickActions: new (string, EventHandler)[]
                {
                    ("New Expense", btnAccounting_Click),
                });
            _kpiA0 = kpisAnalytics[0]; _kpiA1 = kpisAnalytics[1];
            _kpiA2 = kpisAnalytics[2]; _kpiA3 = kpisAnalytics[3]; _kpiA4 = kpisAnalytics[4];
            _analyticsTimeBtns = analyticsTimeBtns;

            // ── Tasks section ─────────────────────────────────────────────────────────
            pnlSectionTasks = BuildSectionView(
                "Tasks",
                new (string, Color, EventHandler?)[]
                {
                    ("Overdue Tasks",    Theme.Gold, (_, _) => btnTaskManager_Click(null!, EventArgs.Empty)),
                    ("Open Tasks",       Theme.Gold, (_, _) => btnTaskManager_Click(null!, EventArgs.Empty)),
                    ("Due This Week",    Theme.Gold, (_, _) => btnTaskManager_Click(null!, EventArgs.Empty)),
                },
                out var kpisTasks,
                hasTimeRange: false,
                out _,
                rows: new (string, bool, (string, string, EventHandler)[])[]
                {
                    ("Task Management", false, new (string, string, EventHandler)[]
                    {
                        ("\u2705", "Task Manager", btnTaskManager_Click),
                    }),
                },
                quickActions: new (string, EventHandler)[]
                {
                    ("New Task", btnTaskManager_Click),
                });
            _kpiT0 = kpisTasks[0]; _kpiT1 = kpisTasks[1]; _kpiT2 = kpisTasks[2];

            // ── Admin section ─────────────────────────────────────────────────────────
            pnlSectionAdmin = BuildSectionView(
                "Team & Admin",
                new (string, Color, EventHandler?)[]
                {
                    ("Active Users",   Theme.Gold, (_, _) => btnManageUsers_Click(null!, EventArgs.Empty)),
                    ("Tasks Overdue",  Theme.Gold, (_, _) => btnTaskManager_Click(null!, EventArgs.Empty)),
                },
                out var kpisAdmin,
                hasTimeRange: false,
                out _,
                rows: new (string, bool, (string, string, EventHandler)[])[]
                {
                    ("User Management", false, new (string, string, EventHandler)[]
                    {
                        ("\U0001F464", "Users",     btnManageUsers_Click),
                        ("\U0001F4DD", "Login Log", btnLoginLog_Click),
                        ("\U0001F4DC", "Audit Log", btnActivityLog_Click),
                        ("\U0001F5C2", "App Logs",  btnAppLogs_Click),
                    }),
                },
                quickActions: new (string, EventHandler)[]
                {
                    ("New User", btnManageUsers_Click),
                });
            _kpiAd0 = kpisAdmin[0]; _kpiAd1 = kpisAdmin[1];

            // ── Data section ──────────────────────────────────────────────────────────
            pnlSectionData = BuildSectionView(
                "Data",
                new (string, Color, EventHandler?)[]
                {
                    ("Total Products", Theme.Gold, (_, _) => btnInventory_Click(null!, EventArgs.Empty)),
                    ("Total Parts",    Theme.Gold, (_, _) => btnParts_Click(null!, EventArgs.Empty)),
                },
                out var kpisData,
                hasTimeRange: false,
                out _,
                rows: new (string, bool, (string, string, EventHandler)[])[]
                {
                    ("Import / Export", true, new (string, string, EventHandler)[]
                    {
                        ("\U0001F4E5", "Imports",    btnImports_Click),
                        ("\U0001F4BE", "Exports",    btnExport_Click),
                    }),
                    ("Bulk Operations", false, new (string, string, EventHandler)[]
                    {
                        ("\U0001F69A", "Move Import", btnInventoryMoveImport_Click),
                    }),
                },
                quickActions: null);
            _kpiD0 = kpisData[0]; _kpiD1 = kpisData[1];

            // ── Content host ──────────────────────────────────────────────────
            pnlContentHost.Dock      = DockStyle.Fill;
            pnlContentHost.BackColor = Theme.Background;
            // Add pnlGrid first (visible), then section panels (hidden)
            pnlContentHost.Controls.Add(pnlGrid);
            pnlContentHost.Controls.Add(pnlSectionSales);
            pnlContentHost.Controls.Add(pnlSectionPurchasing);
            pnlContentHost.Controls.Add(pnlSectionProducts);
            pnlContentHost.Controls.Add(pnlSectionMfg);
            pnlContentHost.Controls.Add(pnlSectionAnalytics);
            pnlContentHost.Controls.Add(pnlSectionTasks);
            pnlContentHost.Controls.Add(pnlSectionData);
            pnlContentHost.Controls.Add(pnlSectionAdmin);

            // ── Form ──────────────────────────────────────────────────────────
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode       = AutoScaleMode.Font;
            ClientSize          = new Size(1080, 800);
            MinimumSize         = new Size(780, 600);
            WindowState         = FormWindowState.Normal;
            FormBorderStyle     = FormBorderStyle.None;
            MaximizeBox         = false;
            Name                = "FormMainMenu";
            StartPosition       = FormStartPosition.CenterScreen;
            Text                = "JaneERP";

            // Add order: Fill first, then Left, then Top (reverse z-order docking)
            Controls.Add(pnlContentHost);
            Controls.Add(pnlSidebar);
            Controls.Add(pnlHeader);

            ((System.ComponentModel.ISupportInitialize)pbLogo).EndInit();
            pnlGrid.ResumeLayout(false);
            pnlContentHost.ResumeLayout(false);
            pnlSidebar.ResumeLayout(false);
            pnlHeader.ResumeLayout(false);
            ResumeLayout(false);

            // Ensure High Level view is visible on startup
            pnlGrid.Visible = true;
        }

        // ── Scroll helper (backward compat) ──────────────────────────────────
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

        // ── KPI tile factory ──────────────────────────────────────────────────
        private static (Panel tile, Label valueLabel) MakeKpiTile(string description, Color accent)
        {
            var wrapper = new Panel
            {
                Size      = new Size(174, 84),
                BackColor = Color.FromArgb(28, 68, 82),
                Margin    = new Padding(0, 0, 14, 0),
            };
            var inner = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(14, 32, 46),
                Padding   = new Padding(1)
            };
            var accentBar = new Panel
            {
                Width     = 3,
                Dock      = DockStyle.Left,
                BackColor = accent,
                Tag       = "accent"
            };
            var lblValue = new Label
            {
                Text      = "\u2014",
                Font      = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                AutoSize  = false,
                Location  = new Point(10, 6),
                Size      = new Size(158, 32),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var lblDesc = new Label
            {
                Text      = description,
                Font      = new Font("Segoe UI", 8F),
                ForeColor = Color.FromArgb(130, 180, 195),
                AutoSize  = false,
                Location  = new Point(10, 42),
                Size      = new Size(158, 36),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Tag       = "accent"
            };
            inner.Controls.Add(accentBar);
            inner.Controls.Add(lblValue);
            inner.Controls.Add(lblDesc);
            wrapper.Controls.Add(inner);
            return (wrapper, lblValue);
        }

        // ── Section view builder (new: rows + arrows + quick-actions sidebar) ─────────
        private Panel BuildSectionView(
            string title,
            (string desc, Color accent, EventHandler? onClick)[] kpiDefs,
            out Label[] kpiLabels,
            bool hasTimeRange,
            out Button[] timeBtns,
            (string header, bool arrows, (string icon, string lbl, EventHandler h)[] btns)[] rows,
            (string lbl, EventHandler h)[]? quickActions)
        {
            var panel = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = Theme.Background };

            // ── Title bar ──────────────────────────────────────────────────────────
            var titleBar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Theme.Header };
            titleBar.Controls.Add(new Label
            {
                Text = title.ToUpper(),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                AutoSize = false, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0),
                BackColor = Color.Transparent
            });

            // ── KPI strip ──────────────────────────────────────────────────────────
            int kpiStripH = hasTimeRange ? 118 : 100;
            var kpiStrip = new Panel
            {
                Dock = DockStyle.Top, Height = kpiStripH,
                BackColor = Color.FromArgb(10, 26, 38),
                Padding = new Padding(0)
            };
            var kpiFlow = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(16, 8, 16, 0),
                AutoSize = true,
                Location = new Point(0, 0)
            };
            var outLabels = new List<Label>();
            foreach (var (desc, accent, onClick) in kpiDefs)
            {
                var (tile, lbl) = MakeKpiTile(desc, accent);
                if (onClick != null)
                {
                    tile.Cursor = Cursors.Hand;
                    tile.Click += onClick;
                    foreach (Control child in tile.Controls)
                        child.Click += onClick;
                }
                kpiFlow.Controls.Add(tile);
                outLabels.Add(lbl);
            }
            kpiLabels = outLabels.ToArray();
            kpiStrip.Controls.Add(kpiFlow);

            // Time range buttons
            timeBtns = Array.Empty<Button>();
            if (hasTimeRange)
            {
                var timeFlow = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    BackColor = Color.Transparent,
                    Padding = new Padding(16, 0, 0, 0),
                    AutoSize = true,
                    Location = new Point(0, 90)
                };
                var tbLabels = new[] { "Today", "7 Days", "30 Days" };
                var buttons = new Button[3];
                for (int i = 0; i < 3; i++)
                {
                    var tb = new Button
                    {
                        Text = tbLabels[i],
                        Size = new Size(62, 20),
                        FlatStyle = FlatStyle.Flat,
                        Font = new Font("Segoe UI", 7.5F),
                        BackColor = i == 0 ? Theme.Gold : Color.FromArgb(22, 60, 70),
                        ForeColor = i == 0 ? Color.White : Color.FromArgb(130, 180, 195),
                        Cursor = Cursors.Hand,
                        UseVisualStyleBackColor = false,
                        Margin = new Padding(0, 0, 4, 0),
                        TabStop = false
                    };
                    tb.FlatAppearance.BorderSize = 0;
                    buttons[i] = tb;
                    timeFlow.Controls.Add(tb);
                }
                timeBtns = buttons;
                kpiStrip.Controls.Add(timeFlow);
            }

            // ── Quick actions sidebar ──────────────────────────────────────────────
            if (quickActions != null && quickActions.Length > 0)
            {
                var qaSidebar = new Panel
                {
                    Dock = DockStyle.Right,
                    Width = 182,
                    BackColor = Color.FromArgb(14, 40, 52)
                };
                var sep = new Panel { Width = 1, Dock = DockStyle.Left, BackColor = Color.FromArgb(22, 58, 68), Tag = "accent" };
                var qaHeader = new Label
                {
                    Text = "QUICK ACTIONS",
                    Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 160, 170),
                    Dock = DockStyle.Top, Height = 34,
                    TextAlign = ContentAlignment.BottomLeft,
                    Padding = new Padding(14, 0, 0, 4),
                    BackColor = Color.Transparent
                };
                var qaFlow = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    BackColor = Color.Transparent,
                    Padding = new Padding(10, 4, 10, 8)
                };
                foreach (var (lbl, h) in quickActions)
                {
                    var qBtn = new Button
                    {
                        Text = "+ " + lbl,
                        Width = 160, Height = 34,
                        FlatStyle = FlatStyle.Flat,
                        Font = new Font("Segoe UI", 9F),
                        BackColor = Color.FromArgb(22, 60, 75),
                        ForeColor = Color.FromArgb(200, 230, 240),
                        Cursor = Cursors.Hand,
                        UseVisualStyleBackColor = false,
                        Margin = new Padding(0, 0, 0, 5),
                        TextAlign = ContentAlignment.MiddleLeft,
                        Padding = new Padding(8, 0, 0, 0),
                        TabStop = false
                    };
                    qBtn.FlatAppearance.BorderColor = Color.FromArgb(30, 80, 95);
                    qBtn.FlatAppearance.BorderSize = 1;
                    qBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 80, 95);
                    qBtn.Click += h;
                    qaFlow.Controls.Add(qBtn);
                }
                qaSidebar.Controls.Add(qaFlow);
                qaSidebar.Controls.Add(qaHeader);
                qaSidebar.Controls.Add(sep);
                panel.Controls.Add(qaSidebar);  // add before Fill content
            }

            // ── Tool rows area ─────────────────────────────────────────────────────
            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Theme.Background
            };
            var rowsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Theme.Background,
                Padding = new Padding(20, 14, 20, 24)
            };

            foreach (var (header, arrows, btns) in rows)
            {
                if (btns.Length == 0) continue;

                // Row header label
                rowsFlow.Controls.Add(new Label
                {
                    Text = header.ToUpper(),
                    Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                    ForeColor = Theme.Gold,
                    AutoSize = false, Width = 900, Height = 26,
                    TextAlign = ContentAlignment.BottomLeft,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0, 8, 0, 2)
                });

                // Buttons (with optional arrows)
                var btnRow = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = true,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    BackColor = Color.Transparent,
                    Padding = new Padding(0),
                    Margin = new Padding(0, 0, 0, 6)
                };
                for (int i = 0; i < btns.Length; i++)
                {
                    var (icon, lbl, h) = btns[i];
                    btnRow.Controls.Add(MakeIconButton(icon, lbl, h));
                    if (arrows && i < btns.Length - 1)
                    {
                        btnRow.Controls.Add(new Label
                        {
                            Text = "\u2192",
                            Font = new Font("Segoe UI", 20F),
                            ForeColor = Color.FromArgb(80, 130, 150),
                            AutoSize = false, Size = new Size(30, 98),
                            TextAlign = ContentAlignment.MiddleCenter,
                            BackColor = Color.Transparent,
                            Margin = new Padding(2, 6, 2, 6)
                        });
                    }
                }
                rowsFlow.Controls.Add(btnRow);

                // Separator
                rowsFlow.Controls.Add(new Panel
                {
                    Height = 1, Width = 900,
                    BackColor = Color.FromArgb(228, 220, 244),
                    Margin = new Padding(0, 2, 0, 2),
                    Tag = "accent"
                });
            }
            rowsFlow.Controls.Add(new Panel { Width = 1, Height = 40, Margin = new Padding(0) });
            scrollPanel.Controls.Add(rowsFlow);
            panel.Controls.Add(scrollPanel);  // DockStyle.Fill

            panel.Controls.Add(kpiStrip);   // DockStyle.Top
            panel.Controls.Add(titleBar);   // DockStyle.Top (renders on top of kpiStrip)

            return panel;
        }
    }
}
