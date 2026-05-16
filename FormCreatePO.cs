using System.IO;
using JaneERP.Data;
using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>Dialog for creating a new Purchase Order, editing a Draft, or viewing an existing one.</summary>
    public class FormCreatePO : Form
    {
        private readonly SupplierRepository    _repo;
        private readonly PurchaseOrder?        _viewOnly;
        private readonly bool                  _editDraft;   // true = existing Draft being edited
        private readonly IPartRepository       _partRepo      = AppServices.Get<IPartRepository>();
        private readonly IProductRepository    _productRepo   = AppServices.Get<IProductRepository>();
        private readonly IAccountingRepository _accountingRepo = AppServices.Get<IAccountingRepository>();

        // Supplier dropdown
        private ComboBox  cboSupplier    = new();
        private Button    btnNewSupplier = new();

        private DateTimePicker  dtpExpected   = new();
        private CheckBox        chkNoExpected = new();
        private TextBox         txtNotes      = new();
        private TextBox         txtShipping   = new();
        private ComboBox        cboTaxRate    = new();   // preset tax rate selector
        private TextBox         txtTax        = new();
        private DataGridView    dgvItems      = new();
        private Label           lblSubtotal   = new();
        private Label           lblTotal      = new();
        private Button          btnAddLine    = new();
        private Button          btnRemoveLine = new();
        private Button          btnSave       = new();
        private Button          btnCancel     = new();

        private List<Supplier>          _suppliers = new();
        private List<Part>              _parts     = new();
        private List<Product>           _products  = new();
        private List<PurchaseOrderItem> _items     = new();

        /// <param name="prePopulateItems">Optional items to pre-load (e.g. from the Reorder Report).</param>
        /// <param name="preselectedSupplierName">Supplier name to auto-select (fuzzy match on load).</param>
        /// <param name="editDraft">When true, the PO is an existing Draft opened for editing (not read-only).</param>
        public FormCreatePO(SupplierRepository repo, PurchaseOrder? po = null,
                            IEnumerable<PurchaseOrderItem>? prePopulateItems = null,
                            string? preselectedSupplierName = null,
                            bool editDraft = false)
        {
            _repo      = repo;
            _viewOnly  = po;
            _editDraft = editDraft;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
            LoadData();
            if (_viewOnly != null)
                PopulateView();
            else if (prePopulateItems != null)
            {
                _items.AddRange(prePopulateItems);
                RefreshGrid();
            }
            // Auto-select supplier by name if provided
            if (!string.IsNullOrEmpty(preselectedSupplierName))
            {
                for (int i = 0; i < cboSupplier.Items.Count; i++)
                {
                    if (cboSupplier.Items[i]?.ToString()?.Contains(preselectedSupplierName,
                        StringComparison.OrdinalIgnoreCase) == true)
                    {
                        cboSupplier.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void BuildUI()
        {
            // readOnly = viewing a non-draft PO; draft edits and new POs are both editable
            bool readOnly = _viewOnly != null && !_editDraft;
            Text          = _editDraft ? $"Edit Draft PO: {_viewOnly!.PONumber}"
                          : readOnly   ? $"View Purchase Order: {_viewOnly!.PONumber}"
                          :              "New Purchase Order";
            ClientSize    = new Size(860, 640);
            MinimumSize   = new Size(760, 560);
            StartPosition = FormStartPosition.CenterParent;

            int y = 64, x = 12, cx = x + 120;

            // ── Supplier dropdown ─────────────────────────────────────────────────
            AddLabel("Supplier:", x, y);
            cboSupplier.Location      = new Point(cx, y - 3);
            cboSupplier.Size          = new Size(340, 24);
            cboSupplier.DropDownStyle = ComboBoxStyle.DropDownList;
            cboSupplier.Enabled       = !readOnly;
            cboSupplier.SelectedIndexChanged += (_, _) =>
            {
                if (cboSupplier.SelectedItem is Supplier s) _selectedSupplierFromCbo = s;
            };
            Controls.Add(cboSupplier);

            if (!readOnly)
            {
                btnNewSupplier.Text     = "+ New Supplier";
                btnNewSupplier.Location = new Point(cx + 348, y - 4);
                btnNewSupplier.Size     = new Size(110, 26);
                btnNewSupplier.UseVisualStyleBackColor = true;
                btnNewSupplier.Click   += BtnNewSupplier_Click;
                Controls.Add(btnNewSupplier);
            }
            y += 32;

            // ── Expected Date ─────────────────────────────────────────────────────
            AddLabel("Expected Date:", x, y);
            dtpExpected.Location = new Point(cx, y - 3);
            dtpExpected.Size     = new Size(180, 24);
            dtpExpected.Format   = DateTimePickerFormat.Short;
            dtpExpected.Enabled  = !readOnly;
            Controls.Add(dtpExpected);

            chkNoExpected.Text     = "No date";
            chkNoExpected.Location = new Point(cx + 190, y - 1);
            chkNoExpected.AutoSize = true;
            chkNoExpected.Enabled  = !readOnly;
            chkNoExpected.CheckedChanged += (_, _) => dtpExpected.Enabled = !chkNoExpected.Checked && !readOnly;
            Controls.Add(chkNoExpected);
            y += 32;

            // ── Notes ─────────────────────────────────────────────────────────────
            AddLabel("Notes:", x, y);
            txtNotes.Location   = new Point(cx, y - 3);
            txtNotes.Size       = new Size(700, 58);
            txtNotes.Multiline  = true;
            txtNotes.ScrollBars = ScrollBars.Vertical;
            txtNotes.ReadOnly   = readOnly;
            Controls.Add(txtNotes);
            y += 70;

            // ── Line items header ─────────────────────────────────────────────────
            Controls.Add(new Label
            {
                Text      = "Line Items",
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Teal,
                AutoSize  = true,
                Location  = new Point(x, y)
            });

            if (!readOnly)
            {
                btnAddLine.Text     = "+ Add Item";
                btnAddLine.Location = new Point(x + 120, y - 2);
                btnAddLine.Size     = new Size(100, 26);
                btnAddLine.UseVisualStyleBackColor = true;
                btnAddLine.Click   += BtnAddLine_Click;
                Controls.Add(btnAddLine);

                btnRemoveLine.Text     = "Remove";
                btnRemoveLine.Location = new Point(x + 228, y - 2);
                btnRemoveLine.Size     = new Size(80, 26);
                btnRemoveLine.UseVisualStyleBackColor = true;
                btnRemoveLine.Click   += BtnRemoveLine_Click;
                Controls.Add(btnRemoveLine);
            }
            y += 30;

            // ── DataGridView (no DataSource binding — rows populated manually) ────
            dgvItems.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right; // Bottom handled manually
            dgvItems.Location = new Point(x, y);
            dgvItems.Size     = new Size(830, 350);
            dgvItems.AllowUserToAddRows    = false;
            dgvItems.AllowUserToDeleteRows = false;
            dgvItems.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvItems.MultiSelect           = false;
            dgvItems.AutoGenerateColumns   = false;
            dgvItems.RowHeadersVisible     = false;
            dgvItems.CellEndEdit          += (_, _) => RecalcTotal();

            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cType",     HeaderText = "Type",         Width = 72,  ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSKU",      HeaderText = "SKU / Part #", Width = 110, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",     HeaderText = "Item Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cQty",      HeaderText = "Qty",          Width = 55,  ReadOnly = readOnly });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCost",     HeaderText = "Unit Cost",    Width = 90,  ReadOnly = readOnly });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cLineTotal",HeaderText = "Line Total",   Width = 100, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "cReceived", HeaderText = "Received",     Width = 70,  ReadOnly = true, Visible = readOnly });

            dgvItems.DataError += (_, e) => e.Cancel = true;
            Controls.Add(dgvItems);

            // ── Footer: subtotal / shipping / tax / total ─────────────────────────
            lblSubtotal.AutoSize  = false;
            lblSubtotal.Size      = new Size(170, 22);
            lblSubtotal.Font      = new Font("Segoe UI", 9F);
            lblSubtotal.ForeColor = Theme.TextSecondary;
            Controls.Add(lblSubtotal);

            Controls.Add(new Label { Name = "_lblShipLbl", Text = "Shipping ($):", AutoSize = true, ForeColor = Theme.TextSecondary });
            txtShipping.Text          = "0.00";
            txtShipping.Enabled       = !readOnly;
            txtShipping.Size          = new Size(80, 23);
            txtShipping.TextChanged  += (_, _) => UpdateTotal();
            txtShipping.Leave        += TxtShipping_Leave;
            Controls.Add(txtShipping);

            // Tax rate preset selector — picks a named rate and auto-calculates the tax amount
            cboTaxRate.DropDownStyle = ComboBoxStyle.DropDownList;
            cboTaxRate.Size          = new Size(120, 23);
            cboTaxRate.Enabled       = !readOnly;
            cboTaxRate.Items.Add("Custom tax ($)");
            cboTaxRate.SelectedIndex = 0;
            cboTaxRate.SelectedIndexChanged += CboTaxRate_Changed;
            Controls.Add(cboTaxRate);

            txtTax.Text               = "0.00";
            txtTax.Enabled            = !readOnly;
            txtTax.Size               = new Size(80, 23);
            txtTax.TextChanged       += (_, _) => UpdateTotal();
            txtTax.Leave             += TxtTax_Leave;
            Controls.Add(txtTax);

            lblTotal.AutoSize  = false;
            lblTotal.Size      = new Size(200, 24);
            lblTotal.Font      = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblTotal.ForeColor = Theme.Gold;
            lblTotal.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(lblTotal);

            // ── Action buttons ────────────────────────────────────────────────────
            if (!readOnly)
            {
                btnSave.Text   = _editDraft ? "Save Changes" : "Save PO";
                btnSave.Size   = new Size(110, 30);
                btnSave.UseVisualStyleBackColor = true;
                btnSave.Click += BtnSave_Click;
                Controls.Add(btnSave);
            }

            if (readOnly)
            {
                var btnExportCsv = new Button { Text = "Export CSV", Size = new Size(110, 30), UseVisualStyleBackColor = true };
                btnExportCsv.Click += (_, _) => ExportPOtoCsv();
                Theme.StyleSecondaryButton(btnExportCsv);
                Controls.Add(btnExportCsv);

                var btnPrint = new Button { Text = "Print / PDF", Size = new Size(110, 30), UseVisualStyleBackColor = true };
                btnPrint.Click += (_, _) => FormReports.PrintGrid(dgvItems, $"PO: {_viewOnly?.PONumber}", this);
                Theme.StyleSecondaryButton(btnPrint);
                Controls.Add(btnPrint);

                // Position export buttons — will be laid out in LayoutSummaryRow
                Load += (_, _) =>
                {
                    var right = ClientSize.Width - 12;
                    btnPrint.Location    = new Point(right - btnPrint.Width, ClientSize.Height - 44);
                    btnExportCsv.Location = new Point(btnPrint.Left - btnExportCsv.Width - 8, ClientSize.Height - 44);
                };
            }

            btnCancel.Text   = readOnly ? "Close" : "Cancel";
            btnCancel.Size   = new Size(80, 30);
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += (_, _) => Close();
            Controls.Add(btnCancel);

            SizeChanged += (_, _) => PositionBottomControls();
            Load        += (_, _) => PositionBottomControls();

            Theme.AddFormHeader(this, "🛒  Purchase Order");
        }

        // Tracks supplier selected via the ComboBox
        private Supplier? _selectedSupplierFromCbo;

        private void PositionBottomControls()
        {
            int bottom = ClientSize.Height - 8;
            int right  = ClientSize.Width  - 12;

            // Action buttons row
            btnCancel.Location = new Point(right - btnCancel.Width, bottom - btnCancel.Height);
            if (btnSave.Parent != null)
                btnSave.Location = new Point(btnCancel.Left - btnSave.Width - 8, bottom - btnSave.Height);

            // Summary row (above buttons)
            int summaryTop = btnCancel.Top - 32;

            lblSubtotal.Location = new Point(12, summaryTop + 3);

            // Position shipping label + textbox
            var shipLbl = Controls.Find("_lblShipLbl", false).FirstOrDefault();
            if (shipLbl != null) shipLbl.Location = new Point(190, summaryTop + 5);
            txtShipping.Location = new Point(262, summaryTop);

            // Tax rate ComboBox + amount textbox
            cboTaxRate.Location = new Point(354, summaryTop);
            txtTax.Location     = new Point(480, summaryTop);

            lblTotal.Location = new Point(right - lblTotal.Width, summaryTop + 3);

            // Resize DGV to fill space between its top and the summary row
            int dgvBottom = summaryTop - 6;
            int newH      = Math.Max(80, dgvBottom - dgvItems.Top);
            if (dgvItems.Height != newH)   dgvItems.Height = newH;
            if (dgvItems.Width  != right - dgvItems.Left + 4)
                dgvItems.Width = right - dgvItems.Left + 4;
        }

        private void LoadData()
        {
            _suppliers = _repo.GetAllSuppliers(includeInactive: false);
            _parts     = _partRepo.GetAll(includeInactive: false);
            _products  = _productRepo.GetProducts().ToList();
            PopulateSupplierCombo();
            LoadTaxRates();
        }

        private void LoadTaxRates()
        {
            try
            {
                var rates = _accountingRepo.GetActiveTaxRates();
                cboTaxRate.Items.Clear();
                cboTaxRate.Items.Add("Custom tax ($)");
                foreach (var r in rates)
                    cboTaxRate.Items.Add(r);
                cboTaxRate.SelectedIndex = 0;
                cboTaxRate.DisplayMember = "";   // uses ToString()
            }
            catch { /* non-fatal — table may not exist yet on first run */ }
        }

        private void CboTaxRate_Changed(object? sender, EventArgs e)
        {
            if (cboTaxRate.SelectedItem is not TaxRate rate) return;
            // Auto-calculate tax from subtotal × rate
            decimal subtotal = _items.Sum(i => i.QuantityOrdered * i.UnitCost);
            txtTax.Text = (subtotal * rate.Rate).ToString("F2");
        }

        private static string StripCurrencyChars(string text) =>
            text.Replace("$", "").Replace("R", "").Replace(",", "").Trim();

        private void TxtShipping_Leave(object? sender, EventArgs e)
        {
            if (decimal.TryParse(StripCurrencyChars(txtShipping.Text),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
                txtShipping.Text = v.ToString("F2");
            else
                txtShipping.Text = "0.00";
            UpdateTotal();
        }

        private void TxtTax_Leave(object? sender, EventArgs e)
        {
            if (decimal.TryParse(StripCurrencyChars(txtTax.Text),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
                txtTax.Text = v.ToString("F2");
            else
                txtTax.Text = "0.00";
            UpdateTotal();
        }

        private void PopulateSupplierCombo()
        {
            cboSupplier.Items.Clear();
            foreach (var s in _suppliers)
                cboSupplier.Items.Add(s);
            // Use supplier name as display text
            cboSupplier.DisplayMember = "SupplierName";
        }

        private void PopulateView()
        {
            var po  = _viewOnly!;
            var sup = _suppliers.FirstOrDefault(s => s.SupplierID == po.SupplierID);
            if (sup != null)
            {
                _selectedSupplierFromCbo = sup;
                cboSupplier.SelectedItem = sup;
            }
            else if (po.SupplierName != null)
            {
                // Supplier not in active list — show name as a read-only text box hint
                cboSupplier.Items.Insert(0, new Supplier { SupplierName = po.SupplierName });
                cboSupplier.SelectedIndex = 0;
            }

            if (po.ExpectedDate.HasValue)
            {
                dtpExpected.Value     = po.ExpectedDate.Value;
                chkNoExpected.Checked = false;
            }
            else
            {
                chkNoExpected.Checked = true;
            }

            txtNotes.Text     = po.Notes ?? "";
            txtShipping.Text = po.ShippingCost.ToString("F2");
            txtTax.Text      = po.TaxAmount.ToString("F2");
            _items            = po.Items.ToList();
            RefreshGrid();
        }

        private void BtnNewSupplier_Click(object? sender, EventArgs e)
        {
            var oldIds = new HashSet<int>(_suppliers.Select(s => s.SupplierID));
            using var frm = new FormSupplierManager(_repo);
            frm.ShowDialog(this);
            _suppliers = _repo.GetAllSuppliers(includeInactive: false);
            PopulateSupplierCombo();
            // Auto-select the newly added supplier
            var newest = _suppliers.Where(s => !oldIds.Contains(s.SupplierID)).MaxBy(s => s.SupplierID);
            if (newest != null)
            {
                cboSupplier.SelectedItem = newest;
                _selectedSupplierFromCbo = newest;
            }
        }

        private void BtnAddLine_Click(object? sender, EventArgs e)
        {
            using var picker = new FormPOItemPicker(_parts, _products);
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            foreach (var item in picker.SelectedItems) _items.Add(item);
            _parts    = picker.Parts;
            _products = picker.Products;
            RefreshGrid();
        }

        private void BtnRemoveLine_Click(object? sender, EventArgs e)
        {
            if (dgvItems.CurrentRow == null || dgvItems.CurrentRow.Index < 0 ||
                dgvItems.CurrentRow.Index >= _items.Count) return;
            _items.RemoveAt(dgvItems.CurrentRow.Index);
            RefreshGrid();
        }

        /// <summary>Populates the grid from <see cref="_items"/> without using DataSource binding,
        /// so all columns (including Line Total) are set explicitly.</summary>
        private void RefreshGrid()
        {
            dgvItems.Rows.Clear();
            foreach (var item in _items)
            {
                int r = dgvItems.Rows.Add();
                var row = dgvItems.Rows[r];
                row.Cells["cType"].Value      = item.PartID.HasValue ? "Part" : "Product";
                row.Cells["cSKU"].Value       = item.SKU ?? "";
                row.Cells["cName"].Value      = item.ItemName;
                row.Cells["cQty"].Value       = item.QuantityOrdered.ToString();
                row.Cells["cCost"].Value      = item.UnitCost.ToString("F2");
                row.Cells["cLineTotal"].Value = $"${item.UnitCost * item.QuantityOrdered:N2} CAD";
                row.Cells["cReceived"].Value  = item.QuantityReceived.ToString();
            }
            UpdateTotal();
        }

        private void RecalcTotal()
        {
            SyncGridToItems();
            // Refresh line total column for each row
            for (int r = 0; r < dgvItems.Rows.Count && r < _items.Count; r++)
            {
                var item = _items[r];
                dgvItems.Rows[r].Cells["cLineTotal"].Value = $"${item.UnitCost * item.QuantityOrdered:N2} CAD";
            }
            // If a preset tax rate is active, re-apply it so tax tracks the new subtotal
            if (cboTaxRate.SelectedItem is TaxRate rate)
            {
                decimal subtotal = _items.Sum(i => i.QuantityOrdered * i.UnitCost);
                txtTax.Text = (subtotal * rate.Rate).ToString("F2");
            }
            UpdateTotal();
        }

        private void UpdateTotal()
        {
            decimal subtotal = _items.Sum(i => i.UnitCost * i.QuantityOrdered);
            lblSubtotal.Text = $"Subtotal: ${subtotal:N2}";
            decimal.TryParse(txtShipping.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var shipAmt);
            decimal.TryParse(txtTax.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var taxAmt);
            decimal total = subtotal + shipAmt + taxAmt;
            lblTotal.Text = $"Total: ${total:N2} CAD";
        }

        private void SyncGridToItems()
        {
            for (int r = 0; r < dgvItems.Rows.Count && r < _items.Count; r++)
            {
                var row  = dgvItems.Rows[r];
                var item = _items[r];
                if (int.TryParse(row.Cells["cQty"].Value?.ToString(),      out int qty))       item.QuantityOrdered = qty;
                if (decimal.TryParse(row.Cells["cCost"].Value?.ToString(), out decimal cost))   item.UnitCost        = cost;
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            SyncGridToItems();

            var supplier = _selectedSupplierFromCbo
                           ?? cboSupplier.SelectedItem as Supplier;

            if (supplier == null)
            {
                MessageBox.Show(this, "Please select a supplier from the dropdown.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_items.Count == 0)
            {
                MessageBox.Show(this, "Add at least one line item.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_items.Any(i => i.QuantityOrdered <= 0))
            {
                MessageBox.Show(this, "All quantities must be greater than 0.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal.TryParse(StripCurrencyChars(txtShipping.Text), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var sc);
            decimal.TryParse(StripCurrencyChars(txtTax.Text), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var ta);

            try
            {
                if (_editDraft && _viewOnly != null)
                {
                    // Update the existing Draft PO in-place
                    var updated = new PurchaseOrder
                    {
                        POID         = _viewOnly.POID,
                        PONumber     = _viewOnly.PONumber,
                        SupplierID   = supplier.SupplierID,
                        Status       = "Draft",
                        OrderDate    = _viewOnly.OrderDate,
                        ExpectedDate = chkNoExpected.Checked ? (DateTime?)null : dtpExpected.Value.Date,
                        Notes        = string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim(),
                        ShippingCost = sc,
                        TaxAmount    = ta,
                        Items        = _items
                    };
                    _repo.UpdateDraftOrder(_viewOnly.POID, updated);
                }
                else
                {
                    // Create brand-new PO
                    var po = new PurchaseOrder
                    {
                        SupplierID   = supplier.SupplierID,
                        Status       = "Draft",
                        OrderDate    = DateTime.Now,
                        ExpectedDate = chkNoExpected.Checked ? (DateTime?)null : dtpExpected.Value.Date,
                        Notes        = string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim(),
                        ShippingCost = sc,
                        TaxAmount    = ta,
                        Items        = _items
                    };
                    _repo.CreateOrder(po);
                }
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to save PO:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportPOtoCsv()
        {
            var po = _viewOnly;
            if (po == null) return;
            using var dlg = new SaveFileDialog
            {
                Filter    = "CSV files (*.csv)|*.csv",
                FileName  = $"PO_{po.PONumber}_{DateTime.Now:yyyyMMdd}.csv"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Purchase Order:,{po.PONumber}");
                sb.AppendLine($"Supplier:,{po.SupplierName}");
                sb.AppendLine($"Date:,{po.OrderDate:yyyy-MM-dd}");
                sb.AppendLine($"Status:,{po.Status}");
                if (po.ShippingCost != 0) sb.AppendLine($"Shipping:,{po.ShippingCost:F2}");
                if (po.TaxAmount    != 0) sb.AppendLine($"Tax:,{po.TaxAmount:F2}");
                sb.AppendLine();
                sb.AppendLine("Type,SKU,Item Name,Qty Ordered,Qty Received,Unit Cost,Line Total");
                foreach (var item in po.Items)
                    sb.AppendLine($"\"{item.ItemType}\",\"{item.SKU}\",\"{item.ItemName}\",{item.QuantityOrdered},{item.QuantityReceived},{item.UnitCost:F2},{item.QuantityOrdered * item.UnitCost:F2}");
                sb.AppendLine();
                decimal subtotal = po.Items.Sum(i => i.QuantityOrdered * i.UnitCost);
                sb.AppendLine($"Subtotal:,{subtotal:F2}");
                sb.AppendLine($"Total:,{subtotal + po.ShippingCost + po.TaxAmount:F2}");
                File.WriteAllText(dlg.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                MessageBox.Show(this, "PO exported successfully.", "Export Complete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Export failed:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                Text      = text,
                AutoSize  = false,
                Size      = new Size(115, 22),
                Location  = new Point(x, y + 2),
                ForeColor = Theme.TextSecondary,
                TextAlign = ContentAlignment.MiddleRight
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Supplier picker dialog
    // ─────────────────────────────────────────────────────────────────────────────

    internal sealed class FormSupplierPicker : Form
    {
        private readonly List<Supplier> _all;
        private TextBox      txtSearch = new();
        private DataGridView dgv       = new();
        private Button       btnPick   = new();

        public Supplier? SelectedSupplier { get; private set; }

        public FormSupplierPicker(List<Supplier> suppliers)
        {
            _all = suppliers;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            ApplyFilter();
        }

        private void BuildUI()
        {
            Text          = "Select Supplier";
            ClientSize    = new Size(500, 400);
            MinimumSize   = new Size(400, 300);
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label { Text = "Search:", Location = new Point(12, 62), AutoSize = true });
            txtSearch.Location        = new Point(70, 60);
            txtSearch.Size            = new Size(260, 23);
            txtSearch.PlaceholderText = "Supplier name…";
            txtSearch.TextChanged    += (_, _) => ApplyFilter();
            Controls.Add(txtSearch);

            dgv.AutoGenerateColumns = false;
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",    HeaderText = "Supplier", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cContact", HeaderText = "Contact",  Width = 130, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPhone",   HeaderText = "Phone",    Width = 110, ReadOnly = true });
            dgv.AllowUserToAddRows    = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.ReadOnly              = true;
            dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect           = false;
            dgv.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgv.Location = new Point(12, 80);
            dgv.Size     = new Size(476, 268);
            dgv.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) Pick(); };
            Controls.Add(dgv);

            btnPick.Text     = "Select";
            btnPick.Size     = new Size(90, 28);
            btnPick.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnPick.Location = new Point(398, 360);
            btnPick.UseVisualStyleBackColor = true;
            btnPick.Click += (_, _) => Pick();
            Controls.Add(btnPick);

            SizeChanged += (_, _) =>
                btnPick.Location = new Point(ClientSize.Width - btnPick.Width - 12, ClientSize.Height - btnPick.Height - 10);

            Theme.AddFormHeader(this, "🏢  Select Supplier");
        }

        private void ApplyFilter()
        {
            var term = txtSearch.Text.Trim();
            var list = string.IsNullOrEmpty(term)
                ? _all
                : _all.Where(s => s.SupplierName.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();

            dgv.Rows.Clear();
            foreach (var s in list)
            {
                int idx = dgv.Rows.Add();
                var row = dgv.Rows[idx];
                row.Cells["cName"].Value    = s.SupplierName;
                row.Cells["cContact"].Value = s.ContactName ?? "";
                row.Cells["cPhone"].Value   = s.Phone ?? "";
                row.Tag = s;
            }
        }

        private void Pick()
        {
            if (dgv.CurrentRow?.Tag is Supplier s)
            {
                SelectedSupplier = s;
                DialogResult     = DialogResult.OK;
                Close();
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // PO line-item picker — enforces selection from existing Parts or Products
    // ─────────────────────────────────────────────────────────────────────────────

    internal sealed class FormPOItemPicker : Form
    {
        private List<Part>    _parts;
        private List<Product> _products;

        private readonly IPartRepository    _partRepo    = AppServices.Get<IPartRepository>();
        private readonly IProductRepository _productRepo = AppServices.Get<IProductRepository>();

        private TextBox      txtSearch   = new();
        private ComboBox     cboCategory = new();
        private DataGridView dgv         = new();
        private Button       btnAdd      = new();

        public List<PurchaseOrderItem> SelectedItems { get; } = new();
        public List<Part>    Parts    => _parts;
        public List<Product> Products => _products;

        public FormPOItemPicker(List<Part> parts, List<Product> products)
        {
            _parts    = parts;
            _products = products;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            ApplyFilter();
        }

        private void BuildUI()
        {
            Text          = "Add PO Line Items";
            ClientSize    = new Size(780, 480);
            MinimumSize   = new Size(700, 380);
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label { Text = "Search:", Location = new Point(12, 62), AutoSize = true });
            txtSearch.Location        = new Point(70, 60);
            txtSearch.Size            = new Size(200, 23);
            txtSearch.PlaceholderText = "Number or name…";
            txtSearch.TextChanged    += (_, _) => ApplyFilter();
            Controls.Add(txtSearch);

            Controls.Add(new Label { Text = "Category:", Location = new Point(284, 62), AutoSize = true });
            cboCategory.Location      = new Point(350, 60);
            cboCategory.Size          = new Size(110, 23);
            cboCategory.DropDownStyle = ComboBoxStyle.DropDownList;
            cboCategory.Items.AddRange(new object[] { "All", "Parts", "Products" });
            cboCategory.SelectedIndex        = 0;
            cboCategory.SelectedIndexChanged += (_, _) => ApplyFilter();
            Controls.Add(cboCategory);

            var btnNewPart = new Button
            {
                Text     = "+ New Part",
                Location = new Point(472, 59),
                Size     = new Size(90, 26),
                UseVisualStyleBackColor = true
            };
            btnNewPart.Click += (_, _) =>
            {
                using var frm = new FormPartsManager();
                frm.ShowDialog(this);
                _parts = _partRepo.GetAll(includeInactive: false);
                ApplyFilter();
            };
            Controls.Add(btnNewPart);

            var btnNewProduct = new Button
            {
                Text     = "+ New Product",
                Location = new Point(570, 59),
                Size     = new Size(100, 26),
                UseVisualStyleBackColor = true
            };
            btnNewProduct.Click += (_, _) =>
            {
                using var frm = new FormAddProduct();
                frm.ShowDialog(this);
                _products = _productRepo.GetProducts().ToList();
                ApplyFilter();
            };
            Controls.Add(btnNewProduct);

            dgv.AutoGenerateColumns = false;
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCat",  HeaderText = "Type",      Width = 72, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cSKU",  HeaderText = "SKU / Part#", Width = 120, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName", HeaderText = "Name",      AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "cCost", HeaderText = "Unit Cost",  Width = 90, ReadOnly = true });
            dgv.AllowUserToAddRows    = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.ReadOnly              = true;
            dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect           = true;
            dgv.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgv.Location = new Point(12, 80);
            dgv.Size     = new Size(656, 358);
            Controls.Add(dgv);

            btnAdd.Text     = "Add Selected";
            btnAdd.Size     = new Size(110, 28);
            btnAdd.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            btnAdd.Location = new Point(558, 442);
            btnAdd.UseVisualStyleBackColor = true;
            btnAdd.Click += BtnAdd_Click;
            Controls.Add(btnAdd);

            SizeChanged += (_, _) =>
                btnAdd.Location = new Point(ClientSize.Width - btnAdd.Width - 12, ClientSize.Height - btnAdd.Height - 10);

            Theme.AddFormHeader(this, "📦  Add PO Items");
        }

        private void ApplyFilter()
        {
            var term = txtSearch.Text.Trim();
            var cat  = cboCategory.SelectedItem?.ToString() ?? "All";

            dgv.Rows.Clear();

            if (cat is "All" or "Parts")
            {
                foreach (var p in _parts.Where(p =>
                    string.IsNullOrEmpty(term) ||
                    p.PartNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    p.PartName.Contains(term, StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = dgv.Rows.Add();
                    var row = dgv.Rows[idx];
                    row.Cells["cCat"].Value  = "Part";
                    row.Cells["cSKU"].Value  = p.PartNumber;
                    row.Cells["cName"].Value = p.PartName;
                    row.Cells["cCost"].Value = $"${p.UnitCost:N2} CAD";
                    row.Tag = (object)p;
                }
            }

            if (cat is "All" or "Products")
            {
                foreach (var p in _products.Where(p =>
                    string.IsNullOrEmpty(term) ||
                    p.SKU.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    p.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase)))
                {
                    int idx = dgv.Rows.Add();
                    var row = dgv.Rows[idx];
                    row.Cells["cCat"].Value  = "Product";
                    row.Cells["cSKU"].Value  = p.SKU;
                    row.Cells["cName"].Value = p.ProductName;
                    row.Cells["cCost"].Value = $"${p.WholesalePrice:N2} CAD";
                    row.Tag = (object)p;
                }
            }
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dgv.SelectedRows)
            {
                if (row.Tag is Part part)
                {
                    SelectedItems.Add(new PurchaseOrderItem
                    {
                        PartID          = part.PartID,
                        SKU             = part.PartNumber,
                        ItemName        = part.PartName,
                        QuantityOrdered = 1,
                        UnitCost        = part.UnitCost
                    });
                }
                else if (row.Tag is Product prod)
                {
                    SelectedItems.Add(new PurchaseOrderItem
                    {
                        ProductID       = prod.ProductID,
                        SKU             = prod.SKU,
                        ItemName        = prod.ProductName,
                        QuantityOrdered = 1,
                        UnitCost        = prod.WholesalePrice
                    });
                }
            }

            if (SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Select at least one item.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
