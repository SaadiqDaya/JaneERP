using JaneERP.Models;

namespace JaneERP
{
    /// <summary>
    /// Dialog for completing a Work Order.
    /// Captures actual completed qty, optional scrap qty + reason, and completion notes.
    /// </summary>
    public class FormWorkOrderComplete : Form
    {
        /// <summary>Actual finished-goods quantity to add to inventory.</summary>
        public int    CompletedQty { get; private set; }
        /// <summary>Units scrapped (materials consumed but not added to finished goods).</summary>
        public int    ScrapQty     { get; private set; }
        /// <summary>Reason for scrap; empty string when scrap is zero.</summary>
        public string ScrapReason  { get; private set; } = "";
        /// <summary>Completion notes.</summary>
        public string Notes        { get; private set; } = "";

        private readonly int      _plannedQty;
        private NumericUpDown     nudCompleted    = new();
        private NumericUpDown     nudScrap        = new();
        private ComboBox          cboReason       = new();
        private TextBox           txtNotes        = new();
        private Label             lblScrapLbl     = new();
        private Label             lblReasonLbl    = new();
        private Label             lblRemaining    = new();

        public FormWorkOrderComplete(WorkOrder wo)
        {
            _plannedQty = wo.Quantity;
            BuildUI(wo);
            Theme.Apply(this);
            Theme.MakeBorderless(this);
        }

        private void BuildUI(WorkOrder wo)
        {
            Text            = "Complete Work Order";
            ClientSize      = new Size(420, 330);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;

            int x = 16, y = 16;

            // ── Title ───────────────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = $"WO #{wo.WorkOrderID}  —  {wo.ProductName}",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(x, y),
                Size      = new Size(388, 22),
                AutoSize  = false
            });
            y += 36;

            // ── Planned Qty ─────────────────────────────────────────────────────
            Controls.Add(new Label { Text = "Planned Qty:", Location = new Point(x, y), AutoSize = true });
            Controls.Add(new Label
            {
                Text     = wo.Quantity.ToString(),
                Location = new Point(200, y),
                AutoSize = true,
                Font     = new Font("Segoe UI", 9F, FontStyle.Bold)
            });
            y += 30;

            // ── Completed Qty ───────────────────────────────────────────────────
            Controls.Add(new Label { Text = "Completed Qty:", Location = new Point(x, y), AutoSize = true });
            nudCompleted.Location  = new Point(200, y - 2);
            nudCompleted.Size      = new Size(90, 23);
            nudCompleted.Minimum   = 0;
            nudCompleted.Maximum   = wo.Quantity;
            nudCompleted.Value     = wo.Quantity;
            nudCompleted.ValueChanged += NudCompleted_ValueChanged;
            Controls.Add(nudCompleted);
            y += 30;

            // ── Scrap Qty ───────────────────────────────────────────────────────
            lblScrapLbl.Text     = "Scrap Qty:";
            lblScrapLbl.Location = new Point(x, y);
            lblScrapLbl.AutoSize = true;
            Controls.Add(lblScrapLbl);
            nudScrap.Location  = new Point(200, y - 2);
            nudScrap.Size      = new Size(90, 23);
            nudScrap.Minimum   = 0;
            nudScrap.Maximum   = 0;
            nudScrap.Value     = 0;
            nudScrap.ValueChanged += NudScrap_ValueChanged;
            Controls.Add(nudScrap);

            // "Remaining" hint label (planned - completed - scrap)
            lblRemaining.Location  = new Point(300, y);
            lblRemaining.AutoSize  = true;
            lblRemaining.ForeColor = Color.DimGray;
            lblRemaining.Font      = new Font("Segoe UI", 8F, FontStyle.Italic);
            Controls.Add(lblRemaining);
            y += 30;

            // ── Scrap Reason ────────────────────────────────────────────────────
            lblReasonLbl.Text     = "Scrap Reason:";
            lblReasonLbl.Location = new Point(x, y);
            lblReasonLbl.AutoSize = true;
            lblReasonLbl.Enabled  = false;
            Controls.Add(lblReasonLbl);
            cboReason.Location      = new Point(200, y - 2);
            cboReason.Size          = new Size(200, 23);
            cboReason.DropDownStyle = ComboBoxStyle.DropDownList;
            cboReason.Enabled       = false;
            cboReason.Items.AddRange(new object[]
            {
                "Defective", "Material Issue", "Machine Issue", "Over-run", "Other"
            });
            Controls.Add(cboReason);
            y += 30;

            // ── Notes ────────────────────────────────────────────────────────────
            Controls.Add(new Label { Text = "Notes:", Location = new Point(x, y), AutoSize = true });
            txtNotes.Location = new Point(200, y - 2);
            txtNotes.Size     = new Size(200, 23);
            Controls.Add(txtNotes);
            y += 42;

            // ── Buttons ──────────────────────────────────────────────────────────
            var btnOK = new Button
            {
                Text     = "Complete",
                Size     = new Size(100, 30),
                Location = new Point(196, y)
            };
            btnOK.Click += BtnOK_Click;
            Controls.Add(btnOK);

            var btnCancel = new Button
            {
                Text         = "Cancel",
                Size         = new Size(80, 30),
                Location     = new Point(308, y),
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(btnCancel);
            CancelButton = btnCancel;

            UpdateRemainingLabel();
        }

        private void NudCompleted_ValueChanged(object? sender, EventArgs e)
        {
            int remaining = _plannedQty - (int)nudCompleted.Value;
            nudScrap.Maximum = remaining;
            if (nudScrap.Value > remaining) nudScrap.Value = remaining;
            UpdateRemainingLabel();
            UpdateScrapReasonState();
        }

        private void NudScrap_ValueChanged(object? sender, EventArgs e)
        {
            UpdateRemainingLabel();
            UpdateScrapReasonState();
        }

        private void UpdateRemainingLabel()
        {
            int leftOver = _plannedQty - (int)nudCompleted.Value - (int)nudScrap.Value;
            lblRemaining.Text = leftOver > 0 ? $"({leftOver} unaccounted)" : "";
        }

        private void UpdateScrapReasonState()
        {
            bool hasScrap          = nudScrap.Value > 0;
            lblReasonLbl.Enabled   = hasScrap;
            cboReason.Enabled      = hasScrap;
            if (!hasScrap) cboReason.SelectedIndex = -1;
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            if (nudScrap.Value > 0 && cboReason.SelectedIndex < 0)
            {
                MessageBox.Show(this, "Please select a scrap reason.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (nudCompleted.Value == 0 && nudScrap.Value == 0)
            {
                if (MessageBox.Show(this,
                        "Completed qty and scrap qty are both 0.\nThis will mark the work order complete with no output.\n\nContinue?",
                        "Zero Output",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            CompletedQty = (int)nudCompleted.Value;
            ScrapQty     = (int)nudScrap.Value;
            ScrapReason  = cboReason.SelectedItem?.ToString() ?? "";
            Notes        = txtNotes.Text.Trim();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
