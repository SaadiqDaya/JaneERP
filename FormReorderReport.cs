using System.Configuration;
using System.Data;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;

namespace JaneERP
{
    /// <summary>
    /// Shows products and parts at or below reorder point, with suggested order quantities and estimated costs.
    /// </summary>
    public class FormReorderReport : Form
    {
        private readonly string _cs =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        private TabControl     tabControl    = new();
        private DataGridView   dgvProducts   = new();
        private DataGridView   dgvParts      = new();
        private Label          lblTotalCost  = new();
        private Button         btnExportCsv  = new();
        private Button         btnCreatePO   = new();
        private Button         btnRefresh    = new();

        // Backing data for export
        private List<ProductReorderRow> _productRows = new();
        private List<PartReorderRow>    _partRows    = new();

        public FormReorderReport()
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
            Text          = "Reorder Report — Purchase Suggestions";
            ClientSize    = new Size(900, 600);
            MinimumSize   = new Size(800, 500);
            StartPosition = FormStartPosition.CenterParent;

            // ── Header ────────────────────────────────────────────────────────────
            var pnlHeader = new Panel
            {
                Tag       = "header",
                Dock      = DockStyle.Top,
                Height    = 52,
                BackColor = Color.FromArgb(8, 16, 28)
            };

            var lblTitle = new Label
            {
                Text      = "Reorder Report — Purchase Suggestions",
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                AutoSize  = false,
                Size      = new Size(550, 36),
                Location  = new Point(14, 8),
                BackColor = Color.Transparent
            };

            btnRefresh = new Button
            {
                Text     = "Refresh",
                Size     = new Size(80, 30),
                Location = new Point(796, 11),
                Font     = new Font("Segoe UI", 9F)
            };
            btnRefresh.Click += (_, _) => LoadData();
            Theme.StyleButton(btnRefresh);

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(btnRefresh);
            Controls.Add(pnlHeader);
            Theme.MakeDraggable(this, pnlHeader);

            // ── Tab control ───────────────────────────────────────────────────────
            tabControl = new TabControl
            {
                Location = new Point(10, 62),
                Size     = new Size(880, 460),
                Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font     = new Font("Segoe UI", 9.5F)
            };

            var tabProducts = new TabPage("Products");
            var tabParts    = new TabPage("Parts");

            // Products grid
            dgvProducts = BuildGrid();
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSKU",          HeaderText = "SKU",               DataPropertyName = "SKU",               Width = 90  });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",         HeaderText = "Product Name",       DataPropertyName = "ProductName",       Width = 220 });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cStock",        HeaderText = "Current Stock",      DataPropertyName = "CurrentStock",      Width = 90  });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cReorder",      HeaderText = "Reorder Point",      DataPropertyName = "ReorderPoint",      Width = 90  });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cShortfall",    HeaderText = "Shortfall",          DataPropertyName = "Shortfall",         Width = 75  });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSuggestedQty", HeaderText = "Suggested Qty",      DataPropertyName = "SuggestedQty",      Width = 95  });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cRetail",       HeaderText = "Retail Price",       DataPropertyName = "RetailPriceDisplay", Width = 90 });
            dgvProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEstCost",      HeaderText = "Est. Cost",          DataPropertyName = "EstCostDisplay",    Width = 90  });
            tabProducts.Controls.Add(dgvProducts);

            // Parts grid
            dgvParts = BuildGrid();
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPartNo",       HeaderText = "Part Number",    DataPropertyName = "PartNumber",       Width = 110 });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPartName",     HeaderText = "Part Name",      DataPropertyName = "PartName",         Width = 220 });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cStock",        HeaderText = "Current Stock",  DataPropertyName = "CurrentStock",     Width = 90  });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cReorder",      HeaderText = "Reorder Point",  DataPropertyName = "ReorderPoint",     Width = 90  });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cShortfall",    HeaderText = "Shortfall",      DataPropertyName = "Shortfall",        Width = 75  });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSuggestedQty", HeaderText = "Suggested Qty",  DataPropertyName = "SuggestedQty",     Width = 95  });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cUnitCost",     HeaderText = "Unit Cost",      DataPropertyName = "UnitCostDisplay",  Width = 90  });
            dgvParts.Columns.Add(new DataGridViewTextBoxColumn { Name = "cEstCost",      HeaderText = "Est. Cost",      DataPropertyName = "EstCostDisplay",   Width = 90  });
            tabParts.Controls.Add(dgvParts);

            tabControl.TabPages.Add(tabProducts);
            tabControl.TabPages.Add(tabParts);
            tabControl.SelectedIndexChanged += (_, _) => UpdateTotalLabel();
            Controls.Add(tabControl);

            // ── Bottom bar ────────────────────────────────────────────────────────
            var pnlBottom = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 46,
                BackColor = Color.FromArgb(14, 26, 42)
            };

            lblTotalCost = new Label
            {
                AutoSize  = false,
                Size      = new Size(380, 30),
                Location  = new Point(12, 8),
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Text      = "Est. Total Reorder Cost: —"
            };

            btnExportCsv = new Button
            {
                Text     = "Export to CSV",
                Size     = new Size(120, 30),
                Location = new Point(630, 8),
                Font     = new Font("Segoe UI", 9F)
            };
            btnExportCsv.Click += BtnExportCsv_Click;
            Theme.StyleSecondaryButton(btnExportCsv);

            btnCreatePO = new Button
            {
                Text     = "Create PO",
                Size     = new Size(100, 30),
                Location = new Point(762, 8),
                Font     = new Font("Segoe UI", 9F)
            };
            btnCreatePO.Click += BtnCreatePO_Click;
            Theme.StyleButton(btnCreatePO);

            pnlBottom.Controls.Add(lblTotalCost);
            pnlBottom.Controls.Add(btnExportCsv);
            pnlBottom.Controls.Add(btnCreatePO);
            Controls.Add(pnlBottom);
        }

        private static DataGridView BuildGrid()
        {
            var dgv = new DataGridView
            {
                Dock                     = DockStyle.Fill,
                ReadOnly                 = true,
                AllowUserToAddRows       = false,
                AllowUserToDeleteRows    = false,
                AllowUserToResizeRows    = false,
                SelectionMode            = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect              = false,
                AutoGenerateColumns      = false,
                RowHeadersVisible        = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight      = 30,
                RowTemplate              = { Height = 26 }
            };
            Theme.StyleGrid(dgv);
            return dgv;
        }

        // ── Data loading ──────────────────────────────────────────────────────────

        private void LoadData()
        {
            btnRefresh.Enabled = false;
            try
            {
                LoadProducts();
                LoadParts();
                UpdateTotalLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error loading reorder data:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                btnRefresh.Enabled = true;
            }
        }

        private void LoadProducts()
        {
            using IDbConnection db = new SqlConnection(_cs);

            // Fetch all active products with ledger-based stock
            var rows = db.Query<ProductReorderRow>(@"
                SELECT  p.SKU,
                        p.ProductName,
                        p.RetailPrice,
                        p.WholesalePrice,
                        p.ReorderPoint,
                        ISNULL((
                            SELECT SUM(t.QuantityChange)
                            FROM   InventoryTransactions t
                            WHERE  t.ProductID = p.ProductID
                        ), 0) AS CurrentStock
                FROM    Products p
                WHERE   p.IsActive = 1
                  AND   p.ReorderPoint > 0
                  AND   ISNULL((
                            SELECT SUM(t.QuantityChange)
                            FROM   InventoryTransactions t
                            WHERE  t.ProductID = p.ProductID
                        ), 0) <= p.ReorderPoint
                ORDER   BY p.SKU").ToList();

            foreach (var r in rows)
                r.Compute();

            _productRows = rows;

            dgvProducts.DataSource = null;
            dgvProducts.DataSource = rows;
        }

        private void LoadParts()
        {
            using IDbConnection db = new SqlConnection(_cs);
            List<PartReorderRow> rows;

            try
            {
                rows = db.Query<PartReorderRow>(@"
                    SELECT  PartNumber,
                            PartName,
                            CurrentStock,
                            ISNULL(ReorderPoint, 0) AS ReorderPoint,
                            UnitCost
                    FROM    Parts
                    WHERE   IsActive = 1
                      AND   CurrentStock <= ISNULL(ReorderPoint, 5)
                    ORDER   BY PartNumber").ToList();
            }
            catch
            {
                // Fallback: ReorderPoint column may not exist — show parts with stock <= 5
                try
                {
                    rows = db.Query<PartReorderRow>(@"
                        SELECT  PartNumber,
                                PartName,
                                CurrentStock,
                                0 AS ReorderPoint,
                                UnitCost
                        FROM    Parts
                        WHERE   IsActive = 1
                          AND   CurrentStock <= 5
                        ORDER   BY PartNumber").ToList();
                }
                catch
                {
                    rows = new List<PartReorderRow>();
                }
            }

            foreach (var r in rows)
                r.Compute();

            _partRows = rows;

            dgvParts.DataSource = null;
            dgvParts.DataSource = rows;
        }

        private void UpdateTotalLabel()
        {
            decimal total;
            if (tabControl.SelectedIndex == 0)
                total = _productRows.Sum(r => r.EstCost);
            else
                total = _partRows.Sum(r => r.EstCost);

            lblTotalCost.Text = $"Est. Total Reorder Cost: {total:C}";
        }

        // ── Export ────────────────────────────────────────────────────────────────

        private void BtnExportCsv_Click(object? sender, EventArgs e)
        {
            using var dlg = new SaveFileDialog
            {
                Title      = "Export to CSV",
                Filter     = "CSV files (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName   = tabControl.SelectedIndex == 0
                    ? $"ReorderReport_Products_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                    : $"ReorderReport_Parts_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var sb = new StringBuilder();
                if (tabControl.SelectedIndex == 0)
                {
                    sb.AppendLine("SKU,Product Name,Current Stock,Reorder Point,Shortfall,Suggested Qty,Retail Price,Est. Cost");
                    foreach (var r in _productRows)
                        sb.AppendLine($"\"{r.SKU}\",\"{r.ProductName}\",{r.CurrentStock},{r.ReorderPoint},{r.Shortfall},{r.SuggestedQty},{r.RetailPrice:F2},{r.EstCost:F2}");
                }
                else
                {
                    sb.AppendLine("Part Number,Part Name,Current Stock,Reorder Point,Shortfall,Suggested Qty,Unit Cost,Est. Cost");
                    foreach (var r in _partRows)
                        sb.AppendLine($"\"{r.PartNumber}\",\"{r.PartName}\",{r.CurrentStock},{r.ReorderPoint},{r.Shortfall},{r.SuggestedQty},{r.UnitCost:F2},{r.EstCost:F2}");
                }

                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show(this, $"Exported successfully to:\n{dlg.FileName}",
                    "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Export failed:\n{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnCreatePO_Click(object? sender, EventArgs e)
        {
            MessageBox.Show(this, "PO creation coming soon.",
                "Create PO", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // ── Row DTOs ──────────────────────────────────────────────────────────────────

    internal class ProductReorderRow
    {
        public string  SKU              { get; set; } = "";
        public string  ProductName      { get; set; } = "";
        public int     CurrentStock     { get; set; }
        public int     ReorderPoint     { get; set; }
        public decimal RetailPrice      { get; set; }
        public decimal WholesalePrice   { get; set; }

        // Computed
        public int     Shortfall        { get; set; }
        public int     SuggestedQty     { get; set; }
        public decimal EstCost          { get; set; }

        // Display-formatted for grid
        public string  RetailPriceDisplay { get; set; } = "";
        public string  EstCostDisplay     { get; set; } = "";

        public void Compute()
        {
            Shortfall           = Math.Max(0, ReorderPoint - CurrentStock);
            SuggestedQty        = (int)Math.Ceiling(Shortfall * 1.5);
            EstCost             = SuggestedQty * WholesalePrice;
            RetailPriceDisplay  = RetailPrice.ToString("C");
            EstCostDisplay      = EstCost.ToString("C");
        }
    }

    internal class PartReorderRow
    {
        public string  PartNumber    { get; set; } = "";
        public string  PartName      { get; set; } = "";
        public int     CurrentStock  { get; set; }
        public int     ReorderPoint  { get; set; }
        public decimal UnitCost      { get; set; }

        // Computed
        public int     Shortfall     { get; set; }
        public int     SuggestedQty  { get; set; }
        public decimal EstCost       { get; set; }

        // Display-formatted for grid
        public string  UnitCostDisplay { get; set; } = "";
        public string  EstCostDisplay  { get; set; } = "";

        public void Compute()
        {
            Shortfall      = Math.Max(0, ReorderPoint - CurrentStock);
            SuggestedQty   = (int)Math.Ceiling(Shortfall * 1.5);
            if (SuggestedQty == 0) SuggestedQty = 5; // fallback minimum
            EstCost        = SuggestedQty * UnitCost;
            UnitCostDisplay = UnitCost.ToString("C");
            EstCostDisplay  = EstCost.ToString("C");
        }
    }
}
