using JaneERP.Data;
using JaneERP.Models;

namespace JaneERP
{
    /// <summary>CRUD form for managing Suppliers.</summary>
    public class FormSupplierManager : Form
    {
        private readonly SupplierRepository _repo;

        private DataGridView dgvSuppliers  = new();
        private TextBox      txtName       = new();
        private TextBox      txtContact    = new();
        private TextBox      txtEmail      = new();
        private TextBox      txtPhone      = new();
        private TextBox      txtAddress    = new();
        private TextBox      txtNotes      = new();
        private CheckBox     chkActive     = new();
        private Button       btnSave       = new();
        private Button       btnNew        = new();
        private Button       btnClose      = new();
        private Label        lblEdit       = new();

        private Supplier? _editing;
        private List<Supplier> _suppliers = new();

        public FormSupplierManager(SupplierRepository repo)
        {
            _repo = repo;
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            LoadSuppliers();
        }

        private void BuildUI()
        {
            Text          = "Supplier Manager";
            ClientSize    = new Size(900, 560);
            MinimumSize   = new Size(800, 500);
            StartPosition = FormStartPosition.CenterParent;

            // ── Grid ─────────────────────────────────────────────────────────────
            dgvSuppliers.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            dgvSuppliers.Location = new Point(12, 12);
            dgvSuppliers.Size     = new Size(480, 510);
            dgvSuppliers.ReadOnly = true;
            dgvSuppliers.AllowUserToAddRows    = false;
            dgvSuppliers.AllowUserToDeleteRows = false;
            dgvSuppliers.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            dgvSuppliers.MultiSelect           = false;
            dgvSuppliers.AutoGenerateColumns   = false;
            dgvSuppliers.RowHeadersVisible     = false;

            dgvSuppliers.Columns.Add(new DataGridViewTextBoxColumn { Name = "cName",    HeaderText = "Name",    DataPropertyName = "SupplierName", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            dgvSuppliers.Columns.Add(new DataGridViewTextBoxColumn { Name = "cContact", HeaderText = "Contact", DataPropertyName = "ContactName",  Width = 120 });
            dgvSuppliers.Columns.Add(new DataGridViewTextBoxColumn { Name = "cPhone",   HeaderText = "Phone",   DataPropertyName = "Phone",        Width = 110 });
            dgvSuppliers.Columns.Add(new DataGridViewCheckBoxColumn { Name = "cActive", HeaderText = "Active",  DataPropertyName = "IsActive",     Width = 55  });

            dgvSuppliers.SelectionChanged += DgvSuppliers_SelectionChanged;
            Controls.Add(dgvSuppliers);

            // ── Edit panel ────────────────────────────────────────────────────────
            int x = 508, y = 12;

            lblEdit.AutoSize  = false;
            lblEdit.Font      = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblEdit.ForeColor = Theme.Gold;
            lblEdit.Location  = new Point(x, y);
            lblEdit.Size      = new Size(370, 26);
            lblEdit.Text      = "Select a supplier to edit";
            Controls.Add(lblEdit);
            y += 36;

            AddField(ref y, x, "Name:",        txtName);
            AddField(ref y, x, "Contact:",     txtContact);
            AddField(ref y, x, "Email:",       txtEmail);
            AddField(ref y, x, "Phone:",       txtPhone);
            AddField(ref y, x, "Address:",     txtAddress);
            AddField(ref y, x, "Notes:",       txtNotes);

            chkActive.Text     = "Active";
            chkActive.Location = new Point(x + 110, y);
            chkActive.AutoSize = true;
            Controls.Add(chkActive);
            y += 30;

            btnSave.Text     = "Save";
            btnSave.Location = new Point(x, y);
            btnSave.Size     = new Size(80, 30);
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click   += BtnSave_Click;
            Controls.Add(btnSave);

            btnNew.Text     = "New Supplier";
            btnNew.Location = new Point(x + 90, y);
            btnNew.Size     = new Size(110, 30);
            btnNew.UseVisualStyleBackColor = true;
            btnNew.Click   += (_, _) =>
            {
                _editing     = null;
                lblEdit.Text = "New Supplier";
                ClearForm();
                txtName.Focus();
            };
            Controls.Add(btnNew);

            btnClose.Text     = "Close";
            btnClose.Location = new Point(x + 210, y);
            btnClose.Size     = new Size(80, 30);
            btnClose.UseVisualStyleBackColor = true;
            btnClose.Click   += (_, _) => Close();
            Controls.Add(btnClose);
        }

        private void AddField(ref int y, int x, string label, TextBox txt)
        {
            Controls.Add(new Label
            {
                Text      = label,
                AutoSize  = false,
                Size      = new Size(100, 22),
                Location  = new Point(x, y + 2),
                ForeColor = Theme.TextSecondary,
                TextAlign = ContentAlignment.MiddleRight
            });
            txt.Location = new Point(x + 108, y);
            txt.Size     = new Size(260, 24);
            Controls.Add(txt);
            y += 32;
        }

        private void ClearForm()
        {
            txtName.Text    = "";
            txtContact.Text = "";
            txtEmail.Text   = "";
            txtPhone.Text   = "";
            txtAddress.Text = "";
            txtNotes.Text   = "";
            chkActive.Checked = true;
        }

        private void LoadSuppliers()
        {
            _suppliers = _repo.GetAllSuppliers(includeInactive: true);
            dgvSuppliers.DataSource = null;
            dgvSuppliers.DataSource = _suppliers;
        }

        private void DgvSuppliers_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvSuppliers.CurrentRow == null || dgvSuppliers.CurrentRow.Index < 0 || dgvSuppliers.CurrentRow.Index >= _suppliers.Count) return;
            _editing = _suppliers[dgvSuppliers.CurrentRow.Index];
            lblEdit.Text    = $"Editing: {_editing.SupplierName}";
            txtName.Text    = _editing.SupplierName;
            txtContact.Text = _editing.ContactName ?? "";
            txtEmail.Text   = _editing.Email       ?? "";
            txtPhone.Text   = _editing.Phone       ?? "";
            txtAddress.Text = _editing.Address     ?? "";
            txtNotes.Text   = _editing.Notes       ?? "";
            chkActive.Checked = _editing.IsActive;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show(this, "Supplier name is required.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var s = _editing ?? new Supplier();
            s.SupplierName = txtName.Text.Trim();
            s.ContactName  = string.IsNullOrWhiteSpace(txtContact.Text) ? null : txtContact.Text.Trim();
            s.Email        = string.IsNullOrWhiteSpace(txtEmail.Text)   ? null : txtEmail.Text.Trim();
            s.Phone        = string.IsNullOrWhiteSpace(txtPhone.Text)   ? null : txtPhone.Text.Trim();
            s.Address      = string.IsNullOrWhiteSpace(txtAddress.Text) ? null : txtAddress.Text.Trim();
            s.Notes        = string.IsNullOrWhiteSpace(txtNotes.Text)   ? null : txtNotes.Text.Trim();
            s.IsActive     = chkActive.Checked;

            try
            {
                if (_editing == null)
                    _repo.AddSupplier(s);
                else
                    _repo.UpdateSupplier(s);

                LoadSuppliers();
                ClearForm();
                _editing = null;
                lblEdit.Text = "Saved. Select a supplier to edit.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to save:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
