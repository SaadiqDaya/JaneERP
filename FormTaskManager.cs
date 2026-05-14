using JaneERP.Security;
using JaneERP.Services;

namespace JaneERP
{
    /// <summary>View, create and manage tasks. Assign to users, set due dates.</summary>
    public class FormTaskManager : Form
    {
        private readonly TaskRepository _repo = new();

        private DataGridView dgvTasks        = new();
        private ComboBox     cboFilter       = new();
        private ComboBox     cboStatusFilter = new();
        private Button       btnAdd          = new();
        private Button       btnDone         = new();
        private Button       btnDelete       = new();
        private Button       btnEmail        = new();
        private Button       btnWorkflows    = new();
        private Button       btnClose        = new();
        private Label        lblFilter   = new();

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
            ClientSize      = new Size(920, 740);
            MinimumSize     = new Size(760, 620);
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

            Controls.Add(new Label { Text = "Status:", Location = new Point(316, 52), AutoSize = true });
            cboStatusFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            cboStatusFilter.Location      = new Point(362, 49);
            cboStatusFilter.Size          = new Size(130, 23);
            cboStatusFilter.Items.AddRange(new object[] { "All Statuses", "Open", "In Progress", "Done", "Overdue" });
            cboStatusFilter.SelectedIndex = 0;
            cboStatusFilter.SelectedIndexChanged += (_, _) => LoadTasks();
            Controls.Add(cboStatusFilter);

            // ── Grid ─────────────────────────────────────────────────────────────
            dgvTasks.AutoGenerateColumns = false;
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTitle",     HeaderText = "Title",       DataPropertyName = "Title",       Width = 200 });
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAssigned",  HeaderText = "Assigned To", DataPropertyName = "AssignedTo",  Width = 120 });
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDue",       HeaderText = "Due Date",    DataPropertyName = "DueDate",     Width = 100 });
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStatus",    HeaderText = "Status",      DataPropertyName = "Status",      Width = 90  });
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPriority",  HeaderText = "Priority",    DataPropertyName = "Priority",    Width = 80  });
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCreatedBy", HeaderText = "Created By",  DataPropertyName = "CreatedBy",   Width = 110 });
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colWorkflow",  HeaderText = "Workflow",    DataPropertyName = "WorkflowName", Width = 120 });
            dgvTasks.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDesc",      HeaderText = "Description", DataPropertyName = "Description",  AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvTasks.ReadOnly              = true;
            dgvTasks.AllowUserToAddRows    = false;
            dgvTasks.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvTasks.MultiSelect           = true;     // allow multi-select for bulk mark-done
            dgvTasks.Anchor                = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            dgvTasks.Location              = new Point(12, 80);
            dgvTasks.Size                  = new Size(896, 390);

            dgvTasks.CellFormatting += (s, e) =>
            {
                if (dgvTasks.Columns["colDue"] is DataGridViewColumn colDue &&
                    e.ColumnIndex == colDue.Index && e.Value is DateTime dt)
                    e.Value = dt.ToString("yyyy-MM-dd");
            };

            // Color overdue rows red, Done rows muted; highlight priority column
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
            };

            // Double-click → open task detail
            dgvTasks.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0 || dgvTasks.Rows[e.RowIndex].DataBoundItem is not ErpTask task) return;
                using var detail = new FormTaskDetail(_repo, task);
                detail.ShowDialog(this);
                if (detail.Changed) LoadTasks();
                LoadMentions(); // refresh mentions after viewing/commenting on a task
            };

            Controls.Add(dgvTasks);

            // ── Mentions panel ────────────────────────────────────────────────────
            lblMentions.Text      = "Recent Mentions (@you):";
            lblMentions.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblMentions.ForeColor = Theme.Gold;
            lblMentions.Location  = new Point(12, 482);
            lblMentions.AutoSize  = true;
            Controls.Add(lblMentions);

            btnClearMentions.Text     = "Clear All";
            btnClearMentions.Size     = new Size(80, 22);
            btnClearMentions.Location = new Point(200, 480);
            btnClearMentions.Click   += BtnClearMentions_Click;
            Controls.Add(btnClearMentions);

            btnClearSelected.Text     = "Clear Selected";
            btnClearSelected.Size     = new Size(100, 22);
            btnClearSelected.Location = new Point(288, 480);
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
            dgvMentions.Location           = new Point(12, 508);
            dgvMentions.Size               = new Size(896, 120);

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
            btnAdd.Location = new Point(12, 694);
            btnAdd.Click   += BtnAdd_Click;
            Controls.Add(btnAdd);

            btnDone.Text     = "Mark Done";
            btnDone.Size     = new Size(110, 30);
            btnDone.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnDone.Location = new Point(140, 694);
            btnDone.Click   += BtnDone_Click;
            Controls.Add(btnDone);

            btnDelete.Text     = "Delete";
            btnDelete.Size     = new Size(80, 30);
            btnDelete.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnDelete.Location = new Point(258, 694);
            btnDelete.Click   += BtnDelete_Click;
            Controls.Add(btnDelete);

            btnEmail.Text     = "Email Outstanding";
            btnEmail.Size     = new Size(150, 30);
            btnEmail.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnEmail.Location = new Point(346, 694);
            btnEmail.Click   += BtnEmail_Click;
            Controls.Add(btnEmail);

            btnWorkflows.Text     = "Manage Workflows";
            btnWorkflows.Size     = new Size(140, 30);
            btnWorkflows.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnWorkflows.Location = new Point(504, 694);
            btnWorkflows.Click   += (_, _) => { using var f = new FormWorkflowEditor(_repo); f.ShowDialog(this); };
            Controls.Add(btnWorkflows);

            btnClose.Text     = "Close";
            btnClose.Size     = new Size(80, 30);
            btnClose.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(828, 694);
            btnClose.Click   += (_, _) => Close();
            Controls.Add(btnClose);
        }

        private void LoadTasks()
        {
            string? filterUser = cboFilter.SelectedIndex == 1
                ? AppSession.CurrentUser?.Username
                : null;

            var statusSel    = cboStatusFilter.SelectedItem?.ToString() ?? "All Statuses";
            bool filterOverdue = statusSel == "Overdue";
            string? filterStatus = statusSel is "All Statuses" or "Overdue" ? null : statusSel;

            var tasks = _repo.GetAll(filterUser, filterStatus);
            if (filterOverdue)
                tasks = tasks.Where(t => t.Status != "Done" && t.DueDate.Date < DateTime.Today).ToList();
            dgvTasks.DataSource = tasks;
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
            // Don't auto-clear — user must explicitly click "Clear Selected" or "Clear All"
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
                LoadMentions(); // refresh immediately so new @mentions appear instantly
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
            LoadTasks();
            if (done > 0) MessageBox.Show(this, $"{done} task(s) marked Done.", "Done",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (dgvTasks.SelectedRows.Count == 0) return;
            if (dgvTasks.SelectedRows[0].DataBoundItem is not ErpTask task) return;
            if (MessageBox.Show(this, $"Delete task '{task.Title}'?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { _repo.Delete(task.TaskID); LoadTasks(); }
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
                sb.AppendLine($"• [{t.Status}] {t.Title} — Assigned: {t.AssignedTo} — Due: {t.DueDate:yyyy-MM-dd}{(string.IsNullOrEmpty(t.Description) ? "" : $"\n  {t.Description}")}");

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
    }

    // ── Add Task(s) dialog ────────────────────────────────────────────────────────
    internal class FormAddTask : Form
    {
        private readonly TaskRepository _repo;

        // One task entry is a row: title | description | assigned-to | due-date | priority
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

            // Default assign + due
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

            // Grid: title | description | assigned | due | priority
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

            // Seed one blank row
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
                    // Parse @mentions in the description and save to TaskMentions
                    if (!string.IsNullOrWhiteSpace(task.Description))
                        SaveDescriptionMentions(taskId, task.Description, task.CreatedBy);
                    // Assign default workflow if one was selected
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
                    // Notify assignee (fire-and-forget; skip if assigning to yourself)
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
