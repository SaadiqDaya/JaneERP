using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;
using System.Text;

namespace JaneERP
{
    /// <summary>
    /// Batch Cooking launcher — select work orders to cook, preview the aggregated ingredient list,
    /// start a cook session, or export Batch Traveller / Label CSVs.
    /// </summary>
    internal class FormBatchCooking : Form
    {
        private readonly IManufacturingRepository _repo =
            AppServices.Get<IManufacturingRepository>();

        // ── Controls ─────────────────────────────────────────────────────────────
        private SplitContainer _split    = new() { Dock = DockStyle.Fill, SplitterDistance = 480 };
        private DataGridView   _dgvWO    = new() { Dock = DockStyle.Fill };
        private DataGridView   _dgvIngr  = new() { Dock = DockStyle.Fill };
        private TextBox        _txtName  = new() { PlaceholderText = "Session name…", Width = 280 };
        private Button         _btnStart = new() { Text = "▶  Start Cook Session", Width = 180, Height = 34 };
        private Button         _btnTraveller = new() { Text = "📄  Export Traveller CSV", Width = 180, Height = 34 };
        private Button         _btnLabels    = new() { Text = "🏷  Export Labels CSV",    Width = 180, Height = 34 };
        private Button         _btnRefresh   = new() { Text = "↺ Refresh", Width = 90,  Height = 34 };
        private Label          _lblStatus    = new() { AutoSize = true, Text = "" };

        public FormBatchCooking()
        {
            Text            = "Batch Cooking";
            ClientSize      = new Size(1050, 640);
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition   = FormStartPosition.CenterParent;
            MinimumSize     = new Size(800, 500);

            BuildGrid();
            BuildLayout();
            Load  += (_, _) => LoadWorkOrders();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
        }

        private void BuildGrid()
        {
            // Left — open work orders with checkboxes
            _dgvWO.AutoGenerateColumns = false;
            _dgvWO.AllowUserToAddRows  = false;
            _dgvWO.SelectionMode       = DataGridViewSelectionMode.FullRowSelect;
            _dgvWO.MultiSelect         = true;
            _dgvWO.ReadOnly            = false;
            _dgvWO.RowHeadersVisible   = false;
            _dgvWO.CellValueChanged   += DgvWO_CellValueChanged;
            _dgvWO.CurrentCellDirtyStateChanged += (_, _) => { if (_dgvWO.IsCurrentCellDirty) _dgvWO.CommitEdit(DataGridViewDataErrorContexts.Commit); };

            _dgvWO.Columns.Add(new DataGridViewCheckBoxColumn { Name = "colSel", HeaderText = "", Width = 30, ReadOnly = false });
            _dgvWO.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colWOID",    Visible = false });
            _dgvWO.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colMO",      HeaderText = "MO#",     Width = 80,  ReadOnly = true });
            _dgvWO.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colProduct",  HeaderText = "Product", Width = 200, ReadOnly = true });
            _dgvWO.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colQty",      HeaderText = "Qty",     Width = 55,  ReadOnly = true });
            _dgvWO.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colStatus",   HeaderText = "Status",  Width = 80,  ReadOnly = true });
            _dgvWO.Columns.Add(new DataGridViewTextBoxColumn  { Name = "colAssigned", HeaderText = "Assigned",Width = 100, ReadOnly = true });

            // Right — aggregated ingredient list
            _dgvIngr.AutoGenerateColumns = false;
            _dgvIngr.AllowUserToAddRows  = false;
            _dgvIngr.SelectionMode       = DataGridViewSelectionMode.FullRowSelect;
            _dgvIngr.ReadOnly            = true;
            _dgvIngr.RowHeadersVisible   = false;

            _dgvIngr.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPartNum",  HeaderText = "Part #",    Width = 110 });
            _dgvIngr.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPartName", HeaderText = "Ingredient",Width = 200 });
            _dgvIngr.Columns.Add(new DataGridViewTextBoxColumn { Name = "colUOM",      HeaderText = "UOM",       Width = 55  });
            _dgvIngr.Columns.Add(new DataGridViewTextBoxColumn { Name = "colRequired", HeaderText = "Required",  Width = 90  });
            _dgvIngr.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOnHand",   HeaderText = "On Hand",   Width = 80  });
            _dgvIngr.Columns.Add(new DataGridViewTextBoxColumn { Name = "colOk",       HeaderText = "✓",         Width = 40  });
        }

        private void BuildLayout()
        {
            // Top toolbar
            var toolbar = new FlowLayoutPanel
            {
                Dock      = DockStyle.Top,
                Height    = 46,
                Padding   = new Padding(6, 6, 6, 0),
                FlowDirection = FlowDirection.LeftToRight
            };
            _btnRefresh.Click += (_, _) => LoadWorkOrders();
            toolbar.Controls.Add(_btnRefresh);
            toolbar.Controls.Add(new Label { Text = "Session name:", Width = 95, TextAlign = ContentAlignment.MiddleLeft, Height = 34 });
            toolbar.Controls.Add(_txtName);
            toolbar.Controls.Add(_btnStart);
            toolbar.Controls.Add(_btnTraveller);
            toolbar.Controls.Add(_btnLabels);
            toolbar.Controls.Add(_lblStatus);

            _btnStart.Click     += BtnStart_Click;
            _btnTraveller.Click += BtnTraveller_Click;
            _btnLabels.Click    += BtnLabels_Click;

            // Section headers
            var lblLeft  = new Label { Text = "Open Work Orders — select batches to include", Dock = DockStyle.Top, Height = 22, Font = new Font(Font, FontStyle.Bold) };
            var lblRight = new Label { Text = "Aggregated Ingredient Preview", Dock = DockStyle.Top, Height = 22, Font = new Font(Font, FontStyle.Bold) };

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
        }

        private void LoadWorkOrders()
        {
            _dgvWO.Rows.Clear();
            var wos = _repo.GetPendingWorkOrders();
            foreach (var wo in wos)
            {
                int idx = _dgvWO.Rows.Add(false, wo.WorkOrderID, $"MO-{wo.MOID}",
                    wo.ProductName ?? "", wo.Quantity, wo.Status, wo.AssignedTo ?? "");
                _dgvWO.Rows[idx].Tag = wo.WorkOrderID;
            }
            RefreshIngredientPreview();
        }

        private void DgvWO_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == _dgvWO.Columns["colSel"]?.Index)
                RefreshIngredientPreview();
        }

        private List<int> GetSelectedWOIds()
        {
            var ids = new List<int>();
            foreach (DataGridViewRow row in _dgvWO.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells["colSel"].Value is true &&
                    row.Cells["colWOID"].Value is int woId)
                    ids.Add(woId);
            }
            return ids;
        }

        private void RefreshIngredientPreview()
        {
            _dgvIngr.Rows.Clear();
            var woIds = GetSelectedWOIds();
            if (woIds.Count == 0) return;

            // Aggregate BOM quantities across selected work orders
            var summary = new Dictionary<int, (string PartNumber, string PartName, string? UOM, decimal Total, int OnHand)>();
            foreach (int woId in woIds)
            {
                var bom = _repo.GetWOBomPreview(woId);
                foreach (var b in bom)
                {
                    if (summary.TryGetValue(b.PartID, out var existing))
                        summary[b.PartID] = (existing.PartNumber, existing.PartName, existing.UOM, existing.Total + b.RequiredQty, b.OnHand);
                    else
                        summary[b.PartID] = (b.PartNumber, b.PartName, b.UOM, b.RequiredQty, b.OnHand);
                }
            }

            foreach (var (partId, s) in summary.OrderBy(x => x.Value.PartName))
            {
                bool ok = s.OnHand >= (int)Math.Ceiling((double)s.Total);
                int idx = _dgvIngr.Rows.Add(s.PartNumber, s.PartName, s.UOM ?? "", $"{s.Total:N4}", s.OnHand, ok ? "✓" : "✗");
                _dgvIngr.Rows[idx].DefaultCellStyle.ForeColor = ok ? Color.LimeGreen : Color.OrangeRed;
            }
        }

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            var woIds = GetSelectedWOIds();
            if (woIds.Count == 0) { MessageBox.Show(this, "Select at least one work order.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            string name = _txtName.Text.Trim();
            if (string.IsNullOrEmpty(name)) name = $"Cook {DateTime.Now:yyyy-MM-dd HH:mm}";

            try
            {
                string user = Security.AppSession.CurrentUser?.Username ?? "system";
                int sessionId = _repo.CreateCookSession(name, woIds, user);
                _lblStatus.Text = "";

                using var frm = new FormCookSession(sessionId);
                frm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to start session: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnTraveller_Click(object? sender, EventArgs e)
        {
            // Export requires an existing open session — prompt user to start one first
            // or pick from existing open sessions
            var sessions = _repo.GetOpenCookSessions();
            if (sessions.Count == 0)
            {
                MessageBox.Show(this, "No open cook sessions found.\nStart a session first.", "No Sessions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var picker = new FormSessionPicker(sessions, "Export Batch Traveller");
            if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedSessionID == 0) return;

            var rows = _repo.GetBatchTravellerData(picker.SelectedSessionID);
            ExportCsv(rows.Select(r => new[] { r.WONumber, r.PartNumber, r.PartName, r.Nicotine ?? "", r.Size ?? "", r.Qty.ToString(), r.FlaskType ?? "", r.Bins ?? "", r.Concentrate ?? "", r.Notes ?? "" }),
                new[] { "WO#", "Part Number", "Part Description", "Nicotine", "Size", "Qty", "Flask Type", "#Bins", "Concentrate", "Notes" },
                "BatchTraveller");
        }

        private void BtnLabels_Click(object? sender, EventArgs e)
        {
            var sessions = _repo.GetOpenCookSessions();
            if (sessions.Count == 0)
            {
                MessageBox.Show(this, "No open cook sessions found.\nStart a session first.", "No Sessions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var picker = new FormSessionPicker(sessions, "Export Label CSV");
            if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedSessionID == 0) return;

            var rows = _repo.GetLabelExportData(picker.SelectedSessionID);
            ExportCsv(rows.Select(r => new[] { r.BottleType ?? "", r.WONumber, r.PartName, r.PartNumber, r.QtyOrdered.ToString(), r.Version ?? "", r.BatchMadeDate, r.Size ?? "", r.Brand ?? "", r.Nicotine ?? "", r.VG ?? "", r.Note ?? "" }),
                new[] { "Bottle Type", "WO#", "Part Description", "Part Number", "Qty Ordered", "Version", "Batch Made Date", "Size", "Brand", "Nicotine", "VG", "Note" },
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
            MessageBox.Show(this, $"Exported to:\n{dlg.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

            var btnOk = new Button { Text = "Select", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom, Height = 32 };
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
                $"{Session.SessionName} ({Session.CreatedAt:MMM d, yyyy HH:mm})";
        }
    }
}
