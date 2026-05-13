using System.Configuration;
using Dapper;
using JaneERP.Data;
using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP
{
    /// <summary>Read-only snapshot of current inventory levels across all products.</summary>
    public class FormInventorySnapshot : Form
    {
        private readonly string _cs =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        private DataGridView dgv          = new();
        private Label        lblStatus    = new();
        private TextBox      txtSearch    = new();
        private List<Product> _all        = new();
        private string        _stockFilter = "All"; // "All" | "Negative" | "Zero" | "Low" | "OK"
        private readonly List<Button> _filterButtons = new();

        // Expiry section
        private Label        lblExpiryHeader = new();
        private DataGridView dgvExpiry       = new();

        public FormInventorySnapshot()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            LoadData();
        }

        private void BuildUI()
        {
            Text          = "Inventory Snapshot";
            ClientSize    = new Size(900, 760);
            MinimumSize   = new Size(700, 600);
            StartPosition = FormStartPosition.CenterParent;

            var lblTitle = new Label
            {
                Text      = "Inventory Snapshot",
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            };
            Controls.Add(lblTitle);

            txtSearch.Location        = new Point(12, 50);
            txtSearch.Size            = new Size(300, 23);
            txtSearch.PlaceholderText = "Search SKU or Name\u2026";
            txtSearch.TextChanged    += (_, _) => ApplyFilter();
            Controls.Add(txtSearch);

            var btnRefresh = new Button { Text = "Refresh", Size = new Size(80, 23), Location = new Point(322, 50) };
            btnRefresh.Click += (_, _) => LoadData();
            Controls.Add(btnRefresh);

            // ── Filter buttons (clickable legend) ───────────────────────────────
            int lx = 420;
            void AddFilterBtn(string label, string filterKey, Color back, Color fore, ref int x)
            {
                var btn = new Button
                {
                    Text      = label,
                    BackColor = back,
                    ForeColor = fore,
                    FlatStyle = FlatStyle.Flat,
                    Size      = new Size(TextRenderer.MeasureText(label, new Font("Segoe UI", 9F)).Width + 16, 24),
                    Padding   = new Padding(4, 1, 4, 1),
                    Location  = new Point(x, 47),
                    Cursor    = Cursors.Hand,
                    Tag       = filterKey
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(
                    Math.Min(back.R + 60, 255), Math.Min(back.G + 60, 255), Math.Min(back.B + 60, 255));
                btn.Click += (_, _) =>
                {
                    _stockFilter = filterKey;
                    UpdateFilterButtonHighlight();
                    ApplyFilter();
                };
                Controls.Add(btn);
                _filterButtons.Add(btn);
                x += btn.Width + 6;
            }
            AddFilterBtn("All",            "All",      Theme.Surface,               Theme.TextPrimary,              ref lx);
            AddFilterBtn("Negative",       "Negative", Color.FromArgb(80, 20, 20),  Color.FromArgb(255, 120, 120), ref lx);
            AddFilterBtn("Zero",           "Zero",     Color.FromArgb(80, 70, 0),   Color.FromArgb(255, 220, 0),   ref lx);
            AddFilterBtn("Low (reorder)",  "Low",      Color.FromArgb(70, 45, 0),   Color.FromArgb(255, 165, 0),   ref lx);
            AddFilterBtn("OK",             "OK",       Color.FromArgb(20, 60, 20),  Color.FromArgb(100, 220, 100), ref lx);

            dgv.AutoGenerateColumns = false;
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Location",     DataPropertyName = "DefaultLocationName", Width = 130, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "SKU",          DataPropertyName = "SKU",                 Width = 120, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Product",      DataPropertyName = "ProductName",         AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Stock",        DataPropertyName = "CurrentStock",        Width = 70,  ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reorder Pt",  DataPropertyName = "ReorderPoint",        Width = 80,  ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Retail Price", DataPropertyName = "RetailPrice",         Width = 100, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" } });
            dgv.AllowUserToAddRows    = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.ReadOnly              = true;
            dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgv.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            dgv.Location = new Point(12, 82);
            dgv.Size     = new Size(876, 350);
            dgv.CellFormatting += Dgv_CellFormatting;
            Controls.Add(dgv);

            lblStatus.Anchor   = AnchorStyles.Top | AnchorStyles.Left;
            lblStatus.Location = new Point(12, 438);
            lblStatus.AutoSize = true;
            Controls.Add(lblStatus);

            // ── Expiry section ─────────────────────────────────────────────────────
            lblExpiryHeader.Text      = "\u26A0 Items Expiring Within 30 Days:";
            lblExpiryHeader.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblExpiryHeader.ForeColor = Color.Orange;
            lblExpiryHeader.Location  = new Point(12, 462);
            lblExpiryHeader.AutoSize  = true;
            Controls.Add(lblExpiryHeader);

            dgvExpiry.AutoGenerateColumns = false;
            dgvExpiry.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "SKU",            DataPropertyName = "SKU",            Width = 110, ReadOnly = true });
            dgvExpiry.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Product",         DataPropertyName = "ProductName",    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            dgvExpiry.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Location",        DataPropertyName = "LocationName",   Width = 120, ReadOnly = true });
            dgvExpiry.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Lot",             DataPropertyName = "LotNumber",      Width = 90,  ReadOnly = true });
            dgvExpiry.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Expiry Date",     DataPropertyName = "ExpirationDate", Width = 110, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" } });
            dgvExpiry.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Qty",             DataPropertyName = "Quantity",       Width = 60,  ReadOnly = true });
            dgvExpiry.AllowUserToAddRows    = false;
            dgvExpiry.AllowUserToDeleteRows = false;
            dgvExpiry.ReadOnly              = true;
            dgvExpiry.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvExpiry.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvExpiry.Location = new Point(12, 485);
            dgvExpiry.Size     = new Size(876, 250);
            Controls.Add(dgvExpiry);
        }

        private void Dgv_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgv.Rows.Count) return;
            if (dgv.Rows[e.RowIndex].DataBoundItem is not Product p) return;

            Color back, fore;
            if (p.CurrentStock < 0)
            {
                back = Color.FromArgb(80, 20, 20);
                fore = Color.FromArgb(255, 120, 120);
            }
            else if (p.CurrentStock == 0)
            {
                back = Color.FromArgb(80, 70, 0);
                fore = Color.FromArgb(255, 220, 0);
            }
            else if (p.ReorderPoint > 0 && p.CurrentStock <= p.ReorderPoint)
            {
                back = Color.FromArgb(70, 45, 0);
                fore = Color.FromArgb(255, 165, 0);
            }
            else
            {
                back = Theme.Surface;
                fore = Theme.TextPrimary;
            }

            e.CellStyle.BackColor = back;
            e.CellStyle.ForeColor = fore;
            e.CellStyle.SelectionBackColor = Color.FromArgb(
                Math.Min(back.R + 40, 255),
                Math.Min(back.G + 40, 255),
                Math.Min(back.B + 40, 255));
            e.CellStyle.SelectionForeColor = fore;
        }

        private void UpdateFilterButtonHighlight()
        {
            foreach (var btn in _filterButtons)
            {
                bool active = btn.Tag?.ToString() == _stockFilter;
                btn.FlatAppearance.BorderSize = active ? 2 : 1;
                btn.Font = active
                    ? new Font("Segoe UI", 8.5F, FontStyle.Bold)
                    : new Font("Segoe UI", 8.5F);
            }
        }

        private void LoadData()
        {
            try
            {
                _all = AppServices.Get<IProductRepository>().GetProducts(false).ToList();
                UpdateFilterButtonHighlight();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            LoadExpiryData();
        }

        private void LoadExpiryData()
        {
            try
            {
                using var db = new SqlConnection(_cs);
                var rows = db.Query<ExpiringItem>(@"
                    SELECT p.SKU, p.ProductName, l.LocationName, it.LotNumber, it.ExpirationDate,
                           it.QuantityChange AS Quantity
                    FROM   InventoryTransactions it
                    JOIN   Products  p ON p.ProductID  = it.ProductID
                    LEFT JOIN Locations l ON l.LocationID = it.LocationID
                    WHERE  it.ExpirationDate IS NOT NULL
                      AND  it.ExpirationDate <= DATEADD(day, 30, GETDATE())
                      AND  it.QuantityChange > 0
                    ORDER BY it.ExpirationDate ASC").ToList();

                if (rows.Count == 0)
                {
                    lblExpiryHeader.Text = "\u26A0 Items Expiring Within 30 Days: None";
                    dgvExpiry.Visible    = false;
                }
                else
                {
                    lblExpiryHeader.Text = $"\u26A0 Items Expiring Within 30 Days: ({rows.Count} item(s))";
                    dgvExpiry.Visible    = true;
                    dgvExpiry.DataSource = rows;
                }
            }
            catch (Exception ex)
            {
                lblExpiryHeader.Text = $"\u26A0 Expiry check failed: {ex.Message}";
                dgvExpiry.Visible    = false;
            }
        }

        private sealed class ExpiringItem
        {
            public string?   SKU            { get; set; }
            public string?   ProductName    { get; set; }
            public string?   LocationName   { get; set; }
            public string?   LotNumber      { get; set; }
            public DateTime? ExpirationDate { get; set; }
            public int       Quantity       { get; set; }
        }

        private void ApplyFilter()
        {
            var term = txtSearch.Text.Trim();
            var filtered = _all
                .Where(p =>
                    string.IsNullOrEmpty(term) ||
                    (p.SKU         ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (p.ProductName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase))
                .Where(p => _stockFilter switch
                {
                    "Negative" => p.CurrentStock < 0,
                    "Zero"     => p.CurrentStock == 0,
                    "Low"      => p.CurrentStock > 0 && p.ReorderPoint > 0 && p.CurrentStock <= p.ReorderPoint,
                    "OK"       => p.CurrentStock > 0 && (p.ReorderPoint == 0 || p.CurrentStock > p.ReorderPoint),
                    _          => true   // "All"
                })
                .OrderBy(p => p.DefaultLocationName ?? "\uFFFF")
                .ThenBy(p => p.ProductName)
                .ToList();
            dgv.DataSource = filtered;
            lblStatus.Text = $"{filtered.Count} product(s) | Negative: {_all.Count(p => p.CurrentStock < 0)} | Out of stock: {_all.Count(p => p.CurrentStock == 0)} | Low (\u2264 reorder): {_all.Count(p => p.CurrentStock > 0 && p.ReorderPoint > 0 && p.CurrentStock <= p.ReorderPoint)}";
        }
    }
}
