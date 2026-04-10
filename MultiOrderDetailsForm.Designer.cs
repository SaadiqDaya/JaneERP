namespace JaneERP
{
    partial class MultiOrderDetailsForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.DataGridView dgvCombined;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnExport;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.dgvCombined = new System.Windows.Forms.DataGridView();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnExport = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dgvCombined)).BeginInit();
            this.SuspendLayout();
            // 
            // dgvCombined
            // 
            this.dgvCombined.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
                                        | System.Windows.Forms.AnchorStyles.Left) 
                                        | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvCombined.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvCombined.Location = new System.Drawing.Point(12, 12);
            this.dgvCombined.Name = "dgvCombined";
            this.dgvCombined.RowTemplate.Height = 25;
            this.dgvCombined.Size = new System.Drawing.Size(760, 392);
            this.dgvCombined.TabIndex = 0;
            this.dgvCombined.ReadOnly = true;
            this.dgvCombined.AllowUserToAddRows = false;
            this.dgvCombined.AllowUserToDeleteRows = false;
            this.dgvCombined.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(697, 416);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(75, 26);
            this.btnClose.TabIndex = 1;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler((s, e) => this.Close());
            // 
            // btnExport
            // 
            this.btnExport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnExport.Location = new System.Drawing.Point(616, 416);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(75, 26);
            this.btnExport.TabIndex = 2;
            this.btnExport.Text = "Export CSV";
            this.btnExport.UseVisualStyleBackColor = true;
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            // 
            // MultiOrderDetailsForm
            // 
            this.ClientSize = new System.Drawing.Size(784, 454);
            this.Controls.Add(this.btnExport);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.dgvCombined);
            this.Name = "MultiOrderDetailsForm";
            this.Text = "Selected Orders - Items";
            ((System.ComponentModel.ISupportInitialize)(this.dgvCombined)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion
    }
}