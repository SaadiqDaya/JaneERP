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

        private readonly System.Windows.Forms.Timer _idleTimer;
        private const int IdleTimeoutMinutes = 30;

        public FormMainMenu(AppUser user)
        {
            _user = user;
            _idleTimer = new System.Windows.Forms.Timer { Interval = 60_000 }; // check every minute
            _idleTimer.Tick += IdleTimer_Tick;
            _idleTimer.Start();
            InitializeComponent();
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
            btnLoginLog.Visible    = PermissionHelper.IsAdmin() || PermissionHelper.CanEdit("Log");
            btnManageUsers.Visible = PermissionHelper.IsAdmin();
            btnActivityLog.Visible = PermissionHelper.IsAdmin();
            btnProductTypes.Visible = PermissionHelper.IsAdmin() || PermissionHelper.CanEdit("Inventory");
            // Parts and Manufacturing visible to all (editing is gated within)
            btnParts.Visible          = true;
            btnManufacturing.Visible  = true;
            btnPurchaseOrders.Visible = true;
            btnInventoryDash.Visible = true;
            btnBOM.Visible           = true;
            btnReports.Visible   = true;
            btnDashboard.Visible = true;
            btnImports.Visible   = PermissionHelper.IsAdmin() || PermissionHelper.CanEdit("Inventory");
            // Tasks/CycleCount visible to all; Jane/Ophelia always visible
            btnTaskManager.Visible = true;
            btnCycleCount.Visible  = PermissionHelper.IsAdmin() || PermissionHelper.CanEdit("CycleCount") || PermissionHelper.CanEdit("Inventory");
            btnJane.Visible          = true;
            btnOphelia.Visible       = true;
            btnProductSearch.Visible = true;
        }

        private DateTime _lastActivity = DateTime.Now;

        protected override void WndProc(ref Message m)
        {
            // Reset idle timer on mouse or keyboard messages
            const int WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_KEYDOWN = 0x0100;
            if (m.Msg == WM_MOUSEMOVE || m.Msg == WM_LBUTTONDOWN || m.Msg == WM_KEYDOWN)
                _lastActivity = DateTime.Now;
            base.WndProc(ref m);
        }

        private void IdleTimer_Tick(object? sender, EventArgs e)
        {
            if ((DateTime.Now - _lastActivity).TotalMinutes >= IdleTimeoutMinutes)
            {
                _idleTimer.Stop();
                AppLogger.Audit(_user.Username, "AutoLogout", $"Idle for {IdleTimeoutMinutes} minutes");
                AppSession.ClearUser();
                LoggedOut = true;
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

        private void btnProductTypes_Click(object sender, EventArgs e)
        {
            using var frm = new FormProductTypes();
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

        private void btnBOM_Click(object sender, EventArgs e)
        {
            var pRepo = new Data.ProductRepository();
            using var picker = new FormProductPicker(pRepo);
            if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedProduct == null) return;
            var partRepo = new Data.PartRepository();
            using var bom = new FormBomEditor(picker.SelectedProduct, partRepo);
            bom.ShowDialog(this);
        }

        private void btnManufacturing_Click(object sender, EventArgs e)
        {
            using var frm = new FormManufacturingDash();
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

        private void btnTaskManager_Click(object sender, EventArgs e)
        {
            using var frm = new FormTaskManager();
            frm.ShowDialog(this);
        }

        private void btnCycleCount_Click(object sender, EventArgs e)
        {
            using var frm = new FormCycleCount();
            frm.ShowDialog(this);
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
    }
}
