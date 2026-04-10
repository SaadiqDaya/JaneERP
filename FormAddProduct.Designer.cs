namespace JaneERP
{
    partial class FormAddProduct
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            lblSKU              = new Label();
            txtSKU              = new TextBox();
            lblProductName      = new Label();
            txtProductName      = new TextBox();
            lblPrice            = new Label();
            txtPrice            = new TextBox();
            lblWholesalePrice   = new Label();
            txtWholesalePrice   = new TextBox();
            lblStock            = new Label();
            txtStock            = new TextBox();
            lblDefaultLocation  = new Label();
            cboDefaultLocation  = new ComboBox();
            lblProductType      = new Label();
            cboProductType      = new ComboBox();
            lblReorderPoint     = new Label();
            nudReorderPoint     = new NumericUpDown();
            lblOrderUpTo        = new Label();
            nudOrderUpTo        = new NumericUpDown();
            lblVendor           = new Label();
            cboVendor           = new ComboBox();
            lblAttributes       = new Label();
            dgvAttributes       = new DataGridView();
            colProperty         = new DataGridViewComboBoxColumn();
            colValue            = new DataGridViewTextBoxColumn();
            pnlPackage          = new Panel();
            lblPackageContents  = new Label();
            dgvPackageComponents = new DataGridView();
            colPkgSKU           = new DataGridViewComboBoxColumn();
            colPkgName          = new DataGridViewTextBoxColumn();
            colPkgQty           = new DataGridViewTextBoxColumn();
            colPkgNotes         = new DataGridViewTextBoxColumn();
            lblPackageNote      = new Label();
            btnManageBOM        = new Button();
            btnSave             = new Button();
            btnCancel           = new Button();
            ((System.ComponentModel.ISupportInitialize)nudReorderPoint).BeginInit();
            ((System.ComponentModel.ISupportInitialize)nudOrderUpTo).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvAttributes).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvPackageComponents).BeginInit();
            SuspendLayout();

            // lblSKU
            lblSKU.AutoSize = true;
            lblSKU.Location = new Point(20, 22);
            lblSKU.Name     = "lblSKU";
            lblSKU.Text     = "SKU:";

            // txtSKU
            txtSKU.Location = new Point(150, 19);
            txtSKU.Name     = "txtSKU";
            txtSKU.Size     = new Size(260, 23);
            txtSKU.TabIndex = 0;

            // lblProductName
            lblProductName.AutoSize = true;
            lblProductName.Location = new Point(20, 57);
            lblProductName.Name     = "lblProductName";
            lblProductName.Text     = "Product Name:";

            // txtProductName
            txtProductName.Location = new Point(150, 54);
            txtProductName.Name     = "txtProductName";
            txtProductName.Size     = new Size(260, 23);
            txtProductName.TabIndex = 1;

            // lblPrice
            lblPrice.AutoSize = true;
            lblPrice.Location = new Point(20, 92);
            lblPrice.Name     = "lblPrice";
            lblPrice.Text     = "Retail Price:";

            // txtPrice
            txtPrice.Location = new Point(150, 89);
            txtPrice.Name     = "txtPrice";
            txtPrice.Size     = new Size(260, 23);
            txtPrice.TabIndex = 2;

            // lblWholesalePrice
            lblWholesalePrice.AutoSize = true;
            lblWholesalePrice.Location = new Point(20, 127);
            lblWholesalePrice.Name     = "lblWholesalePrice";
            lblWholesalePrice.Text     = "Wholesale Price:";

            // txtWholesalePrice
            txtWholesalePrice.Location = new Point(150, 124);
            txtWholesalePrice.Name     = "txtWholesalePrice";
            txtWholesalePrice.Size     = new Size(260, 23);
            txtWholesalePrice.TabIndex = 3;

            // lblStock — hidden in edit mode; visible for new products only
            lblStock.AutoSize = true;
            lblStock.Location = new Point(20, 162);
            lblStock.Name     = "lblStock";
            lblStock.Text     = "Opening Stock:";

            // txtStock
            txtStock.Location = new Point(150, 159);
            txtStock.Name     = "txtStock";
            txtStock.Size     = new Size(260, 23);
            txtStock.TabIndex = 4;

            // lblDefaultLocation
            lblDefaultLocation.AutoSize = true;
            lblDefaultLocation.Location = new Point(20, 197);
            lblDefaultLocation.Name     = "lblDefaultLocation";
            lblDefaultLocation.Text     = "Default Location:";

            // cboDefaultLocation
            cboDefaultLocation.DropDownStyle = ComboBoxStyle.DropDownList;
            cboDefaultLocation.Location      = new Point(150, 194);
            cboDefaultLocation.Name          = "cboDefaultLocation";
            cboDefaultLocation.Size          = new Size(260, 23);
            cboDefaultLocation.TabIndex      = 5;

            // lblProductType
            lblProductType.AutoSize = true;
            lblProductType.Location = new Point(20, 232);
            lblProductType.Name     = "lblProductType";
            lblProductType.Text     = "Product Type:";

            // cboProductType
            cboProductType.DropDownStyle = ComboBoxStyle.DropDownList;
            cboProductType.Location      = new Point(150, 229);
            cboProductType.Name          = "cboProductType";
            cboProductType.Size          = new Size(260, 23);
            cboProductType.TabIndex      = 6;

            // lblReorderPoint
            lblReorderPoint.AutoSize = true;
            lblReorderPoint.Location = new Point(20, 267);
            lblReorderPoint.Name     = "lblReorderPoint";
            lblReorderPoint.Text     = "Reorder Point:";

            // nudReorderPoint
            nudReorderPoint.Location = new Point(150, 264);
            nudReorderPoint.Name     = "nudReorderPoint";
            nudReorderPoint.Size     = new Size(120, 23);
            nudReorderPoint.TabIndex = 7;
            nudReorderPoint.Minimum  = 0;
            nudReorderPoint.Maximum  = 9999;

            // lblOrderUpTo
            lblOrderUpTo.AutoSize = true;
            lblOrderUpTo.Location = new Point(20, 299);
            lblOrderUpTo.Name     = "lblOrderUpTo";
            lblOrderUpTo.Text     = "Order Up To:";

            // nudOrderUpTo
            nudOrderUpTo.Location = new Point(150, 296);
            nudOrderUpTo.Name     = "nudOrderUpTo";
            nudOrderUpTo.Size     = new Size(120, 23);
            nudOrderUpTo.TabIndex = 8;
            nudOrderUpTo.Minimum  = 0;
            nudOrderUpTo.Maximum  = 99999;

            // lblVendor
            lblVendor.AutoSize = true;
            lblVendor.Location = new Point(20, 334);
            lblVendor.Name     = "lblVendor";
            lblVendor.Text     = "Default Vendor:";

            // cboVendor
            cboVendor.DropDownStyle = ComboBoxStyle.DropDownList;
            cboVendor.Location      = new Point(150, 331);
            cboVendor.Name          = "cboVendor";
            cboVendor.Size          = new Size(260, 23);
            cboVendor.TabIndex      = 9;

            // lblAttributes
            lblAttributes.AutoSize = true;
            lblAttributes.Font     = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblAttributes.Location = new Point(20, 369);
            lblAttributes.Name     = "lblAttributes";
            lblAttributes.Text     = "Custom Attributes:";

            // colProperty — ComboBox that also accepts free text
            colProperty.HeaderText = "Property";
            colProperty.Name       = "colProperty";
            colProperty.Width      = 140;
            ((DataGridViewComboBoxColumn)colProperty).FlatStyle = FlatStyle.Flat;
            ((DataGridViewComboBoxColumn)colProperty).DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing;

            // colValue
            colValue.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colValue.HeaderText   = "Value";
            colValue.Name         = "colValue";

            // dgvAttributes
            dgvAttributes.AllowUserToAddRows    = true;
            dgvAttributes.AllowUserToDeleteRows = true;
            dgvAttributes.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvAttributes.Columns.AddRange(new DataGridViewColumn[] { colProperty, colValue });
            dgvAttributes.Location          = new Point(20, 389);
            dgvAttributes.Name              = "dgvAttributes";
            dgvAttributes.RowTemplate.Height = 23;
            dgvAttributes.Size              = new Size(410, 148);
            dgvAttributes.TabIndex          = 10;

            // pnlPackage — shown only when product type is Package
            pnlPackage.Location  = new Point(20, 547);
            pnlPackage.Name      = "pnlPackage";
            pnlPackage.Size      = new Size(410, 180);
            pnlPackage.TabIndex  = 14;
            pnlPackage.Visible   = false;

            // lblPackageContents
            lblPackageContents.AutoSize = true;
            lblPackageContents.Font     = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblPackageContents.Location = new Point(0, 0);
            lblPackageContents.Name     = "lblPackageContents";
            lblPackageContents.Text     = "Package Contents (component products):";

            // colPkgSKU
            colPkgSKU.HeaderText = "SKU";
            colPkgSKU.Name       = "colPkgSKU";
            colPkgSKU.Width      = 110;
            colPkgSKU.FlatStyle  = FlatStyle.Flat;
            colPkgSKU.DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing;

            // colPkgName
            colPkgName.HeaderText = "Product Name";
            colPkgName.Name       = "colPkgName";
            colPkgName.Width      = 120;
            colPkgName.ReadOnly   = true;

            // colPkgQty
            colPkgQty.HeaderText = "Quantity";
            colPkgQty.Name       = "colPkgQty";
            colPkgQty.Width      = 65;

            // colPkgNotes
            colPkgNotes.HeaderText   = "Notes";
            colPkgNotes.Name         = "colPkgNotes";
            colPkgNotes.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            // dgvPackageComponents
            dgvPackageComponents.AllowUserToAddRows    = true;
            dgvPackageComponents.AllowUserToDeleteRows = true;
            dgvPackageComponents.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvPackageComponents.Columns.AddRange(new DataGridViewColumn[]
                { colPkgSKU, colPkgName, colPkgQty, colPkgNotes });
            dgvPackageComponents.Location           = new Point(0, 20);
            dgvPackageComponents.Name               = "dgvPackageComponents";
            dgvPackageComponents.RowTemplate.Height = 23;
            dgvPackageComponents.Size               = new Size(410, 135);
            dgvPackageComponents.TabIndex           = 0;

            // lblPackageNote
            lblPackageNote.AutoSize = true;
            lblPackageNote.Location = new Point(0, 160);
            lblPackageNote.Name     = "lblPackageNote";
            lblPackageNote.Text     = "i Package components are the products included in this bundle. Use BOM for manufacturing.";
            lblPackageNote.Font     = new Font("Segoe UI", 7.5F, FontStyle.Italic);

            pnlPackage.Controls.Add(lblPackageContents);
            pnlPackage.Controls.Add(dgvPackageComponents);
            pnlPackage.Controls.Add(lblPackageNote);

            // btnManageBOM — only shown in edit mode
            btnManageBOM.Location            = new Point(20, 555);
            btnManageBOM.Name                = "btnManageBOM";
            btnManageBOM.Size                = new Size(160, 30);
            btnManageBOM.TabIndex            = 11;
            btnManageBOM.Text                = "Manage BOM / Parts";
            btnManageBOM.UseVisualStyleBackColor = true;
            btnManageBOM.Visible             = false;
            btnManageBOM.Click              += btnManageBOM_Click;

            // btnSave
            btnSave.Location            = new Point(200, 555);
            btnSave.Name                = "btnSave";
            btnSave.Size                = new Size(110, 30);
            btnSave.TabIndex            = 12;
            btnSave.Text                = "Save Product";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click              += btnSave_Click;

            // btnCancel
            btnCancel.Location          = new Point(320, 555);
            btnCancel.Name              = "btnCancel";
            btnCancel.Size              = new Size(110, 30);
            btnCancel.TabIndex          = 13;
            btnCancel.Text              = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click            += btnCancel_Click;

            // FormAddProduct
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode       = AutoScaleMode.Font;
            ClientSize          = new Size(450, 605);
            Controls.Add(lblSKU);
            Controls.Add(txtSKU);
            Controls.Add(lblProductName);
            Controls.Add(txtProductName);
            Controls.Add(lblPrice);
            Controls.Add(txtPrice);
            Controls.Add(lblWholesalePrice);
            Controls.Add(txtWholesalePrice);
            Controls.Add(lblStock);
            Controls.Add(txtStock);
            Controls.Add(lblDefaultLocation);
            Controls.Add(cboDefaultLocation);
            Controls.Add(lblProductType);
            Controls.Add(cboProductType);
            Controls.Add(lblReorderPoint);
            Controls.Add(nudReorderPoint);
            Controls.Add(lblOrderUpTo);
            Controls.Add(nudOrderUpTo);
            Controls.Add(lblVendor);
            Controls.Add(cboVendor);
            Controls.Add(lblAttributes);
            Controls.Add(dgvAttributes);
            Controls.Add(pnlPackage);
            Controls.Add(btnManageBOM);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            Name            = "FormAddProduct";
            StartPosition   = FormStartPosition.CenterParent;
            Text            = "Add New Product";
            ((System.ComponentModel.ISupportInitialize)nudReorderPoint).EndInit();
            ((System.ComponentModel.ISupportInitialize)nudOrderUpTo).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvAttributes).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvPackageComponents).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        private Label      lblSKU;
        private TextBox    txtSKU;
        private Label      lblProductName;
        private TextBox    txtProductName;
        private Label      lblPrice;
        private TextBox    txtPrice;
        private Label      lblWholesalePrice;
        private TextBox    txtWholesalePrice;
        private Label      lblStock;
        private TextBox    txtStock;
        private Label      lblDefaultLocation;
        private ComboBox   cboDefaultLocation;
        private Label      lblProductType;
        private ComboBox   cboProductType;
        private Label            lblReorderPoint;
        private NumericUpDown    nudReorderPoint;
        private Label            lblOrderUpTo;
        private NumericUpDown    nudOrderUpTo;
        private Label      lblVendor;
        private ComboBox   cboVendor;
        private Label      lblAttributes;
        private DataGridView dgvAttributes;
        private DataGridViewComboBoxColumn colProperty;
        private DataGridViewTextBoxColumn colValue;
        private Button     btnManageBOM;
        private Button     btnSave;
        private Button     btnCancel;
        private Panel      pnlPackage;
        private Label      lblPackageContents;
        private DataGridView dgvPackageComponents;
        private DataGridViewComboBoxColumn colPkgSKU;
        private DataGridViewTextBoxColumn  colPkgName;
        private DataGridViewTextBoxColumn  colPkgQty;
        private DataGridViewTextBoxColumn  colPkgNotes;
        private Label      lblPackageNote;
    }
}
