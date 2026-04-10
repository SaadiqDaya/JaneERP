using JaneERP.Manufacturing;
using JaneERP.Models;
using JaneERP.Logging;
using JaneERP.Security;

namespace JaneERP
{
    /// <summary>Lists open Work Orders and allows marking them complete (with optional stock update).</summary>
    public class FormWorkOrders : Form
    {
        private readonly ManufacturingRepository _moRepo = new();

        private DataGridView dgvWOs    = new();
        private TextBox      txtNotes  = new();
        private Button       btnStart  = new();
        private Button       btnComplete = new();
        private Button       btnClose  = new();
        private Label        lblSel    = new();

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

            dgvWOs.Location          = new Point(12, 40);
            dgvWOs.Size              = new Size(876, 380);
            dgvWOs.Anchor            = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvWOs.ReadOnly          = true;
            dgvWOs.AllowUserToAddRows    = false;
            dgvWOs.AllowUserToDeleteRows = false;
            dgvWOs.SelectionMode     = DataGridViewSelectionMode.FullRowSelect;
            dgvWOs.MultiSelect       = false;
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
                var wo = SelectedWO();
                lblSel.Text = wo == null ? "" : $"Selected: {wo.ProductName} × {wo.Quantity}";
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

            btnComplete.Text     = "Mark Complete + Add Stock";
            btnComplete.Location = new Point(668, 468);
            btnComplete.Size     = new Size(180, 30);
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
                dgvWOs.DataSource = _moRepo.GetPendingWorkOrders();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load work orders: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private WorkOrder? SelectedWO()
        {
            if (dgvWOs.SelectedRows.Count == 0) return null;
            return dgvWOs.SelectedRows[0].DataBoundItem as WorkOrder;
        }

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            var wo = SelectedWO();
            if (wo == null) { MessageBox.Show(this, "Select a work order first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            try
            {
                _moRepo.UpdateWorkOrderStatus(wo.WorkOrderID, "InProgress");
                LoadWorkOrders();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnComplete_Click(object? sender, EventArgs e)
        {
            var wo = SelectedWO();
            if (wo == null) { MessageBox.Show(this, "Select a work order first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            if (MessageBox.Show(this,
                    $"Complete WO #{wo.WorkOrderID}?\n\nThis will add {wo.Quantity} unit(s) of '{wo.ProductName}' to inventory.",
                    "Confirm Complete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                // CompleteWorkOrder atomically marks done + adds finished-goods + deducts BOM parts
                _moRepo.CompleteWorkOrder(wo.WorkOrderID, txtNotes.Text.Trim());

                AppLogger.Audit(AppSession.CurrentUser?.Username, "WorkOrderComplete",
                    $"WO#{wo.WorkOrderID} product={wo.ProductName} qty={wo.Quantity}");

                txtNotes.Clear();
                LoadWorkOrders();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
