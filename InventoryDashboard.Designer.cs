namespace JaneERP
{
    partial class InventoryDashboard
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            lblHeader           = new Label();
            chkShowInactive     = new CheckBox();
            txtSearch           = new TextBox();
            dgvProducts         = new DataGridView();
            lblAttributesHeader = new Label();
            dgvDetails          = new DataGridView();
            lblInventorySummary = new Label();
            lblHistoryHeader    = new Label();
            dgvHistory          = new DataGridView();
            btnLocations        = new Button();
            btnAdd              = new Button();
            btnLoad             = new Button();
            btnEdit             = new Button();
            btnDeactivate       = new Button();
            btnAdjustStock      = new Button();
            btnTransfer         = new Button();
            btnImportCSV        = new Button();
            btnExportCSV        = new Button();
            ((System.ComponentModel.ISupportInitialize)dgvProducts).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvHistory).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvDetails).BeginInit();
            SuspendLayout();

            // lblHeader
            lblHeader.AutoSize = true;
            lblHeader.Font     = new Font("Microsoft Sans Serif", 16F, FontStyle.Bold);
            lblHeader.Location = new Point(12, 12);
            lblHeader.Name     = "lblHeader";
            lblHeader.TabIndex = 0;
            lblHeader.Text     = "Inventory Manager";

            // chkShowInactive
            chkShowInactive.AutoSize = true;
            chkShowInactive.Location = new Point(220, 19);
            chkShowInactive.Name     = "chkShowInactive";
            chkShowInactive.TabIndex = 1;
            chkShowInactive.Text     = "Show Inactive / Archived Only";
            chkShowInactive.UseVisualStyleBackColor = true;
            chkShowInactive.CheckedChanged += chkShowInactive_CheckedChanged;

            // txtSearch
            txtSearch.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            txtSearch.Location = new Point(790, 16);
            txtSearch.Name     = "txtSearch";
            txtSearch.Size     = new Size(195, 23);
            txtSearch.TabIndex = 2;

            // dgvProducts
            dgvProducts.Anchor          = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvProducts.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvProducts.Location        = new Point(12, 62);
            dgvProducts.MultiSelect     = false;
            dgvProducts.Name            = "dgvProducts";
            dgvProducts.ReadOnly        = true;
            dgvProducts.RowTemplate.Height = 25;
            dgvProducts.SelectionMode   = DataGridViewSelectionMode.FullRowSelect;
            dgvProducts.Size            = new Size(820, 213);
            dgvProducts.TabIndex        = 3;
            dgvProducts.SelectionChanged += dgvProducts_SelectionChanged;

            // lblAttributesHeader
            lblAttributesHeader.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            lblAttributesHeader.AutoSize = true;
            lblAttributesHeader.Font     = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblAttributesHeader.Location = new Point(848, 55);
            lblAttributesHeader.Name     = "lblAttributesHeader";
            lblAttributesHeader.Text     = "Product Details & Attributes";

            // dgvDetails — two-column property/value grid replacing the old ListBox
            dgvDetails.Anchor          = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            dgvDetails.AllowUserToAddRows    = false;
            dgvDetails.AllowUserToDeleteRows = false;
            dgvDetails.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvDetails.Location        = new Point(848, 75);
            dgvDetails.Name            = "dgvDetails";
            dgvDetails.ReadOnly        = true;
            dgvDetails.RowTemplate.Height = 22;
            dgvDetails.SelectionMode   = DataGridViewSelectionMode.FullRowSelect;
            dgvDetails.Size            = new Size(330, 403);
            dgvDetails.TabIndex        = 4;

            // lblInventorySummary — stock totals status bar above the product grid
            lblInventorySummary.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblInventorySummary.AutoSize = false;
            lblInventorySummary.Location = new Point(12, 40);
            lblInventorySummary.Name     = "lblInventorySummary";
            lblInventorySummary.Size     = new Size(820, 16);
            lblInventorySummary.Text     = "";
            lblInventorySummary.Font     = new Font("Segoe UI", 8.5F);

            // lblHistoryHeader
            lblHistoryHeader.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            lblHistoryHeader.AutoSize = true;
            lblHistoryHeader.Font     = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblHistoryHeader.Location = new Point(12, 283);
            lblHistoryHeader.Name     = "lblHistoryHeader";
            lblHistoryHeader.Text     = "Stock Transaction History";

            // dgvHistory
            dgvHistory.Anchor          = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvHistory.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvHistory.Location        = new Point(12, 303);
            dgvHistory.MultiSelect     = false;
            dgvHistory.Name            = "dgvHistory";
            dgvHistory.ReadOnly        = true;
            dgvHistory.RowTemplate.Height = 23;
            dgvHistory.SelectionMode   = DataGridViewSelectionMode.FullRowSelect;
            dgvHistory.Size            = new Size(820, 175);
            dgvHistory.TabIndex        = 5;

            // btnLocations
            btnLocations.Anchor    = AnchorStyles.Bottom | AnchorStyles.Left;
            btnLocations.Location  = new Point(12, 495);
            btnLocations.Name      = "btnLocations";
            btnLocations.Size      = new Size(105, 30);
            btnLocations.TabIndex  = 12;
            btnLocations.Text      = "\U0001F4CD Locations";
            btnLocations.UseVisualStyleBackColor = true;
            btnLocations.Click    += btnLocations_Click;

            // btnAdd
            btnAdd.Anchor              = AnchorStyles.Bottom | AnchorStyles.Left;
            btnAdd.Location            = new Point(125, 495);
            btnAdd.Name                = "btnAdd";
            btnAdd.Size                = new Size(110, 30);
            btnAdd.TabIndex            = 6;
            btnAdd.Text                = "Add Product";
            btnAdd.UseVisualStyleBackColor = true;
            btnAdd.Click              += btnAdd_Click;

            // btnLoad
            btnLoad.Anchor             = AnchorStyles.Bottom | AnchorStyles.Left;
            btnLoad.Location           = new Point(243, 495);
            btnLoad.Name               = "btnLoad";
            btnLoad.Size               = new Size(110, 30);
            btnLoad.TabIndex           = 7;
            btnLoad.Text               = "Force Reload";
            btnLoad.UseVisualStyleBackColor = true;
            btnLoad.Click             += btnLoad_Click;

            // btnEdit
            btnEdit.Anchor             = AnchorStyles.Bottom | AnchorStyles.Left;
            btnEdit.Location           = new Point(361, 495);
            btnEdit.Name               = "btnEdit";
            btnEdit.Size               = new Size(110, 30);
            btnEdit.TabIndex           = 8;
            btnEdit.Text               = "Edit Selected";
            btnEdit.UseVisualStyleBackColor = true;
            btnEdit.Click             += btnEdit_Click;

            // btnDeactivate
            btnDeactivate.Anchor       = AnchorStyles.Bottom | AnchorStyles.Left;
            btnDeactivate.Location     = new Point(479, 495);
            btnDeactivate.Name         = "btnDeactivate";
            btnDeactivate.Size         = new Size(130, 30);
            btnDeactivate.TabIndex     = 9;
            btnDeactivate.Text         = "Deactivate Selected";
            btnDeactivate.UseVisualStyleBackColor = true;
            btnDeactivate.Click       += btnDeactivate_Click;

            // btnAdjustStock
            btnAdjustStock.Anchor      = AnchorStyles.Bottom | AnchorStyles.Left;
            btnAdjustStock.Location    = new Point(617, 495);
            btnAdjustStock.Name        = "btnAdjustStock";
            btnAdjustStock.Size        = new Size(120, 30);
            btnAdjustStock.TabIndex    = 10;
            btnAdjustStock.Text        = "Adjust Stock";
            btnAdjustStock.UseVisualStyleBackColor = true;
            btnAdjustStock.Click      += btnAdjustStock_Click;

            // btnTransfer
            btnTransfer.Anchor         = AnchorStyles.Bottom | AnchorStyles.Left;
            btnTransfer.Location       = new Point(745, 495);
            btnTransfer.Name           = "btnTransfer";
            btnTransfer.Size           = new Size(120, 30);
            btnTransfer.TabIndex       = 14;
            btnTransfer.Text           = "Transfer Stock";
            btnTransfer.UseVisualStyleBackColor = true;
            btnTransfer.Click         += btnTransfer_Click;

            // btnImportCSV
            btnImportCSV.Anchor        = AnchorStyles.Bottom | AnchorStyles.Left;
            btnImportCSV.Location      = new Point(883, 495);
            btnImportCSV.Name          = "btnImportCSV";
            btnImportCSV.Size          = new Size(130, 30);
            btnImportCSV.TabIndex      = 11;
            btnImportCSV.Text          = "Import from CSV";
            btnImportCSV.UseVisualStyleBackColor = true;
            btnImportCSV.Click        += btnImportCSV_Click;

            // btnExportCSV
            btnExportCSV.Anchor        = AnchorStyles.Bottom | AnchorStyles.Left;
            btnExportCSV.Location      = new Point(1021, 495);
            btnExportCSV.Name          = "btnExportCSV";
            btnExportCSV.Size          = new Size(130, 30);
            btnExportCSV.TabIndex      = 13;
            btnExportCSV.Text          = "Export to CSV";
            btnExportCSV.UseVisualStyleBackColor = true;
            btnExportCSV.Click        += btnExportCSV_Click;

            // Form1
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode       = AutoScaleMode.Font;
            ClientSize          = new Size(1200, 545);
            MinimumSize         = new Size(1200, 545);
            Controls.Add(lblHeader);
            Controls.Add(chkShowInactive);
            Controls.Add(txtSearch);
            Controls.Add(dgvProducts);
            Controls.Add(lblAttributesHeader);
            Controls.Add(dgvDetails);
            Controls.Add(lblInventorySummary);
            Controls.Add(lblHistoryHeader);
            Controls.Add(dgvHistory);
            Controls.Add(btnLocations);
            Controls.Add(btnAdd);
            Controls.Add(btnLoad);
            Controls.Add(btnEdit);
            Controls.Add(btnDeactivate);
            Controls.Add(btnAdjustStock);
            Controls.Add(btnTransfer);
            Controls.Add(btnImportCSV);
            Controls.Add(btnExportCSV);
            Name            = "Form1";
            Text            = "JaneERP — Inventory Manager";
            Load           += Form1_Load;
            ((System.ComponentModel.ISupportInitialize)dgvProducts).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvDetails).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvHistory).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label        lblHeader;
        private CheckBox     chkShowInactive;
        private TextBox      txtSearch;
        private DataGridView dgvProducts;
        private Label        lblAttributesHeader;
        private DataGridView dgvDetails;
        private Label        lblInventorySummary;
        private Label        lblHistoryHeader;
        private DataGridView dgvHistory;
        private Button       btnLocations;
        private Button       btnAdd;
        private Button       btnLoad;
        private Button       btnEdit;
        private Button       btnDeactivate;
        private Button       btnAdjustStock;
        private Button       btnTransfer;
        private Button       btnImportCSV;
        private Button       btnExportCSV;
    }
}
