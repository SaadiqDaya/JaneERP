using JaneERP.Infrastructure;
using JaneERP.Interfaces;
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

        // Linked records (multi)
        private DataGridView dgvLinks     = new();
        private Button       btnAddLink   = new();
        private Button       btnViewLink  = new();
        private Button       btnRemoveLink= new();

        // Subtasks
        private CheckedListBox clbSubtasks        = new();
        private TextBox        txtNewSubtask       = new();
        private Button         btnAddSubtask       = new();
        private Label          lblSubtaskCount     = new();
        private List<TaskSubtask> _subtasks        = new();

        // Activity log
        private ListBox lstHistory = new();

        // Recurrence
        private ComboBox       cboRecurrence  = new();
        private NumericUpDown  nudInterval    = new();
        private Label          lblNextOccur   = new();

        // Tags
        private TextBox txtTags = new();

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
            LoadLinkedRecords();
            LoadSubtasks();
            LoadHistory();
        }

        private static List<string> TryGetUsers(TaskRepository repo)
        {
            try { return repo.GetAllUsernames(); }
            catch { return new List<string>(); }
        }

        private void BuildUI()
        {
            Text          = $"Task: {_task.Title}";
            ClientSize    = new Size(700, 960);
            MinimumSize   = new Size(560, 700);
            StartPosition = FormStartPosition.CenterParent;
            AutoScroll    = true;

            // ── Header panel (y = 0–80) ───────────────────────────────────────────────
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
            lblMeta.Location  = new Point(12, 42);
            lblMeta.Size      = new Size(676, 18);
            lblMeta.AutoSize  = false;
            pnlHeader.Controls.Add(lblMeta);

            Controls.Add(pnlHeader);

            // ── Properties row (Stage | Assigned | Priority) — y=85–120 ──────────────
            // Row 1: Stage / Assigned / Priority dropdowns
            Controls.Add(new Label { Text = "Stage:", Location = new Point(12, 90), AutoSize = true });
            cboStage.DropDownStyle = ComboBoxStyle.DropDownList;
            cboStage.Location      = new Point(62, 86);
            cboStage.Size          = new Size(140, 23);
            PopulateStageDropdown(null);
            Controls.Add(cboStage);

            Controls.Add(new Label { Text = "Assigned:", Location = new Point(216, 90), AutoSize = true });
            cboAssign.DropDownStyle = ComboBoxStyle.DropDownList;
            cboAssign.Location      = new Point(280, 86);
            cboAssign.Size          = new Size(170, 23);
            foreach (var u in _users) cboAssign.Items.Add(u);
            cboAssign.SelectedItem = _task.AssignedTo;
            if (cboAssign.SelectedIndex < 0 && cboAssign.Items.Count > 0) cboAssign.SelectedIndex = 0;
            Controls.Add(cboAssign);

            Controls.Add(new Label { Text = "Priority:", Location = new Point(464, 90), AutoSize = true });
            cboPriority.DropDownStyle = ComboBoxStyle.DropDownList;
            cboPriority.Location      = new Point(518, 86);
            cboPriority.Size          = new Size(100, 23);
            cboPriority.Items.AddRange(new object[] { "Low", "Normal", "High", "Urgent" });
            cboPriority.SelectedItem = _task.Priority;
            if (cboPriority.SelectedIndex < 0) cboPriority.SelectedIndex = 1;
            Controls.Add(cboPriority);

            // Row 2: Stage hint label (own line, y=116)
            lblStageHint.Location  = new Point(12, 116);
            lblStageHint.AutoSize  = true;
            lblStageHint.ForeColor = Theme.TextMuted;
            lblStageHint.Font      = new Font("Segoe UI", 7.5F);
            lblStageHint.Text      = "(select a workflow to use workflow stages)";
            Controls.Add(lblStageHint);

            // ── Due Date — y=133–160 ──────────────────────────────────────────────────
            Controls.Add(new Label { Text = "Due Date:", Location = new Point(12, 137), AutoSize = true });
            dtpDueDate.Location = new Point(80, 133);
            dtpDueDate.Size     = new Size(160, 23);
            dtpDueDate.Format   = DateTimePickerFormat.Short;
            dtpDueDate.Value    = _task.DueDate;
            Controls.Add(dtpDueDate);

            // ── Tags — y=163–188 ──────────────────────────────────────────────────────
            Controls.Add(new Label { Text = "Tags:", Location = new Point(12, 167), AutoSize = true });
            txtTags.Location        = new Point(52, 163);
            txtTags.Size            = new Size(450, 23);
            txtTags.Anchor          = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtTags.PlaceholderText = "e.g. urgent, customer-facing, blocked";
            txtTags.Text            = _task.Tags ?? "";
            Controls.Add(txtTags);

            // ── Description — y=195–255 ───────────────────────────────────────────────
            Controls.Add(new Label { Text = "Description:", Location = new Point(12, 195), AutoSize = true });
            txtDesc.Text       = _task.Description ?? "";
            txtDesc.Multiline  = true;
            txtDesc.ScrollBars = ScrollBars.Vertical;
            txtDesc.Location   = new Point(12, 213);
            txtDesc.Size       = new Size(676, 58);
            txtDesc.Anchor     = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(txtDesc);

            // ── Workflow section — y=278–315 ──────────────────────────────────────────
            Controls.Add(new Label { Text = "Workflow:", Location = new Point(12, 282), AutoSize = true });
            cboWorkflow.DropDownStyle = ComboBoxStyle.DropDownList;
            cboWorkflow.Location      = new Point(80, 278);
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
            btnAdvanceWorkflow.Location = new Point(583, 278);
            btnAdvanceWorkflow.Visible  = false;
            btnAdvanceWorkflow.Click   += BtnAdvanceWorkflow_Click;
            Controls.Add(btnAdvanceWorkflow);

            // After workflow combo is wired, refresh stage dropdown to match the task's workflow/stage
            RefreshStageDropdown();

            // ── Recurrence section — y=312–350 ────────────────────────────────────────
            Controls.Add(new Label { Text = "Recurrence:", Location = new Point(12, 316), AutoSize = true });
            cboRecurrence.DropDownStyle = ComboBoxStyle.DropDownList;
            cboRecurrence.Location      = new Point(94, 312);
            cboRecurrence.Size          = new Size(100, 23);
            cboRecurrence.Items.AddRange(new object[] { "(None)", "Daily", "Weekly", "Monthly" });
            cboRecurrence.SelectedItem  = _task.RecurrencePattern ?? "(None)";
            if (cboRecurrence.SelectedIndex < 0) cboRecurrence.SelectedIndex = 0;
            cboRecurrence.SelectedIndexChanged += CboRecurrence_SelectedIndexChanged;
            Controls.Add(cboRecurrence);

            Controls.Add(new Label { Text = "Every:", Location = new Point(204, 316), AutoSize = true });
            nudInterval.Location = new Point(244, 312);
            nudInterval.Size     = new Size(52, 23);
            nudInterval.Minimum  = 1;
            nudInterval.Maximum  = 30;
            nudInterval.Value    = Math.Max(1, _task.RecurrenceInterval);
            Controls.Add(nudInterval);

            lblNextOccur.Location  = new Point(308, 316);
            lblNextOccur.AutoSize  = true;
            lblNextOccur.ForeColor = Theme.TextMuted;
            lblNextOccur.Font      = new Font("Segoe UI", 8.5F);
            UpdateNextOccurrenceLabel();
            Controls.Add(lblNextOccur);

            // ── Linked Records section — y=355–430 ────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = "Linked Records:",
                Location  = new Point(12, 355),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold)
            });

            dgvLinks.AutoGenerateColumns = false;
            dgvLinks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLinkId",   HeaderText = "ID",        Width = 0,   Visible = false });
            dgvLinks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colModule",   HeaderText = "Module",    Width = 100 });
            dgvLinks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDisplay",  HeaderText = "Reference", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvLinks.ReadOnly              = true;
            dgvLinks.AllowUserToAddRows    = false;
            dgvLinks.AllowUserToDeleteRows = false;
            dgvLinks.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvLinks.RowHeadersVisible     = false;
            dgvLinks.Location              = new Point(12, 373);
            dgvLinks.Size                  = new Size(676, 70);
            dgvLinks.Anchor                = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(dgvLinks);

            btnAddLink.Text     = "Add Link...";
            btnAddLink.Size     = new Size(90, 23);
            btnAddLink.Location = new Point(12, 449);
            btnAddLink.Click   += BtnAddLink_Click;
            Controls.Add(btnAddLink);

            btnViewLink.Text     = "View";
            btnViewLink.Size     = new Size(55, 23);
            btnViewLink.Location = new Point(110, 449);
            btnViewLink.Click   += BtnViewLink_Click;
            Controls.Add(btnViewLink);

            btnRemoveLink.Text     = "Remove";
            btnRemoveLink.Size     = new Size(70, 23);
            btnRemoveLink.Location = new Point(173, 449);
            btnRemoveLink.Click   += BtnRemoveLink_Click;
            Controls.Add(btnRemoveLink);

            // ── Subtasks section — y=480–590 ──────────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = "Subtasks:",
                Location  = new Point(12, 480),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold)
            });

            lblSubtaskCount.Location  = new Point(90, 482);
            lblSubtaskCount.AutoSize  = true;
            lblSubtaskCount.ForeColor = Theme.TextMuted;
            lblSubtaskCount.Font      = new Font("Segoe UI", 8.5F);
            lblSubtaskCount.Text      = "";
            Controls.Add(lblSubtaskCount);

            clbSubtasks.Location      = new Point(12, 500);
            clbSubtasks.Size          = new Size(676, 100);
            clbSubtasks.Anchor        = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            clbSubtasks.CheckOnClick  = true;
            clbSubtasks.ItemCheck    += ClbSubtasks_ItemCheck;
            clbSubtasks.KeyDown      += ClbSubtasks_KeyDown;
            Controls.Add(clbSubtasks);

            txtNewSubtask.Location        = new Point(12, 608);
            txtNewSubtask.Size            = new Size(540, 23);
            txtNewSubtask.Anchor          = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtNewSubtask.PlaceholderText = "New subtask title…";
            txtNewSubtask.KeyPress       += TxtNewSubtask_KeyPress;
            Controls.Add(txtNewSubtask);

            btnAddSubtask.Text     = "Add";
            btnAddSubtask.Size     = new Size(60, 23);
            btnAddSubtask.Location = new Point(562, 608);
            btnAddSubtask.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnAddSubtask.Click   += BtnAddSubtask_Click;
            Controls.Add(btnAddSubtask);

            // ── Activity Log section — y=640–770 ──────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = "Activity:",
                Location  = new Point(12, 642),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold)
            });

            lstHistory.Location      = new Point(12, 662);
            lstHistory.Size          = new Size(676, 120);
            lstHistory.Anchor        = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lstHistory.SelectionMode = SelectionMode.None;
            lstHistory.Font          = new Font("Consolas", 8F);
            Controls.Add(lstHistory);

            // ── Discussion — y=792–930 ────────────────────────────────────────────────
            Controls.Add(new Label { Text = "Discussion (type @ to tag a user):", Location = new Point(12, 792), AutoSize = true });

            lstComments.Location      = new Point(12, 812);
            lstComments.Size          = new Size(676, 120);
            lstComments.Anchor        = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lstComments.SelectionMode = SelectionMode.None;
            Controls.Add(lstComments);

            txtComment.Location        = new Point(12, 942);
            txtComment.Size            = new Size(600, 23);
            txtComment.Anchor          = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtComment.PlaceholderText = "Write a comment… (type @ to tag a user)";
            txtComment.KeyPress       += TxtComment_KeyPress;
            txtComment.TextChanged    += TxtComment_TextChanged;
            Controls.Add(txtComment);

            btnPostComment.Text     = "Post";
            btnPostComment.Size     = new Size(70, 23);
            btnPostComment.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnPostComment.Location = new Point(618, 942);
            btnPostComment.Click   += BtnPostComment_Click;
            Controls.Add(btnPostComment);

            // @mention popup listbox (hidden by default, floats near comment box)
            lstMention.Visible     = false;
            lstMention.Location    = new Point(12, 900);
            lstMention.Size        = new Size(250, 120);
            lstMention.Anchor      = AnchorStyles.Top | AnchorStyles.Left;
            lstMention.BorderStyle = BorderStyle.FixedSingle;
            lstMention.Font        = new Font("Segoe UI", 9.5F);
            lstMention.Click      += LstMention_Click;
            lstMention.KeyDown    += LstMention_KeyDown;
            Controls.Add(lstMention);
            lstMention.BringToFront();

            btnSaveChanges.Text     = "Save Changes";
            btnSaveChanges.Size     = new Size(110, 28);
            btnSaveChanges.Anchor   = AnchorStyles.Top | AnchorStyles.Left;
            btnSaveChanges.Location = new Point(12, 976);
            btnSaveChanges.UseVisualStyleBackColor = true;
            btnSaveChanges.Click   += BtnSaveChanges_Click;
            Controls.Add(btnSaveChanges);

            btnClose.Text     = "Close";
            btnClose.Size     = new Size(80, 28);
            btnClose.Anchor   = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Location = new Point(608, 976);
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
                cboStage.Items.AddRange(new object[] { "Open", "In Progress", "Done", "Cancelled" });
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

                    // Generate next recurrence if applicable
                    TryGenerateNextRecurrence();
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

            // Wire NotifyMentionAsync for each mentioned user
            if (mentions.Count > 0)
            {
                List<(string Username, string Email)> emailTargets;
                try { emailTargets = _repo.GetUserEmails(mentions); } catch { return; }
                foreach (var (username, email) in emailTargets)
                {
                    if (string.IsNullOrWhiteSpace(email)) continue;
                    _ = NotificationService.NotifyMentionAsync(email, postedBy, _task.Title);
                }
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

        // ── Linked Records (multi) ────────────────────────────────────────────────────

        private void AddLinkEntry(string module, string id, string displayLabel)
        {
            try
            {
                _repo.AddLinkedRecord(_task.TaskID, module, id, displayLabel);
                LoadLinkedRecords();
                Changed = true;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void BtnAddLink_Click(object? sender, EventArgs e)
        {
            var modules = new[] { "Sales Order", "Purchase Order", "Customer", "Product", "Part", "Cook Session" };
            using var dlgModule = new Form
            {
                Text            = "Select Module",
                Size            = new Size(260, 120),
                StartPosition   = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox     = false,
                MinimizeBox     = false
            };
            var cboMod = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
            foreach (var m in modules) cboMod.Items.Add(m);
            cboMod.SelectedIndex = 0;
            var btnOkMod = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom };
            dlgModule.Controls.AddRange(new Control[] { btnOkMod, cboMod });
            dlgModule.AcceptButton = btnOkMod;
            if (dlgModule.ShowDialog(this) != DialogResult.OK) return;

            var module = cboMod.SelectedItem?.ToString() ?? "";
            SearchAndAddLink(module);
        }

        private void SearchAndAddLink(string module)
        {
            switch (module)
            {
                case "Customer":
                    try
                    {
                        var customers = AppServices.Get<ICustomerRepository>()
                            .GetSummaries()
                            .Select(c => (id: c.CustomerID.ToString(),
                                         label: string.IsNullOrWhiteSpace(c.FullName)
                                                ? c.Email
                                                : $"{c.FullName} ({c.Email})"))
                            .ToList();
                        var result = ShowSimpleSearchDialog("Select Customer", customers);
                        if (result.HasValue)
                            AddLinkEntry("Customer", result.Value.id, result.Value.display);
                    }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    break;

                case "Purchase Order":
                    try
                    {
                        var pos = AppServices.Get<ISupplierRepository>()
                            .GetOrders()
                            .Select(po => (id: po.POID.ToString(),
                                           label: $"{po.PONumber} – {po.SupplierName ?? "Unknown"} ({po.Status})"))
                            .ToList();
                        var result = ShowSimpleSearchDialog("Select Purchase Order", pos);
                        if (result.HasValue)
                            AddLinkEntry("Purchase Order", result.Value.id, result.Value.display);
                    }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    break;

                case "Product":
                    try
                    {
                        var products = AppServices.Get<IProductRepository>()
                            .GetProducts()
                            .Select(p => (id: p.ProductID.ToString(),
                                          label: string.IsNullOrWhiteSpace(p.SKU)
                                                 ? p.ProductName
                                                 : $"{p.ProductName} ({p.SKU})"))
                            .ToList();
                        var result = ShowSimpleSearchDialog("Select Product", products);
                        if (result.HasValue)
                            AddLinkEntry("Product", result.Value.id, result.Value.display);
                    }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    break;

                case "Part":
                    try
                    {
                        var parts = AppServices.Get<IPartRepository>()
                            .GetAll()
                            .Select(p => (id: p.PartID.ToString(),
                                          label: $"{p.PartNumber} – {p.PartName}"))
                            .ToList();
                        var result = ShowSimpleSearchDialog("Select Part", parts);
                        if (result.HasValue)
                            AddLinkEntry("Part", result.Value.id, result.Value.display);
                    }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    break;

                case "Sales Order":
                    try
                    {
                        var orders = AppServices.Get<IShopifySyncService>()
                            .GetErpOrders()
                            .Select(o => (id: (o.ErpSalesOrderID ?? (int)o.Id).ToString(),
                                          label: $"#{o.OrderNumber} – {o.ContactEmail ?? "(no email)"} ({o.ErpStatus ?? o.SyncStatus ?? ""})"))
                            .ToList();
                        var result = ShowSimpleSearchDialog("Select Sales Order", orders);
                        if (result.HasValue)
                            AddLinkEntry("Sales Order", result.Value.id, result.Value.display);
                    }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    break;

                case "Cook Session":
                    try
                    {
                        var sessions = AppServices.Get<IManufacturingRepository>()
                            .GetOpenCookSessions()
                            .Select(s => (id: s.CookSessionID.ToString(),
                                          label: $"{s.SessionName} ({s.Status}, {s.CreatedAt:yyyy-MM-dd})"))
                            .ToList();
                        var result = ShowSimpleSearchDialog("Select Cook Session", sessions);
                        if (result.HasValue)
                            AddLinkEntry("Cook Session", result.Value.id, result.Value.display);
                    }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    break;

                default:
                    MessageBox.Show(this,
                        "No search available for the selected module type.",
                        "Unknown Module", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
            }
        }

        /// <summary>
        /// Shows a modal search dialog with filter TextBox and ListBox.
        /// Returns the selected (id, display) tuple, or null if cancelled.
        /// </summary>
        private (string id, string display)? ShowSimpleSearchDialog(string title, IEnumerable<(string id, string label)> items)
        {
            using var dlg = new Form
            {
                Text          = title,
                Size          = new Size(420, 400),
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox   = false,
                MaximizeBox   = false
            };
            var txt = new TextBox { Dock = DockStyle.Top, PlaceholderText = "Type to filter…" };
            var lst = new ListBox { Dock = DockStyle.Fill };
            var btn = new Button  { Text = "Select", Dock = DockStyle.Bottom, Height = 32 };

            var allItems = items.ToList();

            void Repopulate(string filter)
            {
                lst.Items.Clear();
                var q = filter.ToLowerInvariant();
                foreach (var item in allItems.Where(i => string.IsNullOrEmpty(q) || i.label.ToLower().Contains(q)))
                    lst.Items.Add(item.label);
                if (lst.Items.Count > 0) lst.SelectedIndex = 0;
            }

            Repopulate("");
            txt.TextChanged += (_, _) => Repopulate(txt.Text);
            lst.DoubleClick += (_, _) => { dlg.DialogResult = DialogResult.OK; dlg.Close(); };
            btn.Click       += (_, _) => { if (lst.SelectedItem != null) { dlg.DialogResult = DialogResult.OK; dlg.Close(); } };
            dlg.Controls.AddRange(new Control[] { btn, lst, txt });
            dlg.AcceptButton = btn;

            if (allItems.Count == 0)
            {
                lst.Items.Add("(No items available)");
            }

            if (dlg.ShowDialog(this) == DialogResult.OK && lst.SelectedItem != null)
            {
                var selected = lst.SelectedItem.ToString() ?? "";
                var match    = allItems.FirstOrDefault(i => i.label == selected);
                return match.id != null ? (match.id, match.label) : null;
            }
            return null;
        }

        private void LoadLinkedRecords()
        {
            dgvLinks.Rows.Clear();
            try
            {
                var links = _repo.GetLinkedRecords(_task.TaskID);
                foreach (var link in links)
                {
                    int idx = dgvLinks.Rows.Add();
                    dgvLinks.Rows[idx].Cells["colLinkId"].Value  = link.LinkId;
                    dgvLinks.Rows[idx].Cells["colModule"].Value  = link.LinkedModule;
                    dgvLinks.Rows[idx].Cells["colDisplay"].Value =
                        !string.IsNullOrWhiteSpace(link.LinkedDisplay)
                            ? link.LinkedDisplay
                            : $"{link.LinkedModule} #{link.LinkedId}";
                    dgvLinks.Rows[idx].Tag = link;
                }
            }
            catch { /* non-fatal */ }
        }

        private void BtnViewLink_Click(object? sender, EventArgs e)
        {
            if (dgvLinks.SelectedRows.Count == 0) return;
            var link = dgvLinks.SelectedRows[0].Tag as TaskLinkedRecord;
            if (link == null) return;
            var module = link.LinkedModule;
            var id     = link.LinkedId;
            try
            {
                switch (module)
                {
                    case "Customer":
                        using (var frm = new FormCustomers()) frm.ShowDialog(this);
                        break;
                    case "Sales Order":
                        using (var frm = new FormStoreDashboard()) frm.ShowDialog(this);
                        break;
                    case "Purchase Order":
                        using (var frm = new FormPurchaseOrders()) frm.ShowDialog(this);
                        break;
                    case "Product":
                        using (var frm = new FormProductSearch()) frm.ShowDialog(this);
                        break;
                    case "Part":
                        using (var frm = new FormPartsManager()) frm.ShowDialog(this);
                        break;
                    case "Cook Session":
                        if (int.TryParse(id, out int sessionId))
                        {
                            using var frm = new FormCookSession(sessionId);
                            frm.ShowDialog(this);
                        }
                        else
                            MessageBox.Show(this, $"Cook Session ID must be a number. Got: {id}", "Invalid ID", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        break;
                    default:
                        MessageBox.Show(this, $"No view available for module: {module}", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void BtnRemoveLink_Click(object? sender, EventArgs e)
        {
            if (dgvLinks.SelectedRows.Count == 0) return;
            var link = dgvLinks.SelectedRows[0].Tag as TaskLinkedRecord;
            if (link == null) return;
            if (MessageBox.Show(this, $"Remove link to '{link.LinkedDisplay}'?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                _repo.RemoveLinkedRecord(link.LinkId);
                LoadLinkedRecords();
                Changed = true;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ── Subtasks ──────────────────────────────────────────────────────────────────

        private void LoadSubtasks()
        {
            clbSubtasks.Items.Clear();
            _subtasks.Clear();
            try
            {
                _subtasks = _repo.GetSubtasks(_task.TaskID);
                // Suppress ItemCheck event while loading
                clbSubtasks.ItemCheck -= ClbSubtasks_ItemCheck;
                foreach (var s in _subtasks)
                    clbSubtasks.Items.Add(s.Title, s.IsComplete);
                clbSubtasks.ItemCheck += ClbSubtasks_ItemCheck;
                UpdateSubtaskCount();
            }
            catch { clbSubtasks.Items.Add("(Could not load subtasks)"); }
        }

        private void UpdateSubtaskCount()
        {
            if (_subtasks.Count == 0)
            {
                lblSubtaskCount.Text = "";
                return;
            }
            int done = _subtasks.Count(s => s.IsComplete);
            lblSubtaskCount.Text = $"({done}/{_subtasks.Count} complete)";
        }

        private void ClbSubtasks_ItemCheck(object? sender, ItemCheckEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _subtasks.Count) return;
            var subtask   = _subtasks[e.Index];
            var user      = AppSession.CurrentUser?.Username ?? "system";
            bool complete = (e.NewValue == CheckState.Checked);
            try
            {
                if (complete)
                    _repo.CompleteSubtask(subtask.SubtaskId, user);
                else
                    _repo.UncompleteSubtask(subtask.SubtaskId);
                subtask.IsComplete = complete;
                UpdateSubtaskCount();
                Changed = true;
            }
            catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormTaskDetail.ClbSubtasks_ItemCheck]: {ex.Message}"); }
        }

        private void TxtNewSubtask_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return) { BtnAddSubtask_Click(sender, EventArgs.Empty); e.Handled = true; }
        }

        private void BtnAddSubtask_Click(object? sender, EventArgs e)
        {
            var title = txtNewSubtask.Text.Trim();
            if (string.IsNullOrWhiteSpace(title)) return;
            try
            {
                _repo.AddSubtask(_task.TaskID, title, _subtasks.Count);
                txtNewSubtask.Clear();
                LoadSubtasks();
                Changed = true;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void ClbSubtasks_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && clbSubtasks.SelectedIndex >= 0)
            {
                int idx = clbSubtasks.SelectedIndex;
                if (idx >= _subtasks.Count) return;
                var subtask = _subtasks[idx];
                if (MessageBox.Show(this, $"Delete subtask \"{subtask.Title}\"?", "Confirm",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try
                {
                    _repo.DeleteSubtask(subtask.SubtaskId);
                    LoadSubtasks();
                    Changed = true;
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        // ── Activity Log ──────────────────────────────────────────────────────────────

        private void LoadHistory()
        {
            lstHistory.Items.Clear();
            try
            {
                var history  = _repo.GetHistory(_task.TaskID);
                var comments = _repo.GetComments(_task.TaskID);

                // Build merged, sorted activity list
                var allActivity = new List<string>();

                foreach (var h in history)
                {
                    var oldVal = string.IsNullOrWhiteSpace(h.OldValue) ? "(empty)" : h.OldValue;
                    var newVal = string.IsNullOrWhiteSpace(h.NewValue) ? "(empty)" : h.NewValue;
                    allActivity.Add($"[{h.ChangedAt:yyyy-MM-dd HH:mm}]  {h.ChangedBy} changed {h.FieldName}: {oldVal} → {newVal}");
                }

                foreach (var c in comments)
                    allActivity.Add($"[{c.CreatedAt:yyyy-MM-dd HH:mm}]  {c.Username} commented: {c.Body}");

                if (allActivity.Count == 0)
                {
                    lstHistory.Items.Add("No activity yet — changes and comments will appear here going forward.");
                    return;
                }

                // Sort by the date prefix (ISO format so lexicographic = chronological)
                foreach (var line in allActivity.OrderBy(l => l))
                    lstHistory.Items.Add(line);

                lstHistory.TopIndex = lstHistory.Items.Count - 1;
            }
            catch { lstHistory.Items.Add("(Could not load activity)"); }
        }

        // ── Recurrence ────────────────────────────────────────────────────────────────

        private void CboRecurrence_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdateNextOccurrenceLabel();
        }

        private void UpdateNextOccurrenceLabel()
        {
            var pattern = cboRecurrence.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(pattern) || pattern == "(None)")
            {
                lblNextOccur.Text   = "";
                nudInterval.Enabled = false;
                return;
            }
            nudInterval.Enabled = true;
            // Show computed next occurrence date even before completion
            if (_task.NextOccurrence.HasValue)
            {
                lblNextOccur.Text = $"Next: {_task.NextOccurrence.Value:yyyy-MM-dd}";
            }
            else
            {
                // Preview: compute from current due date + interval
                int interval = (int)nudInterval.Value;
                var baseDate = _task.DueDate;
                DateTime nextPreview = pattern switch
                {
                    "Daily"   => baseDate.AddDays(interval),
                    "Weekly"  => baseDate.AddDays(interval * 7),
                    "Monthly" => baseDate.AddMonths(interval),
                    _         => baseDate.AddDays(interval)
                };
                lblNextOccur.Text = $"Next: {nextPreview:yyyy-MM-dd} (preview)";
            }
        }

        private void TryGenerateNextRecurrence()
        {
            if (string.IsNullOrEmpty(_task.RecurrencePattern)) return;
            var user = AppSession.CurrentUser?.Username ?? "system";
            try
            {
                var nextTask = _repo.GenerateNextRecurrence(_task.TaskID, user);
                if (nextTask != null)
                    MessageBox.Show(this,
                        $"Next occurrence created for {nextTask.DueDate:yyyy-MM-dd}.",
                        "Recurrence", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormTaskDetail.TryGenerateNextRecurrence]: {ex.Message}"); }
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

                // Tags
                var newTags = txtTags.Text.Trim();
                if (newTags != (_task.Tags ?? ""))
                {
                    _repo.UpdateTags(_task.TaskID, newTags);
                    _task.Tags = newTags;
                }

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

                        // If task just became Done, check recurrence
                        if (derived == "Done") TryGenerateNextRecurrence();
                    }
                }
                else
                {
                    // No workflow — stage combo holds legacy statuses
                    var newStatus = stageVal is "Open" or "In Progress" or "Done" or "Cancelled" ? stageVal : "Open";
                    if (newStatus != _task.Status)
                    {
                        _repo.UpdateStatus(_task.TaskID, newStatus);
                        _task.Status = newStatus;
                        if (newStatus == "Done") TryGenerateNextRecurrence();
                    }
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

                // Recurrence
                var recPattern = cboRecurrence.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(recPattern) || recPattern == "(None)")
                {
                    _repo.ClearRecurrence(_task.TaskID);
                    _task.RecurrencePattern  = null;
                    _task.RecurrenceInterval = 1;
                }
                else
                {
                    int interval = (int)nudInterval.Value;
                    _repo.SetRecurrence(_task.TaskID, recPattern, interval);
                    _task.RecurrencePattern  = recPattern;
                    _task.RecurrenceInterval = interval;
                }
                UpdateNextOccurrenceLabel();

                Changed      = true;
                lblMeta.Text = BuildMetaText();

                // Refresh activity log to show newly-logged changes
                LoadHistory();
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
