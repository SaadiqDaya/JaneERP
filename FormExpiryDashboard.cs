using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;

namespace JaneERP
{
    public class FormExpiryDashboard : Form
    {
        private readonly IExpiryRepository _repo = AppServices.Get<IExpiryRepository>();

        private DataGridView  _dgv        = new();
        private ComboBox      _cboFilter  = new();
        private Button        _btnRefresh = new();
        private Label         _lblStatus  = new();
        private List<LotStockRow> _allRows = [];

        public FormExpiryDashboard()
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
            Text          = "Expiry & Lot Tracker";
            ClientSize    = new Size(900, 580);
            MinimumSize   = new Size(700, 420);
            StartPosition = FormStartPosition.CenterParent;

            // Filter combo
            Controls.Add(new Label
            {
                Text      = "Show:",
                Location  = new Point(12, 62),
                AutoSize  = true
            });
            _cboFilter.Location      = new Point(56, 58);
            _cboFilter.Size          = new Size(180, 23);
            _cboFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboFilter.Items.AddRange(new object[]
            {
                "All lots with stock",
                "Expired",
                "Expiring in 7 days",
                "Expiring in 30 days",
                "Expiring in 60 days",
                "Expiring in 90 days"
            });
            _cboFilter.SelectedIndex    = 0;
            _cboFilter.SelectedIndexChanged += (_, _) => ApplyFilter();
            Controls.Add(_cboFilter);

            _btnRefresh.Text     = "Refresh";
            _btnRefresh.Location = new Point(248, 58);
            _btnRefresh.Size     = new Size(80, 27);
            _btnRefresh.Click   += (_, _) => LoadData();
            Controls.Add(_btnRefresh);
            Theme.StyleButton(_btnRefresh);

            // Grid
            _dgv.Location        = new Point(12, 80);
            _dgv.Size            = new Size(876, 464);
            _dgv.Anchor          = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _dgv.ReadOnly        = true;
            _dgv.AllowUserToAddRows    = false;
            _dgv.AllowUserToDeleteRows = false;
            _dgv.AutoGenerateColumns   = false;
            _dgv.RowHeadersVisible     = false;
            _dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgv.CellFormatting       += Dgv_CellFormatting;

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSKU",     HeaderText = "SKU",          Width = 110 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProduct", HeaderText = "Product",      AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLot",     HeaderText = "Lot #",        Width = 120 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLoc",     HeaderText = "Location",     Width = 130 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colQty",     HeaderText = "Qty",          Width = 70  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colExpiry",  HeaderText = "Expiry Date",  Width = 105 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDays",    HeaderText = "Days",         Width = 65  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStatus",  HeaderText = "Status",       Width = 80  });
            Theme.StyleGrid(_dgv);
            Controls.Add(_dgv);

            _lblStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _lblStatus.Location = new Point(12, ClientSize.Height - 22);
            _lblStatus.AutoSize = true;
            Controls.Add(_lblStatus);
            SizeChanged += (_, _) => _lblStatus.Location = new Point(12, ClientSize.Height - 22);
            Theme.AddFormHeader(this, "⏰  Expiry & Lot Tracker");
        }

        private void LoadData()
        {
            try
            {
                _allRows = _repo.GetLotStock();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load lot data: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyFilter()
        {
            IEnumerable<LotStockRow> rows = _allRows;

            switch (_cboFilter.SelectedIndex)
            {
                case 1: rows = rows.Where(r => r.DaysUntilExpiry < 0);   break;  // Expired
                case 2: rows = rows.Where(r => r.DaysUntilExpiry <= 7);  break;
                case 3: rows = rows.Where(r => r.DaysUntilExpiry <= 30); break;
                case 4: rows = rows.Where(r => r.DaysUntilExpiry <= 60); break;
                case 5: rows = rows.Where(r => r.DaysUntilExpiry <= 90); break;
            }

            var list = rows.ToList();
            _dgv.Rows.Clear();

            foreach (var r in list)
            {
                int idx = _dgv.Rows.Add();
                var row = _dgv.Rows[idx];
                row.Cells["colSKU"].Value     = r.SKU;
                row.Cells["colProduct"].Value = r.ProductName;
                row.Cells["colLot"].Value     = r.LotNumber;
                row.Cells["colLoc"].Value     = r.LocationName ?? "—";
                row.Cells["colQty"].Value     = r.CurrentQty.ToString("N0");
                row.Cells["colExpiry"].Value  = r.ExpirationDate.ToString("yyyy-MM-dd");
                row.Cells["colDays"].Value    = r.DaysUntilExpiry;
                row.Cells["colStatus"].Value  = r.ExpiryStatus;
                row.Tag = r;
            }

            int total   = _allRows.Count;
            int expired = _allRows.Count(r => r.DaysUntilExpiry < 0);
            int crit    = _allRows.Count(r => r.DaysUntilExpiry is >= 0 and <= 7);
            _lblStatus.Text =
                $"{list.Count} lot(s) shown  |  Total: {total}  |  Expired: {expired}  |  Critical (<7d): {crit}";
        }

        private void Dgv_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || _dgv.Rows[e.RowIndex].Tag is not LotStockRow row) return;

            Color fg = row.ExpiryStatus switch
            {
                "Expired"  => Color.FromArgb(255, 90, 90),
                "Critical" => Color.FromArgb(255, 160, 60),
                "Warning"  => Color.FromArgb(230, 210, 60),
                _          => Theme.TextPrimary
            };

            e.CellStyle.ForeColor = fg;
        }
    }
}
