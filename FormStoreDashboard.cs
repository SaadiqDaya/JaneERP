using JaneERP.Data;
using JaneERP.Models;

namespace JaneERP
{
    public class FormStoreDashboard : Form
    {
        private readonly StoreRepository _repo = new();

        private DataGridView grid = null!;
        private TextBox txtName = null!;
        private TextBox txtDomain = null!;
        private TextBox txtToken = null!;
        private Button btnSave = null!;
        private Button btnClear = null!;
        private Button btnRemove = null!;
        private Button btnOpen = null!;
        private Label lblFormTitle = null!;
        private Button btnTest = null!;

        private ShopifyStore? _editing; // null = adding new

        public FormStoreDashboard()
        {
            Text            = "Shopify Stores";
            ClientSize      = new Size(720, 360);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;

            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Load += (_, _) => LoadStores();
        }

        private void BuildUI()
        {
            // ── Left: store list ──────────────────────────────────────────────
            grid = new DataGridView
            {
                Location              = new Point(12, 12),
                Size                  = new Size(380, 280),
                ReadOnly              = true,
                AllowUserToAddRows    = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible     = false,
                BackgroundColor       = SystemColors.Window,
                MultiSelect           = false
            };
            grid.SelectionChanged += Grid_SelectionChanged;

            btnRemove = new Button { Text = "Remove Store", Location = new Point(12, 300), Size = new Size(120, 28), Enabled = false };
            btnRemove.Click += BtnRemove_Click;

            btnOpen = new Button { Text = "Open Sales Dashboard", Location = new Point(272, 300), Size = new Size(120, 28), Enabled = false };
            btnOpen.Click += BtnOpen_Click;

            // ── Right: add/edit panel ─────────────────────────────────────────
            var pnl = new Panel { Location = new Point(408, 12), Size = new Size(296, 336), BorderStyle = BorderStyle.FixedSingle };

            lblFormTitle = new Label { Text = "Add Store", Font = new Font("Segoe UI", 10F, FontStyle.Bold), Location = new Point(8, 10), AutoSize = true };

            var lName   = new Label { Text = "Store Name",     Location = new Point(8,  38), AutoSize = true };
            txtName     = new TextBox { Location = new Point(8, 56),  Size = new Size(276, 23), PlaceholderText = "e.g. VanGo US" };

            var lDomain = new Label { Text = "Store Domain",   Location = new Point(8,  88), AutoSize = true };
            txtDomain   = new TextBox { Location = new Point(8, 106), Size = new Size(276, 23), PlaceholderText = "example.myshopify.com" };

            var lToken  = new Label { Text = "API Token",      Location = new Point(8, 138), AutoSize = true };
            txtToken    = new TextBox { Location = new Point(8, 156), Size = new Size(276, 23), UseSystemPasswordChar = true, PlaceholderText = "shpat_..." };

            btnSave  = new Button { Text = "Add Store",       Location = new Point(8,   194), Size = new Size(130, 28) };
            btnClear = new Button { Text = "Clear",           Location = new Point(146, 194), Size = new Size(80,  28) };
            btnTest  = new Button { Text = "Test Connection", Location = new Point(8,   230), Size = new Size(160, 28) };

            btnSave.Click  += BtnSave_Click;
            btnClear.Click += (_, _) => ClearForm();
            btnTest.Click  += BtnTest_Click;

            pnl.Controls.AddRange(new Control[] { lblFormTitle, lName, txtName, lDomain, txtDomain, lToken, txtToken, btnSave, btnClear, btnTest });

            Controls.Add(grid);
            Controls.Add(btnRemove);
            Controls.Add(btnOpen);
            Controls.Add(pnl);
        }

        private void LoadStores()
        {
            try
            {
                var stores = _repo.GetAll().ToList();
                grid.DataSource = stores.Select(s => new
                {
                    s.StoreID,
                    s.StoreName,
                    s.StoreDomain,
                    Active = s.IsActive ? "Yes" : "No"
                }).ToList();

                if (grid.Columns.Contains("StoreID"))
                    grid.Columns["StoreID"]!.Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load stores:\n\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Grid_SelectionChanged(object? sender, EventArgs e)
        {
            if (grid.SelectedRows.Count == 0)
            {
                btnOpen.Enabled   = false;
                btnRemove.Enabled = false;
                ClearForm();
                return;
            }

            btnOpen.Enabled   = true;
            btnRemove.Enabled = true;

            var row = grid.SelectedRows[0];
            var storeId = row.Cells["StoreID"].Value is int id ? id : Convert.ToInt32(row.Cells["StoreID"].Value);
            var stores  = _repo.GetAll().ToList();
            _editing = stores.FirstOrDefault(s => s.StoreID == storeId);
            if (_editing == null) return;

            lblFormTitle.Text  = "Edit Store";
            txtName.Text       = _editing.StoreName;
            txtDomain.Text     = _editing.StoreDomain;
            txtToken.Text      = "";   // never pre-fill token for security
            txtToken.PlaceholderText = "Leave blank to keep existing token";
            btnSave.Text       = "Update Store";
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            var name   = txtName.Text.Trim();
            var domain = txtDomain.Text.Trim();
            var token  = txtToken.Text.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(domain))
            {
                MessageBox.Show(this, "Store name and domain are required.", "Missing Fields",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                int savedId;
                if (_editing == null)
                {
                    // Adding new store
                    if (string.IsNullOrEmpty(token))
                    {
                        MessageBox.Show(this, "API token is required when adding a new store.", "Missing Token",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    savedId = _repo.Add(name, domain, token).StoreID;
                }
                else
                {
                    // Updating existing store
                    savedId = _editing.StoreID;
                    _repo.Update(_editing.StoreID, name, domain, string.IsNullOrEmpty(token) ? null : token);
                }

                ClearForm();
                LoadStores();

                // Select the saved store in the grid so the user can immediately click Open
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.Cells["StoreID"].Value is int id && id == savedId)
                    {
                        row.Selected = true;
                        grid.FirstDisplayedScrollingRowIndex = row.Index;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not save store:\n\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRemove_Click(object? sender, EventArgs e)
        {
            if (grid.SelectedRows.Count == 0) return;

            var storeName = grid.SelectedRows[0].Cells["StoreName"].Value?.ToString();
            var confirm   = MessageBox.Show(this,
                $"Remove store '{storeName}'? This cannot be undone.",
                "Confirm Remove", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            try
            {
                var storeId = Convert.ToInt32(grid.SelectedRows[0].Cells["StoreID"].Value);
                _repo.Delete(storeId);
                ClearForm();
                LoadStores();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not remove store:\n\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnOpen_Click(object? sender, EventArgs e)
        {
            try
            {
                if (grid.SelectedRows.Count == 0)
                {
                    MessageBox.Show(this, "Please select a store first.", "No Selection",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var cellValue = grid.SelectedRows[0].Cells["StoreID"].Value;
                if (cellValue == null)
                {
                    MessageBox.Show(this, "Could not read store ID from selection.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var storeId   = Convert.ToInt32(cellValue);
                var allStores = _repo.GetAll().ToList();
                var selected  = allStores.FirstOrDefault(s => s.StoreID == storeId);

                if (selected == null)
                {
                    MessageBox.Show(this, $"Store ID {storeId} not found in database.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (string.IsNullOrEmpty(selected.Token))
                {
                    MessageBox.Show(this,
                        $"No API token found for '{selected.StoreName}'.\nSelect the store, enter a new token, and click Update Store.",
                        "Missing Token", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using var dash = new FormSalesDash(selected, allStores);
                dash.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error opening sales dashboard:\n\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnTest_Click(object? sender, EventArgs e)
        {
            var store = txtDomain.Text?.Trim() ?? "";
            var token = txtToken.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(store) || string.IsNullOrEmpty(token))
            {
                MessageBox.Show(this, "Enter store domain and token first.", "Missing Fields",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnTest.Enabled = false;
            btnTest.Text    = "Testing...";

            try
            {
                using var http = new System.Net.Http.HttpClient();
                http.DefaultRequestHeaders.Add("X-Shopify-Access-Token", token);

                var host = store.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(store).Host : store.TrimEnd('/');

                var response = await http.GetAsync(
                    $"https://{host}/admin/api/2024-10/shop.json");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var shopName = doc.RootElement
                                      .GetProperty("shop")
                                      .GetProperty("name")
                                      .GetString();

                    // Pre-fill the store name field if it's empty
                    if (string.IsNullOrWhiteSpace(txtName.Text) && !string.IsNullOrEmpty(shopName))
                        txtName.Text = shopName;

                    MessageBox.Show(this,
                        $"Connected! Shop: {shopName}\n\nClick '{btnSave.Text}' to save this store.",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(this,
                        $"Failed — {(int)response.StatusCode} {response.ReasonPhrase}\n\nCheck your token and store domain.",
                        "Authentication Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error: {ex.Message}", "Connection Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnTest.Enabled = true;
                btnTest.Text    = "Test Connection";
            }
        }

        private void ClearForm()
        {
            _editing           = null;
            lblFormTitle.Text  = "Add Store";
            txtName.Text       = "";
            txtDomain.Text     = "";
            txtToken.Text      = "";
            txtToken.PlaceholderText = "shpat_...";
            btnSave.Text       = "Add Store";
            grid.ClearSelection();
            btnOpen.Enabled    = false;
            btnRemove.Enabled  = false;
        }
    }
}
