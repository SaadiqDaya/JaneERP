using JaneERP.Security;
using JaneERP.Services;

namespace JaneERP
{
    /// <summary>View, create and manage tasks. Assign to users, set due dates.</summary>
    public class FormTaskManager : Form
    {
        private readonly TaskRepository _repo = new();

        private DataGridView dgvTasks        = new();
        private ComboBox     cboFilter      = new();
        private ComboBox     cboStageFilter = new();
        private Button       btnAdd          = new();
        private Button       btnDone         = new();
        private Button       btnDelete       = new();
        private Button       btnEmail        = new();
        private Button       btnWorkflows    = new();
        private Button       btnClose        = new();
        private Label        lblFilter   = new();

        // Search / filter bar extras
        private TextBox      txtSearch      = new();
        private TextBox      txtTagFilter   = new();
        private CheckBox     chkShowAll     = new();

        // Bulk operation buttons
        private Button       btnReassign    = new();
        private Button       btnSetPriority = new();
        private Button       btnSetDue      = new();

        // Workload summary
        private FlowLayoutPanel pnlWorkload = new();
        private Label           lblWorkloadTitle = new();

        // Mentions panel
        private Label        lblMentions          = new();
        private DataGridView dgvMentions          = new();
        private Button       btnClearMentions     = new();
        private Button       btnClearSelected     = new();
        private System.Windows.Forms.Timer _mentionsTimer = new() { Interval = 5000 };

        public FormTaskManager()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            try { _repo.EnsureSchema(); } catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormTaskManager] EnsureSchema: {ex.Message}"); }
            LoadTasks();
            LoadMentions();
            _mentionsTimer.Tick += (_, _) => { if (!IsDisposed && IsHandleCreated) BeginInvoke(LoadMentions); };
            _mentionsTimer.Start();
            FormClosed += (_, _) => _mentionsTimer.Stop();
        }

        private void BuildUI()
        {
            Text            = "Task Manager";
            ClientSize      = new Size(1000, 820);
            MinimumSize     = new Size(820, 700);
            StartPosition   = FormStartPosition.CenterParent;

            // ── Header ────────────────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = "Task Manager",
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            });

            // ── Filter bar row 1: Assignee + Stage ──────────────────────────────
            lblFilter.Text     = "Show tasks for:";
            lblFilter.Location = new Point(12, 52);
            lblFilter.AutoSize = true;
            Controls.Add(lblFilter);

            cboFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cboFilter.Location      = new Point(120, 49);
            cboFilter.Size          = new Size(180, 23);
            cboFilter.Items.Add("All Users");
            cboFilter.Items.Add("Me (" + (AppSession.CurrentUser?.Username ?? "") + ")");
            cboFilter.SelectedIndex = 0;
            cboFilter.SelectedIndexChanged += (_, _) => LoadTasks();
            Controls.Add(cboFilter);

            Controls.Add(new Label { Text = "Stage:", Location = new Point(316, 52), AutoSize = true });
            cboStageFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cboStageFilter.Location      = new Point(362, 49);
            cboStageFilter.Size          = new Size(130, 23);
            cboStageFilter.Items.AddRange(new object[] { "All Stages", "Open", "In Progress", "Done", "Overdue" });
            cboStageFilter.SelectedIndex = 0;
            Load += (_, _) => EnrichStageFilter();
            cboStageFilter.SelectedIndexChanged += (_, _) => LoadTasks();
            Controls.Add(cboStageFilter);

            // ── Filter bar row 2: Search + Tag + Show All ─────────────────────
            Controls.Add(new Label { Text = "Search:", Location = new Point(12, 82), AutoSize = true });
            txtSearch.Location    = new Point(66, 79);
            txtSearch.Size        = new Size(200, 23);
            txtSearch.TextChanged += (_, _) => LoadTasks();
            Controls.Add(txtSearch);

            Controls.Add(new Label { Text = "Tag:", Location = new Point(280, 82), AutoSize = true });
            txtTagFilter.Location    = new Point(310, 79);
            txtTagFilter.Size        = new Size(150, 23);
            txtTagFilter.TextChanged += (_, _) => LoadTasks();
            Controls.Add(txtTagFilter);

            chkShowAll.Text     = "Show all";
            chkShowAll.Location = new Point(474, 81);
            chkShowAll.AutoSize = true;
            chkShowAll.CheckedChanged += (_, _) => LoadTasks();
            Controls.Add(chkShowAll);

            // ── Grid ─────────────────────────────────────────────────────────────
            dgvTasks.AutoGenerateColumns = false;
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTitle",     HeaderText = "Title",       DataPropertyName = "Title",       Width = 200 });
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAssigned",  HeaderText = "Assigned To", DataPropertyName = "AssignedTo",  Width = 120 });
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDue",       HeaderText = "Due Date",    DataPropertyName = "DueDate",     Width = 100 });
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStage",     HeaderText = "Stage",       DataPropertyName = "StageDisplay", Width = 110 });
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPriority",  HeaderText = "Priority",    DataPropertyName = "Priority",    Width = 80  });
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCreatedBy", HeaderText = "Created By",  DataPropertyName = "CreatedBy",   Width = 110 });
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colWorkflow",  HeaderText = "Workflow",    DataPropertyName = "WorkflowName", Width = 120 });
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRef",       HeaderText = "Ref",         Width = 90  }); // LinkedDisplay lives on TaskLinkedRecord; FormTaskDetail shows it
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDesc",      HeaderText = "Description", DataPropertyName = "Description",  AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            // Tooltip on Ref column header
            var colRef = dgvTasks.Columns["colRef"] as DataGridViewTextBoxColumn;
            if (colRef != null) colRef.ToolTipText = "Double-click row to view linked record in task detail";

            dgvTasks.ReadOnly              = true;
            dgvTasks.AllowUserToAddRows    = false;
            dgvTasks.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvTasks.MultiSelect           = true;
            dgvTasks.Anchor                = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            dgvTasks.Location              = new Point(12, 110);
            dgvTasks.Size                  = new Size(976, 360);

            dgvTasks.CellFormatting += (s, e) =>
            {
                if (dgvTasks.Columns["colDue"] is DataGridViewColumn colDue &&
                    e.ColumnIndex == colDue.Index && e.Value is DateTime dt)
                    e.Value = dt.ToString("yyyy-MM-dd");
            };

            // Color overdue rows red, Done rows muted; highlight priority + stage columns
            dgvTasks.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || dgvTasks.Rows[e.RowIndex].DataBoundItem is not ErpTask t) return;
                if (t.Status == "Done")
                {
                    e.CellStyle.ForeColor = Theme.TextMuted;
                }
                else if (t.DueDate.Date < DateTime.Today)
                {
                    e.CellStyle.ForeColor = Color.FromArgb(220, 80, 60);
                }
                else if (dgvTasks.Columns["colPriority"] is DataGridViewColumn colP && e.ColumnIndex == colP.Index)
                {
                    e.CellStyle.ForeColor = t.Priority switch
                    {
                        "Urgent" => Color.FromArgb(220, 60, 50),
                        "High"   => Color.FromArgb(220, 140, 30),
                        "Low"    => Theme.TextMuted,
                        _        => Theme.TextPrimary
                    };
                }
                else if (dgvTasks.Columns["colStage"] is DataGridViewColumn colStage && e.ColumnIndex == colStage.Index)
                {
                    e.CellStyle.ForeColor = t.Status switch
                    {
                        "Open"        => Theme.TextMuted,
                        "Done"        => Theme.TextMuted,
                        "In Progress" => Theme.Teal,
                        _             => Theme.Teal
                    };
                }
            };

            // Double-click → open task detail
            dgvTasks.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0 || dgvTasks.Rows[e.RowIndex].DataBoundItem is not ErpTask task) return;
                using var detail = new FormTaskDetail(_repo, task);
                detail.ShowDialog(this);
                if (detail.Changed) LoadTasks();
                LoadMentions();
            };

            Controls.Add(dgvTasks);

            // ── Workload summary ──────────────────────────────────────────────────
            lblWorkloadTitle.Text      = "Workload:";
            lblWorkloadTitle.Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblWorkloadTitle.ForeColor = Theme.TextMuted;
            lblWorkloadTitle.Location  = new Point(12, 478);
            lblWorkloadTitle.AutoSize  = true;
            Controls.Add(lblWorkloadTitle);

            pnlWorkload.Location    = new Point(80, 474);
            pnlWorkload.Size        = new Size(900, 26);
            pnlWorkload.Anchor      = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlWorkload.FlowDirection = FlowDirection.LeftToRight;
            pnlWorkload.WrapContents  = false;
            Controls.Add(pnlWorkload);

            // ── Mentions panel ────────────────────────────────────────────────────
            lblMentions.Text      = "Recent Mentions (@you):";
            lblMentions.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblMentions.ForeColor = Theme.Gold;
            lblMentions.Location  = new Point(12, 510);
            lblMentions.AutoSize  = true;
            Controls.Add(lblMentions);

            btnClearMentions.Text     = "Clear All";
            btnClearMentions.Size     = new Size(80, 22);
            btnClearMentions.Location = new Point(200, 508);
            btnClearMentions.Click   += BtnClearMentions_Click;
            Controls.Add(btnClearMentions);

            btnClearSelected.Text     = "Clear Selected";
            btnClearSelected.Size     = new Size(100, 22);
            btnClearSelected.Location = new Point(288, 508);
            btnClearSelected.Click   += BtnClearSelected_Click;
            Controls.Add(btnClearSelected);

            dgvMentions.AutoGenerateColumns = false;
            dgvMentions.Columns.Add(new DataGridViewTextBoxColumn { Name = "colMTask",   HeaderText = "Task",         DataPropertyName = "TaskTitle",    Width = 220 });
            dgvMentions.Columns.Add(new DataGridViewTextBoxColumn { Name = "colMBy",     HeaderText = "Mentioned By", DataPropertyName = "MentionedBy",  Width = 130 });
            dgvMentions.Columns.Add(new DataGridViewTextBoxColumn { Name = "colMWhen",   HeaderText = "When",         DataPropertyName = "MentionedAt",  AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvMentions.ReadOnly           = true;
            dgvMentions.AllowUserToAddRows = false;
            dgvMentions.SelectionMode      = DataGridViewSelectionMode.FullRowSelect;
            dgvMentions.Anchor             = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvMentions.Location           = new Point(12, 536);
            dgvMentions.Size               = new Size(976, 130);

            dgvMentions.CellFormatting += (s, e) =>
            {
                if (dgvMentions.Columns["colMWhen"] is DataGridViewColumn colW &&
                    e.ColumnIndex == colW.Index && e.Value is DateTime dt)
                    e.Value = dt.ToString("yyyy-MM-dd HH:mm");
            };

            dgvMentions.CellDoubleClick += DgvMentions_CellDoubleClick;
            Controls.Add(dgvMentions);

            // ── Bottom buttons ────────────────────────────────────────────────────
            btnAdd.Text     = "+ Add Task(s)";
            btnAdd.Size     = new Size(120, 30);
            btnAdd.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnAdd.Location = new Point(12, 778);
            btnAdd.Click   += BtnAdd_Click;
            Controls.Add(btnAdd);

            btnDone.Text     = "Mark Done";
            btnDone.Size     = new Size(100, 30);
            btnDone.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnDone.Location = new Point(140, 778);
            btnDone.Click   += BtnDone_Click;
            Controls.Add(btnDone);

            btnReassign.Text     = "Reassign...";
            btnReassign.Size     = new Size(100, 30);
            btnReassign.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnReassign.Location = new Point(248, 778);
            btnReassign.Click   += BtnReassign_Click;
            Controls.Add(btnReassign);

            btnSetPriority.Text     = "Set Priority...";
            btnSetPriority.Size     = new Size(110, 30);
            btnSetPriority.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnSetPriority.Location = new Point(356, 778);
            btnSetPriority.Click   += BtnSetPriority_Click;
            Controls.Add(btnSetPriority);

            btnSetDue.Text     = "Set Due Date...";
            btnSetDue.Size     = new Size(115, 30);
            btnSetDue.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnSetDue.Location = new Point(474, 778);
            btnSetDue.Click   += BtnSetDue_Click;
            Controls.Add(btnSetDue);

            btnDelete.Text     = "Delete";
            btnDelete.Size     = new Size(80, 30);
            btnDelete.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnDelete.Location = new Point(597, 778);
            btnDelete.Click   += BtnDelete_Click;
            Controls.Add(btnDelete);

            btnEmail.Text     = "Email Outstanding";
            btnEmail.Size     = new Size(140, 30);
            btnEmail.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnEmail.Location = new Point(685, 778);
            btnEmail.Click   += BtnEmail_Click;
            Controls.Add(btnEmail);

            btnWorkflows.Text     = "Manage Workflows";
            btnWorkflows.Size     = new Size(140, 30);
            btnWorkflows.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnWorkflows.Location = new Point(833, 778);
            btnWorkflows.Click   += (_, _) => { using var f = new FormWorkflowEditor(_repo); f.ShowDialog(this); };
            Controls.Add(btnWorkflows);

            btnClose.Text     = "Close";
            btnClose.Size     = new Size(80, 30);
            btnClose.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(908, 778);
            btnClose.Click   += (_, _) => Close();
            Controls.Add(btnClose);
        }

        private void LoadTasks()
        {
            string? filterUser = cboFilter.SelectedIndex == 1
                ? AppSession.CurrentUser?.Username
                : null;

            var stageSel       = cboStageFilter.SelectedItem?.ToString() ?? "All Stages";
            bool filterOverdue = stageSel == "Overdue";
            string? filterStatus = stageSel is "All Stages" or "Overdue" ? null
                : stageSel is "Open" or "In Progress" or "Done" ? stageSel
                : null;
            string? filterStage = stageSel is "All Stages" or "Overdue" or "Open" or "In Progress" or "Done"
                ? null : stageSel;

            var tasks = _repo.GetAll(filterUser, filterStatus);

            // Client-side stage filter for workflow-stage names
            if (!string.IsNullOrEmpty(filterStage))
                tasks = tasks.Where(t => string.Equals(t.WorkflowCurrentStatus, filterStage,
                    StringComparison.OrdinalIgnoreCase)).ToList();

            if (filterOverdue)
                tasks = tasks.Where(t => t.Status != "Done" && t.DueDate.Date < DateTime.Today).ToList();

            // ── Default filter: hide Done tasks older than 30 days ────────────────
            if (!chkShowAll.Checked)
            {
                tasks = tasks.Where(t =>
                    t.Status != "Done" ||
                    t.CreatedAt >= DateTime.Now.AddDays(-30)
                ).ToList();
            }

            // ── Tag filter ────────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(txtTagFilter.Text))
            {
                var tag = txtTagFilter.Text.Trim().ToLower();
                tasks = tasks.Where(t =>
                    !string.IsNullOrEmpty(t.Tags) &&
                    t.Tags.ToLower().Split(',').Select(s => s.Trim()).Any(s => s == tag || s.Contains(tag))
                ).ToList();
            }

            // ── Search filter ─────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                var q = txtSearch.Text.Trim().ToLower();
                tasks = tasks.Where(t =>
                    t.Title.ToLower().Contains(q) ||
                    (t.Description ?? "").ToLower().Contains(q) ||
                    (t.AssignedTo ?? "").ToLower().Contains(q) ||
                    (t.Tags ?? "").ToLower().Contains(q)
                ).ToList();
            }

            dgvTasks.DataSource = tasks;

            // Refresh workload summary after every load
            LoadWorkloadSummary();
        }

        /// <summary>Loads and displays the open task count per user in the workload panel.</summary>
        private void LoadWorkloadSummary()
        {
            try
            {
                pnlWorkload.Controls.Clear();
                var summary = _repo.GetWorkloadSummary();

                // Sort: highest count first, then alphabetically
                foreach (var kvp in summary.OrderByDescending(k => k.Value).ThenBy(k => k.Key))
                {
                    var user  = string.IsNullOrWhiteSpace(kvp.Key) ? "Unassigned" : kvp.Key;
                    var count = kvp.Value;
                    var lbl   = new Label
                    {
                        Text      = $"{user} ({count})",
                        AutoSize  = true,
                        Margin    = new Padding(0, 3, 10, 0),
                        ForeColor = count >= 8 ? Color.FromArgb(220, 100, 30) : Theme.TextPrimary,
                        Font      = count >= 8
                            ? new Font("Segoe UI", 8.5F, FontStyle.Bold)
                            : new Font("Segoe UI", 8.5F)
                    };
                    pnlWorkload.Controls.Add(lbl);
                }
            }
            catch (Exception ex)
            {
                JaneERP.Logging.AppLogger.Info($"[FormTaskManager.LoadWorkloadSummary]: {ex.Message}");
            }
        }

        /// <summary>Adds any workflow stage names to the stage filter combo (deduplicated).</summary>
        private void EnrichStageFilter()
        {
            try
            {
                var workflows = _repo.GetWorkflows();
                var allStages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var wf in workflows)
                    foreach (var s in _repo.GetWorkflowStatusNames(wf.WorkflowID))
                        allStages.Add(s);

                foreach (var stage in allStages)
                {
                    bool exists = false;
                    foreach (var item in cboStageFilter.Items)
                        if (item?.ToString()?.Equals(stage, StringComparison.OrdinalIgnoreCase) == true) { exists = true; break; }
                    if (!exists) cboStageFilter.Items.Add(stage);
                }
            }
            catch { /* non-fatal */ }
        }

        private void LoadMentions()
        {
            var currentUser = AppSession.CurrentUser?.Username;
            if (string.IsNullOrEmpty(currentUser)) return;
            try
            {
                var mentions = _repo.GetMentions(currentUser);
                dgvMentions.DataSource = mentions;
            }
            catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormTaskManager.LoadMentions]: {ex.Message}"); }
        }

        private void DgvMentions_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || dgvMentions.Rows[e.RowIndex].DataBoundItem is not TaskMention mention) return;
            var task = _repo.GetById(mention.TaskID);
            if (task == null) return;

            using var detail = new FormTaskDetail(_repo, task);
            detail.ShowDialog(this);
            if (detail.Changed) LoadTasks();
        }

        private void BtnClearSelected_Click(object? sender, EventArgs e)
        {
            var selected = dgvMentions.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as TaskMention)
                .Where(m => m != null)
                .ToList();

            if (selected.Count == 0) return;
            try
            {
                foreach (var m in selected)
                    _repo.MarkMentionRead(m!.MentionID);
                LoadMentions();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnClearMentions_Click(object? sender, EventArgs e)
        {
            var currentUser = AppSession.CurrentUser?.Username;
            if (string.IsNullOrEmpty(currentUser)) return;
            try
            {
                _repo.MarkAllMentionsRead(currentUser);
                LoadMentions();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            using var frm = new FormAddTask(_repo);
            if (frm.ShowDialog(this) == DialogResult.OK)
            {
                LoadTasks();
                LoadMentions();
            }
        }

        private void BtnDone_Click(object? sender, EventArgs e)
        {
            var selected = dgvTasks.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as ErpTask)
                .Where(t => t != null && t.Status != "Done")
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Select one or more open tasks.", "Nothing Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int done = 0;
            foreach (var task in selected)
            {
                try { _repo.UpdateStatus(task!.TaskID, "Done"); done++; }
                catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormTaskManager.BtnDone_Click]: {ex.Message}"); }
            }
            LoadTasks(); // also refreshes workload
            if (done > 0) MessageBox.Show(this, $"{done} task(s) marked Done.", "Done",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnReassign_Click(object? sender, EventArgs e)
        {
            var selected = GetSelectedTasks();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Select one or more tasks to reassign.", "Nothing Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<string> usernames;
            try { usernames = _repo.GetAllUsernames(); }
            catch { usernames = new List<string>(); }

            var newUser = PickFromList("Reassign to...", usernames);
            if (newUser == null) return;

            var currentUser = AppSession.CurrentUser?.Username ?? "system";
            int updated = 0;
            foreach (var task in selected)
            {
                try { _repo.UpdateAssignedTo(task.TaskID, newUser, currentUser); updated++; }
                catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormTaskManager.BtnReassign_Click]: {ex.Message}"); }
            }
            LoadTasks(); // refreshes workload
            if (updated > 0) MessageBox.Show(this, $"{updated} task(s) reassigned to {newUser}.", "Reassigned",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnSetPriority_Click(object? sender, EventArgs e)
        {
            var selected = GetSelectedTasks();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Select one or more tasks.", "Nothing Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var priority = PickFromList("Set Priority", new[] { "Low", "Normal", "High", "Urgent" });
            if (priority == null) return;

            var currentUser = AppSession.CurrentUser?.Username ?? "system";
            int updated = 0;
            foreach (var task in selected)
            {
                try { _repo.UpdatePriority(task.TaskID, priority, currentUser); updated++; }
                catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormTaskManager.BtnSetPriority_Click]: {ex.Message}"); }
            }
            LoadTasks();
            if (updated > 0) MessageBox.Show(this, $"{updated} task(s) set to {priority} priority.", "Priority Updated",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnSetDue_Click(object? sender, EventArgs e)
        {
            var selected = GetSelectedTasks();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Select one or more tasks.", "Nothing Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var newDate = PickDate("Set Due Date");
            if (newDate == null) return;

            var currentUser = AppSession.CurrentUser?.Username ?? "system";
            int updated = 0;
            foreach (var task in selected)
            {
                try { _repo.UpdateDueDate(task.TaskID, newDate.Value, currentUser); updated++; }
                catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormTaskManager.BtnSetDue_Click]: {ex.Message}"); }
            }
            LoadTasks();
            if (updated > 0) MessageBox.Show(this, $"{updated} task(s) due date set to {newDate.Value:yyyy-MM-dd}.", "Due Date Updated",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (dgvTasks.SelectedRows.Count == 0) return;
            if (dgvTasks.SelectedRows[0].DataBoundItem is not ErpTask task) return;
            if (MessageBox.Show(this, $"Delete task '{task.Title}'?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                _repo.Delete(task.TaskID);
                LoadTasks(); // refreshes workload
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void BtnEmail_Click(object? sender, EventArgs e)
        {
            string? filterUser = cboFilter.SelectedIndex == 1 ? AppSession.CurrentUser?.Username : null;
            var outstanding = _repo.GetOutstanding(filterUser);
            if (outstanding.Count == 0)
            {
                MessageBox.Show(this, "No outstanding tasks.", "Email Tasks", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Outstanding Tasks:");
            sb.AppendLine();
            foreach (var t in outstanding)
                sb.AppendLine($"• [{t.StageDisplay}] {t.Title} — Assigned: {t.AssignedTo} — Due: {t.DueDate:yyyy-MM-dd}{(string.IsNullOrEmpty(t.Description) ? "" : $"\n  {t.Description}")}");

            var body    = Uri.EscapeDataString(sb.ToString());
            var subject = Uri.EscapeDataString($"Outstanding Tasks — {DateTime.Today:yyyy-MM-dd}");
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    $"mailto:?subject={subject}&body={body}") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open email client:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>Returns all ErpTask items currently selected in the grid.</summary>
        private List<ErpTask> GetSelectedTasks() =>
            dgvTasks.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as ErpTask)
                .Where(t => t != null)
                .Select(t => t!)
                .ToList();

        /// <summary>Shows a small inline dialog with a ComboBox and returns the chosen value, or null if cancelled.</summary>
        private string? PickFromList(string title, IEnumerable<string> options)
        {
            using var dlg = new Form
            {
                Text            = title,
                Size            = new Size(280, 130),
                StartPosition   = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox     = false,
                MinimizeBox     = false
            };
            var cbo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock          = DockStyle.Top
            };
            foreach (var o in options) cbo.Items.Add(o);
            if (cbo.Items.Count > 0) cbo.SelectedIndex = 0;

            var btn = new Button
            {
                Text         = "OK",
                DialogResult = DialogResult.OK,
                Dock         = DockStyle.Bottom
            };
            dlg.Controls.AddRange(new Control[] { btn, cbo });
            dlg.AcceptButton = btn;
            return dlg.ShowDialog(this) == DialogResult.OK ? cbo.SelectedItem?.ToString() : null;
        }

        /// <summary>Shows a small inline dialog with a DateTimePicker and returns the chosen date, or null if cancelled.</summary>
        private DateTime? PickDate(string title)
        {
            using var dlg = new Form
            {
                Text            = title,
                Size            = new Size(280, 130),
                StartPosition   = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox     = false,
                MinimizeBox     = false
            };
            var dtp = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Value  = DateTime.Today.AddDays(7),
                Dock   = DockStyle.Top
            };
            var btn = new Button
            {
                Text         = "OK",
                DialogResult = DialogResult.OK,
                Dock         = DockStyle.Bottom
            };
            dlg.Controls.AddRange(new Control[] { btn, dtp });
            dlg.AcceptButton = btn;
            return dlg.ShowDialog(this) == DialogResult.OK ? dtp.Value.Date : (DateTime?)null;
        }
    }

    // ── Add Task(s) dialog ────────────────────────────────────────────────────────
    internal class FormAddTask : Form
    {
        private readonly TaskRepository _repo;

        private DataGridView   dgvTasks          = new();
        private Button         btnSave           = new();
        private Button         btnCancel         = new();
        private ComboBox       cboDefaultUser    = new();
        private DateTimePicker dtpDefaultDue     = new();
        private ComboBox       cboDefaultPriority  = new();
        private ComboBox       cboDefaultWorkflow  = new();

        public FormAddTask(TaskRepository repo)
        {
            _repo = repo;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
        }

        private void BuildUI()
        {
            Text          = "Add Tasks";
            ClientSize    = new Size(900, 430);
            MinimumSize   = new Size(760, 370);
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label
            {
                Text      = "Add Tasks",
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            });

            Controls.Add(new Label { Text = "Default Assign To:", Location = new Point(12, 50), AutoSize = true });
            cboDefaultUser.Location      = new Point(140, 47);
            cboDefaultUser.Size          = new Size(160, 23);
            cboDefaultUser.DropDownStyle = ComboBoxStyle.DropDownList;
            try { foreach (var u in _repo.GetAllUsernames()) cboDefaultUser.Items.Add(u); } catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormAddTask.BuildUI]: {ex.Message}"); }
            if (cboDefaultUser.Items.Count > 0) cboDefaultUser.SelectedIndex = 0;
            Controls.Add(cboDefaultUser);

            Controls.Add(new Label { Text = "Default Due:", Location = new Point(316, 50), AutoSize = true });
            dtpDefaultDue.Location = new Point(394, 47);
            dtpDefaultDue.Size     = new Size(120, 23);
            dtpDefaultDue.Value    = DateTime.Today.AddDays(7);
            Controls.Add(dtpDefaultDue);

            Controls.Add(new Label { Text = "Priority:", Location = new Point(524, 50), AutoSize = true });
            cboDefaultPriority.DropDownStyle = ComboBoxStyle.DropDownList;
            cboDefaultPriority.Location      = new Point(572, 47);
            cboDefaultPriority.Size          = new Size(90, 23);
            cboDefaultPriority.Items.AddRange(new object[] { "Low", "Normal", "High", "Urgent" });
            cboDefaultPriority.SelectedIndex = 1;
            Controls.Add(cboDefaultPriority);

            Controls.Add(new Label { Text = "Workflow:", Location = new Point(12, 78), AutoSize = true });
            cboDefaultWorkflow.Location      = new Point(80, 75);
            cboDefaultWorkflow.Size          = new Size(200, 23);
            cboDefaultWorkflow.DropDownStyle = ComboBoxStyle.DropDownList;
            cboDefaultWorkflow.Items.Add("(none)");
            try { foreach (var w in _repo.GetWorkflows()) cboDefaultWorkflow.Items.Add(w); } catch { }
            cboDefaultWorkflow.DisplayMember = "Name";
            cboDefaultWorkflow.SelectedIndex = 0;
            Controls.Add(cboDefaultWorkflow);

            Controls.Add(new Label
            {
                Text      = "Tasks will start on the first status of the selected workflow.",
                Font      = new Font("Segoe UI", 7.5F),
                ForeColor = Theme.TextMuted,
                Location  = new Point(294, 79),
                AutoSize  = true
            });

            var btnFill = new Button { Text = "Apply Defaults", Size = new Size(120, 23), Location = new Point(670, 47) };
            btnFill.Click += (_, _) =>
            {
                foreach (DataGridViewRow r in dgvTasks.Rows)
                {
                    if (r.IsNewRow) continue;
                    if (r.Cells["colAssign"]?.Value == null || r.Cells["colAssign"]?.Value?.ToString() == "")
                        r.Cells["colAssign"].Value = cboDefaultUser.SelectedItem?.ToString();
                    if (r.Cells["colDue"]?.Value == null || r.Cells["colDue"]?.Value?.ToString() == "")
                        r.Cells["colDue"].Value = dtpDefaultDue.Value.Date.ToString("yyyy-MM-dd");
                    if (r.Cells["colPriority"]?.Value == null || r.Cells["colPriority"]?.Value?.ToString() == "")
                        r.Cells["colPriority"].Value = cboDefaultPriority.SelectedItem?.ToString();
                }
            };
            Controls.Add(btnFill);

            var userNames = new DataGridViewComboBoxColumn
            {
                Name         = "colAssign",
                HeaderText   = "Assign To",
                Width        = 140,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing
            };
            try { foreach (var u in _repo.GetAllUsernames()) userNames.Items.Add(u); } catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormAddTask.BuildUI]: {ex.Message}"); }

            var priorities = new DataGridViewComboBoxColumn
            {
                Name         = "colPriority",
                HeaderText   = "Priority",
                Width        = 90,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing
            };
            priorities.Items.AddRange("Low", "Normal", "High", "Urgent");

            dgvTasks.AutoGenerateColumns = false;
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTitle", HeaderText = "Title *", Width = 200 });
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDesc",  HeaderText = "Description", Width = 200, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvTasks.Columns.Add(userNames);
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDue", HeaderText = "Due (yyyy-MM-dd)", Width = 130 });
            dgvTasks.Columns.Add(priorities);
            dgvTasks.AllowUserToAddRows    = true;
            dgvTasks.AllowUserToDeleteRows = true;
            dgvTasks.Anchor    = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvTasks.Location  = new Point(12, 108);
            dgvTasks.Size      = new Size(876, 270);
            dgvTasks.DataError += (s, e) => e.Cancel = true;
            Controls.Add(dgvTasks);

            dgvTasks.Rows.Add();
            if (cboDefaultUser.SelectedItem != null)
                dgvTasks.Rows[0].Cells["colAssign"].Value = cboDefaultUser.SelectedItem.ToString();
            dgvTasks.Rows[0].Cells["colDue"].Value      = dtpDefaultDue.Value.Date.ToString("yyyy-MM-dd");
            dgvTasks.Rows[0].Cells["colPriority"].Value = cboDefaultPriority.SelectedItem?.ToString() ?? "Normal";

            btnSave.Text     = "Save All Tasks";
            btnSave.Size     = new Size(130, 30);
            btnSave.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSave.Location = new Point(638, 388);
            btnSave.Click   += BtnSave_Click;
            Controls.Add(btnSave);

            btnCancel.Text     = "Cancel";
            btnCancel.Size     = new Size(80, 30);
            btnCancel.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.Location = new Point(776, 388);
            btnCancel.Click   += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);

            var btnAddRow = new Button
            {
                Text     = "+ Add Row",
                Size     = new Size(100, 30),
                Anchor   = AnchorStyles.Bottom | AnchorStyles.Left,
                Location = new Point(12, 388)
            };
            btnAddRow.Click += (_, _) =>
            {
                int idx = dgvTasks.Rows.Add();
                if (cboDefaultUser.SelectedItem != null)
                    dgvTasks.Rows[idx].Cells["colAssign"].Value = cboDefaultUser.SelectedItem.ToString();
                dgvTasks.Rows[idx].Cells["colDue"].Value      = dtpDefaultDue.Value.Date.ToString("yyyy-MM-dd");
                dgvTasks.Rows[idx].Cells["colPriority"].Value = cboDefaultPriority.SelectedItem?.ToString() ?? "Normal";
            };
            Controls.Add(btnAddRow);
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            dgvTasks.CommitEdit(DataGridViewDataErrorContexts.Commit);
            dgvTasks.EndEdit();

            var toAdd = new List<ErpTask>();
            foreach (DataGridViewRow row in dgvTasks.Rows)
            {
                if (row.IsNewRow) continue;
                var title = row.Cells["colTitle"].Value?.ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(title)) continue;

                var assignedTo = row.Cells["colAssign"].Value?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(assignedTo)) assignedTo = cboDefaultUser.SelectedItem?.ToString() ?? "system";

                DateTime due = DateTime.Today.AddDays(7);
                if (DateTime.TryParse(row.Cells["colDue"].Value?.ToString(), out var parsedDue))
                    due = parsedDue.Date;

                toAdd.Add(new ErpTask
                {
                    Title       = title,
                    Description = row.Cells["colDesc"].Value?.ToString()?.Trim(),
                    AssignedTo  = assignedTo,
                    CreatedBy   = AppSession.CurrentUser?.Username ?? "system",
                    DueDate     = due,
                    Status      = "Open",
                    Priority    = row.Cells["colPriority"].Value?.ToString() ?? cboDefaultPriority.SelectedItem?.ToString() ?? "Normal"
                });
            }

            if (toAdd.Count == 0)
            {
                MessageBox.Show(this, "Enter at least one task title.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int saved = 0;
            foreach (var task in toAdd)
            {
                try
                {
                    var taskId = _repo.Add(task);
                    saved++;
                    if (!string.IsNullOrWhiteSpace(task.Description))
                        SaveDescriptionMentions(taskId, task.Description, task.CreatedBy);
                    if (cboDefaultWorkflow.SelectedItem is TaskWorkflow selWorkflow)
                    {
                        try
                        {
                            var firstStatus = _repo.GetWorkflowStatuses(selWorkflow.WorkflowID)
                                .FirstOrDefault()?.StatusName;
                            _repo.SetTaskWorkflow(taskId, selWorkflow.WorkflowID, firstStatus);
                        }
                        catch { }
                    }
                    if (!string.IsNullOrEmpty(task.AssignedTo) &&
                        !task.AssignedTo.Equals(task.CreatedBy, StringComparison.OrdinalIgnoreCase))
                    {
                        var emails = _repo.GetUserEmails(new List<string> { task.AssignedTo });
                        var email = emails.FirstOrDefault().Email;
                        if (!string.IsNullOrWhiteSpace(email))
                            _ = NotificationService.NotifyTaskAssignedAsync(email, task.CreatedBy, task.Title);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Could not save '{task.Title}': {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            if (saved > 0) { DialogResult = DialogResult.OK; Close(); }
        }

        private void SaveDescriptionMentions(int taskId, string description, string createdBy)
        {
            var mentions = System.Text.RegularExpressions.Regex.Matches(description, @"@(\w+)")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var username in mentions)
            {
                try { _repo.AddMention(taskId, username, createdBy, description); }
                catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormAddTask.SaveDescriptionMentions]: {ex.Message}"); }
            }
        }
    }
}
