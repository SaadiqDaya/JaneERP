using JaneERP.Security;
using System.Net.Mail;

namespace JaneERP
{
    /// <summary>Shows full task details and allows posting comments with @mention support.</summary>
    internal class FormTaskDetail : Form
    {
        private readonly TaskRepository _repo;
        private readonly ErpTask        _task;
        private readonly List<string>   _users;

        private Label    lblTitle       = new();
        private Label    lblMeta        = new();
        private ListBox  lstComments    = new();
        private TextBox  txtComment     = new();
        private Button   btnPostComment = new();
        private Button   btnMarkDone    = new();
        private Button   btnClose       = new();

        // @-mention popup
        private ListBox  lstMention     = new();
        private string   _mentionPrefix = "";

        public bool Changed { get; private set; }

        public FormTaskDetail(TaskRepository repo, ErpTask task)
        {
            _repo  = repo;
            _task  = task;
            _users = TryGetUsers(repo);
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            LoadComments();
        }

        private static List<string> TryGetUsers(TaskRepository repo)
        {
            try { return repo.GetAllUsernames(); }
            catch { return new List<string>(); }
        }

        private void BuildUI()
        {
            Text          = $"Task: {_task.Title}";
            ClientSize    = new Size(620, 560);
            MinimumSize   = new Size(520, 460);
            StartPosition = FormStartPosition.CenterParent;

            lblTitle.Text      = _task.Title;
            lblTitle.Font      = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblTitle.ForeColor = Theme.Gold;
            lblTitle.Location  = new Point(12, 12);
            lblTitle.Size      = new Size(580, 28);
            Controls.Add(lblTitle);

            lblMeta.Text      = BuildMetaText();
            lblMeta.Font      = new Font("Segoe UI", 8.5F);
            lblMeta.ForeColor = Theme.TextSecondary;
            lblMeta.Location  = new Point(12, 44);
            lblMeta.Size      = new Size(580, 18);
            lblMeta.AutoSize  = false;
            Controls.Add(lblMeta);

            Controls.Add(new Label { Text = "Description:", Location = new Point(12, 70), AutoSize = true });
            var txtDesc = new TextBox
            {
                Text       = _task.Description ?? "(none)",
                ReadOnly   = true,
                Multiline  = true,
                ScrollBars = ScrollBars.Vertical,
                Location   = new Point(12, 90),
                Size       = new Size(596, 80),
                Anchor     = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(txtDesc);

            Controls.Add(new Label { Text = "Discussion (type @ to tag a user):", Location = new Point(12, 180), AutoSize = true });

            lstComments.Location      = new Point(12, 200);
            lstComments.Size          = new Size(596, 230);
            lstComments.Anchor        = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lstComments.SelectionMode = SelectionMode.None;
            Controls.Add(lstComments);

            txtComment.Location        = new Point(12, ClientSize.Height - 90);
            txtComment.Size            = new Size(490, 23);
            txtComment.Anchor          = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtComment.PlaceholderText = "Write a comment… (type @ to tag a user)";
            txtComment.KeyPress       += TxtComment_KeyPress;
            txtComment.TextChanged    += TxtComment_TextChanged;
            Controls.Add(txtComment);

            btnPostComment.Text     = "Post";
            btnPostComment.Size     = new Size(70, 23);
            btnPostComment.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnPostComment.Location = new Point(ClientSize.Width - 82, ClientSize.Height - 90);
            btnPostComment.Click   += BtnPostComment_Click;
            Controls.Add(btnPostComment);

            // @mention popup listbox (hidden by default)
            lstMention.Visible       = false;
            lstMention.Location      = new Point(12, ClientSize.Height - 130);
            lstMention.Size          = new Size(250, 120);
            lstMention.Anchor        = AnchorStyles.Bottom | AnchorStyles.Left;
            lstMention.BorderStyle   = BorderStyle.FixedSingle;
            lstMention.Font          = new Font("Segoe UI", 9.5F);
            lstMention.Click        += LstMention_Click;
            lstMention.KeyDown      += LstMention_KeyDown;
            Controls.Add(lstMention);
            lstMention.BringToFront();

            btnMarkDone.Text     = _task.Status == "Done" ? "Re-open" : "Mark Done";
            btnMarkDone.Size     = new Size(110, 28);
            btnMarkDone.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnMarkDone.Location = new Point(12, ClientSize.Height - 52);
            btnMarkDone.Click   += BtnMarkDone_Click;
            Controls.Add(btnMarkDone);

            btnClose.Text     = "Close";
            btnClose.Size     = new Size(80, 28);
            btnClose.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(ClientSize.Width - 92, ClientSize.Height - 52);
            btnClose.Click   += (_, _) => Close();
            Controls.Add(btnClose);
        }

        private string BuildMetaText() =>
            $"Assigned to: {_task.AssignedTo}   •   Due: {_task.DueDate:yyyy-MM-dd}   •   Status: {_task.Status}   •   Created by: {_task.CreatedBy}";

        private void LoadComments()
        {
            lstComments.Items.Clear();
            try
            {
                foreach (var c in _repo.GetComments(_task.TaskID))
                    lstComments.Items.Add($"[{c.CreatedAt:yyyy-MM-dd HH:mm}] {c.Username}: {c.Body}");
            }
            catch { lstComments.Items.Add("(Could not load comments)"); }
        }

        // ── @mention logic ────────────────────────────────────────────────────────

        private void TxtComment_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)27) // Escape closes mention popup
            {
                lstMention.Visible = false;
            }
        }

        // Down/Up arrow while the mention popup is visible shifts focus into it
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (lstMention.Visible)
            {
                if (keyData == Keys.Down)
                {
                    lstMention.Focus();
                    if (lstMention.Items.Count > 0 && lstMention.SelectedIndex < 0)
                        lstMention.SelectedIndex = 0;
                    return true;
                }
                if (keyData == Keys.Escape)
                {
                    lstMention.Visible = false;
                    txtComment.Focus();
                    return true;
                }
                if (keyData == Keys.Enter && lstMention.Focused)
                {
                    InsertMention();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void TxtComment_TextChanged(object? sender, EventArgs e)
        {
            var text = txtComment.Text;
            var pos  = txtComment.SelectionStart;

            // Find the last '@' before cursor
            int atIdx = text.LastIndexOf('@', Math.Max(0, pos - 1));
            if (atIdx >= 0)
            {
                var partial = text.Substring(atIdx + 1, Math.Max(0, pos - atIdx - 1));
                // Only show if no space in partial (still typing the username)
                if (!partial.Contains(' '))
                {
                    _mentionPrefix = partial;
                    var matches = _users
                        .Where(u => u.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (matches.Count > 0)
                    {
                        lstMention.Items.Clear();
                        foreach (var m in matches) lstMention.Items.Add(m);
                        lstMention.Visible = true;
                        return;
                    }
                }
            }
            lstMention.Visible = false;
        }

        private void LstMention_Click(object? sender, EventArgs e)
        {
            InsertMention();
        }

        private void LstMention_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
            {
                InsertMention();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                lstMention.Visible = false;
                txtComment.Focus();
            }
        }

        private void InsertMention()
        {
            if (lstMention.SelectedItem is not string selected) return;
            var text = txtComment.Text;
            var pos  = txtComment.SelectionStart;
            int atIdx = text.LastIndexOf('@', Math.Max(0, pos - 1));
            if (atIdx < 0) return;

            // Replace from @ to cursor with @username + space
            var before = text.Substring(0, atIdx);
            var after  = text.Substring(pos);
            txtComment.Text           = before + "@" + selected + " " + after;
            txtComment.SelectionStart = before.Length + selected.Length + 2;
            lstMention.Visible        = false;
            txtComment.Focus();
        }

        // ── Post comment ──────────────────────────────────────────────────────────

        private void BtnPostComment_Click(object? sender, EventArgs e)
        {
            var body = txtComment.Text.Trim();
            if (string.IsNullOrWhiteSpace(body)) return;
            var user = AppSession.CurrentUser?.Username ?? "system";
            try
            {
                _repo.AddComment(_task.TaskID, user, body);
                txtComment.Clear();
                LoadComments();
                lstComments.TopIndex = lstComments.Items.Count - 1;

                // Record and email any @mentioned users
                SaveMentions(body, user);
                SendMentionEmails(body, user);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveMentions(string commentBody, string postedBy)
        {
            var mentions = System.Text.RegularExpressions.Regex.Matches(commentBody, @"@(\w+)")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var username in mentions)
            {
                try { _repo.AddMention(_task.TaskID, username, postedBy, commentBody); }
                catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormTaskDetail.SaveMentions]: {ex.Message}"); }
            }
        }

        private void SendMentionEmails(string commentBody, string postedBy)
        {
            var cfg = AppSettings.Current;
            if (!cfg.IsEmailConfigured) return;

            // Extract @mentions
            var mentions = System.Text.RegularExpressions.Regex.Matches(commentBody, @"@(\w+)")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (mentions.Count == 0) return;

            // Get emails for mentioned users
            List<(string username, string email)> targets;
            try { targets = _repo.GetUserEmails(mentions); }
            catch { return; }

            var subject = $"[JaneERP] You were mentioned in task: {_task.Title}";
            var bodyText =
                $"You were mentioned by {postedBy} in a task comment.\n\n" +
                $"Task: {_task.Title}\n" +
                $"Assigned to: {_task.AssignedTo}\n" +
                $"Due: {_task.DueDate:yyyy-MM-dd}\n" +
                $"Status: {_task.Status}\n\n" +
                $"Comment:\n{commentBody}\n\n" +
                "Please log in to JaneERP to view the full discussion.";

            foreach (var (_, email) in targets)
            {
                if (string.IsNullOrWhiteSpace(email)) continue;
                try
                {
                    using var smtp   = new SmtpClient(cfg.SmtpServer, cfg.SmtpPort);
                    smtp.EnableSsl   = cfg.SmtpUseSsl;
                    smtp.Credentials = new System.Net.NetworkCredential(cfg.SmtpUser, cfg.SmtpPassword);
                    using var msg    = new MailMessage(cfg.FromEmail, email, subject, bodyText);
                    smtp.Send(msg);
                }
                catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormTaskDetail.SendMentionEmails]: {ex.Message}"); }
            }
        }

        // ── Mark Done / Re-open ───────────────────────────────────────────────────

        private void BtnMarkDone_Click(object? sender, EventArgs e)
        {
            var newStatus = _task.Status == "Done" ? "Open" : "Done";
            try
            {
                _repo.UpdateStatus(_task.TaskID, newStatus);
                _task.Status     = newStatus;
                btnMarkDone.Text = newStatus == "Done" ? "Re-open" : "Mark Done";
                Changed          = true;
                lblMeta.Text     = BuildMetaText();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
