using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>
    /// Date-range report over all return orders.
    /// Shows per-return condition breakdown (Resalable / Damaged / Destroy)
    /// and the credit amount issued.  Summary row at bottom totals the period.
    /// </summary>
    public class FormReturnsReport : Form
    {
        private readonly IReturnRepository _repo = AppServices.Get<IReturnRepository>();

        private DateTimePicker _dtpFrom   = new();
        private DateTimePicker _dtpTo     = new();
        private Button         _btnRun    = new();
        private DataGridView   _dgv       = new();
        private Label          _lblSummary = new();
        private Label          _lblStatus  = new();

        public FormReturnsReport()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
        }

        private void BuildUI()
        {
            Text          = "Returns Report";
            ClientSize    = new Size(1060, 620);
            MinimumSize   = new Size(800, 460);
            StartPosition = FormStartPosition.CenterParent;

            // ── Header ────────────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = "Returns Report",
                Font      = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            });

            // ── Date range row ────────────────────────────────────────────────
            Controls.Add(new Label { Text = "From:", Location = new Point(12, 50), AutoSize = true });
            _dtpFrom.Location   = new Point(52, 46);
            _dtpFrom.Size       = new Size(130, 23);
            _dtpFrom.Format     = DateTimePickerFormat.Short;
            _dtpFrom.Value      = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            Controls.Add(_dtpFrom);

            Controls.Add(new Label { Text = "To:", Location = new Point(194, 50), AutoSize = true });
            _dtpTo.Location   = new Point(214, 46);
            _dtpTo.Size       = new Size(130, 23);
            _dtpTo.Format     = DateTimePickerFormat.Short;
            _dtpTo.Value      = DateTime.Today;
            Controls.Add(_dtpTo);

            _btnRun.Text     = "Run Report";
            _btnRun.Location = new Point(356, 44);
            _btnRun.Size     = new Size(100, 27);
            _btnRun.Click   += (_, _) => RunReport();
            Theme.StyleButton(_btnRun);
            Controls.Add(_btnRun);

            // ── Summary bar ───────────────────────────────────────────────────
            _lblSummary.Location  = new Point(12, 80);
            _lblSummary.Size      = new Size(1034, 22);
            _lblSummary.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            _lblSummary.ForeColor = Theme.Gold;
            _lblSummary.Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_lblSummary);

            // ── Grid ──────────────────────────────────────────────────────────
            _dgv.Location        = new Point(12, 108);
            _dgv.Size            = new Size(1036, 480);
            _dgv.Anchor          = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _dgv.ReadOnly        = true;
            _dgv.AllowUserToAddRows    = false;
            _dgv.AllowUserToDeleteRows = false;
            _dgv.AutoGenerateColumns   = false;
            _dgv.RowHeadersVisible     = false;
            _dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgv.CellFormatting       += Dgv_CellFormatting;

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRID",       HeaderText = "Return #",    Width = 76  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOrder",     HeaderText = "Order #",     Width = 80  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCustomer",  HeaderText = "Customer",    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDate",      HeaderText = "Return Date", Width = 105 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colReason",    HeaderText = "Reason",      Width = 160 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colResalable", HeaderText = "Resalable",   Width = 80  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDamaged",   HeaderText = "Damaged",     Width = 72  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colDestroy",   HeaderText = "Destroy",     Width = 68  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCredit",    HeaderText = "Credit",      Width = 90  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStatus",    HeaderText = "Status",      Width = 80  });
            Theme.StyleGrid(_dgv);
            Controls.Add(_dgv);

            _lblStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _lblStatus.Location = new Point(12, ClientSize.Height - 22);
            _lblStatus.AutoSize = true;
            Controls.Add(_lblStatus);
            SizeChanged += (_, _) => _lblStatus.Location = new Point(12, ClientSize.Height - 22);
        }

        private void RunReport()
        {
            try
            {
                DateTime from = _dtpFrom.Value.Date;
                DateTime to   = _dtpTo.Value.Date.AddDays(1).AddSeconds(-1);

                var rows = _repo.GetReturnReport(from, to);

                _dgv.Rows.Clear();
                foreach (var r in rows)
                {
                    int idx = _dgv.Rows.Add();
                    var row = _dgv.Rows[idx];
                    row.Cells["colRID"].Value       = r.ReturnID;
                    row.Cells["colOrder"].Value     = r.OriginalOrderNumber;
                    row.Cells["colCustomer"].Value  = r.CustomerName;
                    row.Cells["colDate"].Value      = r.ReturnDate.ToString("yyyy-MM-dd");
                    row.Cells["colReason"].Value    = r.Reason;
                    row.Cells["colResalable"].Value = r.ResalableQty;
                    row.Cells["colDamaged"].Value   = r.DamagedQty;
                    row.Cells["colDestroy"].Value   = r.DestroyQty;
                    row.Cells["colCredit"].Value    = r.CreditAmount > 0 ? r.CreditAmount.ToString("N2") : "—";
                    row.Cells["colStatus"].Value    = r.Status;
                    row.Tag = r;
                }

                // Summary
                int    totalReturns   = rows.Count;
                int    totalResalable = rows.Sum(r => r.ResalableQty);
                int    totalDamaged   = rows.Sum(r => r.DamagedQty);
                int    totalDestroy   = rows.Sum(r => r.DestroyQty);
                decimal totalCredit   = rows.Sum(r => r.CreditAmount);

                _lblSummary.Text =
                    $"{totalReturns} return(s)   |   " +
                    $"Resalable: {totalResalable}   " +
                    $"Damaged: {totalDamaged}   " +
                    $"Destroy: {totalDestroy}   |   " +
                    $"Total Credits Issued: {totalCredit:C}";

                _lblStatus.Text = $"{rows.Count} row(s)  |  " +
                    $"{_dtpFrom.Value:yyyy-MM-dd} → {_dtpTo.Value:yyyy-MM-dd}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Report failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Dgv_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || _dgv.Rows[e.RowIndex].Tag is not ReturnReportRow row) return;

            // Status column colouring
            if (_dgv.Columns[e.ColumnIndex].Name == "colStatus")
            {
                e.CellStyle.ForeColor = row.Status switch
                {
                    "Approved" => Color.FromArgb(80, 210, 100),
                    "Pending"  => Color.FromArgb(255, 200, 60),
                    "Rejected" => Color.FromArgb(200, 80, 80),
                    _          => Theme.TextSecondary
                };
            }

            // Damaged / Destroy cols in amber/red
            if (_dgv.Columns[e.ColumnIndex].Name == "colDamaged" && row.DamagedQty > 0)
                e.CellStyle.ForeColor = Color.FromArgb(255, 160, 60);
            if (_dgv.Columns[e.ColumnIndex].Name == "colDestroy" && row.DestroyQty > 0)
                e.CellStyle.ForeColor = Color.FromArgb(200, 80, 80);
        }
    }
}
