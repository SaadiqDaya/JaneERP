namespace JaneERP
{
    /// <summary>Create and manage task workflows and their ordered status steps.</summary>
    internal class FormWorkflowEditor : Form
    {
        private readonly TaskRepository _repo;

        private ListBox  lstWorkflows      = new();
        private ListBox  lstStatuses       = new();
        private TextBox  txtNewWorkflow    = new();
        private TextBox  txtNewStatus      = new();
        private Button   btnAddWorkflow    = new();
        private Button   btnDeleteWorkflow = new();
        private Button   btnAddStatus      = new();
        private Button   btnDeleteStatus   = new();
        private Button   btnMoveUp         = new();
        private Button   btnMoveDown       = new();
        private Label    lblStatusHeader   = new();
        private Button   btnClose          = new();

        public FormWorkflowEditor(TaskRepository repo)
        {
            _repo = repo;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            LoadWorkflows();
        }

        private void BuildUI()
        {
            Text          = "Workflow Manager";
            ClientSize    = new Size(680, 540);
            MinimumSize   = new Size(580, 460);
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label
            {
                Text      = "Workflow Manager",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            });

            // ── Left column: Workflows ────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = "Workflows",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(12, 48),
                AutoSize  = true
            });

            lstWorkflows.Location             = new Point(12, 70);
            lstWorkflows.Size                 = new Size(280, 340);
            lstWorkflows.Anchor               = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            lstWorkflows.SelectionMode        = SelectionMode.One;
            lstWorkflows.SelectedIndexChanged += LstWorkflows_SelectedIndexChanged;
            Controls.Add(lstWorkflows);

            txtNewWorkflow.Location        = new Point(12, 420);
            txtNewWorkflow.Size            = new Size(190, 23);
            txtNewWorkflow.Anchor          = AnchorStyles.Bottom | AnchorStyles.Left;
            txtNewWorkflow.PlaceholderText = "Workflow name…";
            txtNewWorkflow.KeyPress       += (_, e) => { if (e.KeyChar == (char)Keys.Enter) { BtnAddWorkflow_Click(null, EventArgs.Empty); e.Handled = true; } };
            Controls.Add(txtNewWorkflow);

            btnAddWorkflow.Text     = "Add";
            btnAddWorkflow.Size     = new Size(80, 23);
            btnAddWorkflow.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnAddWorkflow.Location = new Point(210, 420);
            btnAddWorkflow.Click   += BtnAddWorkflow_Click;
            Controls.Add(btnAddWorkflow);

            btnDeleteWorkflow.Text     = "Delete Workflow";
            btnDeleteWorkflow.Size     = new Size(130, 26);
            btnDeleteWorkflow.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnDeleteWorkflow.Location = new Point(12, 452);
            btnDeleteWorkflow.Click   += BtnDeleteWorkflow_Click;
            Controls.Add(btnDeleteWorkflow);

            // ── Right column: Statuses ────────────────────────────────────────────────
            lblStatusHeader.Text      = "Statuses:";
            lblStatusHeader.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblStatusHeader.ForeColor = Theme.TextSecondary;
            lblStatusHeader.Location  = new Point(310, 48);
            lblStatusHeader.AutoSize  = true;
            Controls.Add(lblStatusHeader);

            lstStatuses.Location  = new Point(310, 70);
            lstStatuses.Size      = new Size(348, 250);
            lstStatuses.Anchor    = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lstStatuses.SelectionMode = SelectionMode.One;
            Controls.Add(lstStatuses);

            txtNewStatus.Location        = new Point(310, 330);
            txtNewStatus.Size            = new Size(238, 23);
            txtNewStatus.Anchor          = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtNewStatus.PlaceholderText = "Status name…";
            txtNewStatus.KeyPress       += (_, e) => { if (e.KeyChar == (char)Keys.Enter) { BtnAddStatus_Click(null, EventArgs.Empty); e.Handled = true; } };
            Controls.Add(txtNewStatus);

            btnAddStatus.Text     = "Add";
            btnAddStatus.Size     = new Size(60, 23);
            btnAddStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnAddStatus.Location = new Point(598, 330);
            btnAddStatus.Click   += BtnAddStatus_Click;
            Controls.Add(btnAddStatus);

            btnDeleteStatus.Text     = "Delete";
            btnDeleteStatus.Size     = new Size(80, 26);
            btnDeleteStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnDeleteStatus.Location = new Point(310, 362);
            btnDeleteStatus.Click   += BtnDeleteStatus_Click;
            Controls.Add(btnDeleteStatus);

            btnMoveUp.Text     = "↑ Up";
            btnMoveUp.Size     = new Size(80, 26);
            btnMoveUp.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnMoveUp.Location = new Point(400, 362);
            btnMoveUp.Click   += BtnMoveUp_Click;
            Controls.Add(btnMoveUp);

            btnMoveDown.Text     = "↓ Down";
            btnMoveDown.Size     = new Size(80, 26);
            btnMoveDown.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnMoveDown.Location = new Point(490, 362);
            btnMoveDown.Click   += BtnMoveDown_Click;
            Controls.Add(btnMoveDown);

            Controls.Add(new Label
            {
                Text      = "Statuses are applied to tasks in order from top to bottom.",
                ForeColor = Theme.TextMuted,
                Font      = new Font("Segoe UI", 8F),
                Location  = new Point(310, 398),
                AutoSize  = true
            });

            // ── Close ─────────────────────────────────────────────────────────────────
            btnClose.Text     = "Close";
            btnClose.Size     = new Size(80, 28);
            btnClose.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(ClientSize.Width - 92, ClientSize.Height - 48);
            btnClose.Click   += (_, _) => Close();
            Controls.Add(btnClose);
        }

        // ── Data loading ──────────────────────────────────────────────────────────────

        private void LoadWorkflows()
        {
            var selected = lstWorkflows.SelectedItem as WorkflowListItem;
            lstWorkflows.Items.Clear();
            try
            {
                foreach (var wf in _repo.GetWorkflows())
                    lstWorkflows.Items.Add(new WorkflowListItem(wf.WorkflowID, wf.Name));
            }
            catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormWorkflowEditor.LoadWorkflows]: {ex.Message}"); }

            // Restore selection
            if (selected != null)
            {
                foreach (WorkflowListItem item in lstWorkflows.Items)
                    if (item.ID == selected.ID) { lstWorkflows.SelectedItem = item; break; }
            }
            if (lstWorkflows.SelectedIndex < 0) lstStatuses.Items.Clear();
            UpdateStatusHeader();
        }

        private void LoadStatuses()
        {
            lstStatuses.Items.Clear();
            if (lstWorkflows.SelectedItem is not WorkflowListItem wf) return;
            try
            {
                foreach (var s in _repo.GetWorkflowStatuses(wf.ID))
                    lstStatuses.Items.Add(new StatusListItem(s.StatusID, s.StatusName));
            }
            catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormWorkflowEditor.LoadStatuses]: {ex.Message}"); }
        }

        private void UpdateStatusHeader()
        {
            lblStatusHeader.Text = lstWorkflows.SelectedItem is WorkflowListItem wf
                ? $"Statuses for \"{wf.Name}\":"
                : "Statuses:";
        }

        // ── Event handlers ────────────────────────────────────────────────────────────

        private void LstWorkflows_SelectedIndexChanged(object? sender, EventArgs e)
        {
            LoadStatuses();
            UpdateStatusHeader();
        }

        private void BtnAddWorkflow_Click(object? sender, EventArgs e)
        {
            var name = txtNewWorkflow.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;
            try
            {
                _repo.AddWorkflow(name);
                txtNewWorkflow.Clear();
                LoadWorkflows();
                // Select the newly added workflow
                foreach (WorkflowListItem item in lstWorkflows.Items)
                    if (item.Name == name) { lstWorkflows.SelectedItem = item; break; }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void BtnDeleteWorkflow_Click(object? sender, EventArgs e)
        {
            if (lstWorkflows.SelectedItem is not WorkflowListItem wf) return;
            if (MessageBox.Show(this, $"Delete workflow \"{wf.Name}\" and all its statuses?",
                    "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { _repo.DeleteWorkflow(wf.ID); LoadWorkflows(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void BtnAddStatus_Click(object? sender, EventArgs e)
        {
            if (lstWorkflows.SelectedItem is not WorkflowListItem wf) return;
            var name = txtNewStatus.Text.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;
            try
            {
                _repo.AddWorkflowStatus(wf.ID, name);
                txtNewStatus.Clear();
                LoadStatuses();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void BtnDeleteStatus_Click(object? sender, EventArgs e)
        {
            if (lstStatuses.SelectedItem is not StatusListItem s) return;
            try { _repo.DeleteWorkflowStatus(s.ID); LoadStatuses(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void BtnMoveUp_Click(object? sender, EventArgs e)
        {
            if (lstStatuses.SelectedItem is not StatusListItem s) return;
            try
            {
                _repo.MoveWorkflowStatus(s.ID, moveUp: true);
                var idx = lstStatuses.SelectedIndex;
                LoadStatuses();
                lstStatuses.SelectedIndex = Math.Max(0, idx - 1);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void BtnMoveDown_Click(object? sender, EventArgs e)
        {
            if (lstStatuses.SelectedItem is not StatusListItem s) return;
            try
            {
                _repo.MoveWorkflowStatus(s.ID, moveUp: false);
                var idx = lstStatuses.SelectedIndex;
                LoadStatuses();
                lstStatuses.SelectedIndex = Math.Min(lstStatuses.Items.Count - 1, idx + 1);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ── List item helpers ─────────────────────────────────────────────────────────

        private class WorkflowListItem
        {
            public int    ID   { get; }
            public string Name { get; }
            public WorkflowListItem(int id, string name) { ID = id; Name = name; }
            public override string ToString() => Name;
        }

        private class StatusListItem
        {
            public int    ID   { get; }
            public string Name { get; }
            public StatusListItem(int id, string name) { ID = id; Name = name; }
            public override string ToString() => Name;
        }
    }
}
