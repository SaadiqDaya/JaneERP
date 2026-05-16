using System.Globalization;
using System.Text;
using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;
using JaneERP.Security;

namespace JaneERP
{
    /// <summary>
    /// Two-step inventory move importer:
    ///   1. Pick CSV → validate → preview grid (colour-coded valid/invalid).
    ///   2. "Execute N Moves" button commits the transfers atomically.
    ///
    /// Expected CSV columns: SKU, FromLocation, ToLocation, RequestedQty (optional).
    /// A blank ToLocation row is silently skipped — this lets users export the
    /// "Inventory by Location" template and fill only the rows they want to move.
    /// </summary>
    public class FormInventoryMoveImport : Form
    {
        private readonly IImportRepository _repo = AppServices.Get<IImportRepository>();

        // ── Controls ─────────────────────────────────────────────────────────────
        private TextBox      _txtFile   = new();
        private Button       _btnBrowse = new();
        private Button       _btnLoad   = new();
        private DataGridView _dgv       = new();
        private Label        _lblStatus = new();
        private Button       _btnExecute = new();

        private List<InventoryMoveRow> _preview = new();

        // ── Colours ───────────────────────────────────────────────────────────────
        private static readonly Color ColValid   = Color.FromArgb(30, 110, 50);
        private static readonly Color ColInvalid = Color.FromArgb(100, 30, 30);
        private static readonly Color ColSkipped = Color.FromArgb(55, 55, 55);

        public FormInventoryMoveImport()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
        }

        private void BuildUI()
        {
            Text          = "Inventory Move Import";
            ClientSize    = new Size(980, 640);
            MinimumSize   = new Size(760, 460);
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label
            {
                Text      = "CSV columns: SKU, FromLocation, ToLocation  (optional: RequestedQty, LotNumber)  —  blank ToLocation rows are skipped",
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(14, 56),
                AutoSize  = true
            });

            // ── File picker row ───────────────────────────────────────────────────
            Controls.Add(new Label { Text = "File:", Location = new Point(12, 68), AutoSize = true });

            _txtFile.Location  = new Point(44, 64);
            _txtFile.Size      = new Size(660, 24);
            _txtFile.ReadOnly  = true;
            _txtFile.BackColor = Theme.Surface;
            _txtFile.ForeColor = Theme.TextPrimary;
            Controls.Add(_txtFile);

            _btnBrowse.Text     = "Browse…";
            _btnBrowse.Location = new Point(714, 63);
            _btnBrowse.Size     = new Size(80, 26);
            _btnBrowse.Click   += BtnBrowse_Click;
            Theme.StyleButton(_btnBrowse);
            Controls.Add(_btnBrowse);

            _btnLoad.Text     = "Load & Validate";
            _btnLoad.Location = new Point(804, 63);
            _btnLoad.Size     = new Size(120, 26);
            _btnLoad.Click   += BtnLoad_Click;
            Theme.StyleButton(_btnLoad);
            Controls.Add(_btnLoad);

            // ── Preview grid ──────────────────────────────────────────────────────
            _dgv.Location        = new Point(12, 100);
            _dgv.Size            = new Size(956, 490);
            _dgv.Anchor          = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _dgv.ReadOnly        = true;
            _dgv.AllowUserToAddRows    = false;
            _dgv.AllowUserToDeleteRows = false;
            _dgv.AutoGenerateColumns   = false;
            _dgv.RowHeadersVisible     = false;
            _dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgv.CellFormatting       += Dgv_CellFormatting;

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSKU",       HeaderText = "SKU",          Width = 110 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProduct",   HeaderText = "Product",      AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colFrom",      HeaderText = "From",         Width = 140 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTo",        HeaderText = "To",           Width = 140 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAvailable", HeaderText = "Available",    Width = 82  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colMoveQty",   HeaderText = "Will Move",    Width = 82  });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStatus",    HeaderText = "Status",       Width = 160 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLots",      HeaderText = "Lots",         Width = 160 });

            Theme.StyleGrid(_dgv);
            Controls.Add(_dgv);

            // ── Status bar + execute button ───────────────────────────────────────
            _lblStatus.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            _lblStatus.Location = new Point(12, ClientSize.Height - 28);
            _lblStatus.AutoSize = true;
            _lblStatus.ForeColor = Theme.TextSecondary;
            Controls.Add(_lblStatus);
            SizeChanged += (_, _) => _lblStatus.Location = new Point(12, ClientSize.Height - 28);

            _btnExecute.Text     = "Execute Moves";
            _btnExecute.Enabled  = false;
            _btnExecute.Size     = new Size(140, 30);
            _btnExecute.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnExecute.Location = new Point(ClientSize.Width - 154, ClientSize.Height - 34);
            _btnExecute.Click   += BtnExecute_Click;
            Theme.StyleButton(_btnExecute);
            Controls.Add(_btnExecute);
            SizeChanged += (_, _) =>
                _btnExecute.Location = new Point(ClientSize.Width - 154, ClientSize.Height - 34);
            Theme.AddFormHeader(this, "📥  Inventory Move Import");
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title            = "Open Inventory Moves CSV",
                Filter           = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                _txtFile.Text = dlg.FileName;
        }

        private void BtnLoad_Click(object? sender, EventArgs e)
        {
            var path = _txtFile.Text.Trim();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show(this, "Please browse to a CSV file first.", "No File",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var parsed = ParseCsv(path);
                if (parsed.Count == 0)
                {
                    MessageBox.Show(this, "The CSV file contains no data rows.", "Empty File",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _lblStatus.Text    = "Validating…";
                _btnExecute.Enabled = false;
                _dgv.Rows.Clear();
                _preview.Clear();
                Refresh();

                Cursor = Cursors.WaitCursor;
                try
                {
                    // Strip lot column before passing to the interface (which takes 4-tuples),
                    // then assign LotNumber back to the validated rows afterwards.
                    var coreInput = parsed.Select(p => (p.sku, p.from, p.to, p.qty));
                    _preview = _repo.ValidateInventoryMoves(coreInput);

                    // Carry lot numbers parsed from the CSV into the validated rows
                    for (int i = 0; i < _preview.Count && i < parsed.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(parsed[i].lot))
                            _preview[i].LotNumber = parsed[i].lot;
                    }
                }
                finally
                {
                    Cursor = Cursors.Default;
                }

                PopulateGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to load file:\n{ex.Message}", "Load Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            int validCount = _preview.Count(r => r.IsValid);
            if (validCount == 0) return;

            var confirm = MessageBox.Show(this,
                $"Execute {validCount} inventory move(s)?\n\nThis will write to InventoryTransactions and cannot be undone.",
                "Confirm Moves", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            try
            {
                _btnExecute.Enabled = false;
                var (moved, skipped) = _repo.ExecuteInventoryMoves(_preview, AppSession.CurrentUser?.Username ?? "system");

                MessageBox.Show(this,
                    $"Done.\n\n{moved} move(s) executed.\n{skipped} skipped (errors).",
                    "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Reload the preview to reflect new stock levels
                BtnLoad_Click(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Execute failed:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnExecute.Enabled = _preview.Any(r => r.IsValid);
            }
        }

        // ── Grid ──────────────────────────────────────────────────────────────────

        private void PopulateGrid()
        {
            _dgv.Rows.Clear();

            foreach (var r in _preview)
            {
                int idx = _dgv.Rows.Add();
                var row = _dgv.Rows[idx];
                row.Cells["colSKU"].Value       = r.SKU;
                row.Cells["colProduct"].Value   = r.IsValid ? r.ProductName : r.SKU;
                row.Cells["colFrom"].Value      = r.FromLocation;
                row.Cells["colTo"].Value        = r.ToLocation;
                row.Cells["colAvailable"].Value = r.IsValid ? r.AvailableQty.ToString() : "—";
                row.Cells["colMoveQty"].Value   = r.IsValid ? r.MoveQty.ToString()      : "—";
                row.Cells["colStatus"].Value    = r.StatusLabel;
                row.Cells["colLots"].Value      = r.LotSummary;
                row.Tag = r;
            }

            int valid   = _preview.Count(r => r.IsValid);
            int invalid = _preview.Count - valid;

            _lblStatus.Text = $"{_preview.Count} row(s)  |  {valid} valid  |  {invalid} skipped";
            _btnExecute.Text    = $"Execute {valid} Moves";
            _btnExecute.Enabled = valid > 0;
        }

        private void Dgv_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || _dgv.Rows[e.RowIndex].Tag is not InventoryMoveRow row) return;

            Color bg = row.IsValid ? ColValid : ColInvalid;
            e.CellStyle.BackColor = bg;
            e.CellStyle.ForeColor = row.IsValid ? Color.FromArgb(160, 255, 160) : Color.FromArgb(255, 160, 160);
            e.CellStyle.SelectionBackColor = bg;
        }

        // ── CSV parsing ───────────────────────────────────────────────────────────

        /// <summary>
        /// Parses the CSV and returns tuples ready for ValidateInventoryMoves.
        /// Rows with a blank ToLocation are silently skipped (move-template export workflow).
        /// Optional columns: RequestedQty, LotNumber.
        /// </summary>
        private static List<(string sku, string from, string to, int? qty, string? lot)> ParseCsv(string path)
        {
            var result  = new List<(string, string, string, int?, string?)>();
            var lines   = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length < 2) return result;

            var headers = lines[0].Split(',').Select(h => h.Trim().Trim('"')).ToArray();

            foreach (var line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cells = SplitCsvLine(line);
                var row   = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < headers.Length && i < cells.Length; i++)
                    row[headers[i]] = cells[i].Trim().Trim('"');

                var sku  = row.GetValueOrDefault("SKU",          "").Trim();
                var from = row.GetValueOrDefault("FromLocation", "").Trim();
                var to   = row.GetValueOrDefault("ToLocation",   "").Trim();

                if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(from)) continue;
                if (string.IsNullOrWhiteSpace(to)) continue; // blank ToLocation = skip

                int? qty = null;
                var qtyStr = row.GetValueOrDefault("RequestedQty", "").Trim();
                if (!string.IsNullOrWhiteSpace(qtyStr) &&
                    int.TryParse(qtyStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var q) && q > 0)
                    qty = q;

                var lotStr = row.GetValueOrDefault("LotNumber", "").Trim();
                string? lot = string.IsNullOrWhiteSpace(lotStr) ? null : lotStr;

                result.Add((sku, from, to, qty, lot));
            }

            return result;
        }

        private static string[] SplitCsvLine(string line)
        {
            var fields  = new List<string>();
            bool inQ    = false;
            var current = new StringBuilder();
            foreach (char c in line)
            {
                if (c == '"')       { inQ = !inQ; }
                else if (c == ',' && !inQ) { fields.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }
            fields.Add(current.ToString());
            return fields.ToArray();
        }
    }
}
