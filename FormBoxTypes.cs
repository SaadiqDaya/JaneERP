using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>
    /// CRUD grid for managing BoxTypes (Small Box, Medium Box, Mailer, etc.)
    /// used when packing and shipping orders.
    /// </summary>
    public class FormBoxTypes : Form
    {
        private readonly IBoxTypeRepository _repo =
            AppServices.Get<IBoxTypeRepository>();

        // ── Controls ──────────────────────────────────────────────────────────
        private DataGridView dgvBoxTypes = new();
        private Button       btnAdd      = new();
        private Button       btnSave     = new();
        private Button       btnDelete   = new();
        private Button       btnClose    = new();
        private Label        lblStatus   = new();

        // ── Dirty tracking ────────────────────────────────────────────────────
        /// <summary>BoxTypeIDs that have been edited in the grid.</summary>
        private readonly HashSet<int> _dirtyIds = new();

        public FormBoxTypes()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            LoadGrid();
        }

        // ── UI construction ───────────────────────────────────────────────────

        private void BuildUI()
        {
            Text          = "Box Types";
            ClientSize    = new Size(640, 480);
            MinimumSize   = new Size(520, 400);
            StartPosition = FormStartPosition.CenterParent;

            // Header added by Theme.AddFormHeader after BuildUI returns
            Theme.AddFormHeader(this, "\U0001F4E6  Box Types");

            // ── Sub-label ─────────────────────────────────────────────────────
            var lblSub = new Label
            {
                AutoSize  = false,
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                Location  = new Point(14, 54),
                Size      = new Size(612, 18),
                Text      = "Define box and package types used when packing orders.",
                BackColor = Color.Transparent
            };
            Controls.Add(lblSub);

            // ── Grid ──────────────────────────────────────────────────────────
            dgvBoxTypes.Location           = new Point(14, 78);
            dgvBoxTypes.Size               = new Size(612, 320);
            dgvBoxTypes.Anchor             = AnchorStyles.Top | AnchorStyles.Bottom
                                           | AnchorStyles.Left | AnchorStyles.Right;
            dgvBoxTypes.AllowUserToAddRows    = false;
            dgvBoxTypes.AllowUserToDeleteRows = false;
            dgvBoxTypes.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvBoxTypes.MultiSelect           = false;
            dgvBoxTypes.AutoGenerateColumns   = false;
            dgvBoxTypes.EditMode              = DataGridViewEditMode.EditOnKeystrokeOrF2;
            dgvBoxTypes.RowHeadersWidth       = 24;
            dgvBoxTypes.ColumnHeadersHeight   = 28;

            // Hidden ID column
            dgvBoxTypes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colID",
                DataPropertyName = "BoxTypeID",
                Visible          = false
            });

            // Box Name column
            dgvBoxTypes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colName",
                HeaderText       = "Box Name",
                DataPropertyName = "BoxName",
                Width            = 200,
                SortMode         = DataGridViewColumnSortMode.Automatic
            });

            // Notes column
            dgvBoxTypes.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colNotes",
                HeaderText       = "Notes",
                DataPropertyName = "Notes",
                AutoSizeMode     = DataGridViewAutoSizeColumnMode.Fill,
                SortMode         = DataGridViewColumnSortMode.Automatic
            });

            // Active column
            dgvBoxTypes.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name             = "colActive",
                HeaderText       = "Active",
                DataPropertyName = "IsActive",
                Width            = 60,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dgvBoxTypes.CellValueChanged += DgvBoxTypes_CellValueChanged;
            dgvBoxTypes.CurrentCellDirtyStateChanged += (_, _) =>
            {
                // Commit checkbox edits immediately so CellValueChanged fires
                if (dgvBoxTypes.CurrentCell is DataGridViewCheckBoxCell)
                    dgvBoxTypes.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            Controls.Add(dgvBoxTypes);

            // ── Status label ──────────────────────────────────────────────────
            lblStatus.AutoSize  = false;
            lblStatus.Font      = new Font("Segoe UI", 8.5F, FontStyle.Italic);
            lblStatus.ForeColor = Theme.TextSecondary;
            lblStatus.Location  = new Point(14, 408);
            lblStatus.Size      = new Size(390, 22);
            lblStatus.Anchor    = AnchorStyles.Bottom | AnchorStyles.Left;
            lblStatus.Text      = "";
            Controls.Add(lblStatus);

            // ── Buttons ───────────────────────────────────────────────────────
            int by = 405;
            StyleActionButton(btnAdd, "+ Add",    new Point(414, by));
            StyleActionButton(btnSave,  "Save",   new Point(494, by));
            StyleActionButton(btnDelete, "Delete", new Point(414, by)); // positioned at runtime

            btnAdd.Anchor    = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSave.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnDelete.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            btnAdd.Click    += BtnAdd_Click;
            btnSave.Click   += BtnSave_Click;
            btnDelete.Click += BtnDelete_Click;

            Controls.Add(btnAdd);
            Controls.Add(btnSave);
            Controls.Add(btnDelete);

            // Close button bottom-right
            btnClose.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Size     = new Size(74, 28);
            btnClose.Text     = "Close";
            btnClose.Click   += (_, _) => Close();
            Controls.Add(btnClose);

            PositionButtons();
            SizeChanged += (_, _) => PositionButtons();
        }

        private void PositionButtons()
        {
            int bRight  = ClientSize.Width - 14;
            int bBottom = ClientSize.Height - 12;

            btnClose.Location  = new Point(bRight - btnClose.Width, bBottom - btnClose.Height);
            btnSave.Location   = new Point(btnClose.Left  - btnSave.Width   - 6, btnClose.Top);
            btnAdd.Location    = new Point(btnSave.Left   - btnAdd.Width    - 6, btnClose.Top);
            btnDelete.Location = new Point(btnAdd.Left    - btnDelete.Width - 6, btnClose.Top);

            lblStatus.Location = new Point(14, btnClose.Top + 4);
        }

        private static void StyleActionButton(Button btn, string text, Point location)
        {
            btn.Size      = new Size(74, 28);
            btn.Text      = text;
            btn.Location  = location;
            btn.FlatStyle = FlatStyle.Flat;
            btn.UseVisualStyleBackColor = false;
            btn.Cursor    = Cursors.Hand;
        }

        // ── Data loading ──────────────────────────────────────────────────────

        private void LoadGrid()
        {
            try
            {
                _dirtyIds.Clear();

                // Bind as a BindingList so DataPropertyName mapping works
                var list = new System.ComponentModel.BindingList<BoxType>(
                    _repo.GetBoxTypes(activeOnly: false).ToList());
                dgvBoxTypes.DataSource = list;
                SetStatus($"{list.Count} box type(s) loaded.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load box types:\n\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void DgvBoxTypes_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = dgvBoxTypes.Rows[e.RowIndex];
            int id  = GetRowId(row);
            if (id != 0)
                _dirtyIds.Add(id);
            // new rows (id==0) are always saved in BtnSave_Click (isNew check)
            SetStatus("Unsaved changes — click Save to persist.");
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            // Insert a blank BoxType row into the binding list
            if (dgvBoxTypes.DataSource is System.ComponentModel.BindingList<BoxType> list)
            {
                list.Add(new BoxType { BoxName = "", Notes = "", IsActive = true });
                int lastRow = dgvBoxTypes.Rows.Count - 1;
                dgvBoxTypes.ClearSelection();
                dgvBoxTypes.Rows[lastRow].Selected = true;
                dgvBoxTypes.CurrentCell = dgvBoxTypes.Rows[lastRow].Cells["colName"];
                dgvBoxTypes.BeginEdit(true);
                SetStatus("New row added — enter a name and click Save.");
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            // Commit any in-progress edit first
            dgvBoxTypes.EndEdit();

            if (dgvBoxTypes.DataSource is not System.ComponentModel.BindingList<BoxType> list)
                return;

            int saved   = 0;
            int skipped = 0;
            var errors  = new List<string>();

            for (int i = 0; i < list.Count; i++)
            {
                var bt = list[i];

                // Only persist rows that are new or dirty
                bool isNew   = bt.BoxTypeID == 0;
                bool isDirty = _dirtyIds.Contains(bt.BoxTypeID);
                if (!isNew && !isDirty) continue;

                if (string.IsNullOrWhiteSpace(bt.BoxName))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    var saved_ = _repo.SaveBoxType(bt);
                    list[i].BoxTypeID = saved_.BoxTypeID;   // update ID for new rows
                    saved++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Row '{bt.BoxName}': {ex.Message}");
                }
            }

            _dirtyIds.Clear();

            if (errors.Count > 0)
            {
                MessageBox.Show(this,
                    $"Save completed with errors:\n\n{string.Join("\n", errors)}",
                    "Partial Save", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (saved == 0 && skipped == 0)
            {
                SetStatus("Nothing to save.");
            }
            else
            {
                string msg = $"{saved} row(s) saved.";
                if (skipped > 0) msg += $"  {skipped} blank row(s) skipped.";
                SetStatus(msg);
            }

            // Refresh to get any server-assigned IDs reflected
            LoadGrid();
        }

        private void BtnDelete_Click(object? sender, EventArgs e)
        {
            if (dgvBoxTypes.SelectedRows.Count == 0)
            {
                MessageBox.Show(this, "Select a row to delete.", "Delete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var row  = dgvBoxTypes.SelectedRows[0];
            int id   = GetRowId(row);
            string name = row.Cells["colName"].Value?.ToString() ?? "(blank)";

            if (id == 0)
            {
                // New row not yet saved — just remove from binding list
                if (dgvBoxTypes.DataSource is System.ComponentModel.BindingList<BoxType> list2)
                {
                    var item = list2.FirstOrDefault(b => b.BoxTypeID == 0 && b.BoxName == (row.Cells["colName"].Value?.ToString() ?? ""));
                    if (item != null) list2.Remove(item);
                }
                SetStatus("Unsaved row removed.");
                return;
            }

            if (MessageBox.Show(this,
                    $"Soft-delete '{name}'? It will be marked inactive and hidden from packing forms.",
                    "Confirm Delete",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                _repo.DeleteBoxType(id);
                _dirtyIds.Remove(id);
                LoadGrid();
                SetStatus($"'{name}' deactivated.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Delete failed:\n\n" + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static int GetRowId(DataGridViewRow row)
        {
            var val = row.Cells["colID"].Value;
            return val is int i ? i : 0;
        }

        private void SetStatus(string msg)
        {
            lblStatus.Text = msg;
        }
    }
}
