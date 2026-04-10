using JaneERP.Data;
using JaneERP.Models;
using JaneERP.Services;

namespace JaneERP
{
    /// <summary>Create a manual (non-Shopify) sales order directly in the ERP.</summary>
    public class FormCreateOrder : Form
    {
        // ── Discount option wrapper for cboDiscountType ────────────────────────────
        private sealed class DiscountOption
        {
            public enum DiscountKind { None, Tier, Fixed, Percent }
            public DiscountKind Kind { get; init; }
            public DiscountTier? Tier { get; init; }

            public override string ToString() => Kind switch
            {
                DiscountKind.Fixed   => "Fixed Amount",
                DiscountKind.Percent => "Percent",
                DiscountKind.Tier    => $"{Tier!.TierName} ({Tier.DiscountPercent:N0}%)",
                _                   => "(None)"
            };
        }

        private readonly List<Customer>         _customers;
        private readonly AppSettings            _cfg      = AppSettings.Current;
        private readonly DiscountTierRepository _tierRepo = new();
        private          List<DiscountTier>     _activeTiers = new();

        // ── Customer row ──────────────────────────────────────────────────────────
        private TextBox  txtCustomerSearch = new();
        private Button   btnCustomerSearch = new();
        private TextBox  txtEmail  = new();
        private TextBox  txtName   = new();
        private int?     _selectedCustomerId;

        // ── Order fields ──────────────────────────────────────────────────────────
        private DateTimePicker dtpDate      = new();
        private ComboBox       cboOrderType = new();
        private ComboBox       cboCurrency  = new();
        private TextBox        txtNotes     = new();

        // ── Discount fields ───────────────────────────────────────────────────────
        private ComboBox      cboDiscountType   = new();
        private NumericUpDown nudDiscountValue  = new();
        private Label         lblDiscountCalc   = new();
        private Label         lblFinalTotal     = new();

        // ── Grid ──────────────────────────────────────────────────────────────────
        private DataGridView dgvItems  = new();
        private Label        lblTotal  = new();
        private Label        lblHomeTotal = new();

        public FormCreateOrder()
        {
            _customers = LoadCustomers();
            try { _activeTiers = _tierRepo.GetActive().ToList(); } catch { _activeTiers = new(); }
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);
        }

        private static List<Customer> LoadCustomers()
        {
            try { return new ShopifySyncService().GetAllCustomers(); }
            catch { return new List<Customer>(); }
        }

        private void BuildUI()
        {
            Text          = "New Sales Order";
            ClientSize    = new Size(780, 680);
            MinimumSize   = new Size(660, 580);
            StartPosition = FormStartPosition.CenterParent;

            var lblTitle = new Label
            {
                Text      = "New Sales Order",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(12, 12),
                AutoSize  = true
            };
            Controls.Add(lblTitle);

            int lx = 14, cx = 160, cw = 280, row = 46;

            // ── Customer search ───────────────────────────────────────────────────
            AddLabel("Customer", lx, row + 2);
            txtCustomerSearch.Location        = new Point(cx, row);
            txtCustomerSearch.Size            = new Size(cw - 120, 23);
            txtCustomerSearch.PlaceholderText = "Type name or email…";
            SetupCustomerAutocomplete();
            Controls.Add(txtCustomerSearch);

            btnCustomerSearch.Text     = "Search";
            btnCustomerSearch.Size     = new Size(54, 23);
            btnCustomerSearch.Location = new Point(cx + cw - 116, row);
            btnCustomerSearch.Click   += BtnCustomerSearch_Click;
            Controls.Add(btnCustomerSearch);

            var btnNewCustomer = new Button
            {
                Text     = "+ New",
                Size     = new Size(54, 23),
                Location = new Point(cx + cw - 56, row)
            };
            btnNewCustomer.Click += (_, _) =>
            {
                txtCustomerSearch.Text  = "";
                txtEmail.Text           = "";
                txtEmail.ReadOnly       = false;
                txtName.Text            = "";
                _selectedCustomerId     = null;
                txtEmail.Focus();
            };
            Controls.Add(btnNewCustomer);

            row += 32;
            AddLabel("Email *", lx, row + 2);
            txtEmail.Location        = new Point(cx, row); txtEmail.Size = new Size(cw, 23);
            txtEmail.PlaceholderText = "customer@example.com";
            Controls.Add(txtEmail);

            row += 32;
            AddLabel("Full Name", lx, row + 2);
            txtName.Location = new Point(cx, row); txtName.Size = new Size(cw, 23);
            Controls.Add(txtName);

            row += 32;
            AddLabel("Order Date", lx, row + 2);
            dtpDate.Location = new Point(cx, row); dtpDate.Size = new Size(180, 23);
            dtpDate.Value    = DateTime.Today;
            Controls.Add(dtpDate);

            // ── Currency ─────────────────────────────────────────────────────────
            row += 32;
            AddLabel("Currency", lx, row + 2);
            cboCurrency.Location      = new Point(cx, row);
            cboCurrency.Size          = new Size(100, 23);
            cboCurrency.DropDownStyle = ComboBoxStyle.DropDownList;
            cboCurrency.Items.Add(_cfg.HomeCurrency);
            foreach (var k in _cfg.CurrencyRates.Keys)
                if (k != _cfg.HomeCurrency) cboCurrency.Items.Add(k);
            cboCurrency.SelectedIndex = 0;
            cboCurrency.SelectedIndexChanged += (_, _) => RecalcTotal();
            Controls.Add(cboCurrency);

            row += 32;
            AddLabel("Order Type", lx, row + 2);
            cboOrderType.Location      = new Point(cx, row);
            cboOrderType.Size          = new Size(180, 23);
            cboOrderType.DropDownStyle = ComboBoxStyle.DropDownList;
            var orderTypes = (_cfg.OrderTypes != null && _cfg.OrderTypes.Count > 0)
                ? _cfg.OrderTypes
                : new System.Collections.Generic.List<string> { "Manual" };
            foreach (var t in orderTypes) cboOrderType.Items.Add(t);
            if (cboOrderType.Items.Count > 0) cboOrderType.SelectedIndex = 0;
            Controls.Add(cboOrderType);

            row += 32;
            AddLabel("Notes", lx, row + 2);
            txtNotes.Location = new Point(cx, row); txtNotes.Size = new Size(cw, 23);
            Controls.Add(txtNotes);

            // ── Line items ────────────────────────────────────────────────────────
            row += 40;
            Controls.Add(new Label
            {
                Text     = "Line Items",
                Font     = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(lx, row),
                AutoSize = true
            });

            var btnAddProducts = new Button
            {
                Text     = "+ Add Products",
                Size     = new Size(130, 26),
                Location = new Point(lx + 118, row - 2)
            };
            btnAddProducts.Click += BtnAddProducts_Click;
            Controls.Add(btnAddProducts);

            var btnRemove = new Button
            {
                Text     = "Remove Selected",
                Size     = new Size(130, 26),
                Location = new Point(lx + 258, row - 2)
            };
            btnRemove.Click += (_, _) =>
            {
                foreach (DataGridViewRow r in dgvItems.SelectedRows.Cast<DataGridViewRow>().ToList())
                    if (!r.IsNewRow) dgvItems.Rows.Remove(r);
                RecalcTotal();
            };
            Controls.Add(btnRemove);

            row += 32;
            BuildGrid(row);

            // ── Totals ────────────────────────────────────────────────────────────
            lblTotal.Text      = "Total: $0.00";
            lblTotal.Font      = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblTotal.ForeColor = Theme.Gold;
            lblTotal.AutoSize  = true;
            lblTotal.Anchor    = AnchorStyles.Bottom | AnchorStyles.Left;
            lblTotal.Location  = new Point(lx, ClientSize.Height - 72);
            Controls.Add(lblTotal);

            lblHomeTotal.Text      = "";
            lblHomeTotal.Font      = new Font("Segoe UI", 9F);
            lblHomeTotal.ForeColor = Theme.TextSecondary;
            lblHomeTotal.AutoSize  = true;
            lblHomeTotal.Anchor    = AnchorStyles.Bottom | AnchorStyles.Left;
            lblHomeTotal.Location  = new Point(lx, ClientSize.Height - 54);
            Controls.Add(lblHomeTotal);

            // ── Discount section ──────────────────────────────────────────────────
            var lblDiscountHdr = new Label
            {
                Text      = "Discount:",
                Font      = new Font("Segoe UI", 9F),
                AutoSize  = true,
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left,
                Location  = new Point(lx + 200, ClientSize.Height - 72)
            };
            Controls.Add(lblDiscountHdr);

            // Populate discount options: (None), each active tier, Fixed Amount, Percent
            cboDiscountType.Items.Add(new DiscountOption { Kind = DiscountOption.DiscountKind.None });
            foreach (var t in _activeTiers)
                cboDiscountType.Items.Add(new DiscountOption { Kind = DiscountOption.DiscountKind.Tier, Tier = t });
            cboDiscountType.Items.Add(new DiscountOption { Kind = DiscountOption.DiscountKind.Fixed });
            cboDiscountType.Items.Add(new DiscountOption { Kind = DiscountOption.DiscountKind.Percent });
            cboDiscountType.SelectedIndex = 0;
            cboDiscountType.DropDownStyle = ComboBoxStyle.DropDownList;
            cboDiscountType.Size          = new Size(160, 23);
            cboDiscountType.Anchor        = AnchorStyles.Bottom | AnchorStyles.Left;
            cboDiscountType.Location      = new Point(lx + 270, ClientSize.Height - 74);
            cboDiscountType.SelectedIndexChanged += (_, _) =>
            {
                var opt = cboDiscountType.SelectedItem as DiscountOption;
                // Tier items carry their own %; show nudDiscountValue only for Fixed/Percent
                nudDiscountValue.Visible = opt?.Kind == DiscountOption.DiscountKind.Fixed
                                       || opt?.Kind == DiscountOption.DiscountKind.Percent;
                RecalcTotal();
            };
            Controls.Add(cboDiscountType);

            nudDiscountValue.Size           = new Size(80, 23);
            nudDiscountValue.Anchor         = AnchorStyles.Bottom | AnchorStyles.Left;
            nudDiscountValue.Location       = new Point(lx + 408, ClientSize.Height - 74);
            nudDiscountValue.Minimum        = 0;
            nudDiscountValue.Maximum        = 9999999;
            nudDiscountValue.DecimalPlaces  = 2;
            nudDiscountValue.Visible        = false;
            nudDiscountValue.ValueChanged  += (_, _) => RecalcTotal();
            Controls.Add(nudDiscountValue);

            lblDiscountCalc.Text      = "";
            lblDiscountCalc.Font      = new Font("Segoe UI", 9F);
            lblDiscountCalc.ForeColor = Theme.Teal;
            lblDiscountCalc.AutoSize  = true;
            lblDiscountCalc.Anchor    = AnchorStyles.Bottom | AnchorStyles.Left;
            lblDiscountCalc.Location  = new Point(lx + 200, ClientSize.Height - 54);
            Controls.Add(lblDiscountCalc);

            lblFinalTotal.Text      = "";
            lblFinalTotal.Font      = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblFinalTotal.ForeColor = Theme.Gold;
            lblFinalTotal.AutoSize  = true;
            lblFinalTotal.Anchor    = AnchorStyles.Bottom | AnchorStyles.Left;
            lblFinalTotal.Location  = new Point(lx + 380, ClientSize.Height - 54);
            Controls.Add(lblFinalTotal);

            // ── Action buttons ────────────────────────────────────────────────────
            var btnDraft = new Button
            {
                Text     = "Save as Draft",
                Size     = new Size(120, 30),
                Anchor   = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point(ClientSize.Width - 354, ClientSize.Height - 48)
            };
            btnDraft.Click += (_, _) => SaveOrder("Draft");
            Controls.Add(btnDraft);

            var btnLive = new Button
            {
                Text      = "Save as Live",
                Size      = new Size(120, 30),
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Right,
                Location  = new Point(ClientSize.Width - 228, ClientSize.Height - 48),
                BackColor = Color.FromArgb(0, 110, 55),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnLive.FlatAppearance.BorderColor = Color.FromArgb(0, 80, 40);
            btnLive.Click += (_, _) => SaveOrder("Live");
            Controls.Add(btnLive);

            var btnCancel = new Button
            {
                Text     = "Cancel",
                Size     = new Size(90, 30),
                Anchor   = AnchorStyles.Bottom | AnchorStyles.Right,
                Location = new Point(ClientSize.Width - 102, ClientSize.Height - 48)
            };
            btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);
        }

        private void SetupCustomerAutocomplete()
        {
            var ac = new AutoCompleteStringCollection();
            foreach (var c in _customers)
            {
                ac.Add(c.Email);
                if (!string.IsNullOrWhiteSpace(c.FullName)) ac.Add(c.FullName);
            }
            txtCustomerSearch.AutoCompleteMode   = AutoCompleteMode.SuggestAppend;
            txtCustomerSearch.AutoCompleteSource = AutoCompleteSource.CustomSource;
            txtCustomerSearch.AutoCompleteCustomSource = ac;

            txtCustomerSearch.TextChanged += (_, _) =>
            {
                // Auto-fill email and name if the typed text matches a customer exactly
                var term = txtCustomerSearch.Text.Trim();
                var match = _customers.FirstOrDefault(c =>
                    c.Email.Equals(term, StringComparison.OrdinalIgnoreCase) ||
                    (c.FullName ?? "").Equals(term, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    txtEmail.Text         = match.Email;
                    txtEmail.ReadOnly     = true;
                    txtName.Text          = match.FullName ?? "";
                    _selectedCustomerId   = match.CustomerID;
                    ApplyCustomerTier(match.CustomerID);
                }
                else
                {
                    txtEmail.ReadOnly   = false;
                    _selectedCustomerId = null;
                    // If no match, populate email from search if it looks like an email
                    if (term.Contains('@')) txtEmail.Text = term;
                    ResetDiscountToNone();
                }
            };
        }

        private void BtnCustomerSearch_Click(object? sender, EventArgs e)
        {
            using var frm = new FormCustomerSearch(_customers);
            if (frm.ShowDialog(this) != DialogResult.OK || frm.SelectedCustomer == null) return;
            var c = frm.SelectedCustomer;
            txtCustomerSearch.Text  = c.DisplayLabel;
            txtEmail.Text           = c.Email;
            txtEmail.ReadOnly       = true;
            txtName.Text            = c.FullName ?? "";
            _selectedCustomerId     = c.CustomerID;
            ApplyCustomerTier(c.CustomerID);
        }

        private void ApplyCustomerTier(int customerId)
        {
            try
            {
                var tier = _tierRepo.GetTierForCustomer(customerId);
                if (tier != null && tier.IsActive)
                {
                    // Find the matching tier item in the dropdown
                    foreach (DiscountOption item in cboDiscountType.Items)
                    {
                        if (item.Kind == DiscountOption.DiscountKind.Tier
                            && item.Tier?.TierID == tier.TierID)
                        {
                            cboDiscountType.SelectedItem = item;
                            nudDiscountValue.Visible     = false; // % is shown in the dropdown item
                            return;
                        }
                    }
                }
            }
            catch { /* non-fatal — discount can be set manually */ }
        }

        private void ResetDiscountToNone()
        {
            if (cboDiscountType.Items.Count > 0) cboDiscountType.SelectedIndex = 0;
            nudDiscountValue.Value   = 0;
            nudDiscountValue.Visible = false;
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label { Text = text, Location = new Point(x, y), AutoSize = true });
        }

        private void BuildGrid(int top)
        {
            dgvItems.AutoGenerateColumns = false;
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colSKU",   HeaderText = "SKU",        Width = 130, ReadOnly = true });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colName",  HeaderText = "Product",    Width = 210, ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colQty",   HeaderText = "Qty",        Width = 60 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colPrice", HeaderText = "Unit Price", Width = 100 });
            dgvItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "colTotal", HeaderText = "Line Total", Width = 100, ReadOnly = true });

            dgvItems.AllowUserToAddRows    = false;
            dgvItems.AllowUserToDeleteRows = false;
            dgvItems.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvItems.MultiSelect           = true;
            dgvItems.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvItems.Location = new Point(14, top);
            dgvItems.Size     = new Size(752, 280);

            dgvItems.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                if (dgvItems.Columns["colQty"] is DataGridViewColumn cqty && dgvItems.Columns["colPrice"] is DataGridViewColumn cprice &&
                    (e.ColumnIndex == cqty.Index || e.ColumnIndex == cprice.Index))
                {
                    UpdateLineTotal(e.RowIndex);
                    RecalcTotal();
                }
            };

            dgvItems.EditingControlShowing += (s, e) =>
            {
                int col = dgvItems.CurrentCell?.ColumnIndex ?? -1;
                if (dgvItems.Columns["colQty"] is DataGridViewColumn colQty && col == colQty.Index && e.Control is TextBox tbq)
                {
                    tbq.KeyPress -= IntegerKeyPress;
                    tbq.KeyPress += IntegerKeyPress;
                }
                else if (dgvItems.Columns["colPrice"] is DataGridViewColumn colPrice && col == colPrice.Index && e.Control is TextBox tbp)
                {
                    tbp.KeyPress -= DecimalKeyPress;
                    tbp.KeyPress += DecimalKeyPress;
                }
            };

            dgvItems.DataError += (s, e) => e.Cancel = true;
            Controls.Add(dgvItems);
        }

        private void UpdateLineTotal(int rowIndex)
        {
            var row = dgvItems.Rows[rowIndex];
            int.TryParse(row.Cells["colQty"].Value?.ToString(), out int qty);
            decimal.TryParse(row.Cells["colPrice"].Value?.ToString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal price);
            row.Cells["colTotal"].Value = (qty * price).ToString("C2");
        }

        private void RecalcTotal()
        {
            decimal subtotal = 0;
            foreach (DataGridViewRow r in dgvItems.Rows)
            {
                if (r.IsNewRow) continue;
                int.TryParse(r.Cells["colQty"].Value?.ToString(), out int q);
                decimal.TryParse(r.Cells["colPrice"].Value?.ToString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal p);
                subtotal += q * p;
            }

            var selectedCurrency = cboCurrency.SelectedItem?.ToString() ?? _cfg.HomeCurrency;
            lblTotal.Text = $"Total: {subtotal:N2} {selectedCurrency}";

            // Discount calculation
            decimal discountAmt = 0;
            var discOpt = cboDiscountType?.SelectedItem as DiscountOption;
            if (discOpt?.Kind != null && discOpt.Kind != DiscountOption.DiscountKind.None)
            {
                decimal val = discOpt.Kind == DiscountOption.DiscountKind.Tier
                    ? discOpt.Tier!.DiscountPercent
                    : nudDiscountValue.Value;
                discountAmt = discOpt.Kind == DiscountOption.DiscountKind.Fixed
                    ? Math.Min(val, subtotal)
                    : Math.Min(subtotal * val / 100m, subtotal);
            }

            decimal finalTotal = subtotal - discountAmt;

            if (lblDiscountCalc != null)
            {
                lblDiscountCalc.Text = discountAmt > 0
                    ? $"Discount: -{discountAmt:N2} {selectedCurrency}"
                    : "";
            }
            if (lblFinalTotal != null)
            {
                lblFinalTotal.Text = discountAmt > 0
                    ? $"Final: {finalTotal:N2} {selectedCurrency}"
                    : "";
            }

            // Show home-currency equivalent if a foreign currency is selected
            if (!selectedCurrency.Equals(_cfg.HomeCurrency, StringComparison.OrdinalIgnoreCase))
            {
                var rate = _cfg.GetRate(selectedCurrency);
                var homeTotal = finalTotal * rate;
                lblHomeTotal.Text = $"≈ {homeTotal:N2} {_cfg.HomeCurrency} (rate: {rate:G})";
            }
            else
            {
                lblHomeTotal.Text = "";
            }
        }

        private static void IntegerKeyPress(object? sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                e.Handled = true;
        }

        private static void DecimalKeyPress(object? sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '.')
                e.Handled = true;
            if (e.KeyChar == '.' && sender is TextBox tb && tb.Text.Contains('.'))
                e.Handled = true;
        }

        private void BtnAddProducts_Click(object? sender, EventArgs e)
        {
            using var picker = new FormOrderProductPicker();
            if (picker.ShowDialog(this) != DialogResult.OK) return;

            foreach (var product in picker.SelectedProducts)
            {
                int idx = dgvItems.Rows.Add();
                var row = dgvItems.Rows[idx];
                row.Cells["colSKU"].Value   = product.SKU;
                row.Cells["colName"].Value  = product.ProductName;
                row.Cells["colQty"].Value   = "1";
                row.Cells["colPrice"].Value = product.RetailPrice.ToString("G");
                row.Tag                     = product;
                UpdateLineTotal(idx);
            }
            RecalcTotal();
        }

        private void SaveOrder(string status)
        {
            var email = txtEmail.Text.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show(this, "Customer email is required.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            dgvItems.CommitEdit(DataGridViewDataErrorContexts.Commit);
            dgvItems.EndEdit();

            var lineItems = new List<(string Sku, string Title, int Qty, decimal UnitPrice)>();
            foreach (DataGridViewRow row in dgvItems.Rows)
            {
                if (row.IsNewRow) continue;
                var sku   = row.Cells["colSKU"]?.Value?.ToString()?.Trim() ?? "";
                var title = row.Cells["colName"]?.Value?.ToString()?.Trim() ?? sku;
                if (!int.TryParse(row.Cells["colQty"]?.Value?.ToString(), out int qty) || qty <= 0) continue;
                decimal.TryParse(row.Cells["colPrice"]?.Value?.ToString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal price);
                if (!string.IsNullOrWhiteSpace(sku))
                    lineItems.Add((sku, title, qty, price));
            }

            if (lineItems.Count == 0)
            {
                MessageBox.Show(this, "Add at least one line item.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var currency  = cboCurrency.SelectedItem?.ToString() ?? _cfg.HomeCurrency;
            var orderType = cboOrderType.SelectedItem?.ToString() ?? "Manual";

            // Resolve discount
            string?  discountType   = null;
            decimal  discountAmount = 0;
            decimal  discountPct    = 0;

            var discOpt = cboDiscountType.SelectedItem as DiscountOption;
            if (discOpt?.Kind != null && discOpt.Kind != DiscountOption.DiscountKind.None)
            {
                decimal subtotal = lineItems.Sum(li => li.Qty * li.UnitPrice);
                if (discOpt.Kind == DiscountOption.DiscountKind.Tier)
                {
                    discountType   = discOpt.Tier!.TierName;
                    discountPct    = discOpt.Tier.DiscountPercent;
                    discountAmount = Math.Min(subtotal * discOpt.Tier.DiscountPercent / 100m, subtotal);
                }
                else if (discOpt.Kind == DiscountOption.DiscountKind.Fixed)
                {
                    discountType   = "Fixed Amount";
                    discountAmount = Math.Min(nudDiscountValue.Value, subtotal);
                }
                else // Percent
                {
                    discountType   = "Percent";
                    discountPct    = nudDiscountValue.Value;
                    discountAmount = Math.Min(subtotal * nudDiscountValue.Value / 100m, subtotal);
                }
            }

            try
            {
                new ShopifySyncService().CreateManualOrder(
                    email,
                    string.IsNullOrWhiteSpace(txtName.Text) ? null : txtName.Text.Trim(),
                    dtpDate.Value.Date,
                    string.IsNullOrWhiteSpace(txtNotes.Text) ? null : txtNotes.Text.Trim(),
                    currency,
                    null,
                    lineItems,
                    status,
                    orderType,
                    discountType,
                    discountAmount,
                    discountPct);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error saving order",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
