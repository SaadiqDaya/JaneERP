using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;
using System.Text;

namespace JaneERP
{
    /// <summary>
    /// Batch Cooking launcher.
    ///
    /// Workflow:
    ///   1. Shows In-Progress work orders only (WOs must be started before arriving here).
    ///   2. User ticks the WOs they want to cook together.
    ///   3. User picks a batch-loss % (from presets or manual entry).
    ///   4. Flask column updates live to show the vessel each batch will use.
    ///   5. Ingredient preview shows aggregated BOM quantities × (1 + loss%).
    ///   6. "Start Cook Session" creates the session and opens FormCookSession.
    ///
    /// Export buttons (Batch Traveller CSV, Labels CSV) work on existing open sessions.
    /// </summary>
    internal class FormBatchCooking : Form
    {
        private readonly IManufacturingRepository _repo =
            AppServices.Get<IManufacturingRepository>();

        // ── Controls ─────────────────────────────────────────────────────────────
        private SplitContainer _split     = new() { Dock = DockStyle.Fill, SplitterDistance = 520 };
        private DataGridView   _dgvWO     = new() { Dock = DockStyle.Fill };
        private DataGridView   _dgvIngr   = new() { Dock = DockStyle.Fill };
        private TextBox        _txtName   = new() { PlaceholderText = "Session name…", Width = 240 };
        private ComboBox       _cboLoss   = new() { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        private NumericUpDown  _nudLoss   = new() { Width = 70, DecimalPlaces = 1, Minimum = 0, Maximum = 100, Increment = 0.5m };
        private Button         _btnStart      = new() { Text = "▶  Start Cook Session", Width = 190, Height = 34 };
        private Button         _btnTraveller  = new() { Text = "Export Traveller CSV",  Width = 170, Height = 34 };
        private Button         _btnLabels     = new() { Text = "Export Labels CSV",      Width = 150, Height = 34 };
        private Button         _btnRefresh    = new() { Text = "↺ Refresh",              Width = 90,  Height = 34 };
        private Button         _btnClose      = new() { Text = "Close",                  Width = 90,  Height = 34 };
        private Label          _lblStatus     = new() { AutoSize = true, Text = "" };

        public FormBatchCooking()
        {
            Text            = "Batch Cooking";
            ClientSize      = new Size(1100, 660);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition   = FormStartPosition.CenterParent;
            MinimumSize     = new Size(860, 520);

            BuildGrid();
            BuildLayout();
            PopulateLossPresets();
            Load  += (_, _) => LoadWorkOrders();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
        }

        private void BuildGrid()
        {
            // Left — In-Progress work orders with checkboxes
            _dgvWO.AutoGenerateColumns = false;
            _dgvWO.AllowUserToAddRows  = false;
            _dgvWO.SelectionMode       = DataGridViewSelectionMode.FullRowSelect;
            _dgvWO.MultiSelect         = true;
            _dgvWO.ReadOnly            = false;
            _dgvWO.RowHeadersVisible   = false;
            _dgvWO.CellValueChanged   += DgvWO_CellValueChanged;
            _dgvWO.CurrentCellDirtyStateChanged += (_, _) =>
            {
                if (_dgvWO.IsCurrentCellDirty) _dgvWO.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            _dgvWO.Columns.Add(new DataGridViewCheckBoxColumn { Name = "colSel",      HeaderText = "",         Width = 30, ReadOnly = false });
            _dgvWO.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colWOID",     Visible = false });
            _dgvWO.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colProductID",Visible = false });
            _dgvWO.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colMO",       HeaderText = "MO#",      Width = 80,  ReadOnly = true });
            _dgvWO.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colProduct",  HeaderText = "Product",  Width = 200, ReadOnly = true });
            _dgvWO.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colQty",      HeaderText = "Qty",      Width = 50,  ReadOnly = true });
            _dgvWO.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colStatus",   HeaderText = "Status",   Width = 80,  ReadOnly = true });
            _dgvWO.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colAssigned", HeaderText = "Assigned", Width = 100, ReadOnly = true });
            _dgvWO.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colFlask",    HeaderText = "Flask",    Width = 110, ReadOnly = true });

            // Right — aggregated ingredient list
            _dgvIngr.AutoGenerateColumns = false;
            _dgvIngr.AllowUserToAddRows  = false;
            _dgvIngr.SelectionMode       = DataGridViewSelectionMode.FullRowSelect;
            _dgvIngr.ReadOnly            = true;
            _dgvIngr.RowHeadersVisible   = false;

            _dgvIngr.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPartNum",  HeaderText = "Part #",     Width = 100 });
            _dgvIngr.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPartName", HeaderText = "Ingredient", Width = 200 });
            _dgvIngr.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUOM",      HeaderText = "UOM",        Width = 50  });
            _dgvIngr.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRequired", HeaderText = "Required",   Width = 90, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _dgvIngr.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOnHand",   HeaderText = "On Hand",    Width = 80  });
            _dgvIngr.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOk",       HeaderText = "✓",          Width = 38  });
        }

        private void BuildLayout()
        {
            // ── Toolbar ───────────────────────────────────────────────────────────
            var toolbar = new FlowLayoutPanel
            {
                Dock          = DockStyle.Top,
                Height        = 52,
                Padding       = new Padding(6, 8, 6, 0),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false
            };

            _btnRefresh.Click += (_, _) => LoadWorkOrders();
            toolbar.Controls.Add(_btnRefresh);

            toolbar.Controls.Add(new Label
                { Text = "Session:", Width = 58, TextAlign = ContentAlignment.MiddleLeft, Height = 34 });
            toolbar.Controls.Add(_txtName);

            toolbar.Controls.Add(new Label
                { Text = "Batch Loss:", Width = 78, TextAlign = ContentAlignment.MiddleLeft, Height = 34 });
            _cboLoss.SelectedIndexChanged += CboLoss_Changed;
            toolbar.Controls.Add(_cboLoss);

            _nudLoss.ValueChanged += NudLoss_Changed;
            toolbar.Controls.Add(_nudLoss);

            toolbar.Controls.Add(_btnStart);
            toolbar.Controls.Add(_btnTraveller);
            toolbar.Controls.Add(_btnLabels);
            toolbar.Controls.Add(_btnClose);
            toolbar.Controls.Add(_lblStatus);

            _btnStart.Click     += BtnStart_Click;
            _btnTraveller.Click += BtnTraveller_Click;
            _btnLabels.Click    += BtnLabels_Click;
            _btnClose.Click     += (_, _) => this.Close();

            // ── Section headers ───────────────────────────────────────────────────
            var lblLeft  = new Label
            {
                Text = "In-Progress Work Orders — tick batches to include",
                Dock = DockStyle.Top, Height = 22,
                Font = new Font(Font, FontStyle.Bold)
            };
            var lblRight = new Label
            {
                Text = "Aggregated Ingredient Preview (with batch loss)",
                Dock = DockStyle.Top, Height = 22,
                Font = new Font(Font, FontStyle.Bold)
            };

            var pnlLeft  = new Panel { Dock = DockStyle.Fill };
            pnlLeft.Controls.Add(_dgvWO);
            pnlLeft.Controls.Add(lblLeft);

            var pnlRight = new Panel { Dock = DockStyle.Fill };
            pnlRight.Controls.Add(_dgvIngr);
            pnlRight.Controls.Add(lblRight);

            _split.Panel1.Controls.Add(pnlLeft);
            _split.Panel2.Controls.Add(pnlRight);

            Controls.Add(_split);
            Controls.Add(toolbar);
            Theme.AddFormHeader(this, "🧪  Batch Cooking");
        }

        private void PopulateLossPresets()
        {
            _cboLoss.Items.Clear();
            _cboLoss.Items.Add("Custom…");
            foreach (var p in AppSettings.Current.BatchLossPresets)
                _cboLoss.Items.Add(new LossPresetItem(p));
            _cboLoss.SelectedIndex = 0;
        }

        private void CboLoss_Changed(object? sender, EventArgs e)
        {
            if (_cboLoss.SelectedItem is LossPresetItem item)
            {
                _nudLoss.Value = item.Preset.Percent;
                _nudLoss.Enabled = false;
            }
            else
            {
                _nudLoss.Enabled = true;
            }
            RefreshFlaskColumn();
            RefreshIngredientPreview();
        }

        private void NudLoss_Changed(object? sender, EventArgs e)
        {
            RefreshFlaskColumn();
            RefreshIngredientPreview();
        }

        private decimal CurrentLossPercent => _nudLoss.Value;

        // ── Data loading ──────────────────────────────────────────────────────────

        private void LoadWorkOrders()
        {
            _dgvWO.Rows.Clear();
            // Only show In Progress WOs — these have been started and inventory allocated
            var wos = _repo.GetPendingWorkOrders()
                           .Where(wo => wo.Status.Equals("In Progress", StringComparison.OrdinalIgnoreCase) ||
                                        wo.Status.Equals("InProgress",  StringComparison.OrdinalIgnoreCase))
                           .ToList();
            foreach (var wo in wos)
            {
                int idx = _dgvWO.Rows.Add(
                    false, wo.WorkOrderID, wo.ProductID,
                    $"MO-{wo.MOID}", wo.ProductName ?? "", wo.Quantity,
                    wo.Status, wo.AssignedTo ?? "", "");
                _dgvWO.Rows[idx].Tag = wo;
            }
            RefreshFlaskColumn();
            RefreshIngredientPreview();
        }

        private void DgvWO_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == _dgvWO.Columns["colSel"]?.Index)
            {
                RefreshFlaskColumn();
                RefreshIngredientPreview();
            }
        }

        private List<WorkOrder> GetSelectedWorkOrders()
        {
            var list = new List<WorkOrder>();
            foreach (DataGridViewRow row in _dgvWO.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells["colSel"].Value is true && row.Tag is WorkOrder wo)
                    list.Add(wo);
            }
            return list;
        }

        private List<int> GetSelectedWOIds() => GetSelectedWorkOrders().Select(w => w.WorkOrderID).ToList();

        /// <summary>Recalculate and display the flask for every row based on the current loss%.</summary>
        private void RefreshFlaskColumn()
        {
            decimal loss = CurrentLossPercent;
            decimal mult = 1m + (loss / 100m);
            var settings = AppSettings.Current;

            foreach (DataGridViewRow row in _dgvWO.Rows)
            {
                if (row.IsNewRow || row.Tag is not WorkOrder wo) continue;

                // Get SizeML from WO's product attributes (best-effort — may not be set yet)
                decimal sizeMl = GetProductSizeMl(wo.ProductID);
                decimal batchMl = wo.Quantity * sizeMl * mult;
                string flask = batchMl > 0 ? settings.GetFlaskForBatchMl(batchMl) : "—";
                row.Cells["colFlask"].Value = flask;
            }
        }

        private readonly Dictionary<int, decimal> _sizeMlCache = new();

        private decimal GetProductSizeMl(int productId)
        {
            if (_sizeMlCache.TryGetValue(productId, out var cached)) return cached;
            try
            {
                var attrs = AppServices.Get<IProductRepository>().GetAttributes(productId);
                var val   = attrs.FirstOrDefault(a =>
                    a.AttributeName.Equals("SizeML", StringComparison.OrdinalIgnoreCase))?.AttributeValue;
                if (decimal.TryParse(val,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var ml))
                {
                    _sizeMlCache[productId] = ml;
                    return ml;
                }
            }
            catch { }
            _sizeMlCache[productId] = 0m;
            return 0m;
        }

        private void RefreshIngredientPreview()
        {
            _dgvIngr.Rows.Clear();
            var selected = GetSelectedWorkOrders();
            if (selected.Count == 0) return;

            decimal sessionLoss = CurrentLossPercent;

            // Aggregate BOM quantities across selected work orders.
            // Each row uses its own batch loss flag & rate; falls back to session loss when rate is 0.
            var summary = new Dictionary<int, (string PartNumber, string PartName, string? UOM, decimal Total, int OnHand)>();
            foreach (var wo in selected)
            {
                var bom = _repo.GetWOBomPreview(wo.WorkOrderID);
                foreach (var b in bom)
                {
                    decimal effectiveRate = b.CreatesBatchLoss
                        ? (b.BatchLossRate > 0 ? b.BatchLossRate : sessionLoss)
                        : 0m;
                    decimal adjustedQty = b.RequiredQty * (1m + effectiveRate / 100m);
                    if (summary.TryGetValue(b.PartID, out var existing))
                        summary[b.PartID] = (existing.PartNumber, existing.PartName, existing.UOM,
                                             existing.Total + adjustedQty, b.OnHand);
                    else
                        summary[b.PartID] = (b.PartNumber, b.PartName, b.UOM, adjustedQty, b.OnHand);
                }
            }

            foreach (var (_, s) in summary.OrderBy(x => x.Value.PartName))
            {
                bool ok  = s.OnHand >= (int)Math.Ceiling((double)s.Total);
                int  idx = _dgvIngr.Rows.Add(
                    s.PartNumber, s.PartName, s.UOM ?? "",
                    $"{s.Total:N3}", s.OnHand, ok ? "✓" : "✗");
                _dgvIngr.Rows[idx].DefaultCellStyle.ForeColor =
                    ok ? Color.LimeGreen : Color.OrangeRed;
            }
        }

        // ── Button handlers ───────────────────────────────────────────────────────

        /// <summary>
        /// Checks whether all ingredients required for the selected work orders are sufficiently
        /// in stock. Warns the user if any are short, but allows override — the user may intentionally
        /// cook with partial stock (e.g. split across two sessions).
        /// Returns false only if the user explicitly cancels after seeing the shortage warning.
        /// </summary>
        private bool CheckIngredientAvailability()
        {
            var selected = GetSelectedWorkOrders();
            if (selected.Count == 0) return true;

            decimal sessionLoss = CurrentLossPercent;

            // Aggregate BOM quantities — same logic as RefreshIngredientPreview
            var summary = new Dictionary<int, (string PartName, decimal Total, int OnHand)>();
            foreach (var wo in selected)
            {
                var bom = _repo.GetWOBomPreview(wo.WorkOrderID);
                foreach (var b in bom)
                {
                    decimal effectiveRate = b.CreatesBatchLoss
                        ? (b.BatchLossRate > 0 ? b.BatchLossRate : sessionLoss)
                        : 0m;
                    decimal adjustedQty = b.RequiredQty * (1m + effectiveRate / 100m);
                    if (summary.TryGetValue(b.PartID, out var existing))
                        summary[b.PartID] = (existing.PartName, existing.Total + adjustedQty, b.OnHand);
                    else
                        summary[b.PartID] = (b.PartName, adjustedQty, b.OnHand);
                }
            }

            var shortages = summary.Values
                .Where(s => s.OnHand < (int)Math.Ceiling((double)s.Total))
                .Select(s => $"  \u2022 {s.PartName}: need {s.Total:N0}, have {s.OnHand:N0}")
                .ToList();

            if (shortages.Count == 0) return true;

            var message = $"Insufficient ingredients for this batch:\n\n{string.Join("\n", shortages)}\n\nContinue anyway?";
            var result = MessageBox.Show(this, message, "Inventory Shortage",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            return result == DialogResult.Yes;
        }

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            var woIds = GetSelectedWOIds();
            if (woIds.Count == 0)
            {
                MessageBox.Show(this, "Tick at least one work order.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!CheckIngredientAvailability()) return;

            string  name = _txtName.Text.Trim();
            if (string.IsNullOrEmpty(name)) name = $"Cook {DateTime.Now:yyyy-MM-dd HH:mm}";
            decimal loss = CurrentLossPercent;

            try
            {
                string user      = Security.AppSession.CurrentUser?.Username ?? "system";
                int    sessionId = _repo.CreateCookSession(name, woIds, loss, user);
                _lblStatus.Text  = "";

                using var frm = new FormCookSession(sessionId);
                frm.ShowDialog(this);
                LoadWorkOrders(); // refresh after returning from session
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to start session:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnTraveller_Click(object? sender, EventArgs e)
        {
            var sessions = _repo.GetOpenCookSessions();
            if (sessions.Count == 0)
            {
                MessageBox.Show(this, "No open cook sessions found.\nStart a session first.",
                    "No Sessions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var picker = new FormSessionPicker(sessions, "Export Batch Traveller");
            if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedSessionID == 0) return;

            var rows = _repo.GetBatchTravellerData(picker.SelectedSessionID);
            ExportCsv(
                rows.Select(r => new[] { r.WONumber, r.PartNumber, r.PartName,
                    r.Nicotine ?? "", r.Size ?? "", r.Qty.ToString(),
                    r.FlaskType ?? "", r.Bins ?? "", r.Concentrate ?? "", r.Notes ?? "" }),
                new[] { "WO#", "Part Number", "Part Description", "Nicotine", "Size",
                        "Qty", "Flask Type", "# Bins", "Concentrate", "Notes" },
                "BatchTraveller");
        }

        private void BtnLabels_Click(object? sender, EventArgs e)
        {
            var sessions = _repo.GetOpenCookSessions();
            if (sessions.Count == 0)
            {
                MessageBox.Show(this, "No open cook sessions found.\nStart a session first.",
                    "No Sessions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var picker = new FormSessionPicker(sessions, "Export Label CSV");
            if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedSessionID == 0) return;

            var rows = _repo.GetLabelExportData(picker.SelectedSessionID);
            ExportCsv(
                rows.Select(r => new[] { r.BottleType ?? "", r.WONumber, r.PartName, r.PartNumber,
                    r.QtyOrdered.ToString(), r.Version ?? "", r.BatchMadeDate,
                    r.Size ?? "", r.Brand ?? "", r.Nicotine ?? "", r.VG ?? "", r.Note ?? "" }),
                new[] { "Bottle Type", "WO#", "Part Description", "Part Number", "Qty Ordered",
                        "Version", "Batch Made Date", "Size", "Brand", "Nicotine", "VG", "Note" },
                "LabelExport");
        }

        private void ExportCsv(IEnumerable<string[]> rows, string[] headers, string filePrefix)
        {
            using var dlg = new SaveFileDialog
            {
                Filter   = "CSV files (*.csv)|*.csv",
                FileName = $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers.Select(h => $"\"{h}\"")));
            foreach (var row in rows)
                sb.AppendLine(string.Join(",", row.Select(v => $"\"{v.Replace("\"", "\"\"")}\"")));
            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show(this, $"Exported to:\n{dlg.FileName}", "Export Complete",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ── Combo item wrapper ────────────────────────────────────────────────────

        private record LossPresetItem(BatchLossPreset Preset)
        {
            public override string ToString() =>
                $"{Preset.Label}  ({Preset.Percent:G}%)";
        }
    }

    /// <summary>Simple dialog to pick one cook session from a list.</summary>
    internal class FormSessionPicker : Form
    {
        public int SelectedSessionID { get; private set; }
        private ListBox _list = new() { Dock = DockStyle.Fill };

        public FormSessionPicker(List<Models.CookSession> sessions, string title)
        {
            Text            = title;
            ClientSize      = new Size(380, 260);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;

            foreach (var s in sessions)
                _list.Items.Add(new SessionItem(s));

            var btnOk = new Button
            {
                Text = "Select", DialogResult = DialogResult.OK,
                Dock = DockStyle.Bottom, Height = 32
            };
            btnOk.Click += (_, _) =>
            {
                if (_list.SelectedItem is SessionItem si)
                    SelectedSessionID = si.Session.CookSessionID;
                Close();
            };

            Controls.Add(_list);
            Controls.Add(btnOk);
            Theme.Apply(this);
            Theme.MakeBorderless(this);
        }

        private record SessionItem(Models.CookSession Session)
        {
            public override string ToString() =>
                $"{Session.SessionName}  ({Session.BatchLossPercent:G}% loss)  {Session.CreatedAt:MMM d HH:mm}";
        }
    }
}
