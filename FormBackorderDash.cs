using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;

namespace JaneERP
{
    public class FormBackorderDash : Form
    {
        private readonly IBackorderRepository _repo = AppServices.Get<IBackorderRepository>();

        private DataGridView   _dgv         = new();
        private Button         _btnRefresh  = new();
        private Button         _btnFulfill  = new();
        private Button         _btnCancel   = new();
        private Button         _btnViewOrder = new();
        private Label          _lblStatus   = new();
        private List<Backorder> _rows       = [];

        public FormBackorderDash()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            Load += (_, _) => LoadData();
        }

        private void BuildUI()
        {
            Text          = "Backorder Dashboard";
            ClientSize    = new Size(1000, 580);
            MinimumSize   = new Size(780, 420);
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label
            {
                Text      = "Backorders",
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            });

            Controls.Add(new Label
            {
                Text      = "Open and partially-filled backorder lines. Fulfill when stock becomes available.",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(12, 44),
                AutoSize  = true
            });

            _btnRefresh.Text     = "Refresh";
            _btnRefresh.Location = new Point(12, 68);
            _btnRefresh.Size     = new Size(80, 27);
            _btnRefresh.Click   += (_, _) => LoadData();
            Theme.StyleButton(_btnRefresh);
            Controls.Add(_btnRefresh);

            _btnFulfill.Text     = "Fulfill Selected";
            _btnFulfill.Location = new Point(100, 68);
            _btnFulfill.Size     = new Size(120, 27);
            _btnFulfill.Enabled  = false;
            _btnFulfill.Click   += BtnFulfill_Click;
            Theme.StyleButton(_btnFulfill);
            Controls.Add(_btnFulfill);

            _btnCancel.Text     = "Cancel Selected";
            _btnCancel.Location = new Point(228, 68);
            _btnCancel.Size     = new Size(120, 27);
            _btnCancel.Enabled  = false;
            _btnCancel.Click   += BtnCancel_Click;
            Theme.StyleButton(_btnCancel);
            Controls.Add(_btnCancel);

            _btnViewOrder.Text     = "View Order";
            _btnViewOrder.Location = new Point(356, 68);
            _btnViewOrder.Size     = new Size(100, 27);
            _btnViewOrder.Enabled  = false;
            _btnViewOrder.Click   += BtnViewOrder_Click;
            Theme.StyleButton(_btnViewOrder);
            Controls.Add(_btnViewOrder);

            _dgv.Location        = new Point(12, 104);
            _dgv.Size            = new Size(976, 444);
            _dgv.Anchor          = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _dgv.ReadOnly        = true;
            _dgv.AllowUserToAddRows    = false;
            _dgv.AllowUserToDeleteRows = false;
            _dgv.AutoGenerateColumns   = false;
            _dgv.RowHeadersVisible     = false;
            _dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgv.SelectionChanged     += (_, _) => UpdateButtons();
            _dgv.CellFormatting       += Dgv_CellFormatting;
            _dgv.CellDoubleClick      += (_, _) => BtnViewOrder_Click(null, EventArgs.Empty);

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOrder",    HeaderText = "Order #",     Width = 80  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCustomer", HeaderText = "Customer",    Width = 160 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSKU",      HeaderText = "SKU",         Width = 110 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProduct",  HeaderText = "Product",     AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colBOQty",    HeaderText = "B/O Qty",     Width = 70  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFilled",   HeaderText = "Filled",      Width = 65  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRemain",   HeaderText = "Remaining",   Width = 80  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAvail",    HeaderText = "In Stock",    Width = 75  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStatus",   HeaderText = "Status",      Width = 110 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCreated",  HeaderText = "Created",     Width = 95  });
            Theme.StyleGrid(_dgv);
            Controls.Add(_dgv);

            _lblStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _lblStatus.Location = new Point(12, ClientSize.Height - 22);
            _lblStatus.AutoSize = true;
            Controls.Add(_lblStatus);
            SizeChanged += (_, _) => _lblStatus.Location = new Point(12, ClientSize.Height - 22);
        }

        private void LoadData()
        {
            try
            {
                _rows = _repo.GetOpenBackorders();
                _dgv.Rows.Clear();

                foreach (var bo in _rows)
                {
                    int idx = _dgv.Rows.Add();
                    var row = _dgv.Rows[idx];
                    row.Cells["colOrder"].Value    = bo.OrderNumber;
                    row.Cells["colCustomer"].Value = bo.CustomerName;
                    row.Cells["colSKU"].Value      = bo.SKU;
                    row.Cells["colProduct"].Value  = bo.ProductName;
                    row.Cells["colBOQty"].Value    = bo.BackorderedQty;
                    row.Cells["colFilled"].Value   = bo.FulfilledQty;
                    row.Cells["colRemain"].Value   = bo.RemainingQty;
                    row.Cells["colAvail"].Value    = bo.AvailableStock;
                    row.Cells["colStatus"].Value   = bo.Status;
                    row.Cells["colCreated"].Value  = bo.CreatedAt.ToString("yyyy-MM-dd");
                    row.Tag = bo;
                }

                _lblStatus.Text = $"{_rows.Count} open backorder line(s)";
                UpdateButtons();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load backorders: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateButtons()
        {
            bool hasSelection = _dgv.SelectedRows.Count > 0
                && _dgv.SelectedRows[0].Tag is Backorder;
            _btnFulfill.Enabled   = hasSelection;
            _btnCancel.Enabled    = hasSelection;
            _btnViewOrder.Enabled = hasSelection;
        }

        private void BtnViewOrder_Click(object? sender, EventArgs e)
        {
            if (_dgv.SelectedRows.Count == 0 || _dgv.SelectedRows[0].Tag is not Backorder bo) return;

            MessageBox.Show(this,
                $"Order #:       {bo.OrderNumber}\n" +
                $"Customer:      {bo.CustomerName ?? "—"}\n" +
                $"Product:       {bo.ProductName}  ({bo.SKU})\n" +
                $"B/O Qty:       {bo.BackorderedQty}   Filled: {bo.FulfilledQty}   Remaining: {bo.RemainingQty}\n" +
                $"In Stock:      {bo.AvailableStock}\n" +
                $"Status:        {bo.Status}\n" +
                $"Created:       {bo.CreatedAt:yyyy-MM-dd}",
                $"Order #{bo.OrderNumber} — Backorder Detail",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnFulfill_Click(object? sender, EventArgs e)
        {
            if (_dgv.SelectedRows.Count == 0 || _dgv.SelectedRows[0].Tag is not Backorder bo) return;

            int avail = bo.AvailableStock;
            if (avail <= 0)
            {
                MessageBox.Show(this, $"No stock available for {bo.SKU}.",
                    "No Stock", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = _repo.FulfillBackorders(bo.ProductID, avail);
            string summary = result.Messages.Count > 0
                ? string.Join("\n", result.Messages)
                : "No backorders fulfilled.";

            MessageBox.Show(this, $"Fulfilled {result.FulfilledCount} backorder(s):\n\n{summary}",
                "Fulfillment Result", MessageBoxButtons.OK, MessageBoxIcon.Information);

            LoadData();
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            if (_dgv.SelectedRows.Count == 0 || _dgv.SelectedRows[0].Tag is not Backorder bo) return;

            var confirm = MessageBox.Show(this,
                $"Cancel backorder for Order #{bo.OrderNumber} — {bo.ProductName} ({bo.RemainingQty} units)?",
                "Confirm Cancel", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            _repo.CancelBackorder(bo.BackorderID);
            LoadData();
        }

        private void Dgv_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || _dgv.Rows[e.RowIndex].Tag is not Backorder bo) return;

            if (bo.AvailableStock >= bo.RemainingQty)
                e.CellStyle.ForeColor = Color.FromArgb(80, 210, 100);   // can fulfill — green
            else if (bo.AvailableStock > 0)
                e.CellStyle.ForeColor = Color.FromArgb(255, 160, 60);   // partial stock — amber
            // else: default colour (red-ish via theme or just white)
        }
    }
}
