using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>
    /// Ingredient-first cook session driver.
    /// Left panel: choose ingredient → choose batch → see required qty → mark done.
    /// Right panel: live progress grid showing all ingredients and their completion.
    /// </summary>
    internal class FormCookSession : Form
    {
        private readonly IManufacturingRepository _repo =
            AppServices.Get<IManufacturingRepository>();

        private readonly int _sessionId;
        private string _user = Security.AppSession.CurrentUser?.Username ?? "system";

        // ── Working data ─────────────────────────────────────────────────────────
        private List<CookIngredientSummary> _ingredients = new();
        private List<CookSessionStep>       _steps       = new();

        // ── Controls ─────────────────────────────────────────────────────────────
        private SplitContainer _split       = new() { Dock = DockStyle.Fill, SplitterDistance = 360 };
        private ComboBox       _cmbIngr     = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
        private ComboBox       _cmbBatch    = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
        private Label          _lblRequired = new() { AutoSize = true };
        private Label          _lblTotal    = new() { AutoSize = true };
        private Label          _lblOnHand   = new() { AutoSize = true };
        private Button         _btnMarkDone    = new() { Text = "✓  Mark This Batch Done",      Height = 36, Width = 240 };
        private Button         _btnMarkAll     = new() { Text = "✓✓  Mark ALL Batches Done",    Height = 36, Width = 240 };
        private Button         _btnComplete    = new() { Text = "🏁  Complete Session",           Height = 36, Width = 240 };
        private Label          _lblProgress    = new() { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
        private DataGridView   _dgvProgress    = new() { Dock = DockStyle.Fill };

        public FormCookSession(int sessionId)
        {
            _sessionId = sessionId;
            Text            = "Cook Session";
            ClientSize      = new Size(1060, 620);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition   = FormStartPosition.CenterParent;
            MinimumSize     = new Size(800, 500);

            BuildProgressGrid();
            BuildLayout();
            Load += (_, _) => Reload();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
        }

        private void BuildProgressGrid()
        {
            _dgvProgress.AutoGenerateColumns = false;
            _dgvProgress.AllowUserToAddRows  = false;
            _dgvProgress.ReadOnly            = true;
            _dgvProgress.SelectionMode       = DataGridViewSelectionMode.FullRowSelect;
            _dgvProgress.RowHeadersVisible   = false;

            _dgvProgress.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPartName",  HeaderText = "Ingredient",   Width = 200 });
            _dgvProgress.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUOM",       HeaderText = "UOM",          Width = 55  });
            _dgvProgress.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTotal",     HeaderText = "Total Needed", Width = 100 });
            _dgvProgress.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOnHand",    HeaderText = "On Hand",      Width = 80  });
            _dgvProgress.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProgress",  HeaderText = "Progress",     Width = 70  });
            _dgvProgress.Columns.Add(new DataGridViewTextBoxColumn { Name = "colStatus",    HeaderText = "Status",       Width = 70  });
        }

        private void BuildLayout()
        {
            // ── Left panel ────────────────────────────────────────────────────────
            var pnlLeft = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };

            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                ColumnCount = 2,
                AutoSize    = true,
                Padding     = new Padding(0, 0, 0, 10)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            tbl.Controls.Add(new Label { Text = "Ingredient:", TextAlign = ContentAlignment.MiddleLeft, Height = 30 }, 0, 0);
            tbl.Controls.Add(_cmbIngr, 1, 0);
            tbl.Controls.Add(new Label { Text = "Batch:",      TextAlign = ContentAlignment.MiddleLeft, Height = 30 }, 0, 1);
            tbl.Controls.Add(_cmbBatch, 1, 1);

            // Info panel
            var pnlInfo = new Panel { Dock = DockStyle.Top, Height = 90, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 8, 0, 8) };
            _lblRequired.Location = new Point(8, 8);
            _lblTotal.Location    = new Point(8, 30);
            _lblOnHand.Location   = new Point(8, 52);
            pnlInfo.Controls.AddRange(new Control[] { _lblRequired, _lblTotal, _lblOnHand });

            // Buttons
            var pnlBtns = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
            pnlBtns.Controls.Add(_btnMarkDone);
            pnlBtns.Controls.Add(_btnMarkAll);
            pnlBtns.Controls.Add(new Panel { Height = 16 }); // spacer
            pnlBtns.Controls.Add(_btnComplete);

            pnlLeft.Controls.Add(pnlBtns);
            pnlLeft.Controls.Add(pnlInfo);
            pnlLeft.Controls.Add(tbl);

            // ── Right panel ───────────────────────────────────────────────────────
            var pnlRight = new Panel { Dock = DockStyle.Fill };
            var lblRight = new Label { Text = "Ingredient Progress", Dock = DockStyle.Top, Height = 24, Font = new Font(Font, FontStyle.Bold) };
            _lblProgress.Dock = DockStyle.Bottom;
            pnlRight.Controls.Add(_dgvProgress);
            pnlRight.Controls.Add(_lblProgress);
            pnlRight.Controls.Add(lblRight);

            _split.Panel1.Controls.Add(pnlLeft);
            _split.Panel2.Controls.Add(pnlRight);

            Controls.Add(_split);

            // Wire events
            _cmbIngr.SelectedIndexChanged  += CmbIngr_Changed;
            _cmbBatch.SelectedIndexChanged += CmbBatch_Changed;
            _btnMarkDone.Click   += BtnMarkDone_Click;
            _btnMarkAll.Click    += BtnMarkAll_Click;
            _btnComplete.Click   += BtnComplete_Click;
        }

        // ── Data loading ──────────────────────────────────────────────────────────

        private void Reload()
        {
            _ingredients = _repo.GetCookIngredients(_sessionId);
            _steps       = _repo.GetCookSessionSteps(_sessionId);

            var session = _repo.GetCookSession(_sessionId);
            Text = $"Cook Session — {session?.SessionName ?? _sessionId.ToString()}";
            if (session?.Status == "Complete") _btnComplete.Enabled = false;

            RefreshProgressGrid();
            RefreshIngredientCombo();
        }

        private void RefreshIngredientCombo()
        {
            int prevPart = (_cmbIngr.SelectedItem as IngredientItem)?.Ingredient.PartID ?? 0;

            _cmbIngr.Items.Clear();
            foreach (var ingr in _ingredients)
                _cmbIngr.Items.Add(new IngredientItem(ingr));

            if (prevPart > 0)
            {
                for (int i = 0; i < _cmbIngr.Items.Count; i++)
                {
                    if ((_cmbIngr.Items[i] as IngredientItem)?.Ingredient.PartID == prevPart)
                    { _cmbIngr.SelectedIndex = i; return; }
                }
            }
            if (_cmbIngr.Items.Count > 0) _cmbIngr.SelectedIndex = 0;
        }

        private void RefreshProgressGrid()
        {
            _dgvProgress.Rows.Clear();
            int done = 0, total = 0;
            foreach (var ingr in _ingredients)
            {
                bool complete = ingr.IsComplete;
                done  += ingr.StepsDone;
                total += ingr.StepsTotal;
                int idx = _dgvProgress.Rows.Add(
                    ingr.PartName,
                    ingr.UnitOfMeasure ?? "",
                    $"{ingr.TotalRequired:N4}",
                    ingr.OnHand,
                    ingr.ProgressText,
                    complete ? "Done ✓" : "Pending");
                _dgvProgress.Rows[idx].DefaultCellStyle.ForeColor =
                    complete ? Color.LimeGreen : Color.OrangeRed;
                _dgvProgress.Rows[idx].Tag = ingr.PartID;
            }
            _lblProgress.Text = $"Overall: {done}/{total} steps done";
        }

        // ── ComboBox handlers ─────────────────────────────────────────────────────

        private void CmbIngr_Changed(object? sender, EventArgs e)
        {
            if (_cmbIngr.SelectedItem is not IngredientItem item) return;
            int partId = item.Ingredient.PartID;

            // Populate batch combobox with work orders that have a step for this ingredient
            var batchSteps = _steps.Where(s => s.PartID == partId).ToList();

            int prevWO = (_cmbBatch.SelectedItem as BatchItem)?.Step.WorkOrderID ?? 0;
            _cmbBatch.Items.Clear();
            foreach (var s in batchSteps)
                _cmbBatch.Items.Add(new BatchItem(s));

            if (prevWO > 0)
            {
                for (int i = 0; i < _cmbBatch.Items.Count; i++)
                {
                    if ((_cmbBatch.Items[i] as BatchItem)?.Step.WorkOrderID == prevWO)
                    { _cmbBatch.SelectedIndex = i; return; }
                }
            }

            // Prefer first undone batch
            for (int i = 0; i < _cmbBatch.Items.Count; i++)
            {
                if (!(_cmbBatch.Items[i] as BatchItem)!.Step.IsDone)
                { _cmbBatch.SelectedIndex = i; return; }
            }
            if (_cmbBatch.Items.Count > 0) _cmbBatch.SelectedIndex = 0;
        }

        private void CmbBatch_Changed(object? sender, EventArgs e)
        {
            RefreshInfoPanel();
        }

        private void RefreshInfoPanel()
        {
            if (_cmbIngr.SelectedItem is not IngredientItem ii)
            {
                _lblRequired.Text = ""; _lblTotal.Text = ""; _lblOnHand.Text = "";
                return;
            }

            var ingr = ii.Ingredient;

            if (_cmbBatch.SelectedItem is BatchItem bi)
            {
                _lblRequired.Text = $"Required (this batch): {bi.Step.RequiredQty:N4} {ingr.UnitOfMeasure ?? ""}";
                _btnMarkDone.Enabled = !bi.Step.IsDone;
            }
            else
            {
                _lblRequired.Text    = "Required (this batch): —";
                _btnMarkDone.Enabled = false;
            }

            _lblTotal.Text  = $"Total (all batches):    {ingr.TotalRequired:N4} {ingr.UnitOfMeasure ?? ""}";
            bool hasEnough  = ingr.HasEnoughStock;
            _lblOnHand.Text = $"On Hand:                {ingr.OnHand}  {(hasEnough ? "✓" : "✗ SHORTAGE")}";
            _lblOnHand.ForeColor = hasEnough ? Color.LimeGreen : Color.OrangeRed;
            _btnMarkAll.Enabled  = !ingr.IsComplete;
        }

        // ── Button handlers ───────────────────────────────────────────────────────

        private void BtnMarkDone_Click(object? sender, EventArgs e)
        {
            if (_cmbBatch.SelectedItem is not BatchItem bi) return;
            if (bi.Step.IsDone) return;

            _repo.MarkStepDone(bi.Step.StepID, _user);
            Reload();
        }

        private void BtnMarkAll_Click(object? sender, EventArgs e)
        {
            if (_cmbIngr.SelectedItem is not IngredientItem ii) return;

            var res = MessageBox.Show(this,
                $"Mark ALL batches done for:\n{ii.Ingredient.PartName}?",
                "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res != DialogResult.Yes) return;

            _repo.MarkAllIngredientStepsDone(_sessionId, ii.Ingredient.PartID, _user);
            Reload();
        }

        private void BtnComplete_Click(object? sender, EventArgs e)
        {
            int pending = _steps.Count(s => !s.IsDone);
            if (pending > 0)
            {
                var res = MessageBox.Show(this,
                    $"{pending} step(s) are still pending.\nForce complete the session anyway?",
                    "Pending Steps", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (res != DialogResult.Yes) return;
                _repo.CompleteCookSession(_sessionId, forceComplete: true);
            }
            else
            {
                _repo.CompleteCookSession(_sessionId);
            }

            MessageBox.Show(this, "Cook session completed!", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _btnComplete.Enabled = false;
            Reload();
        }

        // ── ComboBox item wrappers ────────────────────────────────────────────────

        private record IngredientItem(CookIngredientSummary Ingredient)
        {
            public override string ToString() =>
                $"{Ingredient.PartName} ({Ingredient.ProgressText}){(Ingredient.IsComplete ? " ✓" : "")}";
        }

        private record BatchItem(CookSessionStep Step)
        {
            public override string ToString() =>
                $"{Step.ProductName} — WO #{Step.WorkOrderID}  ×{Step.WorkOrderQty}{(Step.IsDone ? "  ✓" : "")}";
        }
    }
}
