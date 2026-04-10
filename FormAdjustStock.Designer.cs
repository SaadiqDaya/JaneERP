namespace JaneERP
{
    partial class FormAdjustStock
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
            lblProductInfo  = new Label();
            lblType         = new Label();
            rdoAdd          = new RadioButton();
            rdoRemove       = new RadioButton();
            lblQuantity     = new Label();
            txtQuantity     = new TextBox();
            lblStockPreview = new Label();
            lblLocation     = new Label();
            cboLocation     = new ComboBox();
            lblLotNumber    = new Label();
            txtLotNumber    = new TextBox();
            lblExpiry       = new Label();
            chkHasExpiry    = new CheckBox();
            dtpExpiry       = new DateTimePicker();
            lblNote         = new Label();
            txtNote         = new TextBox();
            btnSave         = new Button();
            btnCancel       = new Button();
            SuspendLayout();

            // lblProductInfo
            lblProductInfo.Location = new Point(15, 14);
            lblProductInfo.Size     = new Size(420, 40);
            lblProductInfo.Font     = new Font("Segoe UI", 9F, FontStyle.Bold);

            // lblType
            lblType.AutoSize = true;
            lblType.Location = new Point(15, 64);
            lblType.Text     = "Adjustment Type:";

            // rdoAdd
            rdoAdd.AutoSize = true;
            rdoAdd.Checked  = true;
            rdoAdd.Location = new Point(15, 82);
            rdoAdd.TabIndex = 0;
            rdoAdd.Text     = "Add Stock";

            // rdoRemove
            rdoRemove.AutoSize = true;
            rdoRemove.Location = new Point(130, 82);
            rdoRemove.TabIndex = 1;
            rdoRemove.Text     = "Remove Stock";

            // lblQuantity
            lblQuantity.AutoSize = true;
            lblQuantity.Location = new Point(15, 116);
            lblQuantity.Text     = "Quantity:";

            // txtQuantity
            txtQuantity.Location = new Point(140, 113);
            txtQuantity.Size     = new Size(100, 23);
            txtQuantity.TabIndex = 2;

            // lblStockPreview — live "Current: N → New: M" feedback
            lblStockPreview.AutoSize  = false;
            lblStockPreview.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblStockPreview.Location  = new Point(15, 143);
            lblStockPreview.Size      = new Size(425, 18);
            lblStockPreview.Text      = "";

            // ── Location (required) — shifted down 30px ──────────────────────────
            lblLocation.AutoSize = true;
            lblLocation.Location = new Point(15, 176);
            lblLocation.Text     = "Location  \u2736";   // star = required

            cboLocation.DropDownStyle = ComboBoxStyle.DropDownList;
            cboLocation.Location      = new Point(15, 194);
            cboLocation.Size          = new Size(420, 23);
            cboLocation.TabIndex      = 3;

            // ── Lot / Batch Number ───────────────────────────────────────────────
            lblLotNumber.AutoSize = true;
            lblLotNumber.Location = new Point(15, 232);
            lblLotNumber.Text     = "Lot / Batch Number:";

            txtLotNumber.Location    = new Point(15, 250);
            txtLotNumber.Size        = new Size(420, 23);
            txtLotNumber.TabIndex    = 4;
            txtLotNumber.PlaceholderText = "Optional";

            // ── Expiration Date ──────────────────────────────────────────────────
            lblExpiry.AutoSize = true;
            lblExpiry.Location = new Point(15, 288);
            lblExpiry.Text     = "Expiration Date:";

            chkHasExpiry.AutoSize = true;
            chkHasExpiry.Location = new Point(140, 286);
            chkHasExpiry.TabIndex = 5;
            chkHasExpiry.Text     = "Has Expiry";
            chkHasExpiry.CheckedChanged += ChkHasExpiry_CheckedChanged;

            dtpExpiry.Location  = new Point(15, 308);
            dtpExpiry.Size      = new Size(210, 23);
            dtpExpiry.TabIndex  = 6;
            dtpExpiry.Format    = DateTimePickerFormat.Short;
            dtpExpiry.Enabled   = false;

            // ── Note ─────────────────────────────────────────────────────────────
            lblNote.AutoSize = true;
            lblNote.Location = new Point(15, 346);
            lblNote.Text     = "Note (optional):";

            txtNote.Location  = new Point(15, 364);
            txtNote.Multiline = true;
            txtNote.Size      = new Size(420, 50);
            txtNote.TabIndex  = 7;

            // ── Buttons ──────────────────────────────────────────────────────────
            btnSave.Location = new Point(100, 432);
            btnSave.Size     = new Size(140, 30);
            btnSave.TabIndex = 8;
            btnSave.Text     = "Save Adjustment";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click   += btnSave_Click;

            btnCancel.Location = new Point(252, 432);
            btnCancel.Size     = new Size(110, 30);
            btnCancel.TabIndex = 9;
            btnCancel.Text     = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click   += btnCancel_Click;

            // FormAdjustStock
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode       = AutoScaleMode.Font;
            ClientSize          = new Size(460, 480);
            Controls.Add(lblProductInfo);
            Controls.Add(lblType);
            Controls.Add(rdoAdd);
            Controls.Add(rdoRemove);
            Controls.Add(lblQuantity);
            Controls.Add(txtQuantity);
            Controls.Add(lblStockPreview);
            Controls.Add(lblLocation);
            Controls.Add(cboLocation);
            Controls.Add(lblLotNumber);
            Controls.Add(txtLotNumber);
            Controls.Add(lblExpiry);
            Controls.Add(chkHasExpiry);
            Controls.Add(dtpExpiry);
            Controls.Add(lblNote);
            Controls.Add(txtNote);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;
            Text            = "Adjust Stock";
            ResumeLayout(false);
            PerformLayout();
        }

        private Label         lblProductInfo;
        private Label         lblType;
        private RadioButton   rdoAdd;
        private RadioButton   rdoRemove;
        private Label         lblQuantity;
        private TextBox       txtQuantity;
        private Label         lblStockPreview;
        private Label         lblLocation;
        private ComboBox      cboLocation;
        private Label         lblLotNumber;
        private TextBox       txtLotNumber;
        private Label         lblExpiry;
        private CheckBox      chkHasExpiry;
        private DateTimePicker dtpExpiry;
        private Label         lblNote;
        private TextBox       txtNote;
        private Button        btnSave;
        private Button        btnCancel;
    }
}
