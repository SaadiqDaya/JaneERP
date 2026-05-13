using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;
using JaneERP.Logging;
using JaneERP.Security;

namespace JaneERP
{
    /// <summary>Lists open Work Orders and allows marking them complete (with optional stock update).</summary>
    public class FormWorkOrders : Form
    {
        private readonly IManufacturingRepository _moRepo = AppServices.Get<IManufacturingRepository>();

        private DataGridView   dgvWOs      = new();
        private TextBox        txtNotes    = new();
        private Button         btnStart    = new();
        private Button         btnComplete = new();
        private Button         btnClose    = new();
        private Label          lblSel      = new();
        private DateTimePicker dtpFrom     = new();
        private DateTimePicker dtpTo       = new();
        private CheckBox       chkDateFilter = new();

        public FormWorkOrders()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            LoadWorkOrders();
        }

        private void BuildUI()
        {
            Text            = "Process Work Orders";
            ClientSize      = new Size(900, 560);
            MinimumSize     = new Size(900, 560);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;

            var lbl = new Label { Text = "Open Work Orders", Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold, AutoSize = true, Location = new Point(12, 12) };
            Controls.Add(lbl);

            // ── Date range filter ─────────────────────────────────────────────────
            chkDateFilter.Text     = "Filter by date:";
            chkDateFilter.AutoSize = true;
            chkDateFilter.Location = new Point(220, 14);
            chkDateFilter.CheckedChanged += (_, _) => { dtpFrom.Enabled = dtpTo.Enabled = chkDateFilter.Checked; LoadWorkOrders(); };
            Controls.Add(chkDateFilter);

            dtpFrom.Location = new Point(336, 10);
            dtpFrom.Size     = new Size(120, 23);
            dtpFrom.Format   = DateTimePickerFormat.Short;
            dtpFrom.Value    = DateTime.Today.AddMonths(-1);
            dtpFrom.Enabled  = false;
            dtpFrom.ValueChanged += (_, _) => { if (chkDateFilter.Checked) LoadWorkOrders(); };
            Controls.Add(dtpFrom);

            Controls.Add(new Label { Text = "→", Location = new Point(460, 14), AutoSize = true });

            dtpTo.Location = new Point(478, 10);
            dtpTo.Size     = new Size(120, 23);
            dtpTo.Format   = DateTimePickerFormat.Short;
            dtpTo.Value    = DateTime.Today;
            dtpTo.Enabled  = false;
            dtpTo.ValueChanged += (_, _) => { if (chkDateFilter.Checked) LoadWorkOrders(); };
            Controls.Add(dtpTo);

            dgvWOs.Location          = new Point(12, 40);
            dgvWOs.Size              = new Size(876, 380);
            dgvWOs.Anchor            = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvWOs.ReadOnly          = true;
            dgvWOs.AllowUserToAddRows    = false;
            dgvWOs.AllowUserToDeleteRows = false;
            dgvWOs.SelectionMode     = DataGridViewSelectionMode.FullRowSelect;
            dgvWOs.MultiSelect       = true;
            dgvWOs.AutoGenerateColumns = false;
            dgvWOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cWOID",    HeaderText = "WO #",     DataPropertyName = "WorkOrderID", Width = 60  });
            dgvWOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cMOID",    HeaderText = "MO #",     DataPropertyName = "MOID",        Width = 60  });
            dgvWOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cProduct", HeaderText = "Product",  DataPropertyName = "ProductName", Width = 260 });
            dgvWOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSKU",     HeaderText = "SKU",      DataPropertyName = "SKU",         Width = 120 });
            dgvWOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cQty",     HeaderText = "Qty",      DataPropertyName = "Quantity",    Width = 60  });
            dgvWOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cStatus",  HeaderText = "Status",   DataPropertyName = "Status",      Width = 90  });
            dgvWOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cNotes",   HeaderText = "Notes",    DataPropertyName = "Notes",       Width = 180 });
            dgvWOs.SelectionChanged += (_, _) =>
            {
                var wos = SelectedWOs();
                lblSel.Text = wos.Count switch
                {
                    0 => "",
                    1 => $"Selected: {wos[0].ProductName} × {wos[0].Quantity}",
                    _ => $"{wos.Count} work orders selected"
                };
            };
            Controls.Add(dgvWOs);

            lblSel.AutoSize = true;
            lblSel.Location = new Point(12, 428);
            lblSel.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(lblSel);

            Controls.Add(new Label { Text = "Completion Notes:", AutoSize = true,
                Location = new Point(12, 455), Anchor = AnchorStyles.Bottom | AnchorStyles.Left });
            txtNotes.Location   = new Point(12, 472);
            txtNotes.Size       = new Size(500, 23);
            txtNotes.Anchor     = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(txtNotes);

            btnStart.Text     = "Mark In Progress";
            btnStart.Location = new Point(520, 468);
            btnStart.Size     = new Size(140, 30);
            btnStart.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnStart.Click   += BtnStart_Click;
            Controls.Add(btnStart);

            btnComplete.Text     = "Complete Selected + Add Stock";
            btnComplete.Location = new Point(648, 468);
            btnComplete.Size     = new Size(200, 30);
            btnComplete.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnComplete.Click   += BtnComplete_Click;
            Controls.Add(btnComplete);

            btnClose.Text     = "Close";
            btnClose.Location = new Point(796, 516);
            btnClose.Size     = new Size(90, 30);
            btnClose.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Click   += (_, _) => Close();
            Controls.Add(btnClose);
        }

        private void LoadWorkOrders()
        {
            try
            {
                DateTime? from = chkDateFilter.Checked ? dtpFrom.Value.Date : (DateTime?)null;
                DateTime? to   = chkDateFilter.Checked ? dtpTo.Value.Date   : (DateTime?)null;
                dgvWOs.DataSource = _moRepo.GetPendingWorkOrders(from, to);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load work orders: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private WorkOrder? SelectedWO() =>
            dgvWOs.SelectedRows.Count == 0 ? null : dgvWOs.SelectedRows[0].DataBoundItem as WorkOrder;

        private List<WorkOrder> SelectedWOs() =>
            dgvWOs.SelectedRows
                  .Cast<DataGridViewRow>()
                  .Select(r => r.DataBoundItem as WorkOrder)
                  .Where(w => w != null)
                  .Select(w => w!)
                  .ToList();

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            var wo = SelectedWO();
            if (wo == null)
            {
                MessageBox.Show(this, "Select a work order first.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var lines = _moRepo.GetWOReservationItems(wo.WorkOrderID);
                if (lines.Count > 0)
                {
                    using var resForm = new FormStockReservation(
                        $"Reserve Parts — WO #{wo.WorkOrderID}  ({wo.ProductName} × {wo.Quantity})", lines);
                    if (resForm.ShowDialog(this) != DialogResult.OK) return;
                    if (resForm.ConfirmedLines?.Count > 0)
                        _moRepo.SaveWOReservations(wo.WorkOrderID, resForm.ConfirmedLines);
                }

                _moRepo.UpdateWorkOrderStatus(wo.WorkOrderID, "InProgress");
                AppLogger.Audit(AppSession.CurrentUser?.Username, "WorkOrderStarted",
                    $"WO#{wo.WorkOrderID} product={wo.ProductName} qty={wo.Quantity}");
                LoadWorkOrders();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnComplete_Click(object? sender, EventArgs e)
        {
            var wos = SelectedWOs();
            if (wos.Count == 0)
            {
                MessageBox.Show(this, "Select one or more work orders first.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Aggregate parts shortages across all selected WOs
            var allShortages = new List<string>();
            foreach (var wo in wos)
            {
                var negParts = _moRepo.GetNegativePartsForWorkOrder(wo.WorkOrderID);
                foreach (var p in negParts)
                    allShortages.Add($"  WO#{wo.WorkOrderID} {p.PartName}: need {p.RequiredQty}, have {p.CurrentStock}");
            }

            if (allShortages.Count > 0)
            {
                var warn = MessageBox.Show(this,
                    $"Insufficient parts stock for {(wos.Count == 1 ? "this work order" : "some work orders")}:\n\n" +
                    string.Join("\n", allShortages) +
                    "\n\nCompleting will result in negative part stock. Continue anyway?",
                    "Insufficient Parts Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (warn != DialogResult.Yes) return;
            }

            int succeeded = 0, failed = 0;
            var errors = new List<string>();

            foreach (var wo in wos)
            {
                using var dlg = new FormWorkOrderComplete(wo);
                if (dlg.ShowDialog(this) != DialogResult.OK) continue;

                try
                {
                    _moRepo.PartialCompleteWorkOrder(wo.WorkOrderID, dlg.CompletedQty, dlg.ScrapQty,
                        string.IsNullOrEmpty(dlg.ScrapReason) ? null : dlg.ScrapReason, dlg.Notes);
                    AppLogger.Audit(AppSession.CurrentUser?.Username, "WorkOrderComplete",
                        $"WO#{wo.WorkOrderID} product={wo.ProductName} planned={wo.Quantity} completed={dlg.CompletedQty} scrap={dlg.ScrapQty}");
                    succeeded++;
                }
                catch (Exception ex)
                {
                    errors.Add($"WO#{wo.WorkOrderID}: {ex.Message}");
                    failed++;
                }
            }

            txtNotes.Clear();
            LoadWorkOrders();

            if (failed > 0)
                MessageBox.Show(this,
                    $"{succeeded} completed, {failed} failed:\n\n" + string.Join("\n", errors),
                    "Partial Completion", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
