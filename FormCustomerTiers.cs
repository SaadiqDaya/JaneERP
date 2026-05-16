using JaneERP.Data;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>
    /// Admin screen for assigning discount tiers to customers.
    /// When a customer has a tier assigned, creating a new order for them will
    /// automatically pre-select their tier discount.
    /// </summary>
    public class FormCustomerTiers : Form
    {
        private readonly DiscountTierRepository _tierRepo = new();
        private DataGridView dgv     = new();
        private ComboBox     cboTier = new();
        private Button       btnAssign = new();
        private Button       btnClear  = new();
        private Button       btnClose  = new();
        private Label        lblSel    = new();

        public FormCustomerTiers()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            LoadData();
        }

        private void BuildUI()
        {
            Text            = "Customer Tier Assignment";
            ClientSize      = new Size(700, 460);
            MinimumSize     = new Size(600, 380);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;

            dgv.Location          = new Point(12, 64);
            dgv.Size              = new Size(672, 300);
            dgv.Anchor            = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgv.ReadOnly          = true;
            dgv.AllowUserToAddRows    = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.SelectionMode     = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect       = false;
            dgv.AutoGenerateColumns = false;
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colID",    HeaderText = "ID",       DataPropertyName = "CustomerID",    Width = 50  });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName",  HeaderText = "Customer", DataPropertyName = "FullName",       Width = 220 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colEmail", HeaderText = "Email",    DataPropertyName = "Email",          Width = 200 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTier",  HeaderText = "Tier",     DataPropertyName = "TierName",       Width = 120 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDisc",  HeaderText = "Discount", DataPropertyName = "DiscountPercent", Width = 70  });
            dgv.SelectionChanged += (_, _) =>
            {
                if (dgv.SelectedRows.Count > 0)
                {
                    var tierName = dgv.SelectedRows[0].Cells["colTier"].Value?.ToString();
                    if (!string.IsNullOrEmpty(tierName))
                    {
                        foreach (DiscountTier item in cboTier.Items)
                            if (item.TierName == tierName) { cboTier.SelectedItem = item; break; }
                    }
                    else
                        cboTier.SelectedIndex = 0;
                }
            };
            Controls.Add(dgv);

            lblSel.AutoSize = true;
            lblSel.Location = new Point(12, 374);
            lblSel.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(lblSel);

            Controls.Add(new Label { Text = "Assign Tier:", AutoSize = true, Location = new Point(12, 400), Anchor = AnchorStyles.Bottom | AnchorStyles.Left });

            cboTier.Location      = new Point(100, 396);
            cboTier.Size          = new Size(180, 23);
            cboTier.DropDownStyle = ComboBoxStyle.DropDownList;
            cboTier.Anchor        = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(cboTier);

            btnAssign.Text     = "Assign";
            btnAssign.Location = new Point(290, 394);
            btnAssign.Size     = new Size(80, 28);
            btnAssign.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnAssign.Click   += BtnAssign_Click;
            Controls.Add(btnAssign);

            btnClear.Text     = "Clear Tier";
            btnClear.Location = new Point(378, 394);
            btnClear.Size     = new Size(90, 28);
            btnClear.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnClear.Click   += BtnClear_Click;
            Controls.Add(btnClear);

            btnClose.Text     = "Close";
            btnClose.Location = new Point(596, 394);
            btnClose.Size     = new Size(88, 28);
            btnClose.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Click   += (_, _) => Close();
            Controls.Add(btnClose);
            Theme.AddFormHeader(this, "🏷️  Customer Tiers");
        }

        private void LoadData()
        {
            try
            {
                // Load tiers into combo
                cboTier.Items.Clear();
                cboTier.Items.Add(new DiscountTier { TierID = 0, TierName = "(No Tier)" });
                foreach (var t in _tierRepo.GetActive())
                    cboTier.Items.Add(t);
                cboTier.DisplayMember = "TierName";
                cboTier.SelectedIndex = 0;

                // Load customers with tiers
                var rows = _tierRepo.GetCustomersWithTiers().ToList();
                dgv.DataSource = rows.Select(r => new
                {
                    CustomerID      = (int)r.CustomerID,
                    FullName        = (string?)r.FullName ?? "",
                    Email           = (string?)r.Email ?? "",
                    TierName        = (string?)r.TierName ?? "",
                    DiscountPercent = r.DiscountPercent != null ? $"{r.DiscountPercent:N0}%" : "",
                    TierID          = (int?)r.TierID
                }).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load customers: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAssign_Click(object? sender, EventArgs e)
        {
            if (dgv.SelectedRows.Count == 0)
            {
                MessageBox.Show(this, "Select a customer first.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cboTier.SelectedItem is not DiscountTier tier || tier.TierID == 0)
            {
                MessageBox.Show(this, "Select a tier to assign.", "No Tier",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            dynamic row = dgv.SelectedRows[0].DataBoundItem!;
            int customerId = (int)row.CustomerID;
            try
            {
                _tierRepo.SetCustomerTier(customerId, tier.TierID);
                LoadData();
                MessageBox.Show(this,
                    $"Tier '{tier.TierName}' assigned to customer.",
                    "Assigned", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnClear_Click(object? sender, EventArgs e)
        {
            if (dgv.SelectedRows.Count == 0) return;
            dynamic row = dgv.SelectedRows[0].DataBoundItem!;
            int customerId = (int)row.CustomerID;
            try
            {
                _tierRepo.SetCustomerTier(customerId, null);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
