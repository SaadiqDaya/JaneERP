namespace JaneERP
{
    partial class FormAppLogin
    {
        private System.ComponentModel.IContainer components = null;

        // Left panel
        private PictureBox pbMascot;

        // Right panel and its children
        private Panel   pnlRight;
        private PictureBox pbLogo;
        private Label   lblTitle;
        private Label   lblSubtitle;
        private Label   lblUsername;
        private TextBox txtUsername;
        private Label   lblPassword;
        private TextBox txtPassword;
        private Label   lblConfirmPassword;
        private TextBox txtConfirmPassword;
        private Button  btnLogin;
        private Label   lblPoweredBy;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            pbMascot           = new PictureBox();
            pnlRight           = new Panel();
            pbLogo             = new PictureBox();
            lblTitle           = new Label();
            lblSubtitle        = new Label();
            lblUsername        = new Label();
            txtUsername        = new TextBox();
            lblPassword        = new Label();
            txtPassword        = new TextBox();
            lblConfirmPassword = new Label();
            txtConfirmPassword = new TextBox();
            btnLogin           = new Button();
            lblPoweredBy       = new Label();

            ((System.ComponentModel.ISupportInitialize)pbMascot).BeginInit();
            ((System.ComponentModel.ISupportInitialize)pbLogo).BeginInit();
            pnlRight.SuspendLayout();
            SuspendLayout();

            // ── Mascot (left panel) ───────────────────────────────────────────────
            pbMascot.Dock     = DockStyle.Left;
            pbMascot.Width    = 320;
            pbMascot.SizeMode = PictureBoxSizeMode.Zoom;
            pbMascot.BackColor = Theme.Background;

            // ── Right panel ───────────────────────────────────────────────────────
            pnlRight.Location  = new Point(320, 0);
            pnlRight.Size      = new Size(440, 480);
            pnlRight.BackColor = Theme.Surface;

            // pbLogo (company logo, white background)
            pbLogo.Location   = new Point(120, 24);
            pbLogo.Size       = new Size(200, 58);
            pbLogo.SizeMode   = PictureBoxSizeMode.Zoom;
            pbLogo.BackColor  = Color.White;
            pbLogo.BorderStyle = BorderStyle.None;

            // lblTitle — "JaneERP" in violet
            lblTitle.Font      = new Font("Segoe UI", 22F, FontStyle.Bold);
            lblTitle.ForeColor = Theme.Gold;
            lblTitle.Location  = new Point(0, 100);
            lblTitle.Size      = new Size(440, 42);
            lblTitle.Text      = "JaneERP";
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;

            // lblSubtitle
            lblSubtitle.Font      = new Font("Segoe UI", 9F);
            lblSubtitle.ForeColor = Theme.TextSecondary;
            lblSubtitle.Location  = new Point(0, 146);
            lblSubtitle.Size      = new Size(440, 20);
            lblSubtitle.Text      = "Sign in to your account";
            lblSubtitle.TextAlign = ContentAlignment.MiddleCenter;

            // lblUsername
            lblUsername.AutoSize  = true;
            lblUsername.Font      = new Font("Segoe UI", 9F);
            lblUsername.ForeColor = Theme.TextSecondary;
            lblUsername.Location  = new Point(65, 184);
            lblUsername.Text      = "Username";

            // txtUsername
            txtUsername.Font      = new Font("Segoe UI", 10F);
            txtUsername.Location  = new Point(65, 202);
            txtUsername.Size      = new Size(310, 27);
            txtUsername.TabIndex  = 0;
            txtUsername.BackColor = Theme.InputBg;
            txtUsername.ForeColor = Theme.TextPrimary;
            txtUsername.BorderStyle = BorderStyle.FixedSingle;

            // lblPassword
            lblPassword.AutoSize  = true;
            lblPassword.Font      = new Font("Segoe UI", 9F);
            lblPassword.ForeColor = Theme.TextSecondary;
            lblPassword.Location  = new Point(65, 242);
            lblPassword.Text      = "Password";

            // txtPassword
            txtPassword.Font      = new Font("Segoe UI", 10F);
            txtPassword.Location  = new Point(65, 260);
            txtPassword.Size      = new Size(310, 27);
            txtPassword.TabIndex  = 1;
            txtPassword.UseSystemPasswordChar = true;
            txtPassword.BackColor = Theme.InputBg;
            txtPassword.ForeColor = Theme.TextPrimary;
            txtPassword.BorderStyle = BorderStyle.FixedSingle;

            // lblConfirmPassword
            lblConfirmPassword.AutoSize  = true;
            lblConfirmPassword.Font      = new Font("Segoe UI", 9F);
            lblConfirmPassword.ForeColor = Theme.TextSecondary;
            lblConfirmPassword.Location  = new Point(65, 300);
            lblConfirmPassword.Text      = "Confirm Password";
            lblConfirmPassword.Visible   = false;

            // txtConfirmPassword
            txtConfirmPassword.Font      = new Font("Segoe UI", 10F);
            txtConfirmPassword.Location  = new Point(65, 318);
            txtConfirmPassword.Size      = new Size(310, 27);
            txtConfirmPassword.TabIndex  = 2;
            txtConfirmPassword.UseSystemPasswordChar = true;
            txtConfirmPassword.BackColor = Theme.InputBg;
            txtConfirmPassword.ForeColor = Theme.TextPrimary;
            txtConfirmPassword.BorderStyle = BorderStyle.FixedSingle;
            txtConfirmPassword.Visible   = false;

            // btnLogin
            btnLogin.Font      = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnLogin.Location  = new Point(65, 362);
            btnLogin.Size      = new Size(310, 40);
            btnLogin.TabIndex  = 3;
            btnLogin.Text      = "Sign In";
            btnLogin.FlatStyle = FlatStyle.Flat;
            btnLogin.FlatAppearance.BorderColor        = Theme.Gold;
            btnLogin.FlatAppearance.MouseOverBackColor = Theme.GoldDark;
            btnLogin.BackColor = Theme.Gold;
            btnLogin.ForeColor = Color.White;
            btnLogin.Cursor    = Cursors.Hand;
            btnLogin.Click    += btnLogin_Click;

            // lblPoweredBy
            lblPoweredBy.Font      = new Font("Segoe UI", 8F);
            lblPoweredBy.ForeColor = Color.FromArgb(32, 184, 204);
            lblPoweredBy.Location  = new Point(0, 450);
            lblPoweredBy.Size      = new Size(440, 20);
            lblPoweredBy.Text      = "Powered by Jvnction";
            lblPoweredBy.TextAlign = ContentAlignment.MiddleCenter;

            // ── Assemble pnlRight ─────────────────────────────────────────────────
            pnlRight.Controls.Add(pbLogo);
            pnlRight.Controls.Add(lblTitle);
            pnlRight.Controls.Add(lblSubtitle);
            pnlRight.Controls.Add(lblUsername);
            pnlRight.Controls.Add(txtUsername);
            pnlRight.Controls.Add(lblPassword);
            pnlRight.Controls.Add(txtPassword);
            pnlRight.Controls.Add(lblConfirmPassword);
            pnlRight.Controls.Add(txtConfirmPassword);
            pnlRight.Controls.Add(btnLogin);
            pnlRight.Controls.Add(lblPoweredBy);

            // ── FormAppLogin ──────────────────────────────────────────────────────
            AcceptButton        = btnLogin;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode       = AutoScaleMode.Font;
            BackColor           = Theme.Background;
            ClientSize          = new Size(760, 480);
            FormBorderStyle     = FormBorderStyle.FixedSingle;
            MaximizeBox         = false;
            MinimizeBox         = false;
            Name                = "FormAppLogin";
            StartPosition       = FormStartPosition.CenterScreen;
            Text                = "JaneERP — Sign In";
            Controls.Add(pbMascot);
            Controls.Add(pnlRight);
            Load += FormAppLogin_Load;

            ((System.ComponentModel.ISupportInitialize)pbMascot).EndInit();
            ((System.ComponentModel.ISupportInitialize)pbLogo).EndInit();
            pnlRight.ResumeLayout(false);
            pnlRight.PerformLayout();
            ResumeLayout(false);
        }
    }
}
