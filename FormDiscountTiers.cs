using JaneERP.Data;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>Manage discount tiers used for customer pricing.</summary>
    public class FormDiscountTiers : Form
    {
        private readonly DiscountTierRepository _repo = new();
        private DataGridView dgvTiers = new();

        public FormDiscountTiers()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            LoadTiers();
        }

        private void BuildUI()
        {
            Text          = "Discount Tiers";
            ClientSize    = new Size(700, 500);
            MinimumSize   = new Size(560, 380);
            StartPosition = FormStartPosition.CenterParent;

            var lblTitle = new Label
            {
                Text      = "Discount Tiers",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            };
            Controls.Add(lblTitle);

            // ── Grid ──────────────────────────────────────────────────────────────────
            dgvTiers.AutoGenerateColumns = false;
            dgvTiers.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colTierName", HeaderText = "Tier Name",
                DataPropertyName = "TierName",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true
            });
            dgvTiers.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDiscount", HeaderText = "Discount %",
                DataPropertyName = "DiscountPercent",
                Width = 100, ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" }
            });
            dgvTiers.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDesc", HeaderText = "Description",
                DataPropertyName = "Description",
                Width = 220, ReadOnly = true
            });
            dgvTiers.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "colActive", HeaderText = "Active",
                DataPropertyName = "IsActive",
                Width = 60, ReadOnly = true
            });

            dgvTiers.AllowUserToAddRows    = false;
            dgvTiers.AllowUserToDeleteRows = false;
            dgvTiers.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvTiers.MultiSelect           = false;
            dgvTiers.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvTiers.Location = new Point(12, 48);
            dgvTiers.Size     = new Size(676, 380);
            Controls.Add(dgvTiers);

            // ── Buttons ───────────────────────────────────────────────────────────────
            int bx = 12, by = ClientSize.Height - 44;

            var btnAdd = new Button
            {
                Text = "Add Tier", Size = new Size(100, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Location = new Point(bx, by)
            };
            btnAdd.Click += (_, _) => OpenEditDialog(null);
            Controls.Add(btnAdd);

            bx += 110;
            var btnEdit = new Button
            {
                Text = "Edit Tier", Size = new Size(100, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Location = new Point(bx, by)
            };
            btnEdit.Click += (_, _) =>
            {
                var tier = SelectedTier();
                if (tier == null) return;
                OpenEditDialog(tier);
            };
            Controls.Add(btnEdit);

            bx += 110;
            var btnDeactivate = new Button
            {
                Text = "Deactivate", Size = new Size(100, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Location = new Point(bx, by)
            };
            btnDeactivate.Click += (_, _) =>
            {
                var tier = SelectedTier();
                if (tier == null) return;
                if (MessageBox.Show(this, $"Deactivate tier \"{tier.TierName}\"?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                _repo.Deactivate(tier.TierID);
                LoadTiers();
            };
            Controls.Add(btnDeactivate);

            var btnClose = new Button
            {
                Text     = "Close",
                Size     = new Size(90, 30),
                Anchor   = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point(ClientSize.Width - 102, by)
            };
            btnClose.Click += (_, _) => Close();
            Controls.Add(btnClose);
        }

        private void LoadTiers()
        {
            try
            {
                var tiers = _repo.GetAll().ToList();
                dgvTiers.DataSource = null;
                dgvTiers.DataSource = tiers;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Load error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private DiscountTier? SelectedTier()
        {
            if (dgvTiers.SelectedRows.Count == 0) return null;
            return dgvTiers.SelectedRows[0].DataBoundItem as DiscountTier;
        }

        private void OpenEditDialog(DiscountTier? existing)
        {
            using var dlg = new FormEditTier(existing);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            var tier = dlg.Result!;
            try
            {
                if (existing == null)
                    _repo.Add(tier);
                else
                    _repo.Update(tier);
                LoadTiers();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Save error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Inner dialog ──────────────────────────────────────────────────────────────
        private class FormEditTier : Form
        {
            public DiscountTier? Result { get; private set; }

            private TextBox        txtName   = new();
            private NumericUpDown  nudPct    = new();
            private TextBox        txtDesc   = new();
            private readonly int?  _tierId;

            public FormEditTier(DiscountTier? existing)
            {
                _tierId = existing?.TierID;
                BuildUI(existing);
                Theme.Apply(this);
            }

            private void BuildUI(DiscountTier? existing)
            {
                Text          = existing == null ? "Add Discount Tier" : "Edit Discount Tier";
                ClientSize    = new Size(380, 220);
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox   = false;
                MinimizeBox   = false;
                StartPosition = FormStartPosition.CenterParent;

                int lx = 14, cx = 130, row = 20;

                Controls.Add(new Label { Text = "Tier Name *", Location = new Point(lx, row + 2), AutoSize = true });
                txtName.Location = new Point(cx, row);
                txtName.Size     = new Size(220, 23);
                txtName.Text     = existing?.TierName ?? "";
                Controls.Add(txtName);

                row += 36;
                Controls.Add(new Label { Text = "Discount %", Location = new Point(lx, row + 2), AutoSize = true });
                nudPct.Location  = new Point(cx, row);
                nudPct.Size      = new Size(100, 23);
                nudPct.Minimum   = 0;
                nudPct.Maximum   = 100;
                nudPct.DecimalPlaces = 2;
                nudPct.Value     = existing != null ? (decimal)existing.DiscountPercent : 0;
                Controls.Add(nudPct);

                row += 36;
                Controls.Add(new Label { Text = "Description", Location = new Point(lx, row + 2), AutoSize = true });
                txtDesc.Location = new Point(cx, row);
                txtDesc.Size     = new Size(220, 23);
                txtDesc.Text     = existing?.Description ?? "";
                Controls.Add(txtDesc);

                row += 50;
                var btnOk = new Button
                {
                    Text     = "Save",
                    Size     = new Size(90, 30),
                    Location = new Point(cx, row),
                    DialogResult = DialogResult.OK
                };
                btnOk.Click += BtnOk_Click;
                Controls.Add(btnOk);
                AcceptButton = btnOk;

                var btnCancel = new Button
                {
                    Text         = "Cancel",
                    Size         = new Size(90, 30),
                    Location     = new Point(cx + 100, row),
                    DialogResult = DialogResult.Cancel
                };
                Controls.Add(btnCancel);
                CancelButton = btnCancel;
            }

            private void BtnOk_Click(object? sender, EventArgs e)
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show(this, "Tier name is required.", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }

                Result = new DiscountTier
                {
                    TierID          = _tierId ?? 0,
                    TierName        = txtName.Text.Trim(),
                    DiscountPercent = nudPct.Value,
                    Description     = string.IsNullOrWhiteSpace(txtDesc.Text) ? null : txtDesc.Text.Trim(),
                    IsActive        = true
                };
            }
        }
    }
}
