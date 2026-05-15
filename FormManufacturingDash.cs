using JaneERP.Data;
using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Manufacturing;
using JaneERP.Models;
using JaneERP.Security;

namespace JaneERP
{
    /// <summary>
    /// Manufacturing Dashboard — create and view Manufacturing Orders (MOs).
    /// Each MO contains Work Orders (one per product to build).
    /// </summary>
    public class FormManufacturingDash : Form
    {
        private readonly IManufacturingRepository _moRepo = AppServices.Get<IManufacturingRepository>();
        private readonly IProductRepository       _pRepo  = AppServices.Get<IProductRepository>();

        private DataGridView dgvMOs        = new();
        private DataGridView dgvWOs        = new();
        private Button       btnNewMO      = new();
        private Button       btnPicking    = new();
        private Button       btnClose      = new();
        private Label        lblMOs        = new();
        private Label        lblWOs        = new();

        // Smart filter & badges
        private CheckBox _chkShowCompleted = new() { Text = "Show completed" };
        private Label    _lblOpenCount     = new();
        private Label    _lblDueToday      = new();

        // WO pagination (client-side slicing per selected MO)
        private List<WorkOrder> _currentWOs = [];
        private int  _woPage     = 1;
        private const int WoPageSize = 50;
        private Button _btnWoPrev   = new() { Text = "← Prev" };
        private Button _btnWoNext   = new() { Text = "Next →" };
        private Label  _lblWoPage   = new();

        public FormManufacturingDash()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            LoadOrders();
        }

        private void BuildUI()
        {
            Text            = "Manufacturing";
            ClientSize      = new Size(1000, 620);
            MinimumSize     = new Size(1000, 620);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;

            // ── MO section label ─────────────────────────────────────────────────────
            lblMOs.Text     = "Manufacturing Orders";
            lblMOs.Font     = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblMOs.Location = new Point(12, 60);
            lblMOs.AutoSize = true;
            Controls.Add(lblMOs);

            // "Show completed" filter checkbox
            _chkShowCompleted.Location      = new Point(190, 63);
            _chkShowCompleted.AutoSize      = true;
            _chkShowCompleted.ForeColor     = Theme.TextSecondary;
            _chkShowCompleted.Font          = new Font("Segoe UI", 8.5F);
            _chkShowCompleted.CheckedChanged += (_, _) => { LoadOrders(); };
            Controls.Add(_chkShowCompleted);

            // ── Badge row — open count + due-today ──────────────────────────────────
            _lblOpenCount.Location  = new Point(12, 82);
            _lblOpenCount.AutoSize  = true;
            _lblOpenCount.Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _lblOpenCount.ForeColor = Theme.TextSecondary;
            Controls.Add(_lblOpenCount);

            _lblDueToday.Location  = new Point(200, 82);
            _lblDueToday.AutoSize  = true;
            _lblDueToday.Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _lblDueToday.ForeColor = Color.FromArgb(255, 195, 60); // amber
            _lblDueToday.Visible   = false;
            Controls.Add(_lblDueToday);

            // ── MO grid ─────────────────────────────────────────────────────────────
            dgvMOs.Location          = new Point(12, 102);
            dgvMOs.Size              = new Size(460, 432);
            dgvMOs.Anchor            = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            dgvMOs.ReadOnly          = true;
            dgvMOs.AllowUserToAddRows    = false;
            dgvMOs.AllowUserToDeleteRows = false;
            dgvMOs.SelectionMode     = DataGridViewSelectionMode.FullRowSelect;
            dgvMOs.MultiSelect       = false;
            dgvMOs.AutoGenerateColumns = false;
            dgvMOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cMONum",    HeaderText = "MO #",      DataPropertyName = "MONumber",  Width = 90 });
            dgvMOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cStatus",   HeaderText = "Status",    DataPropertyName = "Status",    Width = 90 });
            dgvMOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cOrdered",  HeaderText = "Ordered By",DataPropertyName = "OrderedBy", Width = 120 });
            dgvMOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCreated",  HeaderText = "Created",   DataPropertyName = "CreatedAt", Width = 130 });
            dgvMOs.SelectionChanged += DgvMOs_SelectionChanged;
            Controls.Add(dgvMOs);

            // ── WO section label ─────────────────────────────────────────────────────
            lblWOs.Text     = "Work Orders (select an MO)";
            lblWOs.Font     = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblWOs.Location = new Point(490, 60);
            lblWOs.AutoSize = true;
            Controls.Add(lblWOs);

            // ── WO grid ─────────────────────────────────────────────────────────────
            dgvWOs.Location          = new Point(490, 102);
            dgvWOs.Size              = new Size(496, 412);
            dgvWOs.Anchor            = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvWOs.ReadOnly          = true;
            dgvWOs.AllowUserToAddRows    = false;
            dgvWOs.AllowUserToDeleteRows = false;
            dgvWOs.SelectionMode     = DataGridViewSelectionMode.FullRowSelect;
            dgvWOs.MultiSelect       = false;
            dgvWOs.AutoGenerateColumns = false;
            dgvWOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cProduct", HeaderText = "Product",    DataPropertyName = "ProductName", Width = 200 });
            dgvWOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSKU",     HeaderText = "SKU",        DataPropertyName = "SKU",         Width = 100 });
            dgvWOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cQty",     HeaderText = "Qty",        DataPropertyName = "Quantity",    Width = 50  });
            dgvWOs.Columns.Add(new DataGridViewTextBoxColumn { Name = "cWOStatus",HeaderText = "Status",     DataPropertyName = "Status",      Width = 90  });
            Controls.Add(dgvWOs);

            // ── WO pagination bar (below WO grid) ────────────────────────────────────
            _btnWoPrev.Size    = new Size(68, 26);
            _btnWoPrev.Anchor  = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnWoPrev.Enabled = false;
            _btnWoPrev.Click  += (_, _) => { _woPage--; ApplyWoPage(); };

            _btnWoNext.Size    = new Size(68, 26);
            _btnWoNext.Anchor  = AnchorStyles.Bottom | AnchorStyles.Left;
            _btnWoNext.Enabled = false;
            _btnWoNext.Click  += (_, _) => { _woPage++; ApplyWoPage(); };

            _lblWoPage.AutoSize  = true;
            _lblWoPage.Font      = new Font("Segoe UI", 8F);
            _lblWoPage.ForeColor = Theme.TextSecondary;
            _lblWoPage.Anchor    = AnchorStyles.Bottom | AnchorStyles.Left;

            // Positions are set in OnResize / via Anchor; lay them out relative to dgvWOs bottom
            // We'll place them in a small panel docked to the correct area via absolute coords after layout
            void PlaceWoPagination()
            {
                int px = dgvWOs.Left;
                int py = dgvWOs.Bottom + 4;
                _btnWoPrev.Location  = new Point(px,       py);
                _lblWoPage.Location  = new Point(px + 74,  py + 4);
                _btnWoNext.Location  = new Point(px + 240, py);
            }
            Load += (_, _) => PlaceWoPagination();
            Resize += (_, _) => PlaceWoPagination();

            Controls.Add(_btnWoPrev);
            Controls.Add(_lblWoPage);
            Controls.Add(_btnWoNext);

            // ── Bottom action buttons ─────────────────────────────────────────────────
            btnNewMO.Text     = "+ New Manufacturing Order";
            btnNewMO.Location = new Point(12, 564);
            btnNewMO.Size     = new Size(200, 32);
            btnNewMO.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnNewMO.Click   += BtnNewMO_Click;
            Controls.Add(btnNewMO);

            var btnProcess = new Button
            {
                Text     = "Process Work Orders…",
                Location = new Point(220, 564),
                Size     = new Size(180, 32),
                Anchor   = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnProcess.Click += (_, _) =>
            {
                using var frm = new FormWorkOrders();
                frm.ShowDialog(this);
                LoadOrders();
            };
            Controls.Add(btnProcess);

            btnPicking.Text     = "→ Picking Dashboard";
            btnPicking.Location = new Point(408, 564);
            btnPicking.Size     = new Size(160, 32);
            btnPicking.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
            btnPicking.Click   += (_, _) =>
            {
                using var frm = new FormPickingDash();
                frm.ShowDialog(this);
            };
            Controls.Add(btnPicking);

            btnClose.Text     = "Close";
            btnClose.Location = new Point(896, 564);
            btnClose.Size     = new Size(90, 32);
            btnClose.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Click   += (_, _) => Close();
            Controls.Add(btnClose);

            Theme.AddFormHeader(this, "🏭  Manufacturing");
        }

        private void LoadOrders()
        {
            try
            {
                bool openOnly = !_chkShowCompleted.Checked;
                var orders = _moRepo.GetOrders(openOnly);
                dgvMOs.DataSource = orders;
                dgvWOs.DataSource = null;
                lblWOs.Text       = "Work Orders (select an MO)";
                _currentWOs.Clear();
                _woPage = 1;
                UpdateWoPaginationBar();
                UpdateBadges(orders);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load orders: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Updates the open-count and in-progress badges above the MO grid.</summary>
        private void UpdateBadges(List<ManufacturingOrder> orders)
        {
            var allWOs = orders.SelectMany(mo => mo.WorkOrders).ToList();

            // Count WOs that are open / pending (not yet started)
            int openCount = allWOs.Count(wo =>
                wo.Status is "Open" or "Pending" or "In Progress" or "InProgress");

            _lblOpenCount.Text = $"{openCount} open work order{(openCount == 1 ? "" : "s")}";

            // Count MOs that are still in-progress (not complete)
            int inProgressMOs = orders.Count(mo =>
                mo.Status is "Open" or "InProgress" or "In Progress");

            if (inProgressMOs > 0 && inProgressMOs != orders.Count)
            {
                _lblDueToday.Text    = $"{inProgressMOs} MO{(inProgressMOs == 1 ? "" : "s")} in progress";
                _lblDueToday.Visible = true;
            }
            else
            {
                _lblDueToday.Visible = false;
            }
        }

        private void DgvMOs_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvMOs.SelectedRows.Count == 0)
            {
                dgvWOs.DataSource = null;
                _currentWOs.Clear();
                _woPage = 1;
                UpdateWoPaginationBar();
                lblWOs.Text = "Work Orders (select an MO)";
                return;
            }
            if (dgvMOs.SelectedRows[0].DataBoundItem is not ManufacturingOrder mo) return;

            _currentWOs = mo.WorkOrders ?? [];
            _woPage     = 1;
            ApplyWoPage();
            lblWOs.Text = $"Work Orders — {mo.MONumber}";
        }

        /// <summary>Slices _currentWOs for the current page and binds to dgvWOs.</summary>
        private void ApplyWoPage()
        {
            int total = _currentWOs.Count;
            int pages = Math.Max(1, (int)Math.Ceiling(total / (double)WoPageSize));
            _woPage   = Math.Clamp(_woPage, 1, pages);

            var slice = _currentWOs
                .Skip((_woPage - 1) * WoPageSize)
                .Take(WoPageSize)
                .ToList();

            dgvWOs.DataSource = slice;
            UpdateWoPaginationBar();
        }

        private void UpdateWoPaginationBar()
        {
            int total = _currentWOs.Count;
            int pages = Math.Max(1, (int)Math.Ceiling(total / (double)WoPageSize));

            if (total <= WoPageSize)
            {
                _lblWoPage.Text    = total > 0 ? $"{total} work order{(total == 1 ? "" : "s")}" : "";
                _btnWoPrev.Enabled = false;
                _btnWoNext.Enabled = false;
                return;
            }

            int from = (_woPage - 1) * WoPageSize + 1;
            int to   = Math.Min(_woPage * WoPageSize, total);
            _lblWoPage.Text    = $"Showing {from}–{to} of {total} work orders";
            _btnWoPrev.Enabled = _woPage > 1;
            _btnWoNext.Enabled = _woPage < pages;
        }

        private void BtnNewMO_Click(object? sender, EventArgs e)
        {
            using var frm = new FormNewMO(_pRepo, _moRepo);
            if (frm.ShowDialog(this) == DialogResult.OK)
                LoadOrders();
        }
    }

    // ── Inline "New MO" dialog ────────────────────────────────────────────────────

    internal class FormNewMO : Form
    {
        private readonly IProductRepository       _pRepo;
        private readonly IManufacturingRepository _moRepo;

        private TextBox      txtNotes     = new();
        private DataGridView dgvLines     = new();
        private Button       btnSave      = new();
        private Button       btnCancel    = new();
        private Button       btnAddLine   = new();

        public FormNewMO(IProductRepository pRepo, IManufacturingRepository moRepo)
        {
            _pRepo  = pRepo;
            _moRepo = moRepo;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
        }

        private void BuildUI()
        {
            Text            = "New Manufacturing Order";
            ClientSize      = new Size(640, 500);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;

            int y = 14;
            var lbl = new Label { Text = "New Manufacturing Order", Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                AutoSize = true, Location = new Point(16, y), ForeColor = Theme.Gold };
            Controls.Add(lbl);
            y += 36;

            Controls.Add(new Label { Text = "Notes:", AutoSize = true, Location = new Point(16, y) });
            y += 20;
            txtNotes.Location   = new Point(16, y);
            txtNotes.Size       = new Size(600, 50);
            txtNotes.Multiline  = true;
            Controls.Add(txtNotes);
            y += 58;

            Controls.Add(new Label { Text = "Products to Build:", Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                AutoSize = true, Location = new Point(16, y) });
            y += 22;

            dgvLines.Location          = new Point(16, y);
            dgvLines.Size              = new Size(600, 250);
            dgvLines.AllowUserToAddRows    = false;
            dgvLines.AllowUserToDeleteRows = true;
            dgvLines.SelectionMode     = DataGridViewSelectionMode.FullRowSelect;
            dgvLines.AutoGenerateColumns = false;
            dgvLines.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProductID",   Visible      = false  });
            dgvLines.Columns.Add(new DataGridViewTextBoxColumn { Name = "colProductName", HeaderText  = "Product",  Width = 330 });
            dgvLines.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSKU",         HeaderText  = "SKU",      Width = 120 });
            dgvLines.Columns.Add(new DataGridViewTextBoxColumn { Name = "colQty",         HeaderText  = "Quantity", Width = 80  });
            Controls.Add(dgvLines);
            y += 258;

            btnAddLine.Text     = "+ Add Product";
            btnAddLine.Location = new Point(16, y);
            btnAddLine.Size     = new Size(130, 30);
            btnAddLine.Click   += BtnAddLine_Click;
            Controls.Add(btnAddLine);

            btnSave.Text     = "Create MO";
            btnSave.Location = new Point(420, y);
            btnSave.Size     = new Size(100, 30);
            btnSave.Click   += BtnSave_Click;
            Controls.Add(btnSave);

            btnCancel.Text     = "Cancel";
            btnCancel.Location = new Point(528, y);
            btnCancel.Size     = new Size(88, 30);
            btnCancel.Click   += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);
        }

        private void BtnAddLine_Click(object? sender, EventArgs e)
        {
            using var picker = new FormProductPicker(_pRepo);
            if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedProduct == null) return;

            var p   = picker.SelectedProduct;
            var bom = AppServices.Get<IPartRepository>().GetBom(p.ProductID);
            if (bom.Count == 0)
            {
                MessageBox.Show(this,
                    $"'{p.ProductName}' has no Bill of Materials defined.\n\n" +
                    "Manufacturing can only be created for products with a BOM.\n" +
                    "Go to Parts → Edit BOM to set it up first.",
                    "No BOM Defined", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            dgvLines.Rows.Add(p.ProductID, p.ProductName, p.SKU, 1);
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            var workOrders = new List<WorkOrder>();
            foreach (DataGridViewRow row in dgvLines.Rows)
            {
                if (row.IsNewRow) continue;
                if (!int.TryParse(row.Cells["colQty"].Value?.ToString(), out int qty) || qty <= 0) continue;
                if (!int.TryParse(row.Cells["colProductID"].Value?.ToString(), out int pid)) continue;
                workOrders.Add(new WorkOrder { ProductID = pid, Quantity = qty });
            }

            if (workOrders.Count == 0)
            {
                MessageBox.Show(this, "Add at least one product line.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var mo = new ManufacturingOrder
                {
                    Notes     = txtNotes.Text.Trim(),
                    OrderedBy = AppSession.CurrentUser?.Username,
                    WorkOrders = workOrders
                };
                _moRepo.CreateOrder(mo);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not create MO: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // ── Simple product picker dialog ──────────────────────────────────────────────

    internal class FormProductPicker : Form
    {
        private readonly IProductRepository _repo;
        private DataGridView dgv = new();
        public Product? SelectedProduct { get; private set; }

        public FormProductPicker(IProductRepository repo)
        {
            _repo = repo;
            Text            = "Select Product";
            ClientSize      = new Size(540, 400);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;

            dgv.Location          = new Point(12, 12);
            dgv.Size              = new Size(516, 330);
            dgv.ReadOnly          = true;
            dgv.AllowUserToAddRows    = false;
            dgv.SelectionMode     = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect       = false;
            dgv.AutoGenerateColumns = true;
            Controls.Add(dgv);

            var btnOk = new Button { Text = "Select", Location = new Point(330, 355), Size = new Size(90, 30) };
            btnOk.Click += (_, _) =>
            {
                if (dgv.SelectedRows.Count > 0 && dgv.SelectedRows[0].DataBoundItem is Product p)
                {
                    SelectedProduct = p;
                    DialogResult    = DialogResult.OK;
                    Close();
                }
            };
            var btnCancel = new Button { Text = "Cancel", Location = new Point(428, 355), Size = new Size(90, 30) };
            btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            Theme.Apply(this);
            Theme.MakeBorderless(this);

            dgv.DataSource = _repo.GetProducts().ToList();
        }
    }
}
