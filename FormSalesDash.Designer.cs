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
        private System.Windows.Forms.Button btnStoreSettings;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            btnFetch = new Button();
            dgvOrders = new DataGridView();
            lblStatus = new Label();
            btnSave = new Button();
            btnViewMultiple = new Button();
            dtpFrom = new DateTimePicker();
            dtpTo = new DateTimePicker();
            lblFrom = new Label();
            lblTo = new Label();
            txtMinAmount = new TextBox();
            txtMaxAmount = new TextBox();
            lblMinAmount = new Label();
            lblMaxAmount = new Label();
            lblStore = new Label();
            cboStoreFilter = new ComboBox();
            btnSyncToERP  = new Button();
            btnCancelSync  = new Button();
            btnCreateOrder  = new Button();
            btnMarkStatus   = new Button();
            btnStoreSettings = new Button();
            ((System.ComponentModel.ISupportInitialize)dgvOrders).BeginInit();
            SuspendLayout();
            // 
            // btnFetch
            // 
            btnFetch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnFetch.Location = new Point(684, 11);
            btnFetch.Name = "btnFetch";
            btnFetch.Size = new Size(94, 24);
            btnFetch.TabIndex = 2;
            btnFetch.Text = "Fetch Orders";
            btnFetch.UseVisualStyleBackColor = true;
            btnFetch.Click += btnFetch_Click;
            // 
            // dgvOrders
            // 
            dgvOrders.AllowUserToAddRows = false;
            dgvOrders.AllowUserToDeleteRows = false;
            dgvOrders.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvOrders.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvOrders.Location = new Point(12, 76);
            dgvOrders.Name = "dgvOrders";
            dgvOrders.ReadOnly = true;
            dgvOrders.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvOrders.Size = new Size(776, 362);
            dgvOrders.TabIndex = 3;
            // 
            // lblStatus
            // 
            lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(12, 449);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(39, 15);
            lblStatus.TabIndex = 4;
            lblStatus.Text = "Ready";
            // 
            // btnSave
            // 
            btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSave.Location = new Point(684, 41);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(94, 24);
            btnSave.TabIndex = 5;
            btnSave.Text = "Export JSON…";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            // 
            // btnViewMultiple
            // 
            btnViewMultiple.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnViewMultiple.Location = new Point(584, 41);
            btnViewMultiple.Name = "btnViewMultiple";
            btnViewMultiple.Size = new Size(94, 24);
            btnViewMultiple.TabIndex = 14;
            btnViewMultiple.Text = "View Multiple";
            btnViewMultiple.UseVisualStyleBackColor = true;
            btnViewMultiple.Click += btnViewMultiple_Click;
            // 
            // dtpFrom
            // 
            dtpFrom.Location = new Point(12, 42);
            dtpFrom.Name = "dtpFrom";
            dtpFrom.Size = new Size(200, 23);
            dtpFrom.TabIndex = 6;
            // 
            // dtpTo
            // 
            dtpTo.Location = new Point(218, 42);
            dtpTo.Name = "dtpTo";
            dtpTo.Size = new Size(200, 23);
            dtpTo.TabIndex = 7;
            // 
            // lblFrom
            // 
            lblFrom.AutoSize = true;
            lblFrom.Location = new Point(12, 24);
            lblFrom.Name = "lblFrom";
            lblFrom.Size = new Size(69, 15);
            lblFrom.TabIndex = 8;
            lblFrom.Text = "From (date)";
            // 
            // lblTo
            // 
            lblTo.AutoSize = true;
            lblTo.Location = new Point(218, 24);
            lblTo.Name = "lblTo";
            lblTo.Size = new Size(54, 15);
            lblTo.TabIndex = 9;
            lblTo.Text = "To (date)";
            // 
            // txtMinAmount
            // 
            txtMinAmount.Location = new Point(434, 42);
            txtMinAmount.Name = "txtMinAmount";
            txtMinAmount.PlaceholderText = "0.00";
            txtMinAmount.Size = new Size(80, 23);
            txtMinAmount.TabIndex = 10;
            // 
            // txtMaxAmount
            // 
            txtMaxAmount.Location = new Point(520, 42);
            txtMaxAmount.Name = "txtMaxAmount";
            txtMaxAmount.PlaceholderText = "9999.99";
            txtMaxAmount.Size = new Size(80, 23);
            txtMaxAmount.TabIndex = 11;
            // 
            // lblMinAmount
            // 
            lblMinAmount.AutoSize = true;
            lblMinAmount.Location = new Point(434, 24);
            lblMinAmount.Name = "lblMinAmount";
            lblMinAmount.Size = new Size(73, 15);
            lblMinAmount.TabIndex = 12;
            lblMinAmount.Text = "Min amount";
            // 
            // lblMaxAmount
            // 
            lblMaxAmount.AutoSize = true;
            lblMaxAmount.Location = new Point(520, 24);
            lblMaxAmount.Name = "lblMaxAmount";
            lblMaxAmount.Size = new Size(74, 15);
            lblMaxAmount.TabIndex = 13;
            lblMaxAmount.Text = "Max amount";
            //
            // lblStore
            //
            lblStore.AutoSize = true;
            lblStore.Location = new Point(484, 14);
            lblStore.Name = "lblStore";
            lblStore.TabIndex = 15;
            lblStore.Text = "Store:";
            //
            // cboStoreFilter
            //
            cboStoreFilter.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cboStoreFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cboStoreFilter.Location = new Point(524, 11);
            cboStoreFilter.Name = "cboStoreFilter";
            cboStoreFilter.Size = new Size(154, 23);
            cboStoreFilter.TabIndex = 16;
            cboStoreFilter.SelectedIndexChanged += CboStoreFilter_SelectedIndexChanged;
            //
            // btnSyncToERP
            //
            btnSyncToERP.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSyncToERP.Location = new Point(560, 443);
            btnSyncToERP.Name = "btnSyncToERP";
            btnSyncToERP.Size = new Size(110, 24);
            btnSyncToERP.TabIndex = 16;
            btnSyncToERP.Text = "Sync to ERP";
            btnSyncToERP.UseVisualStyleBackColor = true;
            btnSyncToERP.Click += btnSyncToERP_Click;
            //
            // btnCancelSync
            //
            btnCancelSync.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancelSync.Location = new Point(676, 443);
            btnCancelSync.Name = "btnCancelSync";
            btnCancelSync.Size = new Size(110, 24);
            btnCancelSync.TabIndex = 17;
            btnCancelSync.Text = "Cancel Sync";
            btnCancelSync.Enabled = false;
            btnCancelSync.UseVisualStyleBackColor = true;
            btnCancelSync.Click += btnCancelSync_Click;
            //
            // btnCreateOrder
            //
            btnCreateOrder.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnCreateOrder.Location = new Point(12, 443);
            btnCreateOrder.Name = "btnCreateOrder";
            btnCreateOrder.Size = new Size(130, 24);
            btnCreateOrder.TabIndex = 18;
            btnCreateOrder.Text = "+ New Manual Order";
            btnCreateOrder.UseVisualStyleBackColor = true;
            btnCreateOrder.Click += btnCreateOrder_Click;
            //
            // btnMarkStatus
            //
            btnMarkStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnMarkStatus.Location = new Point(150, 443);
            btnMarkStatus.Name     = "btnMarkStatus";
            btnMarkStatus.Size     = new Size(120, 24);
            btnMarkStatus.TabIndex = 19;
            btnMarkStatus.Text     = "Change Status…";
            btnMarkStatus.UseVisualStyleBackColor = true;
            btnMarkStatus.Click += btnMarkStatus_Click;
            //
            // btnStoreSettings
            //
            btnStoreSettings.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnStoreSettings.Location = new Point(668, 443);
            btnStoreSettings.Name     = "btnStoreSettings";
            btnStoreSettings.Size     = new Size(120, 24);
            btnStoreSettings.TabIndex = 20;
            btnStoreSettings.Text     = "⚙ Store Settings";
            btnStoreSettings.UseVisualStyleBackColor = true;
            btnStoreSettings.Click += btnStoreSettings_Click;
            //
            // FormSalesDash
            //
            ClientSize = new Size(800, 475);
            Controls.Add(btnSyncToERP);
            Controls.Add(btnCancelSync);
            Controls.Add(btnCreateOrder);
            Controls.Add(btnMarkStatus);
            Controls.Add(btnStoreSettings);
            Controls.Add(cboStoreFilter);
            Controls.Add(lblStore);
            Controls.Add(btnViewMultiple);
            Controls.Add(lblMaxAmount);
            Controls.Add(lblMinAmount);
            Controls.Add(txtMaxAmount);
            Controls.Add(txtMinAmount);
            Controls.Add(lblTo);
            Controls.Add(lblFrom);
            Controls.Add(dtpTo);
            Controls.Add(dtpFrom);
            Controls.Add(btnSave);
            Controls.Add(lblStatus);
            Controls.Add(dgvOrders);
            Controls.Add(btnFetch);
            Name = "FormSalesDash";
            Text = "Shopify Orders";
            ((System.ComponentModel.ISupportInitialize)dgvOrders).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}