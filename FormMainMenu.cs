using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Logging;
using JaneERP.Models;
using JaneERP.Security;

namespace JaneERP
{
    public partial class FormMainMenu : Form
    {
        private readonly AppUser _user;

        /// <summary>True when the user explicitly clicked Logout (vs closing the window).</summary>
        public bool LoggedOut { get; private set; }

        /// <summary>True when the session was ended by the idle timeout (not a manual logout).</summary>
        public bool SessionExpired { get; private set; }

        private readonly System.Windows.Forms.Timer _idleTimer;
        private const int IdleTimeoutMinutes = 30;

        // ── Mention badge ─────────────────────────────────────────────────────────
        private int _mentionCount;
        private int _unverifiedCount;
        private int _cycleCountOverdueCount;
        private readonly System.Windows.Forms.Timer _badgeTimer =
            new System.Windows.Forms.Timer { Interval = 20_000 };

        public FormMainMenu(AppUser user)
        {
            _user = user;
            _idleTimer = new System.Windows.Forms.Timer { Interval = 60_000 }; // check every minute
            _idleTimer.Tick += IdleTimer_Tick;
            _idleTimer.Start();
            InitializeComponent();

            // ── Mention badge overlay on Tasks tile ───────────────────────────
            btnTaskManager.Paint += DrawMentionBadge;
            // ── Unverified items badge overlay on Unverified tile ────────────
            btnUnverified.Paint  += DrawUnverifiedBadge;
            // ── Overdue cycle count badge on Cycle Count tile ─────────────────
            btnCycleCount.Paint  += DrawCycleCountBadge;
            _badgeTimer.Tick     += (_, _) => System.Threading.Tasks.Task.Run(FetchBadgeCounts);
            Load                 += (_, _) => System.Threading.Tasks.Task.Run(FetchBadgeCounts);
            FormClosed           += (_, _) => _badgeTimer.Stop();
            _badgeTimer.Start();

            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.MakeResizable(this);
            Theme.MakeDraggable(this, pnlHeader);

            // Header stays the darkest shade
            pnlHeader.BackColor = Theme.Header;
            pnlGrid.BackColor   = Theme.Background;

            // Load logo into header badge
            pbLogo.Image = AppSettings.Current.LoadLogoImage();

            lblWelcome.Text = $"Welcome, {user.Username}";

            // Role badge — coloured pill (gold=Admin, teal=Editor, grey=Viewer)
            // Positioned to the right of the welcome label
            var lblRoleBadge = new Label
            {
                AutoSize  = false,
                Size      = new Size(62, 18),
                Location  = new Point(500, 38),
                Text      = user.Role,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = user.Role switch
                {
                    "Admin"  => Color.FromArgb(195, 145, 0),   // keep amber/gold for Admin badge
                    "Editor" => Color.FromArgb(0, 130, 130),    // teal for Editor
                    _        => Color.FromArgb(80, 65, 110)     // muted violet for Viewer
                }
            };
            pnlHeader.Controls.Add(lblRoleBadge);
            bool isAdmin  = PermissionHelper.IsAdmin();
            bool isEditor = user.Role == "Editor";
            bool isViewer = user.Role == "Viewer";

            // Sales & Purchasing
            btnSales.Visible          = isAdmin || PermissionHelper.CanEdit("SalesOrders");
            btnPickingDash.Visible    = isAdmin || PermissionHelper.CanEdit("SalesOrders");
            btnPackingDash.Visible    = isAdmin || PermissionHelper.CanEdit("SalesOrders");
            btnPurchaseOrders.Visible = isAdmin || PermissionHelper.CanEdit("Parts");
            btnCustomers.Visible      = isAdmin || isEditor;
            btnVendors.Visible        = isAdmin || PermissionHelper.CanEdit("Parts");
            btnShopifyStores.Visible  = isAdmin;

            // Products & Inventory
            btnInventory.Visible      = isAdmin || isEditor;  // inventory managers need this
            btnParts.Visible          = isAdmin || PermissionHelper.CanEdit("Parts");
            btnBOM.Visible            = isAdmin || PermissionHelper.CanEdit("Parts");
            btnPackageExplorer.Visible = isAdmin || isEditor;
            btnProductSearch.Visible  = true;  // read-only, visible to all
            btnLocations.Visible      = isAdmin || PermissionHelper.CanEdit("Inventory");
            btnProductTypes.Visible   = isAdmin || PermissionHelper.CanEdit("Inventory");
            btnAttributeLists.Visible = isAdmin || PermissionHelper.CanEdit("Inventory");
            btnCycleCount.Visible     = isAdmin || PermissionHelper.CanEdit("CycleCount") || PermissionHelper.CanEdit("Inventory");
            btnInventoryDash.Visible  = isAdmin || isEditor;
            btnReorderReport.Visible  = isAdmin || isEditor;
            btnUnverified.Visible     = isAdmin || PermissionHelper.CanEdit("Inventory");
            btnExpiryTracker.Visible   = isAdmin || isEditor;
            btnBackorders.Visible      = isAdmin || PermissionHelper.CanEdit("SalesOrders");
            btnReturnsManager.Visible  = isAdmin || PermissionHelper.CanEdit("SalesOrders");
            btnReturnsReport.Visible   = isAdmin || isEditor;

            // Manufacturing
            btnManufacturing.Visible  = isAdmin || PermissionHelper.CanEdit("Manufacturing");
            btnWorkOrders.Visible     = isAdmin || PermissionHelper.CanEdit("Manufacturing");
            btnBatchCooking.Visible   = isAdmin || PermissionHelper.CanEdit("Manufacturing");

            // Analytics & Reports
            btnDashboard.Visible  = isAdmin || isEditor;
            btnReports.Visible    = isAdmin || isEditor;
            btnBreakeven.Visible  = isAdmin || isEditor;
            btnAccounting.Visible = isAdmin || isEditor;

            // Data
            btnImports.Visible             = isAdmin || PermissionHelper.CanEdit("Inventory");
            btnInventoryMoveImport.Visible = isAdmin || PermissionHelper.CanEdit("Inventory");
            btnExport.Visible              = isAdmin || isEditor;

            // Team & Administration
            btnTaskManager.Visible = true;  // all roles can see and use tasks
            btnManageUsers.Visible = isAdmin;
            btnLoginLog.Visible    = isAdmin || PermissionHelper.CanEdit("Log");
            btnActivityLog.Visible = isAdmin;
            btnAppLogs.Visible     = isAdmin;

            // Quick Dial (in header) — always visible
            btnJane.Visible    = true;
            btnOphelia.Visible = true;
        }

        // Add WS_THICKFRAME so Windows enables OS-level resize on this borderless window.
        // Without this, returning HTLEFT/HTRIGHT/etc from WM_NCHITTEST may be ignored on
        // some Windows versions/DWM configurations.
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= 0x00040000; // WS_SIZEBOX (WS_THICKFRAME)
                return cp;
            }
        }

        private void IdleTimer_Tick(object? sender, EventArgs e)
        {
            if ((DateTime.Now - Security.AppSession.LastActivityTime).TotalMinutes >= IdleTimeoutMinutes)
            {
                _idleTimer.Stop();
                AppLogger.Audit(_user.Username, "AutoLogout", $"Idle for {IdleTimeoutMinutes} minutes");
                AppSession.ClearUser();
                LoggedOut     = true;
                SessionExpired = true;
                Close();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _idleTimer.Stop();
            if (!LoggedOut && AppSession.CurrentUser != null)
            {
                AppLogger.Audit(AppSession.CurrentUser.Username, "Logout", "AppClosed");
                AppSession.ClearUser();
            }
            base.OnFormClosing(e);
        }

        private void btnInventory_Click(object sender, EventArgs e)
        {
            Hide();
            using var frm = new InventoryDashboard();
            frm.ShowDialog(this);
            Show();
        }

        private void btnSales_Click(object sender, EventArgs e)
        {
            Hide();
            var stores = new Data.StoreRepository().GetAll().ToList();
            using var frm = new FormSalesDash(stores);
            frm.ShowDialog(this);
            Show();
        }

        private void btnPickingDash_Click(object sender, EventArgs e)
        {
            Hide();
            using var frm = new FormPickingDash();
            frm.ShowDialog(this);
            Show();
        }

        private void btnPackingDash_Click(object sender, EventArgs e)
        {
            Hide();
            using var frm = new FormPackingDash();
            frm.ShowDialog(this);
            Show();
        }

        private void btnLoginLog_Click(object sender, EventArgs e)
        {
            using var frm = new FormLoginLog();
            frm.ShowDialog(this);
        }

        private void btnManageUsers_Click(object sender, EventArgs e)
        {
            using var frm = new FormUserManagement();
            frm.ShowDialog(this);
        }

        private void btnLocations_Click(object sender, EventArgs e)
        {
            using var frm = new FormLocationManager();
            frm.ShowDialog(this);
        }

        private void btnShopifyStores_Click(object sender, EventArgs e)
        {
            using var frm = new FormStoreDashboard();
            frm.ShowDialog(this);
        }

        private void btnProductTypes_Click(object sender, EventArgs e)
        {
            using var frm = new FormProductTypes();
            frm.ShowDialog(this);
        }

        private void btnAttributeLists_Click(object sender, EventArgs e)
        {
            using var frm = new FormAttributeLists();
            frm.ShowDialog(this);
        }

        private void btnParts_Click(object sender, EventArgs e)
        {
            using var frm = new FormPartsManager();
            frm.ShowDialog(this);
        }

        private void btnInventoryDash_Click(object sender, EventArgs e)
        {
            Hide();
            using var frm = new FormInventorySnapshot();
            frm.ShowDialog(this);
            Show();
        }

        private void btnUnverified_Click(object sender, EventArgs e)
        {
            using var frm = new FormUnverifiedItems();
            frm.ShowDialog(this);
            // Refresh unverified badge after user may have verified some items
            System.Threading.Tasks.Task.Run(FetchBadgeCounts);
        }

        private void btnBOM_Click(object sender, EventArgs e)
        {
            using var frm = new FormBomExplorer();
            frm.ShowDialog(this);
        }

        private void btnPackageExplorer_Click(object sender, EventArgs e)
        {
            using var frm = new FormPackageExplorer();
            frm.ShowDialog(this);
        }

        private void btnManufacturing_Click(object sender, EventArgs e)
        {
            using var frm = new FormManufacturingDash();
            frm.ShowDialog(this);
        }

        private void btnWorkOrders_Click(object sender, EventArgs e)
        {
            using var frm = new FormWorkOrders();
            frm.ShowDialog(this);
        }

        private void btnBatchCooking_Click(object sender, EventArgs e)
        {
            using var frm = new FormBatchCooking();
            frm.ShowDialog(this);
        }

        private void btnPurchaseOrders_Click(object sender, EventArgs e)
        {
            using var frm = new FormPurchaseOrders();
            frm.ShowDialog(this);
        }

        private void btnReports_Click(object sender, EventArgs e)
        {
            using var frm = new FormReports();
            frm.ShowDialog(this);
        }

        private void btnDashboard_Click(object sender, EventArgs e)
        {
            using var frm = new FormKPIDashboard();
            frm.ShowDialog(this);
        }

        private void btnReorderReport_Click(object sender, EventArgs e)
        {
            using var frm = new FormReorderReport();
            frm.ShowDialog(this);
        }

        private void btnActivityLog_Click(object sender, EventArgs e)
        {
            using var frm = new FormActivityLog();
            frm.ShowDialog(this);
        }

        private void btnAppLogs_Click(object sender, EventArgs e)
        {
            using var frm = new FormLogViewer();
            frm.ShowDialog(this);
        }

        private void btnProductSearch_Click(object sender, EventArgs e)
        {
            using var frm = new FormProductSearch();
            frm.ShowDialog(this);
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            using var frm = new FormExports();
            frm.ShowDialog(this);
        }

        private void btnImports_Click(object sender, EventArgs e)
        {
            using var frm = new FormImports();
            frm.ShowDialog(this);
        }

        private void btnInventoryMoveImport_Click(object sender, EventArgs e)
        {
            using var frm = new FormInventoryMoveImport();
            frm.ShowDialog(this);
        }

        private void btnTaskManager_Click(object sender, EventArgs e)
        {
            using var frm = new FormTaskManager();
            frm.ShowDialog(this);
            // Refresh badges after visiting Tasks
            System.Threading.Tasks.Task.Run(FetchBadgeCounts);
        }

        private void btnBreakeven_Click(object sender, EventArgs e)
        {
            using var frm = new FormBreakevenCalculator();
            frm.ShowDialog(this);
        }

        private void btnAccounting_Click(object sender, EventArgs e)
        {
            using var frm = new FormAccounting();
            frm.ShowDialog(this);
        }

        private void btnCycleCount_Click(object sender, EventArgs e)
        {
            using var frm = new FormCycleCount();
            frm.ShowDialog(this);
            // Refresh badge after verifications may have been recorded
            System.Threading.Tasks.Task.Run(FetchBadgeCounts);
        }

        private void btnJane_Click(object sender, EventArgs e)
        {
            var phone = AppSettings.Current.JanePhone;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"tel:{phone}") { UseShellExecute = true }); }
            catch { MessageBox.Show(this, $"Call Jane: {phone}", "Talk to Jane", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        }

        private void btnOphelia_Click(object sender, EventArgs e)
        {
            var phone = AppSettings.Current.OpheliaPhone;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"tel:{phone}") { UseShellExecute = true }); }
            catch { MessageBox.Show(this, $"Call Ophelia: {phone}", "Talk to Ophelia", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        }

        private void btnCustomers_Click(object sender, EventArgs e)
        {
            using var frm = new FormCustomers();
            frm.ShowDialog(this);
        }

        private void btnVendors_Click(object sender, EventArgs e)
        {
            using var frm = new FormVendors();
            frm.ShowDialog(this);
        }

        private void btnExpiryTracker_Click(object sender, EventArgs e)
        {
            using var frm = new FormExpiryDashboard();
            frm.ShowDialog(this);
        }

        private void btnBackorders_Click(object sender, EventArgs e)
        {
            using var frm = new FormBackorderDash();
            frm.ShowDialog(this);
        }

        private void btnReturnsManager_Click(object sender, EventArgs e)
        {
            using var frm = new FormReturnsManager();
            frm.ShowDialog(this);
        }

        private void btnReturnsReport_Click(object sender, EventArgs e)
        {
            using var frm = new FormReturnsReport();
            frm.ShowDialog(this);
        }

        private void btnSettings_Click(object sender, EventArgs e)
        {
            using var frm = new FormSettings();
            frm.ShowDialog(this);
            // Reload logo in case it changed
            pbLogo.Image?.Dispose();
            pbLogo.Image = AppSettings.Load().LoadLogoImage();
        }

        private void btnLogout_Click(object sender, EventArgs e)
        {
            AppLogger.Audit(_user.Username, "Logout", "UserInitiated");
            AppSession.ClearUser();
            LoggedOut = true;
            Close();
        }

        // ── Mention badge helpers ─────────────────────────────────────────────────

        private void FetchBadgeCounts()
        {
            try
            {
                string username = AppSession.CurrentUser?.Username ?? _user.Username;

                int mentions      = AppServices.Get<ITaskRepository>().GetMentions(username, unreadOnly: true).Count;
                int unverified    = AppServices.Get<IProductRepository>().GetUnverifiedCount();
                int cycleOverdue  = AppServices.Get<ICycleCountRepository>().GetOverdueCount();

                bool changed = mentions    != _mentionCount
                            || unverified  != _unverifiedCount
                            || cycleOverdue != _cycleCountOverdueCount;

                _mentionCount           = mentions;
                _unverifiedCount        = unverified;
                _cycleCountOverdueCount = cycleOverdue;

                if (changed && IsHandleCreated && !IsDisposed)
                    BeginInvoke(() =>
                    {
                        btnTaskManager.Invalidate();
                        btnUnverified.Invalidate();
                        btnCycleCount.Invalidate();
                    });
            }
            catch { }
        }

        private void DrawMentionBadge(object? sender, PaintEventArgs e)
        {
            if (_mentionCount <= 0) return;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            string text   = _mentionCount > 99 ? "99+" : _mentionCount.ToString();
            int badgeW    = _mentionCount > 9 ? 22 : 18;
            var badgeRect = new Rectangle(btnTaskManager.Width - badgeW - 4, 4, badgeW, 18);
            using var bgBrush = new SolidBrush(Theme.Danger);
            g.FillEllipse(bgBrush, badgeRect);
            using var tf = new Font("Segoe UI", 7F, FontStyle.Bold, GraphicsUnit.Point);
            using var tb = new SolidBrush(Color.White);
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(text, tf, tb, badgeRect, sf);
        }

        private void DrawUnverifiedBadge(object? sender, PaintEventArgs e)
        {
            if (_unverifiedCount <= 0) return;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            string text   = _unverifiedCount > 99 ? "99+" : _unverifiedCount.ToString();
            int badgeW    = _unverifiedCount > 9 ? 22 : 18;
            var badgeRect = new Rectangle(btnUnverified.Width - badgeW - 4, 4, badgeW, 18);
            using var bgBrush = new SolidBrush(Theme.Danger);
            g.FillEllipse(bgBrush, badgeRect);
            using var tf = new Font("Segoe UI", 7F, FontStyle.Bold, GraphicsUnit.Point);
            using var tb = new SolidBrush(Color.White);
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(text, tf, tb, badgeRect, sf);
        }

        private void DrawCycleCountBadge(object? sender, PaintEventArgs e)
        {
            if (_cycleCountOverdueCount <= 0) return;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            string text   = _cycleCountOverdueCount > 99 ? "99+" : _cycleCountOverdueCount.ToString();
            int badgeW    = _cycleCountOverdueCount > 9 ? 22 : 18;
            var badgeRect = new Rectangle(btnCycleCount.Width - badgeW - 4, 4, badgeW, 18);
            // Amber badge — visually distinct from the red danger badges
            using var bgBrush = new SolidBrush(Color.FromArgb(210, 130, 0));
            g.FillEllipse(bgBrush, badgeRect);
            using var tf = new Font("Segoe UI", 7F, FontStyle.Bold, GraphicsUnit.Point);
            using var tb = new SolidBrush(Color.White);
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString(text, tf, tb, badgeRect, sf);
        }
    }
}
