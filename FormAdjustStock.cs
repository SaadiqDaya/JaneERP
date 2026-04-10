using JaneERP.Data;
using JaneERP.Logging;
using JaneERP.Models;
using JaneERP.Security;

namespace JaneERP
{
    public partial class FormAdjustStock : Form
    {
        private readonly Product _product;

        public FormAdjustStock(Product product)
        {
            InitializeComponent();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            _product = product;

            lblProductInfo.Text =
                $"{product.ProductName}\n" +
                $"SKU: {product.SKU}  |  Current Stock: {product.CurrentStock}";

            LoadLocations();

            // Wire live stock preview
            txtQuantity.TextChanged    += (_, _) => UpdateStockPreview();
            rdoAdd.CheckedChanged      += (_, _) => UpdateStockPreview();
            rdoRemove.CheckedChanged   += (_, _) => UpdateStockPreview();
        }

        private void UpdateStockPreview()
        {
            if (!int.TryParse(txtQuantity.Text, out int qty) || qty <= 0)
            {
                lblStockPreview.Text = "";
                return;
            }
            int change   = rdoAdd.Checked ? qty : -qty;
            int newStock = _product.CurrentStock + change;
            string arrow = change >= 0 ? "↑" : "↓";
            lblStockPreview.Text      = $"Current: {_product.CurrentStock}  {arrow}  New: {newStock}";
            lblStockPreview.ForeColor = newStock < 0 ? Color.DarkRed : Color.DarkGreen;
        }

        private void LoadLocations()
        {
            try
            {
                var locations = new LocationRepository().GetAll();
                cboLocation.DataSource    = locations.ToList();
                cboLocation.DisplayMember = "LocationName";
                cboLocation.ValueMember   = "LocationID";
                cboLocation.SelectedIndex = -1;

                // Pre-select the product's default location if set
                if (_product.DefaultLocationID.HasValue)
                {
                    foreach (Location loc in cboLocation.Items)
                    {
                        if (loc.LocationID == _product.DefaultLocationID.Value)
                        {
                            cboLocation.SelectedItem = loc;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load locations: " + ex.Message, "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ChkHasExpiry_CheckedChanged(object? sender, EventArgs e)
        {
            dtpExpiry.Enabled = chkHasExpiry.Checked;
        }

        private void btnSave_Click(object? sender, EventArgs e)
        {
            // Validate quantity
            if (!int.TryParse(txtQuantity.Text, out int qty) || qty <= 0)
            {
                MessageBox.Show("Please enter a valid positive whole number.",
                    "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Location is required
            if (cboLocation.SelectedItem is not Location selectedLocation)
            {
                MessageBox.Show("Please select a location. All inventory movements require a location.",
                    "Location Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cboLocation.Focus();
                return;
            }

            try
            {
                int change = rdoAdd.Checked ? qty : -qty;

                new ProductRepository().AddTransaction(new InventoryTransaction
                {
                    ProductID       = _product.ProductID,
                    QuantityChange  = change,
                    TransactionType = rdoAdd.Checked ? "Adjustment In" : "Adjustment Out",
                    Notes           = txtNote.Text.Trim(),
                    TransactionDate = DateTime.Now,
                    LocationID      = selectedLocation.LocationID,
                    LotNumber       = string.IsNullOrWhiteSpace(txtLotNumber.Text) ? null : txtLotNumber.Text.Trim(),
                    ExpirationDate  = chkHasExpiry.Checked ? dtpExpiry.Value.Date : null
                });

                AppLogger.Audit(AppSession.CurrentUser?.Username, "InventoryAdjustment",
                    $"SKU={_product.SKU} qty={change:+0;-0} location={selectedLocation.LocationName}");

                string action = rdoAdd.Checked ? "added to" : "removed from";
                MessageBox.Show(
                    $"{qty} unit(s) {action} {_product.ProductName}\n" +
                    $"Location: {selectedLocation.LocationName}" +
                    (string.IsNullOrWhiteSpace(txtLotNumber.Text) ? "" : $"\nLot: {txtLotNumber.Text.Trim()}"),
                    "Stock Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error saving adjustment",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCancel_Click(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
