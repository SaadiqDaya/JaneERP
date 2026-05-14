namespace JaneERP
{
    partial class FormSalesDash
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Button btnFetch;
        private System.Windows.Forms.DataGridView dgvOrders;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnViewMultiple;
        private System.Windows.Forms.DateTimePicker dtpFrom;
        private System.Windows.Forms.DateTimePicker dtpTo;
        private System.Windows.Forms.Label lblFrom;
        private System.Windows.Forms.Label lblTo;
        private System.Windows.Forms.TextBox txtMinAmount;
        private System.Windows.Forms.TextBox txtMaxAmount;
        private System.Windows.Forms.Label lblMinAmount;
        private System.Windows.Forms.Label lblMaxAmount;
        private System.Windows.Forms.Label lblStore;
        private System.Windows.Forms.ComboBox cboStoreFilter;
        private System.Windows.Forms.Button btnSyncToERP;
        private System.Windows.Forms.Button btnCancelSync;
        private System.Windows.Forms.Button btnCreateOrder;
        private System.Windows.Forms.Button btnMarkStatus;
        private System.Windows.Forms.Button btnQuickFulfil;
        private System.Windows.Forms.Button btnStoreSettings;
        private System.Windows.Forms.Label lblLastSync;
        private System.Windows.Forms.Button btnSyncNow;
        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.Label lblDetailHeader;
        private System.Windows.Forms.RichTextBox rtbOrderDetail;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            btnFetch         = new Button();
            dgvOrders        = new DataGridView();
            lblStatus        = new Label();
            btnSave          = new Button();
            btnViewMultiple  = new Button();
            dtpFrom          = new DateTimePicker();
            dtpTo            = new DateTimePicker();
            lblFrom          = new Label();
            lblTo            = new Label();
            txtMinAmount     = new TextBox();
            txtMaxAmount     = new TextBox();
            lblMinAmount     = new Label();
            lblMaxAmount     = new Label();
            lblStore         = new Label();
            cboStoreFilter   = new ComboBox();
            btnSyncToERP     = new Button();
            btnCancelSync    = new Button();
            btnCreateOrder   = new Button();
            btnMarkStatus    = new Button();
            btnQuickFulfil   = new Button();
            btnStoreSettings = new Button();
            lblLastSync      = new Label();
            btnSyncNow       = new Button();
            splitMain        = new SplitContainer();
            lblDetailHeader  = new Label();
            rtbOrderDetail   = new RichTextBox();

            var pnlHeader  = new Panel();
            var pnlFilter  = new Panel();
            var pnlActions = new Panel();
            var pnlStatus  = new Panel();
            var sepHF      = new Label();   // header → filter
            var sepFA      = new Label();   // filter → grid
            var sepAS      = new Label();   // actions → status

            ((System.ComponentModel.ISupportInitialize)dgvOrders).BeginInit();
            SuspendLayout();

            // ── Header panel — store selector + fetch/sync controls ───────────────
            pnlHeader.Dock      = DockStyle.Top;
            pnlHeader.Height    = 46;
            pnlHeader.Tag       = "header";
            pnlHeader.BackColor = Theme.Header;

            lblStore.Text     = "Store:";
            lblStore.AutoSize = true;
            lblStore.Location = new Point(12, 15);
            lblStore.Name     = "lblStore";
            lblStore.TabIndex = 0;

            cboStoreFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cboStoreFilter.Location      = new Point(58, 11);
            cboStoreFilter.Name          = "cboStoreFilter";
            cboStoreFilter.Size          = new Size(200, 23);
            cboStoreFilter.TabIndex      = 1;
            cboStoreFilter.SelectedIndexChanged += CboStoreFilter_SelectedIndexChanged;

            btnFetch.Text                  = "Fetch Orders";
            btnFetch.Location              = new Point(268, 11);
            btnFetch.Name                  = "btnFetch";
            btnFetch.Size                  = new Size(100, 24);
            btnFetch.TabIndex              = 2;
            btnFetch.UseVisualStyleBackColor = true;
            btnFetch.Click                 += btnFetch_Click;

            btnSyncNow.Text                  = "\u21BB Refresh";
            btnSyncNow.Location              = new Point(376, 11);
            btnSyncNow.Name                  = "btnSyncNow";
            btnSyncNow.Size                  = new Size(90, 24);
            btnSyncNow.TabIndex              = 3;
            btnSyncNow.Enabled               = false;
            btnSyncNow.UseVisualStyleBackColor = true;
            btnSyncNow.Click                 += BtnSyncNow_Click;

            lblLastSync.AutoSize = true;
            lblLastSync.Location = new Point(476, 16);
            lblLastSync.Name     = "lblLastSync";
            lblLastSync.TabIndex = 4;
            lblLastSync.Text     = "";

            pnlHeader.Controls.AddRange(new Control[] {
                lblStore, cboStoreFilter, btnFetch, btnSyncNow, lblLastSync
            });

            // Separator
            sepHF.Dock      = DockStyle.Top;
            sepHF.Height    = 1;
            sepHF.BackColor = Theme.Border;
            sepHF.Text      = "";

            // ── Filter toolbar — date range + amount filters ──────────────────────
            pnlFilter.Dock      = DockStyle.Top;
            pnlFilter.Height    = 52;
            pnlFilter.BackColor = Theme.Surface;

            lblFrom.Text     = "From:";
            lblFrom.AutoSize = true;
            lblFrom.Location = new Point(12, 16);
            lblFrom.Name     = "lblFrom";
            lblFrom.TabIndex = 5;

            dtpFrom.Location = new Point(55, 13);
            dtpFrom.Name     = "dtpFrom";
            dtpFrom.Size     = new Size(165, 23);
            dtpFrom.TabIndex = 6;

            lblTo.Text     = "To:";
            lblTo.AutoSize = true;
            lblTo.Location = new Point(228, 16);
            lblTo.Name     = "lblTo";
            lblTo.TabIndex = 7;

            dtpTo.Location = new Point(252, 13);
            dtpTo.Name     = "dtpTo";
            dtpTo.Size     = new Size(165, 23);
            dtpTo.TabIndex = 8;

            lblMinAmount.Text     = "Min $:";
            lblMinAmount.AutoSize = true;
            lblMinAmount.Location = new Point(432, 16);
            lblMinAmount.Name     = "lblMinAmount";
            lblMinAmount.TabIndex = 9;

            txtMinAmount.Location        = new Point(476, 13);
            txtMinAmount.Name            = "txtMinAmount";
            txtMinAmount.PlaceholderText = "0.00";
            txtMinAmount.Size            = new Size(72, 23);
            txtMinAmount.TabIndex        = 10;

            lblMaxAmount.Text     = "Max $:";
            lblMaxAmount.AutoSize = true;
            lblMaxAmount.Location = new Point(558, 16);
            lblMaxAmount.Name     = "lblMaxAmount";
            lblMaxAmount.TabIndex = 11;

            txtMaxAmount.Location        = new Point(602, 13);
            txtMaxAmount.Name            = "txtMaxAmount";
            txtMaxAmount.PlaceholderText = "9999.99";
            txtMaxAmount.Size            = new Size(72, 23);
            txtMaxAmount.TabIndex        = 12;

            // Right-anchored: View Multiple then Export JSON (initial positions for 1140px form)
            btnViewMultiple.Text                  = "View Multiple";
            btnViewMultiple.Anchor                = AnchorStyles.Top | AnchorStyles.Right;
            btnViewMultiple.Location              = new Point(936, 13);   // 1140-8-94-8-94
            btnViewMultiple.Name                  = "btnViewMultiple";
            btnViewMultiple.Size                  = new Size(94, 26);
            btnViewMultiple.TabIndex              = 13;
            btnViewMultiple.UseVisualStyleBackColor = true;
            btnViewMultiple.Click                 += btnViewMultiple_Click;

            btnSave.Text                  = "Export JSON\u2026";
            btnSave.Anchor                = AnchorStyles.Top | AnchorStyles.Right;
            btnSave.Location              = new Point(1038, 13);   // 1140-8-94
            btnSave.Name                  = "btnSave";
            btnSave.Size                  = new Size(94, 26);
            btnSave.TabIndex              = 14;
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click                 += btnSave_Click;

            pnlFilter.Controls.AddRange(new Control[] {
                lblFrom, dtpFrom, lblTo, dtpTo,
                lblMinAmount, txtMinAmount, lblMaxAmount, txtMaxAmount,
                btnViewMultiple, btnSave
            });

            // Separator
            sepFA.Dock      = DockStyle.Top;
            sepFA.Height    = 1;
            sepFA.BackColor = Theme.Border;
            sepFA.Text      = "";

            // ── Orders grid ───────────────────────────────────────────────────────
            dgvOrders.AllowUserToAddRows          = false;
            dgvOrders.AllowUserToDeleteRows       = false;
            dgvOrders.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvOrders.Dock                        = DockStyle.Fill;
            dgvOrders.Name                        = "dgvOrders";
            dgvOrders.ReadOnly                    = true;
            dgvOrders.SelectionMode               = DataGridViewSelectionMode.FullRowSelect;
            dgvOrders.TabIndex                    = 3;

            // ── Right panel (order detail) + SplitContainer ──────────────────────
            lblDetailHeader.Text      = "Order Details";
            lblDetailHeader.Font      = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblDetailHeader.ForeColor = Theme.Gold;
            lblDetailHeader.Dock      = DockStyle.Top;
            lblDetailHeader.Height    = 28;
            lblDetailHeader.TextAlign = ContentAlignment.MiddleLeft;
            lblDetailHeader.Padding   = new Padding(8, 0, 0, 0);
            lblDetailHeader.BackColor = Theme.Header;

            rtbOrderDetail.Dock        = DockStyle.Fill;
            rtbOrderDetail.ReadOnly    = true;
            rtbOrderDetail.BackColor   = Theme.Surface;
            rtbOrderDetail.ForeColor   = Theme.TextPrimary;
            rtbOrderDetail.BorderStyle = BorderStyle.None;
            rtbOrderDetail.Font        = new Font("Consolas", 8.5F);
            rtbOrderDetail.Text        = "Select an order to view details.";
            rtbOrderDetail.ScrollBars  = RichTextBoxScrollBars.Vertical;
            rtbOrderDetail.Padding     = new Padding(8);
            rtbOrderDetail.Name        = "rtbOrderDetail";

            splitMain.Dock             = DockStyle.Fill;
            splitMain.SplitterDistance = 776;
            splitMain.SplitterWidth    = 4;
            splitMain.FixedPanel       = FixedPanel.Panel2;
            splitMain.Panel2MinSize    = 180;
            splitMain.Name             = "splitMain";

            splitMain.Panel1.Controls.Add(dgvOrders);     // grid fills left panel
            splitMain.Panel2.BackColor = Theme.Surface;
            // Dock: Fill first (processed last), then Top
            splitMain.Panel2.Controls.Add(rtbOrderDetail);   // Fill
            splitMain.Panel2.Controls.Add(lblDetailHeader);  // Top

            // ── Action bar — order actions left, sync/settings right ─────────────
            pnlActions.Dock      = DockStyle.Bottom;
            pnlActions.Height    = 46;
            pnlActions.Tag       = "header";
            pnlActions.BackColor = Theme.Header;

            btnCreateOrder.Text                  = "+ New Manual Order";
            btnCreateOrder.Location              = new Point(8, 8);
            btnCreateOrder.Name                  = "btnCreateOrder";
            btnCreateOrder.Size                  = new Size(138, 30);
            btnCreateOrder.TabIndex              = 18;
            btnCreateOrder.UseVisualStyleBackColor = true;
            btnCreateOrder.Click                 += btnCreateOrder_Click;

            btnMarkStatus.Text                  = "Change Status\u2026";
            btnMarkStatus.Location              = new Point(154, 8);
            btnMarkStatus.Name                  = "btnMarkStatus";
            btnMarkStatus.Size                  = new Size(120, 30);
            btnMarkStatus.TabIndex              = 19;
            btnMarkStatus.UseVisualStyleBackColor = true;
            btnMarkStatus.Click                 += btnMarkStatus_Click;

            btnQuickFulfil.Text                  = "\u26A1 Quick Fulfil";
            btnQuickFulfil.Location              = new Point(282, 8);
            btnQuickFulfil.Name                  = "btnQuickFulfil";
            btnQuickFulfil.Size                  = new Size(120, 30);
            btnQuickFulfil.TabIndex              = 20;
            btnQuickFulfil.UseVisualStyleBackColor = true;
            btnQuickFulfil.Click                 += BtnQuickFulfil_Click;

            // Right-anchored: Settings | Cancel | Sync (initial positions for 1140px form)
            btnStoreSettings.Text                  = "\u2699 Store Settings";
            btnStoreSettings.Anchor                = AnchorStyles.Top | AnchorStyles.Right;
            btnStoreSettings.Location              = new Point(1008, 8);   // 1140-8-124
            btnStoreSettings.Name                  = "btnStoreSettings";
            btnStoreSettings.Size                  = new Size(124, 30);
            btnStoreSettings.TabIndex              = 23;
            btnStoreSettings.UseVisualStyleBackColor = true;
            btnStoreSettings.Click                 += btnStoreSettings_Click;

            btnCancelSync.Text                  = "Cancel Sync";
            btnCancelSync.Anchor                = AnchorStyles.Top | AnchorStyles.Right;
            btnCancelSync.Enabled               = false;
            btnCancelSync.Location              = new Point(882, 8);   // 1008-8-118
            btnCancelSync.Name                  = "btnCancelSync";
            btnCancelSync.Size                  = new Size(118, 30);
            btnCancelSync.TabIndex              = 22;
            btnCancelSync.UseVisualStyleBackColor = true;
            btnCancelSync.Click                 += btnCancelSync_Click;

            btnSyncToERP.Text                  = "Sync to ERP";
            btnSyncToERP.Anchor                = AnchorStyles.Top | AnchorStyles.Right;
            btnSyncToERP.Location              = new Point(756, 8);   // 882-8-118
            btnSyncToERP.Name                  = "btnSyncToERP";
            btnSyncToERP.Size                  = new Size(118, 30);
            btnSyncToERP.TabIndex              = 21;
            btnSyncToERP.UseVisualStyleBackColor = true;
            btnSyncToERP.Click                 += btnSyncToERP_Click;

            pnlActions.Controls.AddRange(new Control[] {
                btnCreateOrder, btnMarkStatus, btnQuickFulfil,
                btnSyncToERP, btnCancelSync, btnStoreSettings
            });

            // Separator
            sepAS.Dock      = DockStyle.Bottom;
            sepAS.Height    = 1;
            sepAS.BackColor = Theme.Border;
            sepAS.Text      = "";

            // ── Status bar ────────────────────────────────────────────────────────
            pnlStatus.Dock      = DockStyle.Bottom;
            pnlStatus.Height    = 24;
            pnlStatus.Tag       = "header";
            pnlStatus.BackColor = Theme.Header;

            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(8, 4);
            lblStatus.Name     = "lblStatus";
            lblStatus.TabIndex = 24;
            lblStatus.Text     = "Ready";

            pnlStatus.Controls.Add(lblStatus);

            // ── Form ─────────────────────────────────────────────────────────────
            ClientSize  = new Size(1140, 680);
            MinimumSize = new Size(900, 560);
            Padding     = new Padding(8);   // keeps dock-panels off the edges so ResizableHelper
                                            // sees WM_NCHITTEST on the form's own 8px border zone
            Name        = "FormSalesDash";
            Text        = "Shopify Orders";

            // Dock ordering: Fill first, then Bottom (first=bottommost), then Top (last=topmost)
            Controls.Add(splitMain);    // Fill (SplitContainer holds dgvOrders + detail panel)
            Controls.Add(pnlStatus);    // Bottom — very bottom (added first)
            Controls.Add(sepAS);        // Bottom — above status
            Controls.Add(pnlActions);   // Bottom — above separator (added last, closest to grid)
            Controls.Add(sepFA);        // Top — between filter and grid (added first among Top)
            Controls.Add(pnlFilter);    // Top — below header
            Controls.Add(sepHF);        // Top — between header and filter
            Controls.Add(pnlHeader);    // Top — topmost (added last)

            ((System.ComponentModel.ISupportInitialize)dgvOrders).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
