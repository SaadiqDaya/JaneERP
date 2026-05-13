using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;
using JaneERP.Security;

namespace JaneERP
{
    /// <summary>
    /// Creates a return (RMA) against an existing sales order.
    /// Opened from the Customers form when an order is selected.
    /// </summary>
    public class FormCreateReturn : Form
    {
        private readonly IReturnRepository    _returnRepo    = AppServices.Get<IReturnRepository>();
        private readonly ICustomerRepository  _custRepo      = AppServices.Get<ICustomerRepository>();
        private readonly ILocationRepository  _locationRepo  = AppServices.Get<ILocationRepository>();

        private readonly int    _salesOrderId;
        private readonly string _orderNumber;

        private DataGridView _dgvItems  = new();
        private TextBox      _txtReason = new();
        private TextBox      _txtNotes  = new();
        private Button       _btnSubmit = new();
        private Label        _lblStatus = new();

        private List<CustomerOrderItem> _originalItems = [];
        private List<Location>          _locations     = [];

        public FormCreateReturn(int salesOrderId, string orderNumber)
        {
            _salesOrderId = salesOrderId;
            _orderNumber  = orderNumber;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            Load += (_, _) => LoadData();
        }

        private void BuildUI()
        {
            Text          = $"Create Return — Order {_orderNumber}";
            ClientSize    = new Size(860, 560);
            MinimumSize   = new Size(700, 460);
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label
            {
                Text      = $"Create Return — Order {_orderNumber}",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            });

            Controls.Add(new Label
            {
                Text      = "Select the items to return and set quantity and condition:",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(12, 44),
                AutoSize  = true
            });

            // Items grid (editable ReturnQty and Condition columns)
            _dgvItems.Location        = new Point(12, 68);
            _dgvItems.Size            = new Size(836, 310);
            _dgvItems.Anchor          = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _dgvItems.AllowUserToAddRows    = false;
            _dgvItems.AllowUserToDeleteRows = false;
            _dgvItems.AutoGenerateColumns   = false;
            _dgvItems.RowHeadersVisible     = false;
            _dgvItems.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;

            _dgvItems.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colSKU",    HeaderText = "SKU",     Width = 100, ReadOnly = true });
            _dgvItems.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colName",   HeaderText = "Product", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            _dgvItems.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colOrdered", HeaderText = "Ordered", Width = 70, ReadOnly = true });
            _dgvItems.Columns.Add(new DataGridViewTextBoxColumn
                { Name = "colReturnQty", HeaderText = "Return Qty", Width = 80 });

            var conditionCol = new DataGridViewComboBoxColumn
            {
                Name       = "colCondition",
                HeaderText = "Condition",
                Width      = 110,
                DataSource = new[] { "Resalable", "Damaged", "Destroy" }
            };
            _dgvItems.Columns.Add(conditionCol);
            Theme.StyleGrid(_dgvItems);
            Controls.Add(_dgvItems);

            // Reason
            int y = 390;
            Controls.Add(new Label { Text = "Reason:", Location = new Point(12, y), AutoSize = true });
            _txtReason.Location    = new Point(12, y + 20);
            _txtReason.Size        = new Size(836, 23);
            _txtReason.Anchor      = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            Controls.Add(_txtReason);

            // Notes
            y += 50;
            Controls.Add(new Label { Text = "Notes (optional):", Location = new Point(12, y), AutoSize = true });
            _txtNotes.Location    = new Point(12, y + 20);
            _txtNotes.Size        = new Size(836, 46);
            _txtNotes.Multiline   = true;
            _txtNotes.Anchor      = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            Controls.Add(_txtNotes);

            // Submit
            y += 74;
            _btnSubmit.Text     = "Submit Return";
            _btnSubmit.Location = new Point(12, y);
            _btnSubmit.Size     = new Size(130, 30);
            _btnSubmit.Click   += BtnSubmit_Click;
            Theme.StyleButton(_btnSubmit);
            Controls.Add(_btnSubmit);

            _lblStatus.Location = new Point(156, y + 7);
            _lblStatus.AutoSize = true;
            Controls.Add(_lblStatus);
        }

        private void LoadData()
        {
            try
            {
                _originalItems = _custRepo.GetOrderLineItems(_salesOrderId);
                _locations     = _locationRepo.GetAll().ToList();

                _dgvItems.Rows.Clear();
                foreach (var item in _originalItems)
                {
                    int idx = _dgvItems.Rows.Add();
                    var row = _dgvItems.Rows[idx];
                    row.Cells["colSKU"].Value       = item.SKU;
                    row.Cells["colName"].Value      = item.ProductName;
                    row.Cells["colOrdered"].Value   = item.Quantity;
                    row.Cells["colReturnQty"].Value = 0;
                    row.Cells["colCondition"].Value = "Resalable";
                    row.Tag = item;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load order items: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSubmit_Click(object? sender, EventArgs e)
        {
            var items = new List<ReturnOrderItem>();

            for (int i = 0; i < _dgvItems.Rows.Count; i++)
            {
                var row = _dgvItems.Rows[i];
                if (row.Tag is not CustomerOrderItem original) continue;

                if (!int.TryParse(row.Cells["colReturnQty"].Value?.ToString(), out int returnQty)
                    || returnQty <= 0) continue;

                if (returnQty > original.Quantity)
                {
                    MessageBox.Show(this,
                        $"Return qty for '{original.ProductName}' ({returnQty}) exceeds original ordered qty ({original.Quantity}).",
                        "Invalid Quantity", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string condition = row.Cells["colCondition"].Value?.ToString() ?? "Resalable";

                items.Add(new ReturnOrderItem
                {
                    ProductID   = 0,   // ReturnRepository will resolve via SKU join if needed
                    SKU         = original.SKU,
                    ProductName = original.ProductName,
                    OriginalQty = original.Quantity,
                    ReturnQty   = returnQty,
                    Condition   = condition
                });
            }

            if (items.Count == 0)
            {
                MessageBox.Show(this, "Enter a return quantity for at least one item.",
                    "Nothing to Return", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtReason.Text))
            {
                MessageBox.Show(this, "Please enter a reason for the return.",
                    "Reason Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Resolve ProductIDs from SKUs — need a product lookup
                ResolveProductIds(items);

                var request = new CreateReturnRequest
                {
                    OriginalOrderID = _salesOrderId,
                    Reason          = _txtReason.Text.Trim(),
                    Notes           = _txtNotes.Text.Trim(),
                    Items           = items
                };

                int returnId = _returnRepo.CreateReturn(request);
                _lblStatus.ForeColor = Color.FromArgb(80, 210, 100);
                _lblStatus.Text      = $"Return #{returnId} created (Pending approval).";
                _btnSubmit.Enabled   = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not create return: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResolveProductIds(List<ReturnOrderItem> items)
        {
            // Look up ProductID from the original SalesOrderItems by SKU
            var originalItems = _custRepo.GetOrderLineItems(_salesOrderId);
            // We need a product repo or direct SQL — use IProductRepository if available
            try
            {
                var productRepo = AppServices.Get<IProductRepository>();
                var products    = productRepo.GetProducts();
                var bySkuMap    = products.ToDictionary(p => p.SKU, p => p.ProductID,
                                    StringComparer.OrdinalIgnoreCase);

                foreach (var item in items)
                {
                    if (bySkuMap.TryGetValue(item.SKU, out int pid))
                        item.ProductID = pid;
                }
            }
            catch { /* ProductID will be 0; ReturnRepository handles gracefully */ }
        }
    }
}
