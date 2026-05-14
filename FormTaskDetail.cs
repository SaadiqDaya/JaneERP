using JaneERP.Security;
using JaneERP.Services;
using System.Net.Mail;

namespace JaneERP
{
    /// <summary>Shows full task details — status, assignee, priority, workflow, comments with @mentions.</summary>
    internal class FormTaskDetail : Form
    {
        private readonly TaskRepository _repo;
        private readonly ErpTask        _task;
        private readonly List<string>   _users;

        private Label          lblTitle           = new();
        private Label          lblMeta            = new();
        private ListBox        lstComments        = new();
        private TextBox        txtComment         = new();
        private TextBox        txtDesc            = new();
        private Button         btnPostComment     = new();
        private Button         btnClose           = new();
        private DateTimePicker dtpDueDate         = new();
        private Button         btnSaveChanges     = new();
        /// <summary>Stage dropdown — shows workflow stages when a workflow is active, legacy statuses otherwise.</summary>
        private ComboBox       cboStage           = new();
        private Label          lblStageHint       = new();
        private ComboBox       cboAssign          = new();
        private ComboBox       cboPriority        = new();
        private ComboBox       cboWorkflow        = new();
        private Button         btnAdvanceWorkflow = new();

        // @-mention popup
        private ListBox lstMention     = new();
        private string  _mentionPrefix = "";

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
            Load += (_, _) =>
            {
                foreach (Control c in Controls)
                {
                    if (c is Panel p && p.Tag as string == "header")
                    {
                        Theme.MakeDraggable(this, p);
                        break;
                    }
                }
            };
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
            ClientSize    = new Size(700, 660);
            MinimumSize   = new Size(560, 580);
            StartPosition = FormStartPosition.CenterParent;

            // ── Header panel ──────────────────────────────────────────────────────────
            var pnlHeader = new Panel
            {
                Tag      = "header",
                Location = new Point(0, 0),
                Size     = new Size(700, 76),
                Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblTitle.Text      = _task.Title;
            lblTitle.Font      = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblTitle.ForeColor = Theme.Gold;
            lblTitle.Location  = new Point(12, 8);
            lblTitle.Size      = new Size(660, 28);
            pnlHeader.Controls.Add(lblTitle);

            lblMeta.Text      = BuildMetaText();
            lblMeta.Font      = new Font("Segoe UI", 8.5F);
            lblMeta.ForeColor = Theme.TextSecondary;
            lblMeta.Location  = new Point(12, 40);
            lblMeta.Size      = new Size(510, 18);
            lblMeta.AutoSize  = false;
            pnlHeader.Controls.Add(lblMeta);

            pnlHeader.Controls.Add(new Label
            {
                Text      = "Due:",
                Location  = new Point(532, 42),
                AutoSize  = true,
                ForeColor = Theme.TextSecondary,
                Font      = new Font("Segoe UI", 8.5F)
            });
            dtpDueDate.Location = new Point(556, 38);
            dtpDueDate.Size     = new Size(130, 23);
            dtpDueDate.Format   = DateTimePickerFormat.Short;
            dtpDueDate.Value    = _task.DueDate;
            pnlHeader.Controls.Add(dtpDueDate);

            Controls.Add(pnlHeader);

            // ── Properties row (Stage | Assigned | Priority) ──────────────────────────
            Controls.Add(new Label { Text = "Stage:", Location = new Point(12, 86), AutoSize = true });
            cboStage.DropDownStyle = ComboBoxStyle.DropDownList;
            cboStage.Location      = new Point(62, 82);
            cboStage.Size          = new Size(140, 23);
            // Initial population: legacy statuses (overridden by workflow once loaded)
            PopulateStageDropdown(null);
            Controls.Add(cboStage);

            lblStageHint.Location  = new Point(210, 86);
            lblStageHint.AutoSize  = true;
            lblStageHint.ForeColor = Theme.TextMuted;
            lblStageHint.Font      = new Font("Segoe UI", 7.5F);
            lblStageHint.Text      = "(select a workflow to use workflow stages)";
            Controls.Add(lblStageHint);

            Controls.Add(new Label { Text = "Assigned:", Location = new Point(192, 86), AutoSize = true });
            cboAssign.DropDownStyle = ComboBoxStyle.DropDownList;
            cboAssign.Location      = new Point(258, 82);
            cboAssign.Size          = new Size(170, 23);
            foreach (var u in _users) cboAssign.Items.Add(u);
            cboAssign.SelectedItem = _task.AssignedTo;
            if (cboAssign.SelectedIndex < 0 && cboAssign.Items.Count > 0) cboAssign.SelectedIndex = 0;
            Controls.Add(cboAssign);

            Controls.Add(new Label { Text = "Priority:", Location = new Point(442, 86), AutoSize = true });
            cboPriority.DropDownStyle = ComboBoxStyle.DropDownList;
            cboPriority.Location      = new Point(496, 82);
            cboPriority.Size          = new Size(100, 23);
            cboPriority.Items.AddRange(new object[] { "Low", "Normal", "High", "Urgent" });
            cboPriority.SelectedItem = _task.Priority;
            if (cboPriority.SelectedIndex < 0) cboPriority.SelectedIndex = 1;
            Controls.Add(cboPriority);

            // ── Description ───────────────────────────────────────────────────────────
            Controls.Add(new Label { Text = "Description:", Location = new Point(12, 118), AutoSize = true });
            txtDesc.Text       = _task.Description ?? "";
            txtDesc.Multiline  = true;
            txtDesc.ScrollBars = ScrollBars.Vertical;
            txtDesc.Location   = new Point(12, 136);
            txtDesc.Size       = new Size(676, 58);
            txtDesc.Anchor     = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(txtDesc);

            // ── Workflow section ──────────────────────────────────────────────────────
            Controls.Add(new Label { Text = "Workflow:", Location = new Point(12, 210), AutoSize = true });
            cboWorkflow.DropDownStyle = ComboBoxStyle.DropDownList;
            cboWorkflow.Location      = new Point(78, 206);
            cboWorkflow.Size          = new Size(184, 23);
            try
            {
                cboWorkflow.Items.Add(new WorkflowComboItem(null, "(None)"));
                foreach (var wf in _repo.GetWorkflows())
                    cboWorkflow.Items.Add(new WorkflowComboItem(wf.WorkflowID, wf.Name));
                if (_task.WorkflowID.HasValue)
                {
                    foreach (WorkflowComboItem item in cboWorkflow.Items)
                        if (item.ID == _task.WorkflowID) { cboWorkflow.SelectedItem = item; break; }
                }
                if (cboWorkflow.SelectedIndex < 0) cboWorkflow.SelectedIndex = 0;
            }
            catch
            {
                cboWorkflow.Items.Clear();
                cboWorkflow.Items.Add(new WorkflowComboItem(null, "(None)"));
                cboWorkflow.SelectedIndex = 0;
            }
            cboWorkflow.SelectedIndexChanged += CboWorkflow_SelectedIndexChanged;
            Controls.Add(cboWorkflow);

            btnAdvanceWorkflow.Text     = "→ Next Stage";
            btnAdvanceWorkflow.Size     = new Size(105, 23);
            btnAdvanceWorkflow.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnAdvanceWorkflow.Location = new Point(583, 206);
            btnAdvanceWorkflow.Visible  = false;
            btnAdvanceWorkflow.Click   += BtnAdvanceWorkflow_Click;
            Controls.Add(btnAdvanceWorkflow);

            // After workflow combo is wired, refresh stage dropdown to match the task's workflow/stage
            RefreshStageDropdown();

            // ── Discussion ────────────────────────────────────────────────────────────
            Controls.Add(new Label { Text = "Discussion (type @ to tag a user):", Location = new Point(12, 242), AutoSize = true });

            lstComments.Location      = new Point(12, 262);
            lstComments.Size          = new Size(676, 200);
            lstComments.Anchor        = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lstComments.SelectionMode = SelectionMode.None;
            Controls.Add(lstComments);

            txtComment.Location        = new Point(12, ClientSize.Height - 90);
            txtComment.Size            = new Size(600, 23);
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
            lstMention.Visible     = false;
            lstMention.Location    = new Point(12, ClientSize.Height - 130);
            lstMention.Size        = new Size(250, 120);
            lstMention.Anchor      = AnchorStyles.Bottom | AnchorStyles.Left;
            lstMention.BorderStyle = BorderStyle.FixedSingle;
            lstMention.Font        = new Font("Segoe UI", 9.5F);
            lstMention.Click      += LstMention_Click;
            lstMention.KeyDown    += LstMention_KeyDown;
            Controls.Add(lstMention);
            lstMention.BringToFront();

            btnSaveChanges.Text     = "Save Changes";
            btnSaveChanges.Size     = new Size(110, 28);
            btnSaveChanges.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnSaveChanges.Location = new Point(12, ClientSize.Height - 52);
            btnSaveChanges.UseVisualStyleBackColor = true;
            btnSaveChanges.Click   += BtnSaveChanges_Click;
            Controls.Add(btnSaveChanges);

            btnClose.Text     = "Close";
            btnClose.Size     = new Size(80, 28);
            btnClose.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(ClientSize.Width - 92, ClientSize.Height - 52);
            btnClose.Click   += (_, _) => Close();
            Controls.Add(btnClose);
        }

        private string BuildMetaText()
        {
            var stageDisplay = _task.WorkflowCurrentStatus ?? _task.Status;
            return $"Created by: {_task.CreatedBy}   •   Stage: {stageDisplay}   •   Priority: {_task.Priority}   •   Assigned: {_task.AssignedTo}";
        }

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

        // ── Workflow + Stage ─────────────────────────────────────────────────────────

        /// <summary>
        /// Populates <see cref="cboStage"/> based on the currently selected workflow.
        /// When no workflow is active, shows the legacy Open/In Progress/Done statuses.
        /// </summary>
        private void PopulateStageDropdown(List<string>? stages)
        {
            cboStage.Items.Clear();
            if (stages == null || stages.Count == 0)
            {
                // Legacy mode — no workflow
                cboStage.Items.AddRange(new object[] { "Open", "In Progress", "Done" });
                lblStageHint.Text = "(no workflow — using legacy statuses)";
                // Select from task's Status field
                cboStage.SelectedItem = _task.Status;
                if (cboStage.SelectedIndex < 0) cboStage.SelectedIndex = 0;
                btnAdvanceWorkflow.Visible = false;
            }
            else
            {
                foreach (var s in stages) cboStage.Items.Add(s);
                var current = _task.WorkflowCurrentStatus ?? stages[0];
                int idx = stages.IndexOf(current);
                cboStage.SelectedIndex = idx >= 0 ? idx : 0;

                int curIdx = cboStage.SelectedIndex;
                lblStageHint.Text = curIdx + 1 < stages.Count
                    ? $"Step {curIdx + 1}/{stages.Count}  (next: {stages[curIdx + 1]})"
                    : $"Step {stages.Count}/{stages.Count}  (final step)";
                btnAdvanceWorkflow.Text    = (curIdx + 1 < stages.Count) ? "→ Next Stage" : "Complete ✓";
                btnAdvanceWorkflow.Visible = true;
            }
        }

        /// <summary>Re-reads the workflow combo and refreshes Stage dropdown accordingly.</summary>
        private void RefreshStageDropdown()
        {
            if (cboWorkflow.SelectedItem is not WorkflowComboItem wfItem || wfItem.ID == null)
            {
                PopulateStageDropdown(null);
                return;
            }
            try
            {
                var statuses = _repo.GetWorkflowStatusNames(wfItem.ID.Value);
                PopulateStageDropdown(statuses.Count > 0 ? statuses : null);
            }
            catch
            {
                PopulateStageDropdown(null);
            }
        }

        private void CboWorkflow_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cboWorkflow.SelectedItem is not WorkflowComboItem wfItem) return;
            if (wfItem.ID == null)
            {
                if (_task.WorkflowID.HasValue)
                {
                    try { _repo.UpdateWorkflowStatus(_task.TaskID, null, null); } catch { }
                    _task.WorkflowID            = null;
                    _task.WorkflowCurrentStatus = null;
                    Changed = true;
                }
                RefreshStageDropdown();
                return;
            }
            try
            {
                var statuses = _repo.GetWorkflowStatusNames(wfItem.ID.Value);
                var initial  = statuses.FirstOrDefault();
                _repo.UpdateWorkflowStatus(_task.TaskID, wfItem.ID, initial);
                _task.WorkflowID            = wfItem.ID;
                _task.WorkflowCurrentStatus = initial;
                Changed = true;
                RefreshStageDropdown();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void BtnAdvanceWorkflow_Click(object? sender, EventArgs e)
        {
            if (cboWorkflow.SelectedItem is not WorkflowComboItem wfItem || wfItem.ID == null) return;
            try
            {
                var statuses = _repo.GetWorkflowStatusNames(wfItem.ID.Value);
                if (statuses.Count == 0) return;
                var current = _task.WorkflowCurrentStatus ?? statuses[0];
                var idx     = statuses.IndexOf(current);
                if (idx < 0) idx = 0;
                int nextIdx = idx + 1;
                if (nextIdx >= statuses.Count)
                {
                    if (MessageBox.Show(this, "This is the final workflow step. Mark task as Done?",
                            "Complete Workflow", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    _repo.UpdateWorkflowStatus(_task.TaskID, wfItem.ID, statuses[^1]);
                    _repo.UpdateStatus(_task.TaskID, "Done");
                    _task.WorkflowCurrentStatus = statuses[^1];
                    _task.Status = "Done";
                }
                else
                {
                    var next = statuses[nextIdx];
                    _repo.UpdateWorkflowStatus(_task.TaskID, wfItem.ID, next);
                    _task.WorkflowCurrentStatus = next;
                }
                Changed = true;
                RefreshStageDropdown();
                lblMeta.Text = BuildMetaText();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ── @mention logic ────────────────────────────────────────────────────────────

        private void TxtComment_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)27)
                lstMention.Visible = false;
        }

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
            int atIdx = text.LastIndexOf('@', Math.Max(0, pos - 1));
            if (atIdx >= 0)
            {
                var partial = text.Substring(atIdx + 1, Math.Max(0, pos - atIdx - 1));
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

        private void LstMention_Click(object? sender, EventArgs e) => InsertMention();

        private void LstMention_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab) { InsertMention(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { lstMention.Visible = false; txtComment.Focus(); }
        }

        private void InsertMention()
        {
            if (lstMention.SelectedItem is not string selected) return;
            var text  = txtComment.Text;
            var pos   = txtComment.SelectionStart;
            int atIdx = text.LastIndexOf('@', Math.Max(0, pos - 1));
            if (atIdx < 0) return;
            var before = text.Substring(0, atIdx);
            var after  = text.Substring(pos);
            txtComment.Text           = before + "@" + selected + " " + after;
            txtComment.SelectionStart = before.Length + selected.Length + 2;
            lstMention.Visible        = false;
            txtComment.Focus();
        }

        // ── Post comment ──────────────────────────────────────────────────────────────

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
                SaveMentions(body, user);
                SendMentionEmails(body, user);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
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
            var mentions = System.Text.RegularExpressions.Regex.Matches(commentBody, @"@(\w+)")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (mentions.Count == 0) return;
            List<(string username, string email)> targets;
            try { targets = _repo.GetUserEmails(mentions); } catch { return; }
            var subject  = $"[JaneERP] You were mentioned in task: {_task.Title}";
            var stageDisplay = _task.WorkflowCurrentStatus ?? _task.Status;
            var bodyText =
                $"You were mentioned by {postedBy} in a task comment.\n\n" +
                $"Task: {_task.Title}\nAssigned to: {_task.AssignedTo}\nDue: {_task.DueDate:yyyy-MM-dd}\nStage: {stageDisplay}\n\n" +
                $"Comment:\n{commentBody}\n\nPlease log in to JaneERP to view the full discussion.";
            foreach (var (_, email) in targets)
            {
                if (string.IsNullOrWhiteSpace(email)) continue;
                try
                {
                    using var smtp   = new SmtpClient(cfg.SmtpServer, cfg.SmtpPort);
                    smtp.EnableSsl   = cfg.SmtpUseSsl;
                    smtp.Credentials = new System.Net.NetworkCredential(cfg.SmtpUser, cfg.SmtpPasswordPlain);
                    using var msg    = new MailMessage(cfg.FromEmail, email, subject, bodyText);
                    smtp.Send(msg);
                }
                catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormTaskDetail.SendMentionEmails]: {ex.Message}"); }
            }
        }

        // ── Save Changes ──────────────────────────────────────────────────────────────

        private void BtnSaveChanges_Click(object? sender, EventArgs e)
        {
            try
            {
                // Due date
                _repo.UpdateDueDate(_task.TaskID, dtpDueDate.Value.Date);
                _task.DueDate = dtpDueDate.Value.Date;

                // Description
                string newDesc = txtDesc.Text.Trim();
                _repo.UpdateDescription(_task.TaskID, newDesc);
                _task.Description = newDesc;

                // Stage / Status
                // If a workflow is active, save the selected stage as WorkflowCurrentStatus
                // and derive Status from it (Done if last stage, otherwise In Progress/Open).
                var stageVal = cboStage.SelectedItem?.ToString() ?? "";
                if (cboWorkflow.SelectedItem is WorkflowComboItem wfi && wfi.ID != null)
                {
                    // Workflow active — save stage
                    if (stageVal != _task.WorkflowCurrentStatus)
                    {
                        _repo.UpdateWorkflowStatus(_task.TaskID, wfi.ID, stageVal);
                        _task.WorkflowCurrentStatus = stageVal;
                        // Auto-derive legacy Status
                        var allStages = _repo.GetWorkflowStatusNames(wfi.ID.Value);
                        bool isFinal = allStages.Count > 0 && stageVal == allStages[^1];
                        var derived  = isFinal ? "Done" : (allStages.IndexOf(stageVal) == 0 ? "Open" : "In Progress");
                        if (derived != _task.Status) { _repo.UpdateStatus(_task.TaskID, derived); _task.Status = derived; }
                        RefreshStageDropdown();
                    }
                }
                else
                {
                    // No workflow — stage combo holds legacy statuses
                    var newStatus = stageVal is "Open" or "In Progress" or "Done" ? stageVal : "Open";
                    if (newStatus != _task.Status) { _repo.UpdateStatus(_task.TaskID, newStatus); _task.Status = newStatus; }
                }

                // Assigned To
                var newAssigned = cboAssign.SelectedItem?.ToString() ?? _task.AssignedTo;
                if (!newAssigned.Equals(_task.AssignedTo, StringComparison.OrdinalIgnoreCase))
                {
                    _repo.UpdateAssignedTo(_task.TaskID, newAssigned);
                    if (!newAssigned.Equals(AppSession.CurrentUser?.Username, StringComparison.OrdinalIgnoreCase))
                    {
                        var emails = _repo.GetUserEmails(new List<string> { newAssigned });
                        var email  = emails.FirstOrDefault().Email;
                        if (!string.IsNullOrWhiteSpace(email))
                            _ = NotificationService.NotifyTaskAssignedAsync(email, AppSession.CurrentUser?.Username ?? "system", _task.Title);
                    }
                    _task.AssignedTo = newAssigned;
                }

                // Priority
                var newPriority = cboPriority.SelectedItem?.ToString() ?? "Normal";
                if (newPriority != _task.Priority)
                {
                    _repo.UpdatePriority(_task.TaskID, newPriority);
                    _task.Priority = newPriority;
                }

                Changed      = true;
                lblMeta.Text = BuildMetaText();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ── Helper ────────────────────────────────────────────────────────────────────

        private class WorkflowComboItem
        {
            public int?   ID      { get; }
            public string Display { get; }
            public WorkflowComboItem(int? id, string display) { ID = id; Display = display; }
            public override string ToString() => Display;
        }
    }
}
