namespace JaneERP
{
    /// <summary>Create and manage task workflows, workflow statuses, and task templates.</summary>
    internal class FormWorkflowEditor : Form
    {
        private readonly TaskRepository _repo;
        private readonly string         _currentUser;

        // ── Nav buttons ───────────────────────────────────────────────────────────
        private Button _btnNavWorkflows  = new();
        private Button _btnNavTemplates  = new();

        // ── Workflows panel ───────────────────────────────────────────────────────
        private Panel    _pnlWorkflows      = new();
        private ListBox  lstWorkflows       = new();
        private ListBox  lstStatuses        = new();
        private TextBox  txtNewWorkflow     = new();
        private TextBox  txtNewStatus       = new();
        private Button   btnAddWorkflow     = new();
        private Button   btnDeleteWorkflow  = new();
        private Button   btnAddStatus       = new();
        private Button   btnDeleteStatus    = new();
        private Button   btnMoveUp          = new();
        private Button   btnMoveDown        = new();
        private Label    lblStatusHeader    = new();

        // ── Templates panel ───────────────────────────────────────────────────────
        private Panel         _pnlTemplates        = new();
        private ListBox       _lstTemplates         = new();
        private Button        _btnAddTemplate       = new();
        private Button        _btnDeleteTemplate    = new();
        private TextBox       _txtTmplName          = new();
        private TextBox       _txtTmplDesc          = new();
        private DataGridView  _dgvTemplateItems     = new();
        private Button        _btnAddItem           = new();
        private Button        _btnDeleteItem        = new();
        private Button        _btnItemUp            = new();
        private Button        _btnItemDown          = new();
        private Button        _btnSaveTemplate      = new();
        private Button        _btnCreateFromTemplate = new();
        private Panel         _pnlTmplDetail        = new();

        // ── Close ─────────────────────────────────────────────────────────────────
        private Button   btnClose = new();

        public FormWorkflowEditor(TaskRepository repo)
        {
            _repo        = repo;
            _currentUser = JaneERP.Security.AppSession.CurrentUser?.Username ?? "system";
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            ShowWorkflowsPanel();
            LoadWorkflows();
        }

        // ── UI construction ───────────────────────────────────────────────────────

        private void BuildUI()
        {
            Text          = "Workflow Manager";
            ClientSize    = new Size(740, 560);
            MinimumSize   = new Size(620, 480);
            StartPosition = FormStartPosition.CenterParent;

            // ── Nav toggle buttons ────────────────────────────────────────────────
            _btnNavWorkflows.Text     = "Workflows";
            _btnNavWorkflows.Size     = new Size(110, 28);
            _btnNavWorkflows.Location = new Point(12, 56);
            _btnNavWorkflows.Click   += (_, _) => ShowWorkflowsPanel();
            Controls.Add(_btnNavWorkflows);

            _btnNavTemplates.Text     = "Templates";
            _btnNavTemplates.Size     = new Size(110, 28);
            _btnNavTemplates.Location = new Point(130, 56);
            _btnNavTemplates.Click   += (_, _) => ShowTemplatesPanel();
            Controls.Add(_btnNavTemplates);

            // ── Close ─────────────────────────────────────────────────────────────
            btnClose.Text     = "Close";
            btnClose.Size     = new Size(80, 28);
            btnClose.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(ClientSize.Width - 92, ClientSize.Height - 44);
            btnClose.Click   += (_, _) => Close();
            Controls.Add(btnClose);

            BuildWorkflowsPanel();
            BuildTemplatesPanel();
        }

        private void BuildWorkflowsPanel()
        {
            _pnlWorkflows.Location = new Point(0, 80);
            _pnlWorkflows.Size     = new Size(ClientSize.Width, ClientSize.Height - 130);
            _pnlWorkflows.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            // ── Left column: Workflows ────────────────────────────────────────────
            _pnlWorkflows.Controls.Add(new Label
            {
                Text      = "Workflows",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(12, 0),
                AutoSize  = true
            });

            lstWorkflows.Location             = new Point(12, 22);
            lstWorkflows.Size                 = new Size(280, 330);
            lstWorkflows.Anchor               = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            lstWorkflows.SelectionMode        = SelectionMode.One;
            lstWorkflows.SelectedIndexChanged += LstWorkflows_SelectedIndexChanged;
            _pnlWorkflows.Controls.Add(lstWorkflows);

            txtNewWorkflow.Location        = new Point(12, 362);
            txtNewWorkflow.Size            = new Size(188, 23);
            txtNewWorkflow.Anchor          = AnchorStyles.Bottom | AnchorStyles.Left;
            txtNewWorkflow.PlaceholderText = "Workflow name…";
            txtNewWorkflow.KeyPress       += (_, e) => { if (e.KeyChar == (char)Keys.Enter) { BtnAddWorkflow_Click(null, EventArgs.Empty); e.Handled = true; } };
            _pnlWorkflows.Controls.Add(txtNewWorkflow);

            btnAddWorkflow.Text     = "Add";
            btnAddWorkflow.Size     = new Size(80, 23);
            btnAddWorkflow.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnAddWorkflow.Location = new Point(208, 362);
            btnAddWorkflow.Click   += BtnAddWorkflow_Click;
            _pnlWorkflows.Controls.Add(btnAddWorkflow);

            btnDeleteWorkflow.Text     = "Delete Workflow";
            btnDeleteWorkflow.Size     = new Size(130, 26);
            btnDeleteWorkflow.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnDeleteWorkflow.Location = new Point(12, 394);
            btnDeleteWorkflow.Click   += BtnDeleteWorkflow_Click;
            _pnlWorkflows.Controls.Add(btnDeleteWorkflow);

            // ── Right column: Statuses ────────────────────────────────────────────
            lblStatusHeader.Text      = "Statuses:";
            lblStatusHeader.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblStatusHeader.ForeColor = Theme.TextSecondary;
            lblStatusHeader.Location  = new Point(310, 0);
            lblStatusHeader.AutoSize  = true;
            _pnlWorkflows.Controls.Add(lblStatusHeader);

            lstStatuses.Location      = new Point(310, 22);
            lstStatuses.Size          = new Size(400, 240);
            lstStatuses.Anchor        = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lstStatuses.SelectionMode = SelectionMode.One;
            _pnlWorkflows.Controls.Add(lstStatuses);

            txtNewStatus.Location        = new Point(310, 272);
            txtNewStatus.Size            = new Size(290, 23);
            txtNewStatus.Anchor          = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            txtNewStatus.PlaceholderText = "Status name…";
            txtNewStatus.KeyPress       += (_, e) => { if (e.KeyChar == (char)Keys.Enter) { BtnAddStatus_Click(null, EventArgs.Empty); e.Handled = true; } };
            _pnlWorkflows.Controls.Add(txtNewStatus);

            btnAddStatus.Text     = "Add";
            btnAddStatus.Size     = new Size(60, 23);
            btnAddStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnAddStatus.Location = new Point(650, 272);
            btnAddStatus.Click   += BtnAddStatus_Click;
            _pnlWorkflows.Controls.Add(btnAddStatus);

            btnDeleteStatus.Text     = "Delete";
            btnDeleteStatus.Size     = new Size(80, 26);
            btnDeleteStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnDeleteStatus.Location = new Point(310, 304);
            btnDeleteStatus.Click   += BtnDeleteStatus_Click;
            _pnlWorkflows.Controls.Add(btnDeleteStatus);

            btnMoveUp.Text     = "↑ Up";
            btnMoveUp.Size     = new Size(80, 26);
            btnMoveUp.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnMoveUp.Location = new Point(400, 304);
            btnMoveUp.Click   += BtnMoveUp_Click;
            _pnlWorkflows.Controls.Add(btnMoveUp);

            btnMoveDown.Text     = "↓ Down";
            btnMoveDown.Size     = new Size(80, 26);
            btnMoveDown.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnMoveDown.Location = new Point(490, 304);
            btnMoveDown.Click   += BtnMoveDown_Click;
            _pnlWorkflows.Controls.Add(btnMoveDown);

            _pnlWorkflows.Controls.Add(new Label
            {
                Text      = "Statuses are applied to tasks in order from top to bottom.",
                ForeColor = Theme.TextMuted,
                Font      = new Font("Segoe UI", 8F),
                Location  = new Point(310, 338),
                AutoSize  = true
            });

            Controls.Add(_pnlWorkflows);
        }

        private void BuildTemplatesPanel()
        {
            _pnlTemplates.Location = new Point(0, 80);
            _pnlTemplates.Size     = new Size(ClientSize.Width, ClientSize.Height - 130);
            _pnlTemplates.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            // ── Left column: Template list ────────────────────────────────────────
            _pnlTemplates.Controls.Add(new Label
            {
                Text      = "Templates",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(12, 0),
                AutoSize  = true
            });

            _lstTemplates.Location             = new Point(12, 22);
            _lstTemplates.Size                 = new Size(200, 330);
            _lstTemplates.Anchor               = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            _lstTemplates.SelectionMode        = SelectionMode.One;
            _lstTemplates.SelectedIndexChanged += LstTemplates_SelectedIndexChanged;
            _pnlTemplates.Controls.Add(_lstTemplates);

            _btnAddTemplate.Text     = "Add Template";
            _btnAddTemplate.Size     = new Size(100, 26);
            _btnAddTemplate.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnAddTemplate.Location = new Point(12, 362);
            _btnAddTemplate.Click   += BtnAddTemplate_Click;
            _pnlTemplates.Controls.Add(_btnAddTemplate);

            _btnDeleteTemplate.Text     = "Delete";
            _btnDeleteTemplate.Size     = new Size(80, 26);
            _btnDeleteTemplate.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnDeleteTemplate.Location = new Point(120, 362);
            _btnDeleteTemplate.Click   += BtnDeleteTemplate_Click;
            _pnlTemplates.Controls.Add(_btnDeleteTemplate);

            // ── Right column: Template detail ─────────────────────────────────────
            _pnlTmplDetail.Location = new Point(226, 0);
            _pnlTmplDetail.Size     = new Size(500, 430);
            _pnlTmplDetail.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _pnlTmplDetail.Visible  = false;

            _pnlTmplDetail.Controls.Add(new Label
            {
                Text      = "Name",
                ForeColor = Theme.TextSecondary,
                Font      = new Font("Segoe UI", 8F),
                Location  = new Point(0, 0),
                AutoSize  = true
            });

            _txtTmplName.Location        = new Point(0, 18);
            _txtTmplName.Size            = new Size(490, 23);
            _txtTmplName.Anchor          = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _txtTmplName.PlaceholderText = "Template name…";
            _pnlTmplDetail.Controls.Add(_txtTmplName);

            _pnlTmplDetail.Controls.Add(new Label
            {
                Text      = "Description",
                ForeColor = Theme.TextSecondary,
                Font      = new Font("Segoe UI", 8F),
                Location  = new Point(0, 48),
                AutoSize  = true
            });

            _txtTmplDesc.Location        = new Point(0, 66);
            _txtTmplDesc.Size            = new Size(490, 46);
            _txtTmplDesc.Anchor          = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _txtTmplDesc.Multiline       = true;
            _txtTmplDesc.PlaceholderText = "Optional description…";
            _pnlTmplDetail.Controls.Add(_txtTmplDesc);

            _pnlTmplDetail.Controls.Add(new Label
            {
                Text      = "Tasks",
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(0, 120),
                AutoSize  = true
            });

            // DataGridView for template items
            _dgvTemplateItems.Location              = new Point(0, 140);
            _dgvTemplateItems.Size                  = new Size(490, 180);
            _dgvTemplateItems.Anchor                = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            _dgvTemplateItems.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _dgvTemplateItems.ColumnHeadersHeight   = 24;
            _dgvTemplateItems.RowHeadersVisible     = false;
            _dgvTemplateItems.MultiSelect           = false;
            _dgvTemplateItems.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgvTemplateItems.AllowUserToAddRows    = false;
            _dgvTemplateItems.AllowUserToDeleteRows = false;
            _dgvTemplateItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTitle",      HeaderText = "Title",       Width = 160, SortMode = DataGridViewColumnSortMode.NotSortable });
            _dgvTemplateItems.Columns.Add(new DataGridViewComboBoxColumn
            {
                Name       = "colPriority",
                HeaderText = "Priority",
                Width      = 80,
                SortMode   = DataGridViewColumnSortMode.NotSortable,
                Items      = { "Low", "Normal", "High", "Urgent" }
            });
            _dgvTemplateItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDaysOffset", HeaderText = "Days Offset", Width = 80,  SortMode = DataGridViewColumnSortMode.NotSortable });
            _dgvTemplateItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDesc",       HeaderText = "Description", Width = 160, SortMode = DataGridViewColumnSortMode.NotSortable });
            _pnlTmplDetail.Controls.Add(_dgvTemplateItems);

            // Item action buttons
            _btnAddItem.Text     = "Add Item";
            _btnAddItem.Size     = new Size(80, 26);
            _btnAddItem.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnAddItem.Location = new Point(0, 328);
            _btnAddItem.Click   += BtnAddItem_Click;
            _pnlTmplDetail.Controls.Add(_btnAddItem);

            _btnDeleteItem.Text     = "Delete Item";
            _btnDeleteItem.Size     = new Size(90, 26);
            _btnDeleteItem.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnDeleteItem.Location = new Point(88, 328);
            _btnDeleteItem.Click   += BtnDeleteItem_Click;
            _pnlTmplDetail.Controls.Add(_btnDeleteItem);

            _btnItemUp.Text     = "↑ Up";
            _btnItemUp.Size     = new Size(70, 26);
            _btnItemUp.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnItemUp.Location = new Point(186, 328);
            _btnItemUp.Click   += BtnItemUp_Click;
            _pnlTmplDetail.Controls.Add(_btnItemUp);

            _btnItemDown.Text     = "↓ Down";
            _btnItemDown.Size     = new Size(70, 26);
            _btnItemDown.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnItemDown.Location = new Point(264, 328);
            _btnItemDown.Click   += BtnItemDown_Click;
            _pnlTmplDetail.Controls.Add(_btnItemDown);

            _btnSaveTemplate.Text     = "Save Template";
            _btnSaveTemplate.Size     = new Size(110, 26);
            _btnSaveTemplate.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnSaveTemplate.Location = new Point(380, 328);
            _btnSaveTemplate.Click   += BtnSaveTemplate_Click;
            _pnlTmplDetail.Controls.Add(_btnSaveTemplate);

            _btnCreateFromTemplate.Text     = "Create Tasks from Template…";
            _btnCreateFromTemplate.Size     = new Size(210, 26);
            _btnCreateFromTemplate.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnCreateFromTemplate.Location = new Point(0, 364);
            _btnCreateFromTemplate.Click   += BtnCreateFromTemplate_Click;
            _pnlTmplDetail.Controls.Add(_btnCreateFromTemplate);

            _pnlTemplates.Controls.Add(_pnlTmplDetail);
            Controls.Add(_pnlTemplates);
            Theme.AddFormHeader(this, "⚙️  Workflow Manager");
        }

        // ── Panel switching ───────────────────────────────────────────────────────

        private void ShowWorkflowsPanel()
        {
            _pnlWorkflows.Visible  = true;
            _pnlTemplates.Visible  = false;
            _btnNavWorkflows.Font  = new Font("Segoe UI", 9F, FontStyle.Bold);
            _btnNavTemplates.Font  = new Font("Segoe UI", 9F, FontStyle.Regular);
        }

        private void ShowTemplatesPanel()
        {
            _pnlWorkflows.Visible  = false;
            _pnlTemplates.Visible  = true;
            _btnNavTemplates.Font  = new Font("Segoe UI", 9F, FontStyle.Bold);
            _btnNavWorkflows.Font  = new Font("Segoe UI", 9F, FontStyle.Regular);
            LoadTemplates();
        }

        // ── Data loading — Workflows ──────────────────────────────────────────────

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

        // ── Data loading — Templates ──────────────────────────────────────────────

        private void LoadTemplates()
        {
            var selected = _lstTemplates.SelectedItem as TemplateListItem;
            _lstTemplates.Items.Clear();
            try
            {
                foreach (var t in _repo.GetTemplates())
                    _lstTemplates.Items.Add(new TemplateListItem(t.TemplateId, t.Name));
            }
            catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormWorkflowEditor.LoadTemplates]: {ex.Message}"); }

            // Restore selection
            if (selected != null)
            {
                foreach (TemplateListItem item in _lstTemplates.Items)
                    if (item.ID == selected.ID) { _lstTemplates.SelectedItem = item; break; }
            }
            if (_lstTemplates.SelectedIndex < 0) _pnlTmplDetail.Visible = false;
        }

        private void LoadTemplateDetail()
        {
            if (_lstTemplates.SelectedItem is not TemplateListItem t)
            {
                _pnlTmplDetail.Visible = false;
                return;
            }
            try
            {
                var tmpl = _repo.GetTemplate(t.ID);
                if (tmpl == null) { _pnlTmplDetail.Visible = false; return; }

                _txtTmplName.Text = tmpl.Name;
                _txtTmplDesc.Text = tmpl.Description;
                _dgvTemplateItems.Rows.Clear();

                foreach (var item in tmpl.Items.OrderBy(i => i.SortOrder))
                {
                    _dgvTemplateItems.Rows.Add(item.Title, item.Priority,
                        item.DueDaysOffset.ToString(), item.Description);
                }

                _pnlTmplDetail.Visible = true;
            }
            catch (Exception ex) { JaneERP.Logging.AppLogger.Info($"[FormWorkflowEditor.LoadTemplateDetail]: {ex.Message}"); }
        }

        // ── Workflow event handlers ───────────────────────────────────────────────

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

        // ── Template event handlers ───────────────────────────────────────────────

        private void LstTemplates_SelectedIndexChanged(object? sender, EventArgs e)
            => LoadTemplateDetail();

        private void BtnAddTemplate_Click(object? sender, EventArgs e)
        {
            try
            {
                var tmpl = new TaskTemplate
                {
                    Name      = "New Template",
                    CreatedBy = _currentUser
                };
                int id = _repo.SaveTemplate(tmpl);
                LoadTemplates();
                foreach (TemplateListItem item in _lstTemplates.Items)
                    if (item.ID == id) { _lstTemplates.SelectedItem = item; break; }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void BtnDeleteTemplate_Click(object? sender, EventArgs e)
        {
            if (_lstTemplates.SelectedItem is not TemplateListItem t) return;
            if (MessageBox.Show(this, $"Delete template \"{t.Name}\"?",
                    "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                _repo.DeleteTemplate(t.ID);
                _pnlTmplDetail.Visible = false;
                LoadTemplates();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void BtnAddItem_Click(object? sender, EventArgs e)
        {
            _dgvTemplateItems.Rows.Add("New task", "Normal", "0", "");
            var idx = _dgvTemplateItems.Rows.Count - 1;
            _dgvTemplateItems.CurrentCell = _dgvTemplateItems.Rows[idx].Cells["colTitle"];
            _dgvTemplateItems.BeginEdit(true);
        }

        private void BtnDeleteItem_Click(object? sender, EventArgs e)
        {
            if (_dgvTemplateItems.CurrentRow == null) return;
            _dgvTemplateItems.Rows.Remove(_dgvTemplateItems.CurrentRow);
        }

        private void BtnItemUp_Click(object? sender, EventArgs e)
        {
            int idx = _dgvTemplateItems.CurrentCell?.RowIndex ?? -1;
            if (idx <= 0) return;
            SwapDgvRows(idx, idx - 1);
            _dgvTemplateItems.Rows[idx - 1].Selected = true;
            _dgvTemplateItems.CurrentCell = _dgvTemplateItems.Rows[idx - 1].Cells[0];
        }

        private void BtnItemDown_Click(object? sender, EventArgs e)
        {
            int idx = _dgvTemplateItems.CurrentCell?.RowIndex ?? -1;
            if (idx < 0 || idx >= _dgvTemplateItems.Rows.Count - 1) return;
            SwapDgvRows(idx, idx + 1);
            _dgvTemplateItems.Rows[idx + 1].Selected = true;
            _dgvTemplateItems.CurrentCell = _dgvTemplateItems.Rows[idx + 1].Cells[0];
        }

        private void SwapDgvRows(int a, int b)
        {
            var rowA = _dgvTemplateItems.Rows[a];
            var rowB = _dgvTemplateItems.Rows[b];
            for (int c = 0; c < _dgvTemplateItems.Columns.Count; c++)
                (rowA.Cells[c].Value, rowB.Cells[c].Value) = (rowB.Cells[c].Value, rowA.Cells[c].Value);
        }

        private void BtnSaveTemplate_Click(object? sender, EventArgs e)
        {
            if (_lstTemplates.SelectedItem is not TemplateListItem t) return;
            var name = _txtTmplName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Template name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                var items = new List<TaskTemplateItem>();
                for (int i = 0; i < _dgvTemplateItems.Rows.Count; i++)
                {
                    var row = _dgvTemplateItems.Rows[i];
                    var title = row.Cells["colTitle"].Value?.ToString()?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(title)) continue;
                    int.TryParse(row.Cells["colDaysOffset"].Value?.ToString(), out int offset);
                    items.Add(new TaskTemplateItem
                    {
                        Title         = title,
                        Priority      = row.Cells["colPriority"].Value?.ToString() ?? "Normal",
                        DueDaysOffset = offset,
                        Description   = row.Cells["colDesc"].Value?.ToString() ?? "",
                        SortOrder     = i
                    });
                }

                var tmpl = new TaskTemplate
                {
                    TemplateId  = t.ID,
                    Name        = name,
                    Description = _txtTmplDesc.Text.Trim(),
                    CreatedBy   = _currentUser,
                    Items       = items
                };
                _repo.SaveTemplate(tmpl);

                // Refresh list item name in the listbox
                LoadTemplates();
                foreach (TemplateListItem item in _lstTemplates.Items)
                    if (item.ID == t.ID) { _lstTemplates.SelectedItem = item; break; }

                MessageBox.Show(this, "Template saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void BtnCreateFromTemplate_Click(object? sender, EventArgs e)
        {
            if (_lstTemplates.SelectedItem is not TemplateListItem t) return;
            try
            {
                using var dlg = new FormCreateFromTemplate(_repo, t.ID, _currentUser);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    MessageBox.Show(this, $"{dlg.CreatedCount} task(s) created successfully.",
                        "Tasks Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        // ── List item helpers ─────────────────────────────────────────────────────

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

        private class TemplateListItem
        {
            public int    ID   { get; }
            public string Name { get; }
            public TemplateListItem(int id, string name) { ID = id; Name = name; }
            public override string ToString() => Name;
        }
    }

    // ── "Create Tasks from Template" dialog ──────────────────────────────────────

    internal class FormCreateFromTemplate : Form
    {
        private readonly TaskRepository _repo;
        private readonly int            _templateId;
        private readonly string         _currentUser;

        private ComboBox        _cboAssignTo  = new();
        private DateTimePicker  _dtpStartDate = new();
        private Button          _btnCreate    = new();
        private Button          _btnCancel    = new();

        public int CreatedCount { get; private set; }

        public FormCreateFromTemplate(TaskRepository repo, int templateId, string currentUser)
        {
            _repo        = repo;
            _templateId  = templateId;
            _currentUser = currentUser;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
        }

        private void BuildUI()
        {
            Text          = "Create Tasks from Template";
            ClientSize    = new Size(360, 200);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label
            {
                Text      = "Create Tasks from Template",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            });

            Controls.Add(new Label
            {
                Text      = "Assign to:",
                ForeColor = Theme.TextSecondary,
                Font      = new Font("Segoe UI", 9F),
                Location  = new Point(12, 52),
                AutoSize  = true
            });

            _cboAssignTo.Location     = new Point(12, 70);
            _cboAssignTo.Size         = new Size(330, 23);
            _cboAssignTo.DropDownStyle = ComboBoxStyle.DropDownList;
            try
            {
                foreach (var u in _repo.GetAllUsernames())
                    _cboAssignTo.Items.Add(u);
            }
            catch { /* swallow — usernames list is cosmetic */ }
            Controls.Add(_cboAssignTo);

            Controls.Add(new Label
            {
                Text      = "Start date:",
                ForeColor = Theme.TextSecondary,
                Font      = new Font("Segoe UI", 9F),
                Location  = new Point(12, 100),
                AutoSize  = true
            });

            _dtpStartDate.Location = new Point(12, 118);
            _dtpStartDate.Size     = new Size(200, 23);
            _dtpStartDate.Format   = DateTimePickerFormat.Short;
            _dtpStartDate.Value    = DateTime.Today;
            Controls.Add(_dtpStartDate);

            _btnCreate.Text     = "Create Tasks";
            _btnCreate.Size     = new Size(110, 28);
            _btnCreate.Location = new Point(12, 156);
            _btnCreate.Click   += BtnCreate_Click;
            Controls.Add(_btnCreate);

            _btnCancel.Text     = "Cancel";
            _btnCancel.Size     = new Size(80, 28);
            _btnCancel.Location = new Point(130, 156);
            _btnCancel.Click   += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(_btnCancel);
        }

        private void BtnCreate_Click(object? sender, EventArgs e)
        {
            var assignTo = _cboAssignTo.SelectedItem?.ToString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(assignTo))
            {
                MessageBox.Show(this, "Please select a user to assign the tasks to.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                var tasks = _repo.CreateTasksFromTemplate(
                    _templateId, assignTo, _dtpStartDate.Value.Date, _currentUser);
                CreatedCount  = tasks.Count;
                DialogResult  = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
