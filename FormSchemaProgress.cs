using JaneERP.Data;

namespace JaneERP
{
    /// <summary>
    /// Splash-style progress screen shown while SchemaBootstrap.Run() creates or updates
    /// all database tables.  Call the static <see cref="RunWithProgress"/> helper instead
    /// of instantiating directly.
    /// </summary>
    public class FormSchemaProgress : Form
    {
        private readonly Label       _lblTitle;
        private readonly Label       _lblStep;
        private readonly Label       _lblCount;
        private readonly ProgressBar _bar;

        private FormSchemaProgress()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.CenterScreen;
            ClientSize      = new Size(520, 180);
            BackColor       = Theme.Background;

            // ── thin dark top bar ──────────────────────────────────────────────
            var topBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 38,
                BackColor = Color.FromArgb(30, 20, 50)
            };
            var lblBar = new Label
            {
                Text      = "JaneERP  —  Database Setup",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Theme.Gold,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(12, 0, 0, 0)
            };
            topBar.Controls.Add(lblBar);

            // ── body ──────────────────────────────────────────────────────────
            _lblTitle = new Label
            {
                Text      = "Setting up database, please wait…",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                AutoSize  = false,
                Size      = new Size(488, 28),
                Location  = new Point(16, 50),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _lblStep = new Label
            {
                Text      = "Initialising…",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                AutoSize  = false,
                Size      = new Size(370, 22),
                Location  = new Point(16, 84)
            };

            _lblCount = new Label
            {
                Text      = "0 / 72",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                AutoSize  = false,
                Size      = new Size(118, 22),
                Location  = new Point(386, 84),
                TextAlign = ContentAlignment.MiddleRight
            };

            _bar = new ProgressBar
            {
                Location = new Point(16, 114),
                Size     = new Size(488, 18),
                Style    = ProgressBarStyle.Continuous,
                Minimum  = 0,
                Maximum  = 72,
                Value    = 0
            };

            Controls.AddRange(new Control[] { topBar, _lblTitle, _lblStep, _lblCount, _bar });
        }

        private void UpdateProgress(string stepName, int current, int total)
        {
            _bar.Maximum   = total;
            _bar.Value     = current;
            _lblStep.Text  = stepName;
            _lblCount.Text = $"{current} / {total}";
            Application.DoEvents();
        }

        /// <summary>
        /// Shows the progress screen, runs <see cref="SchemaBootstrap.Run"/> synchronously
        /// with live step updates, then closes the screen.
        /// </summary>
        public static void RunWithProgress(string connectionString)
        {
            var frm = new FormSchemaProgress();
            frm.Show();
            Application.DoEvents();

            try
            {
                SchemaBootstrap.Run(connectionString,
                    (name, cur, total) => frm.UpdateProgress(name, cur, total));
            }
            finally
            {
                frm.UpdateProgress("Complete", 72, 72);
                System.Threading.Thread.Sleep(300); // brief pause so user sees "72/72"
                frm.Close();
                frm.Dispose();
            }
        }
    }
}
