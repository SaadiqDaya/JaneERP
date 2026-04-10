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

        // ── Content grid ──────────────────────────────────────────────────────
        private FlowLayoutPanel pnlGrid;

        // ── Button fields (kept for handler compatibility) ─────────────────────
        private Button btnInventory;
        private Button btnParts;
        private Button btnBOM;
        private Button btnSales;
        private Button btnPurchaseOrders;
        private Button btnManufacturing;
        private Button btnProductSearch;
        private Button btnTaskManager;
        private Button btnCycleCount;
        private Button btnLocations;
        private Button btnProductTypes;
        private Button btnManageUsers;
        private Button btnLoginLog;
        private Button btnActivityLog;
        private Button btnDashboard;
        private Button btnReports;
        private Button btnReorderReport;
        private Button btnInventoryDash;
        private Button btnExport;
        private Button btnImports;
        private Button btnJane;
        private Button btnOphelia;
        private Button btnExitApp;

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
            pnlGrid       = new FlowLayoutPanel();
            lblSelectDept = new Label { Visible = false };
            btnExitApp    = new Button { Visible = false, Size = new Size(1, 1) };
            btnExitApp.Click += (_, _) => Application.Exit();

            // ── Build icon buttons ─────────────────────────────────────────────
            btnInventory      = MakeIconButton("\U0001F4E6", "Products",        btnInventory_Click);
            btnParts          = MakeIconButton("\U0001F527", "Parts",           btnParts_Click);
            btnBOM            = MakeIconButton("\U0001F4CB", "BOM",             btnBOM_Click);
            btnSales          = MakeIconButton("\U0001F6D2", "Sales",           btnSales_Click);
            btnPurchaseOrders = MakeIconButton("\U0001F69B", "Purchase",        btnPurchaseOrders_Click);
            btnManufacturing  = MakeIconButton("\U0001F3ED", "Mfg",             btnManufacturing_Click);
            btnProductSearch  = MakeIconButton("\U0001F50D", "Product Explorer",btnProductSearch_Click);
            btnTaskManager    = MakeIconButton("\u2705",     "Tasks",           btnTaskManager_Click);
            btnCycleCount     = MakeIconButton("\U0001F504", "Cycle Count",     btnCycleCount_Click);
            btnLocations      = MakeIconButton("\U0001F4CD", "Locations",       btnLocations_Click);
            btnProductTypes   = MakeIconButton("\U0001F3F7", "Types",           btnProductTypes_Click);
            btnManageUsers    = MakeIconButton("\U0001F464", "Users",           btnManageUsers_Click);
            btnLoginLog       = MakeIconButton("\U0001F4DD", "Login Log",       btnLoginLog_Click);
            btnActivityLog    = MakeIconButton("\U0001F4DC", "Audit Log",       btnActivityLog_Click);
            btnDashboard      = MakeIconButton("\U0001F4C9", "KPI",             btnDashboard_Click);
            btnReports        = MakeIconButton("\U0001F4C8", "Reports",         btnReports_Click);
            btnReorderReport  = MakeIconButton("\u26A0",     "Reorder",         btnReorderReport_Click);
            btnInventoryDash  = MakeIconButton("\U0001F4CA", "Inventory",       btnInventoryDash_Click);
            btnExport         = MakeIconButton("\U0001F4BE", "Exports",         btnExport_Click);
            btnImports        = MakeIconButton("\U0001F4E5", "Imports",         btnImports_Click);
            btnJane           = MakeIconButton("\U0001F4DE", "Call Jane",       btnJane_Click);
            btnOphelia        = MakeIconButton("\U0001F4F1", "Call Ophelia",    btnOphelia_Click);

            // ── Tooltips ──────────────────────────────────────────────────────
            toolTip1.SetToolTip(btnInventory,      "Browse and manage product inventory");
            toolTip1.SetToolTip(btnParts,          "Manage raw material parts and BOM components");
            toolTip1.SetToolTip(btnBOM,            "Edit Bill of Materials for products");
            toolTip1.SetToolTip(btnSales,          "View and manage Shopify & manual sales orders");
            toolTip1.SetToolTip(btnPurchaseOrders, "Create and manage supplier purchase orders");
            toolTip1.SetToolTip(btnManufacturing,  "Work orders and production management");
            toolTip1.SetToolTip(btnTaskManager,    "Create tasks, assign to team members");
            toolTip1.SetToolTip(btnCycleCount,     "Schedule and record stock cycle counts");
            toolTip1.SetToolTip(btnLocations,      "Manage warehouse storage locations");
            toolTip1.SetToolTip(btnProductTypes,   "Define product categories and required attributes");
            toolTip1.SetToolTip(btnManageUsers,    "Add, edit and manage user accounts");
            toolTip1.SetToolTip(btnLoginLog,       "View login history and failed attempts");
            toolTip1.SetToolTip(btnActivityLog,    "Full audit trail of all system changes");
            toolTip1.SetToolTip(btnDashboard,      "Live KPI dashboard — orders, stock, revenue");
            toolTip1.SetToolTip(btnReports,        "Stock, sales, COGS and cycle count reports");
            toolTip1.SetToolTip(btnReorderReport,  "Products and parts that need reordering");
            toolTip1.SetToolTip(btnInventoryDash,  "Inventory health snapshot");
            toolTip1.SetToolTip(btnSettings,       "Configure theme, logo, colors and app settings");
            toolTip1.SetToolTip(btnExport,         "Export ERP data to CSV files");
            toolTip1.SetToolTip(btnImports,        "Import data from CSV files into the ERP");
            toolTip1.SetToolTip(btnJane,           "Dial Jane directly from this screen");
            toolTip1.SetToolTip(btnOphelia,        "Dial Ophelia directly from this screen");
            toolTip1.SetToolTip(btnProductSearch,  "Explore products with custom attribute filters");

            // ════════════════════════════════════════════════════════════════
            // BEGIN INIT
            // ════════════════════════════════════════════════════════════════
            ((System.ComponentModel.ISupportInitialize)pbLogo).BeginInit();
            pnlHeader.SuspendLayout();
            pnlGrid.SuspendLayout();
            SuspendLayout();

            // ── Header ────────────────────────────────────────────────────────
            pnlHeader.Tag       = "header";
            pnlHeader.BackColor = Theme.Header;
            pnlHeader.Dock      = DockStyle.Top;
            pnlHeader.Height    = 66;

            // Logo badge
            pbLogo.Location    = new Point(12, 8);
            pbLogo.Size        = new Size(130, 48);
            pbLogo.SizeMode    = PictureBoxSizeMode.Zoom;
            pbLogo.BackColor   = Color.White;
            pbLogo.BorderStyle = BorderStyle.None;

            // App name — white bold
            lblAppName.AutoSize  = false;
            lblAppName.Font      = new Font("Segoe UI", 17F, FontStyle.Bold);
            lblAppName.ForeColor = Color.White;
            lblAppName.Location  = new Point(155, 10);
            lblAppName.Size      = new Size(180, 30);
            lblAppName.Text      = "JaneERP";

            // Subtitle
            lblWelcome.AutoSize  = false;
            lblWelcome.Font      = new Font("Segoe UI", 9F);
            lblWelcome.ForeColor = Color.FromArgb(160, 140, 190);  // soft lavender
            lblWelcome.Location  = new Point(155, 38);
            lblWelcome.Size      = new Size(340, 18);
            lblWelcome.Text      = "Welcome";

            // Settings button — flat text-link style
            btnSettings.FlatStyle = FlatStyle.Flat;
            btnSettings.FlatAppearance.BorderColor        = Theme.Border;
            btnSettings.FlatAppearance.BorderSize         = 1;
            btnSettings.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 15, 50);
            btnSettings.Font      = new Font("Segoe UI", 9.5F);
            btnSettings.ForeColor = Theme.TextSecondary;
            btnSettings.BackColor = Color.Transparent;
            btnSettings.Size      = new Size(96, 28);
            btnSettings.Anchor    = AnchorStyles.Top | AnchorStyles.Right;
            btnSettings.Text      = "\u2699 Settings";
            btnSettings.Click    += btnSettings_Click;
            btnSettings.Cursor    = Cursors.Hand;

            // Logout button
            btnLogout.FlatStyle = FlatStyle.Flat;
            btnLogout.FlatAppearance.BorderColor        = Theme.Border;
            btnLogout.FlatAppearance.BorderSize         = 1;
            btnLogout.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 15, 50);
            btnLogout.Font      = new Font("Segoe UI", 9F);
            btnLogout.ForeColor = Theme.TextSecondary;
            btnLogout.BackColor = Color.Transparent;
            btnLogout.Size      = new Size(76, 28);
            btnLogout.Anchor    = AnchorStyles.Top | AnchorStyles.Right;
            btnLogout.Text      = "Logout";
            btnLogout.Click    += btnLogout_Click;
            btnLogout.Cursor    = Cursors.Hand;

            pnlHeader.Controls.Add(pbLogo);
            pnlHeader.Controls.Add(lblAppName);
            pnlHeader.Controls.Add(lblWelcome);
            pnlHeader.Controls.Add(btnSettings);
            pnlHeader.Controls.Add(btnLogout);

            // Header right-side button positioning
            void PositionHeaderButtons()
            {
                btnLogout.Location   = new Point(pnlHeader.Width - btnLogout.Width - 12, 19);
                btnSettings.Location = new Point(btnLogout.Left - btnSettings.Width - 8, 19);
            }
            pnlHeader.Resize += (_, _) => PositionHeaderButtons();
            Load             += (_, _) => PositionHeaderButtons();

            // Thin violet accent line at bottom of header
            var headerAccentLine = new Panel
            {
                Height    = 1,
                Dock      = DockStyle.Bottom,
                BackColor = Theme.Gold   // violet accent
            };
            pnlHeader.Controls.Add(headerAccentLine);

            // ── Grid Panel ────────────────────────────────────────────────────
            pnlGrid.Dock          = DockStyle.Fill;
            pnlGrid.FlowDirection = FlowDirection.LeftToRight;
            pnlGrid.WrapContents  = true;
            pnlGrid.AutoScroll    = true;
            pnlGrid.Padding       = new Padding(18, 14, 18, 18);
            pnlGrid.BackColor     = Theme.Background;

            // ── Group header helper ──────────────────────────────────────────
            var _grpHeaders = new List<Control>();
            Control GrpHdr(string title)
            {
                var pnl = new Panel
                {
                    Height    = 36,
                    Width     = 800,
                    BackColor = Theme.Background,
                    Margin    = new Padding(0, 16, 0, 4)
                };

                // Section label — violet, small caps
                pnl.Controls.Add(new Label
                {
                    Text      = title.ToUpper(),
                    Font      = new Font("Segoe UI", 8F, FontStyle.Bold),
                    ForeColor = Theme.Gold,
                    Location  = new Point(2, 4),
                    AutoSize  = true
                });

                // Divider line — full width, dark violet tint
                pnl.Controls.Add(new Panel
                {
                    Location  = new Point(0, 28),
                    Height    = 1,
                    Width     = 600,
                    BackColor = Theme.Border,
                    Anchor    = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
                });

                _grpHeaders.Add(pnl);
                return pnl;
            }
            Panel GrpSpacer() => new Panel { Size = new Size(1, 1), Margin = new Padding(0) };

            // ── Section: Sales & Purchasing ──────────────────────────────────
            var hdrSales = GrpHdr("Sales & Purchasing");
            pnlGrid.Controls.Add(hdrSales);
            pnlGrid.SetFlowBreak(hdrSales, true);
            pnlGrid.Controls.Add(btnSales);
            pnlGrid.Controls.Add(btnPurchaseOrders);
            var sp1 = GrpSpacer(); pnlGrid.Controls.Add(sp1); pnlGrid.SetFlowBreak(sp1, true);

            // ── Section: Products & Inventory ────────────────────────────────
            var hdrProducts = GrpHdr("Products & Inventory");
            pnlGrid.Controls.Add(hdrProducts);
            pnlGrid.SetFlowBreak(hdrProducts, true);
            pnlGrid.Controls.Add(btnInventory);
            pnlGrid.Controls.Add(btnParts);
            pnlGrid.Controls.Add(btnBOM);
            pnlGrid.Controls.Add(btnProductSearch);
            pnlGrid.Controls.Add(btnCycleCount);
            pnlGrid.Controls.Add(btnLocations);
            pnlGrid.Controls.Add(btnProductTypes);
            var sp2 = GrpSpacer(); pnlGrid.Controls.Add(sp2); pnlGrid.SetFlowBreak(sp2, true);

            // ── Section: Manufacturing ───────────────────────────────────────
            var hdrMfg = GrpHdr("Manufacturing");
            pnlGrid.Controls.Add(hdrMfg);
            pnlGrid.SetFlowBreak(hdrMfg, true);
            pnlGrid.Controls.Add(btnManufacturing);
            pnlGrid.Controls.Add(btnReorderReport);
            pnlGrid.Controls.Add(btnInventoryDash);
            var sp3 = GrpSpacer(); pnlGrid.Controls.Add(sp3); pnlGrid.SetFlowBreak(sp3, true);

            // ── Section: Analytics & Reports ─────────────────────────────────
            var hdrAnalytics = GrpHdr("Analytics & Reports");
            pnlGrid.Controls.Add(hdrAnalytics);
            pnlGrid.SetFlowBreak(hdrAnalytics, true);
            pnlGrid.Controls.Add(btnDashboard);
            pnlGrid.Controls.Add(btnReports);
            pnlGrid.Controls.Add(btnExport);
            pnlGrid.Controls.Add(btnImports);
            var sp4 = GrpSpacer(); pnlGrid.Controls.Add(sp4); pnlGrid.SetFlowBreak(sp4, true);

            // ── Section: Team & Administration ───────────────────────────────
            var hdrAdmin = GrpHdr("Team & Administration");
            pnlGrid.Controls.Add(hdrAdmin);
            pnlGrid.SetFlowBreak(hdrAdmin, true);
            pnlGrid.Controls.Add(btnTaskManager);
            pnlGrid.Controls.Add(btnManageUsers);
            pnlGrid.Controls.Add(btnLoginLog);
            pnlGrid.Controls.Add(btnActivityLog);
            var sp5 = GrpSpacer(); pnlGrid.Controls.Add(sp5); pnlGrid.SetFlowBreak(sp5, true);

            // ── Section: Quick Dial ──────────────────────────────────────────
            var hdrDial = GrpHdr("Quick Dial");
            pnlGrid.Controls.Add(hdrDial);
            pnlGrid.SetFlowBreak(hdrDial, true);
            pnlGrid.Controls.Add(btnJane);
            pnlGrid.Controls.Add(btnOphelia);

            pnlGrid.Controls.Add(btnExitApp);
            pnlGrid.Controls.Add(lblSelectDept);

            // Keep group headers full-width
            void _updateHeaders()
            {
                int w = Math.Max(400, pnlGrid.ClientSize.Width
                                    - pnlGrid.Padding.Left - pnlGrid.Padding.Right);
                foreach (var h in _grpHeaders) h.Width = w;
            }
            pnlGrid.Resize += (_, _) => _updateHeaders();
            Load           += (_, _) => _updateHeaders();

            // ── Form ──────────────────────────────────────────────────────────
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode       = AutoScaleMode.Font;
            ClientSize          = new Size(940, 720);
            MinimumSize         = new Size(660, 540);
            FormBorderStyle     = FormBorderStyle.None;
            MaximizeBox         = false;
            Name                = "FormMainMenu";
            StartPosition       = FormStartPosition.CenterScreen;
            Text                = "JaneERP";

            Controls.Add(pnlGrid);
            Controls.Add(pnlHeader);

            ((System.ComponentModel.ISupportInitialize)pbLogo).EndInit();
            pnlGrid.ResumeLayout(false);
            pnlHeader.ResumeLayout(false);
            ResumeLayout(false);
        }

        // ── Neon icon button factory ───────────────────────────────────────────
        private Button MakeIconButton(string icon, string label, EventHandler handler)
        {
            // Tile surface colour — slightly lighter than background so tiles are visible
            var tileBg = Color.FromArgb(16, 18, 34);

            var btn = new Button
            {
                Size      = new Size(124, 124),
                FlatStyle = FlatStyle.Flat,
                BackColor = tileBg,
                ForeColor = Theme.TextPrimary,
                Font      = new Font("Segoe UI", 9F),
                Cursor    = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Margin    = new Padding(8),
                Text      = "",
                TabStop   = false,
            };
            btn.FlatAppearance.BorderSize         = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(35, 15, 60);
            btn.Click += handler;

            bool _hovered = false;
            btn.MouseEnter += (_, _) => { _hovered = true;  btn.Invalidate(); };
            btn.MouseLeave += (_, _) => { _hovered = false; btn.Invalidate(); };

            btn.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                var bounds = new Rectangle(1, 1, btn.Width - 2, btn.Height - 2);

                // ── Fill rounded background ───────────────────────────────────
                using var bgPath = RoundedRect(bounds, 14);
                using var bgBrush = new SolidBrush(_hovered
                    ? Color.FromArgb(30, 12, 54)
                    : tileBg);
                g.FillPath(bgBrush, bgPath);

                // ── Outer glow (semi-transparent, thicker) ────────────────────
                var glowRect   = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using var glowPath = RoundedRect(glowRect, 15);
                using var glowPen  = new Pen(Theme.GlowOuter, _hovered ? 4f : 3f);
                g.DrawPath(glowPen, glowPath);

                // ── Inner bright border ───────────────────────────────────────
                using var borderPath = RoundedRect(new Rectangle(2, 2, btn.Width - 5, btn.Height - 5), 13);
                var borderColor = _hovered
                    ? Color.FromArgb(210, 100, 255)   // brighter violet on hover
                    : Theme.Gold;
                using var borderPen = new Pen(borderColor, 1.5f);
                g.DrawPath(borderPen, borderPath);

                // ── Icon ──────────────────────────────────────────────────────
                using var iconFont  = new Font("Segoe UI Emoji", 26F, GraphicsUnit.Point);
                using var iconBrush = new SolidBrush(Theme.TextPrimary);
                var iconFmt = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(icon, iconFont, iconBrush,
                    new RectangleF(0, 4, btn.Width, 72), iconFmt);

                // ── Label ─────────────────────────────────────────────────────
                var labelColor = _hovered ? Color.White : Theme.TextSecondary;
                using var labelFont  = new Font("Segoe UI", 8.5F, GraphicsUnit.Point);
                using var labelBrush = new SolidBrush(labelColor);
                var labelFmt = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming      = StringTrimming.EllipsisCharacter
                };
                g.DrawString(label, labelFont, labelBrush,
                    new RectangleF(4, 82, btn.Width - 8, 34), labelFmt);
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
