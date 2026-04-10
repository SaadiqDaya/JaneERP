using System.Configuration;
using Dapper;
using JaneERP.Data;
using JaneERP.Logging;
using JaneERP.Models;
using JaneERP.Security;
using Microsoft.Data.SqlClient;

namespace JaneERP
{
    public class FormStockTransfer : Form
    {
        private readonly string _cs =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

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

            var lblTitle = new Label
            {
                Text      = "Location-to-Location Stock Transfer",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            };
            Controls.Add(lblTitle);

            int labelX  = 20;
            int controlX = 160;
            int y        = 55;
            int rowH     = 38;

            // Product
            Controls.Add(new Label { Text = "Product:", Location = new Point(labelX, y + 3), AutoSize = true });
            cboProduct.DropDownStyle = ComboBoxStyle.DropDownList;
            cboProduct.Location      = new Point(controlX, y);
            cboProduct.Size          = new Size(290, 23);
            cboProduct.SelectedIndexChanged += (_, _) => UpdatePreview();
            Controls.Add(cboProduct);
            y += rowH;

            // From Location
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
        }

        private void LoadData(Product? preselected)
        {
            try
            {
                _products  = new ProductRepository().GetProducts(false).ToList();
                _locations = new LocationRepository().GetAll().ToList();

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

        private void UpdatePreview()
        {
            if (cboProduct.SelectedItem is not ProductComboItem prod
                || cboFromLocation.SelectedItem is not Location from
                || cboToLocation.SelectedItem   is not Location to)
            {
                lblPreview.Text = "";
                return;
            }

            int qty = (int)nudQuantity.Value;
            lblPreview.Text = $"Moving {qty} unit(s): {prod.ProductName} from {from.LocationName} \u2192 {to.LocationName}";
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            // Validate
            if (cboProduct.SelectedItem is not ProductComboItem selectedProd)
            {
                MessageBox.Show("Please select a product.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cboFromLocation.SelectedItem is not Location fromLoc)
            {
                MessageBox.Show("Please select a From Location.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cboToLocation.SelectedItem is not Location toLoc)
            {
                MessageBox.Show("Please select a To Location.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (fromLoc.LocationID == toLoc.LocationID)
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
                using var checkDb = new SqlConnection(_cs);
                currentStock = checkDb.ExecuteScalar<int>(@"
                    SELECT ISNULL(SUM(QuantityChange), 0)
                    FROM   InventoryTransactions
                    WHERE  ProductID  = @ProductID
                      AND  LocationID = @LocationID",
                    new { selectedProd.ProductID, fromLoc.LocationID });
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
                    $"Insufficient stock at {fromLoc.LocationName}.\n" +
                    $"Available: {currentStock}  |  Requested: {qty}",
                    "Insufficient Stock", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Execute transfer in a DB transaction
            string note = txtNote.Text.Trim();
            string transferNote = string.IsNullOrEmpty(note)
                ? $"Transfer from {fromLoc.LocationName} to {toLoc.LocationName}"
                : note;

            try
            {
                using var db = new SqlConnection(_cs);
                db.Open();
                using var tx = db.BeginTransaction();
                try
                {
                    // Transfer Out
                    db.Execute(@"
                        INSERT INTO InventoryTransactions
                            (ProductID, QuantityChange, TransactionType, LocationID, Notes, TransactionDate)
                        VALUES
                            (@ProductID, @QuantityChange, 'Transfer Out', @LocationID, @Notes, @TransactionDate)",
                        new
                        {
                            ProductID       = selectedProd.ProductID,
                            QuantityChange  = -qty,
                            LocationID      = fromLoc.LocationID,
                            Notes           = transferNote,
                            TransactionDate = DateTime.Now
                        }, tx);

                    // Transfer In
                    db.Execute(@"
                        INSERT INTO InventoryTransactions
                            (ProductID, QuantityChange, TransactionType, LocationID, Notes, TransactionDate)
                        VALUES
                            (@ProductID, @QuantityChange, 'Transfer In', @LocationID, @Notes, @TransactionDate)",
                        new
                        {
                            ProductID       = selectedProd.ProductID,
                            QuantityChange  = qty,
                            LocationID      = toLoc.LocationID,
                            Notes           = transferNote,
                            TransactionDate = DateTime.Now
                        }, tx);

                    tx.Commit();
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }

                // Audit log
                AppLogger.Audit(
                    AppSession.CurrentUser?.Username,
                    "StockTransfer",
                    $"SKU={selectedProd.SKU} qty={qty} from={fromLoc.LocationName} to={toLoc.LocationName}");

                MessageBox.Show(
                    $"Transfer complete.\n\n" +
                    $"Moved {qty} unit(s) of {selectedProd.ProductName}\n" +
                    $"From: {fromLoc.LocationName}\n" +
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
