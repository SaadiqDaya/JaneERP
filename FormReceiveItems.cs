using JaneERP.Data;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>Dialog for recording received goods against a Purchase Order.</summary>
    public class FormReceiveItems : Form
    {
        private readonly SupplierRepository _repo;
        private readonly PurchaseOrder      _po;

        private DataGridView dgvItems  = new();
        private Button       btnSave   = new();
        private Button       btnCancel = new();
        private Label        lblStatus = new();

        public FormReceiveItems(SupplierRepository repo, PurchaseOrder po)
        {
            _repo = repo;
            _po   = po;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            PopulateGrid();
        }

        private void BuildUI()
        {
            Text          = $"Receive Items – {_po.PONumber}";
            ClientSize    = new Size(820, 520);
            MinimumSize   = new Size(700, 420);
            StartPosition = FormStartPosition.CenterParent;

            // ── Header info ───────────────────────────────────────────────────────
            var lblTitle = new Label
            {
                Text      = $"Receive Items for {_po.PONumber}",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                AutoSize  = false,
                Location  = new Point(12, 12),
                Size      = new Size(600, 28)
            };
            Controls.Add(lblTitle);

            var lblInfo = new Label
            {
                Text      = $"Supplier: {_po.SupplierName}   |   Status: {_po.Status}",
                AutoSize  = true,
                Location  = new Point(12, 44),
                ForeColor = Theme.TextSecondary
            };
            Controls.Add(lblInfo);

            var lblNote = new Label
            {
                Text      = "Enter quantities being received now (leave 0 to skip an item):",
                AutoSize  = true,
                Location  = new Point(12, 66),
                ForeColor = Theme.TextMuted
            };
            Controls.Add(lblNote);

            // ── DataGridView ──────────────────────────────────────────────────────
            dgvItems.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvItems.Location = new Point(12, 94);
            dgvItems.Size     = new Size(796, 370);
            dgvItems.AllowUserToAddRows    = false;
            dgvItems.AllowUserToDeleteRows = false;
            dgvItems.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvItems.MultiSelect           = false;
            dgvItems.AutoGenerateColumns   = false;
            dgvItems.RowHeadersVisible     = false;
            dgvItems.DataError            += (_, e) => e.Cancel = true;

            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cName", HeaderText = "Item Name", DataPropertyName = "ItemName",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true
            });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cSKU", HeaderText = "SKU", DataPropertyName = "SKU",
                Width = 100, ReadOnly = true
            });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cOrdered", HeaderText = "Ordered", DataPropertyName = "QuantityOrdered",
                Width = 70, ReadOnly = true
            });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cReceived", HeaderText = "Already Received", DataPropertyName = "QuantityReceived",
                Width = 110, ReadOnly = true
            });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "cRemaining", HeaderText = "Remaining", Width = 80, ReadOnly = true
            });

            // "Receiving Now" column – editable numeric
            var colNow = new DataGridViewTextBoxColumn
            {
                Name       = "cNow",
                HeaderText = "Receiving Now",
                Width      = 100
            };
            dgvItems.Columns.Add(colNow);

            dgvItems.CellFormatting += DgvItems_CellFormatting;
            Controls.Add(dgvItems);

            // ── Status label ──────────────────────────────────────────────────────
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(12, 474);
            lblStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(lblStatus);

            // ── Buttons ───────────────────────────────────────────────────────────
            btnSave.Text     = "Save Receivals";
            btnSave.Size     = new Size(120, 30);
            btnSave.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click   += BtnSave_Click;
            Controls.Add(btnSave);

            btnCancel.Text     = "Cancel";
            btnCancel.Size     = new Size(80, 30);
            btnCancel.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click   += (_, _) => Close();
            Controls.Add(btnCancel);

            SizeChanged += (_, _) => PositionBottomControls();
            Load        += (_, _) => PositionBottomControls();
        }

        private void PositionBottomControls()
        {
            int bottom = ClientSize.Height - 8;
            int right  = ClientSize.Width  - 8;
            btnCancel.Location = new Point(right - btnCancel.Width, bottom - btnCancel.Height);
            btnSave.Location   = new Point(right - btnCancel.Width - btnSave.Width - 8, bottom - btnSave.Height);
            lblStatus.Location = new Point(12, bottom - lblStatus.Height);
        }

        private void DgvItems_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _po.Items.Count) return;
            if (dgvItems.Columns[e.ColumnIndex].Name == "cRemaining")
            {
                var item  = _po.Items[e.RowIndex];
                e.Value   = item.QuantityRemaining;
                e.FormattingApplied = true;
            }
        }

        private void PopulateGrid()
        {
            dgvItems.DataSource = null;
            dgvItems.DataSource = _po.Items;

            for (int r = 0; r < _po.Items.Count; r++)
            {
                // Default "Receiving Now" to 0
                dgvItems.Rows[r].Cells["cNow"].Value = "0";
            }
            lblStatus.Text = $"{_po.Items.Count} line item(s)";
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            // Commit any in-progress edit
            dgvItems.EndEdit();

            var receivals = new List<(int poItemId, int qty)>();
            bool anyQty = false;

            for (int r = 0; r < _po.Items.Count; r++)
            {
                var item    = _po.Items[r];
                string? raw = dgvItems.Rows[r].Cells["cNow"].Value?.ToString();
                if (!int.TryParse(raw, out int qty) || qty < 0)
                {
                    MessageBox.Show(this, $"Invalid quantity on row {r + 1}. Enter a non-negative integer.",
                        "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                int maxAllowed = item.QuantityRemaining;
                if (qty > maxAllowed)
                {
                    MessageBox.Show(this,
                        $"Row {r + 1}: Cannot receive {qty} — only {maxAllowed} remaining for '{item.ItemName}'.",
                        "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (qty > 0)
                {
                    receivals.Add((item.POItemID, qty));
                    anyQty = true;
                }
            }

            if (!anyQty)
            {
                MessageBox.Show(this, "Enter a quantity greater than 0 for at least one item.",
                    "Nothing to Receive", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                _repo.ReceiveItems(_po.POID, receivals);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to save receivals:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
