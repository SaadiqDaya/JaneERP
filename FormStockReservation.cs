using JaneERP.Models;

namespace JaneERP
{
    /// <summary>
    /// Modal dialog that lets the user review and adjust the quantities to lock (reserve)
    /// when a Sales Order goes Live or a Work Order goes InProgress.
    /// Confirmed lines are returned via <see cref="ConfirmedLines"/>; null means cancelled.
    /// </summary>
    internal class FormStockReservation : Form
    {
        private readonly List<ReservationLine> _lines;
        private readonly DataGridView          _dgv        = new();
        private readonly Label                 _lblSummary = new();
        private          Button?               _btnConfirm;
        private          Button?               _btnCancel;

        public List<ReservationLine>? ConfirmedLines { get; private set; }

        public FormStockReservation(string title, List<ReservationLine> lines)
        {
            _lines = lines;

            Text            = title;
            ClientSize      = new Size(870, 440);
            MinimumSize     = new Size(720, 380);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;

            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);

            LoadGrid();
            UpdateSummary();
        }

        private void BuildUI()
        {
            Controls.Add(new Label
            {
                Text     = "Review stock availability and set the quantity to lock for each item. Only locked items are reserved.",
                Location = new Point(12, 14),
                AutoSize = true
            });

            _dgv.Location              = new Point(12, 40);
            _dgv.Size                  = new Size(846, 330);
            _dgv.Anchor                = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _dgv.AutoGenerateColumns   = false;
            _dgv.AllowUserToAddRows    = false;
            _dgv.AllowUserToDeleteRows = false;
            _dgv.RowHeadersVisible     = false;
            _dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            _dgv.EditMode              = DataGridViewEditMode.EditOnKeystrokeOrF2;

            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colItem",      HeaderText = "Item",             Width = 210, ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLocation",  HeaderText = "Location",         Width = 130, ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRequired",  HeaderText = "Required",         Width = 74,  ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOnHand",    HeaderText = "On Hand",          Width = 74,  ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colReserved",  HeaderText = "Reserved",         Width = 80,  ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colAvailable", HeaderText = "Available",        Width = 80,  ReadOnly = true });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "colLock",      HeaderText = "Lock Qty \u270e",  Width = 90,  ReadOnly = false });

            _dgv.CellFormatting += Dgv_CellFormatting;
            _dgv.CellValidating += Dgv_CellValidating;
            _dgv.CellEndEdit    += Dgv_CellEndEdit;
            Controls.Add(_dgv);

            _lblSummary.Location = new Point(12, 380);
            _lblSummary.AutoSize = true;
            _lblSummary.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            Controls.Add(_lblSummary);

            _btnConfirm = new Button { Text = "Confirm Reservations", Size = new Size(168, 30), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            _btnConfirm.Click += BtnConfirm_Click;
            Theme.StyleButton(_btnConfirm);
            Controls.Add(_btnConfirm);

            _btnCancel = new Button { Text = "Cancel", Size = new Size(90, 30), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            _btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Theme.StyleSecondaryButton(_btnCancel);
            Controls.Add(_btnCancel);

            Resize += (_, _) => PositionButtons();
            Load   += (_, _) => PositionButtons();
        }

        private void PositionButtons()
        {
            if (_btnConfirm == null || _btnCancel == null) return;
            _btnConfirm.Location = new Point(ClientSize.Width - 276, ClientSize.Height - 44);
            _btnCancel.Location  = new Point(ClientSize.Width - 100, ClientSize.Height - 44);
        }

        private void LoadGrid()
        {
            _dgv.Rows.Clear();
            foreach (var line in _lines)
            {
                int i = _dgv.Rows.Add(
                    line.DisplayLabel,
                    line.LocationName,
                    line.Required,
                    line.OnHand,
                    line.AlreadyReserved,
                    line.Available,
                    line.ToLock
                );
                _dgv.Rows[i].Tag = line;
            }
        }

        private void Dgv_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _dgv.Rows.Count) return;
            if (_dgv.Rows[e.RowIndex].Tag is not ReservationLine line) return;

            var row = _dgv.Rows[e.RowIndex];
            if (line.Available < line.Required)
            {
                row.DefaultCellStyle.BackColor = Color.FromArgb(80, 30, 30);
                row.DefaultCellStyle.ForeColor = Color.FromArgb(255, 140, 140);
            }
            else
            {
                row.DefaultCellStyle.BackColor = _dgv.DefaultCellStyle.BackColor;
                row.DefaultCellStyle.ForeColor = _dgv.DefaultCellStyle.ForeColor;
            }
        }

        private void Dgv_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
        {
            if (_dgv.Columns[e.ColumnIndex].Name != "colLock") return;
            if (_dgv.Rows[e.RowIndex].Tag is not ReservationLine line) return;

            var raw = e.FormattedValue?.ToString() ?? "";
            if (!int.TryParse(raw, out int val) || val < 0 || val > line.Available)
            {
                e.Cancel = true;
                _dgv.Rows[e.RowIndex].ErrorText = $"Enter a whole number from 0 to {line.Available}.";
            }
        }

        private void Dgv_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            _dgv.Rows[e.RowIndex].ErrorText = "";
            if (_dgv.Columns[e.ColumnIndex].Name != "colLock") return;
            if (_dgv.Rows[e.RowIndex].Tag is not ReservationLine line) return;

            if (int.TryParse(_dgv.Rows[e.RowIndex].Cells["colLock"].Value?.ToString(), out int val))
            {
                line.ToLock = Math.Clamp(val, 0, line.Available);
                _dgv.Rows[e.RowIndex].Cells["colLock"].Value = line.ToLock;
            }

            UpdateSummary();
        }

        private void UpdateSummary()
        {
            int total    = _lines.Count(l => l.Required > 0);
            int full     = _lines.Count(l => l.Required > 0 && l.ToLock >= l.Required);
            int partial  = _lines.Count(l => l.Required > 0 && l.ToLock > 0 && l.ToLock < l.Required);
            int unlocked = _lines.Count(l => l.Required > 0 && l.ToLock == 0);

            _lblSummary.Text = $"Coverage: {full}/{total} fully locked" +
                               (partial  > 0 ? $"  |  {partial} partial"  : "") +
                               (unlocked > 0 ? $"  |  {unlocked} unlocked" : "");

            _lblSummary.ForeColor = full == total
                ? Color.FromArgb(100, 210, 100)
                : Color.FromArgb(255, 195, 60);
        }

        private void BtnConfirm_Click(object? sender, EventArgs e)
        {
            _dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
            _dgv.EndEdit();

            // Sync grid values back to line objects in case edit wasn't committed yet
            for (int i = 0; i < _dgv.Rows.Count; i++)
            {
                if (_dgv.Rows[i].Tag is not ReservationLine line) continue;
                if (int.TryParse(_dgv.Rows[i].Cells["colLock"].Value?.ToString(), out int val))
                    line.ToLock = Math.Clamp(val, 0, line.Available);
            }

            var uncovered = _lines.Where(l => l.Required > 0 && l.ToLock < l.Required).ToList();
            if (uncovered.Any())
            {
                var detail = string.Join("\n", uncovered.Select(l =>
                    $"  \u2022 {l.DisplayLabel}" +
                    (l.LocationId.HasValue ? $" @ {l.LocationName}" : "") +
                    $": locking {l.ToLock} of {l.Required}"));

                var result = MessageBox.Show(this,
                    $"{uncovered.Count} item(s) are not fully covered:\n\n{detail}\n\n" +
                    "Proceed with partial reservation?",
                    "Incomplete Coverage",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result != DialogResult.Yes) return;
            }

            ConfirmedLines = _lines.Where(l => l.ToLock > 0).ToList();
            DialogResult   = DialogResult.OK;
            Close();
        }
    }
}
