using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;
using JaneERP.Security;

namespace JaneERP
{
    public class FormStockTransfer : Form
    {
        private readonly IInventoryService _invSvc  = AppServices.Get<IInventoryService>();

        private ComboBox       cboProduct      = new();
        private ComboBox       cboFromLocation = new();
        private ComboBox       cboToLocation   = new();
        private NumericUpDown  nudQuantity      = new();
        private TextBox        txtNote          = new();
        private Label          lblPreview       = new();
        private Button         btnSave          = new();
        private Button         btnCancel        = new();

        private List<Product>  _products        = [];
        private List<Location> _locations       = [];

        // Optional: pre-select product
        public FormStockTransfer(Product? preselected = null)
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            LoadData(preselected);
        }

        private void BuildUI()
        {
            Text          = "Stock Transfer";
            ClientSize    = new Size(480, 390);
            MinimumSize   = new Size(480, 390);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;


            int labelX  = 20;
            int controlX = 160;
            int y        = 55;
            int rowH     = 38;

            // Product
            Controls.Add(new Label { Text = "Product:", Location = new Point(labelX, y + 3), AutoSize = true });
            cboProduct.DropDownStyle = ComboBoxStyle.DropDownList;
            cboProduct.Location      = new Point(controlX, y);
            cboProduct.Size          = new Size(290, 23);
            cboProduct.SelectedIndexChanged += (_, _) => { RefreshFromLocations(); UpdatePreview(); };
            Controls.Add(cboProduct);
            y += rowH;

            // From Location — populated based on selected product (only locations with stock)
            Controls.Add(new Label { Text = "From Location:", Location = new Point(labelX, y + 3), AutoSize = true });
            cboFromLocation.DropDownStyle = ComboBoxStyle.DropDownList;
            cboFromLocation.Location      = new Point(controlX, y);
            cboFromLocation.Size          = new Size(290, 23);
            cboFromLocation.SelectedIndexChanged += (_, _) => UpdatePreview();
            Controls.Add(cboFromLocation);
            y += rowH;

            // To Location
            Controls.Add(new Label { Text = "To Location:", Location = new Point(labelX, y + 3), AutoSize = true });
            cboToLocation.DropDownStyle = ComboBoxStyle.DropDownList;
            cboToLocation.Location      = new Point(controlX, y);
            cboToLocation.Size          = new Size(290, 23);
            cboToLocation.SelectedIndexChanged += (_, _) => UpdatePreview();
            Controls.Add(cboToLocation);
            y += rowH;

            // Quantity
            Controls.Add(new Label { Text = "Quantity:", Location = new Point(labelX, y + 3), AutoSize = true });
            nudQuantity.Location  = new Point(controlX, y);
            nudQuantity.Size      = new Size(120, 23);
            nudQuantity.Minimum   = 1;
            nudQuantity.Maximum   = 9999;
            nudQuantity.Value     = 1;
            nudQuantity.ValueChanged += (_, _) => UpdatePreview();
            Controls.Add(nudQuantity);
            y += rowH;

            // Note
            Controls.Add(new Label { Text = "Note (optional):", Location = new Point(labelX, y + 3), AutoSize = true });
            txtNote.Location = new Point(controlX, y);
            txtNote.Size     = new Size(290, 23);
            Controls.Add(txtNote);
            y += rowH;

            // Preview label
            lblPreview.Location  = new Point(20, y);
            lblPreview.Size      = new Size(440, 40);
            lblPreview.Font      = new Font("Segoe UI", 9F, FontStyle.Italic);
            lblPreview.ForeColor = Color.LightSkyBlue;
            lblPreview.Text      = "";
            Controls.Add(lblPreview);
            y += 50;

            // Buttons
            btnSave.Text     = "Save Transfer";
            btnSave.Size     = new Size(130, 32);
            btnSave.Location = new Point(200, y);
            btnSave.Click   += BtnSave_Click;
            Controls.Add(btnSave);

            btnCancel.Text     = "Cancel";
            btnCancel.Size     = new Size(80, 32);
            btnCancel.Location = new Point(340, y);
            btnCancel.Click   += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);
            Theme.AddFormHeader(this, "🔄  Stock Transfer");
        }

        private void LoadData(Product? preselected)
        {
            try
            {
                _products  = AppServices.Get<IProductRepository>().GetProducts(false).ToList();
                _locations = AppServices.Get<ILocationRepository>().GetAll().ToList();

                // Build display strings for products
                cboProduct.DisplayMember = "ProductName";
                cboProduct.ValueMember   = "ProductID";

                // Use a BindingSource-style approach via DataSource
                cboProduct.DataSource    = _products.Select(p => new ProductComboItem(p)).ToList();
                cboProduct.DisplayMember = "Display";
                cboProduct.ValueMember   = "ProductID";

                cboFromLocation.DataSource    = _locations.ToList();
                cboFromLocation.DisplayMember = "LocationName";
                cboFromLocation.ValueMember   = "LocationID";
                cboFromLocation.SelectedIndex = -1;

                cboToLocation.DataSource    = _locations.ToList();
                cboToLocation.DisplayMember = "LocationName";
                cboToLocation.ValueMember   = "LocationID";
                cboToLocation.SelectedIndex = -1;

                // Pre-select product if provided
                if (preselected != null)
                {
                    foreach (ProductComboItem item in cboProduct.Items)
                    {
                        if (item.ProductID == preselected.ProductID)
                        {
                            cboProduct.SelectedItem = item;
                            break;
                        }
                    }
                    cboProduct.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load data: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Rebuilds the From Location dropdown showing only locations with stock for the selected product.</summary>
        private void RefreshFromLocations()
        {
            if (cboProduct.SelectedItem is not ProductComboItem prod)
            {
                cboFromLocation.DataSource    = _locations.ToList();
                cboFromLocation.DisplayMember = "LocationName";
                cboFromLocation.ValueMember   = "LocationID";
                cboFromLocation.SelectedIndex = -1;
                return;
            }

            try
            {
                var rows = _invSvc.GetStockPerLocation(prod.ProductID);
                cboFromLocation.DataSource    = rows;
                cboFromLocation.DisplayMember = "Display";
                cboFromLocation.ValueMember   = "LocationID";
                cboFromLocation.SelectedIndex = rows.Count > 0 ? 0 : -1;
            }
            catch
            {
                // Fall back to all locations
                cboFromLocation.DataSource    = _locations.ToList();
                cboFromLocation.DisplayMember = "LocationName";
                cboFromLocation.ValueMember   = "LocationID";
                cboFromLocation.SelectedIndex = -1;
            }
        }

        private void UpdatePreview()
        {
            if (cboProduct.SelectedItem is not ProductComboItem prod)
            {
                lblPreview.Text = "";
                return;
            }

            string fromName = cboFromLocation.SelectedItem switch
            {
                LocationStock ls => ls.LocationName,
                Location l       => l.LocationName,
                _                => null!
            };
            string toName = (cboToLocation.SelectedItem as Location)?.LocationName!;

            if (fromName == null || toName == null) { lblPreview.Text = ""; return; }

            int qty = (int)nudQuantity.Value;
            lblPreview.Text = $"Moving {qty} unit(s): {prod.ProductName} from {fromName} \u2192 {toName}";
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            // Validate
            if (cboProduct.SelectedItem is not ProductComboItem selectedProd)
            {
                MessageBox.Show("Please select a product.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int    fromLocId;
            string fromLocName;
            if (cboFromLocation.SelectedItem is LocationStock fromStock)
            {
                fromLocId   = fromStock.LocationID;
                fromLocName = fromStock.LocationName;
            }
            else if (cboFromLocation.SelectedItem is Location fromLoc)
            {
                fromLocId   = fromLoc.LocationID;
                fromLocName = fromLoc.LocationName;
            }
            else
            {
                MessageBox.Show("Please select a From Location.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cboToLocation.SelectedItem is not Location toLoc)
            {
                MessageBox.Show("Please select a To Location.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (fromLocId == toLoc.LocationID)
            {
                MessageBox.Show("From and To locations must be different.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int qty = (int)nudQuantity.Value;
            if (qty <= 0)
            {
                MessageBox.Show("Quantity must be greater than 0.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Check current stock at From location
            int currentStock;
            try
            {
                currentStock = _invSvc.GetStockAtLocation(selectedProd.ProductID, fromLocId);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not verify stock: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (currentStock < qty)
            {
                MessageBox.Show(
                    $"Insufficient stock at {fromLocName}.\n" +
                    $"Available: {currentStock}  |  Requested: {qty}",
                    "Insufficient Stock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Execute transfer in a DB transaction
            string note = txtNote.Text.Trim();
            string transferNote = string.IsNullOrEmpty(note)
                ? $"Transfer from {fromLocName} to {toLoc.LocationName}"
                : note;

            try
            {
                _invSvc.TransferStock(
                    selectedProd.ProductID,
                    fromLocId,
                    toLoc.LocationID,
                    qty,
                    transferNote,
                    AppSession.CurrentUser?.Username ?? "system");

                MessageBox.Show(
                    $"Transfer complete.\n\n" +
                    $"Moved {qty} unit(s) of {selectedProd.ProductName}\n" +
                    $"From: {fromLocName}\n" +
                    $"To:   {toLoc.LocationName}",
                    "Transfer Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Transfer failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Wraps a Product for display in the combo box as "ProductName [SKU]".</summary>
        private sealed class ProductComboItem
        {
            public int    ProductID   { get; }
            public string ProductName { get; }
            public string SKU         { get; }
            public string Display     => $"{ProductName}  [{SKU}]";

            public ProductComboItem(Product p)
            {
                ProductID   = p.ProductID;
                ProductName = p.ProductName;
                SKU         = p.SKU;
            }
        }
    }
}
