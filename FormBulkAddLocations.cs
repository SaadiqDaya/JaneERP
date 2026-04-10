using JaneERP.Data;

namespace JaneERP
{
    /// <summary>
    /// Wizard for bulk-generating structured location names.
    /// Pattern: {RoomPrefix}{room}{Sep}{UnitPrefix}{unit}{Sep}{ShelfPrefix}{shelf}{Sep}{BinPrefix}{bin}
    /// Example with defaults: R01-SU01-S01-B01
    /// Any level set to 0 units is skipped entirely.
    /// </summary>
    public class FormBulkAddLocations : Form
    {
        // Input controls
        private NumericUpDown nudRooms      = new();
        private NumericUpDown nudUnits      = new();
        private NumericUpDown nudShelves    = new();
        private NumericUpDown nudBins       = new();
        private NumericUpDown nudPadding    = new();
        private TextBox txtRoomPrefix   = new();
        private TextBox txtUnitPrefix   = new();
        private TextBox txtShelfPrefix  = new();
        private TextBox txtBinPrefix    = new();
        private TextBox txtSeparator    = new();

        // Output
        private TextBox txtPreview  = new();
        private Label   lblCount    = new();
        private Button  btnGenerate = new();
        private Button  btnCancel   = new();

        public FormBulkAddLocations()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            // Preview box stays readable in dark theme
            txtPreview.BackColor = Theme.InputBg;
            RefreshPreview();
        }

        private void BuildUI()
        {
            Text            = "Bulk Add Locations";
            ClientSize      = new Size(560, 560);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;

            int y = 14;

            var lblTitle = new Label
            {
                Text      = "Generate Structured Location Names",
                Font      = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(16, y),
                Size      = new Size(520, 26)
            };
            Controls.Add(lblTitle);
            y += 38;

            var lblSub = new Label
            {
                Text      = "Set any level to 0 to omit that level from the name.",
                ForeColor = Theme.TextSecondary,
                AutoSize  = true,
                Location  = new Point(16, y)
            };
            Controls.Add(lblSub);
            y += 28;

            // ── Grid header ──────────────────────────────────────────────────────
            AddRowHeader(y);
            y += 24;

            // ── Level rows ───────────────────────────────────────────────────────
            y = AddLevelRow(y, "Room",           "R",  ref nudRooms,   ref txtRoomPrefix,  10);
            y = AddLevelRow(y, "Shelving Unit",  "SU", ref nudUnits,   ref txtUnitPrefix,  4);
            y = AddLevelRow(y, "Shelf",          "S",  ref nudShelves, ref txtShelfPrefix, 5);
            y = AddLevelRow(y, "Bin",            "B",  ref nudBins,    ref txtBinPrefix,   0);

            y += 8;

            // ── Separator and padding ────────────────────────────────────────────
            AddField(y, "Separator between levels:", txtSeparator, "-", 80);
            txtSeparator.TextChanged += (_, _) => RefreshPreview();
            y += 40;

            var lblPad = new Label { AutoSize = true, Location = new Point(16, y), Text = "Number padding (digits):" };
            Controls.Add(lblPad);
            nudPadding.Location = new Point(220, y - 2);
            nudPadding.Size     = new Size(60, 23);
            nudPadding.Minimum  = 1; nudPadding.Maximum = 4; nudPadding.Value = 2;
            nudPadding.ValueChanged += (_, _) => RefreshPreview();
            Controls.Add(nudPadding);
            y += 38;

            // ── Preview ──────────────────────────────────────────────────────────
            var lblPreview = new Label { AutoSize = true, Location = new Point(16, y), Text = "Preview (first 8 names):" };
            Controls.Add(lblPreview);
            y += 20;

            txtPreview.Location   = new Point(16, y);
            txtPreview.Size       = new Size(524, 90);
            txtPreview.Multiline  = true;
            txtPreview.ReadOnly   = true;
            txtPreview.ScrollBars = ScrollBars.Vertical;
            Controls.Add(txtPreview);
            y += 100;

            lblCount.AutoSize = true;
            lblCount.Location = new Point(16, y);
            lblCount.ForeColor = Theme.Teal;
            Controls.Add(lblCount);
            y += 28;

            // ── Buttons ──────────────────────────────────────────────────────────
            btnGenerate.Location = new Point(220, y);
            btnGenerate.Size     = new Size(150, 32);
            btnGenerate.Text     = "Generate Locations";
            btnGenerate.Click   += BtnGenerate_Click;
            Controls.Add(btnGenerate);

            btnCancel.Location = new Point(380, y);
            btnCancel.Size     = new Size(90, 32);
            btnCancel.Text     = "Cancel";
            btnCancel.Click   += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);
        }

        private void AddRowHeader(int y)
        {
            void Hdr(string t, int x, int w)
            {
                Controls.Add(new Label
                {
                    Text      = t,
                    Font      = new Font("Segoe UI", 8F, FontStyle.Bold),
                    ForeColor = Theme.TextSecondary,
                    Location  = new Point(x, y),
                    Size      = new Size(w, 20)
                });
            }
            Hdr("Level",   16,  130);
            Hdr("Prefix", 156,  70);
            Hdr("Count",  236,  70);
        }

        private int AddLevelRow(int y, string label, string defaultPrefix,
                                ref NumericUpDown nud, ref TextBox txt, int defaultCount)
        {
            Controls.Add(new Label { AutoSize = true, Location = new Point(16, y + 3), Text = label });

            txt = new TextBox { Location = new Point(156, y), Size = new Size(60, 23), Text = defaultPrefix };
            txt.TextChanged += (_, _) => RefreshPreview();
            Controls.Add(txt);

            nud = new NumericUpDown { Location = new Point(236, y), Size = new Size(70, 23), Minimum = 0, Maximum = 999, Value = defaultCount };
            nud.ValueChanged += (_, _) => RefreshPreview();
            Controls.Add(nud);

            return y + 32;
        }

        private void AddField(int y, string lbl, TextBox txt, string defaultVal, int width)
        {
            Controls.Add(new Label { AutoSize = true, Location = new Point(16, y + 3), Text = lbl });
            txt.Location = new Point(220, y);
            txt.Size     = new Size(width, 23);
            txt.Text     = defaultVal;
            Controls.Add(txt);
        }

        private List<string> BuildNames(int previewOnly = 0)
        {
            var names = new List<string>();
            string sep  = txtSeparator.Text;
            int    pad  = (int)nudPadding.Value;
            int    rooms   = (int)nudRooms.Value;
            int    units   = (int)nudUnits.Value;
            int    shelves = (int)nudShelves.Value;
            int    bins    = (int)nudBins.Value;

            string rPfx = txtRoomPrefix.Text;
            string uPfx = txtUnitPrefix.Text;
            string sPfx = txtShelfPrefix.Text;
            string bPfx = txtBinPrefix.Text;

            string Fmt(int n) => n.ToString().PadLeft(pad, '0');

            int roomMax   = rooms   > 0 ? rooms   : 1;
            int unitMax   = units   > 0 ? units   : 1;
            int shelfMax  = shelves > 0 ? shelves : 1;
            int binMax    = bins    > 0 ? bins    : 1;

            for (int r = 1; r <= roomMax; r++)
            {
                for (int u = 1; u <= unitMax; u++)
                {
                    for (int s = 1; s <= shelfMax; s++)
                    {
                        for (int b = 1; b <= binMax; b++)
                        {
                            var parts = new List<string>();
                            if (rooms   > 0) parts.Add(rPfx + Fmt(r));
                            if (units   > 0) parts.Add(uPfx + Fmt(u));
                            if (shelves > 0) parts.Add(sPfx + Fmt(s));
                            if (bins    > 0) parts.Add(bPfx + Fmt(b));

                            if (parts.Count > 0)
                                names.Add(string.Join(sep, parts));

                            if (previewOnly > 0 && names.Count >= previewOnly)
                                return names;
                        }
                    }
                }
            }

            return names;
        }

        private void RefreshPreview()
        {
            var preview = BuildNames(8);
            int total   = (int)(
                Math.Max(nudRooms.Value,   1) *
                Math.Max(nudUnits.Value,   1) *
                Math.Max(nudShelves.Value, 1) *
                Math.Max(nudBins.Value,    1));

            // Subtract empty combinations where all counts are 0
            if (nudRooms.Value == 0 && nudUnits.Value == 0 &&
                nudShelves.Value == 0 && nudBins.Value == 0)
                total = 0;

            txtPreview.Text = string.Join(Environment.NewLine, preview) +
                              (total > 8 ? Environment.NewLine + "…" : "");
            lblCount.Text = total == 0
                ? "No locations will be created"
                : $"Will create {total:N0} location(s)";
        }

        private void BtnGenerate_Click(object? sender, EventArgs e)
        {
            var names = BuildNames();
            if (names.Count == 0)
            {
                MessageBox.Show(this, "Set at least one level to a count greater than 0.",
                    "Nothing to generate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show(this,
                    $"This will generate {names.Count:N0} location(s).\n\nContinue?",
                    "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            btnGenerate.Enabled = false;
            try
            {
                new LocationRepository().BulkAddLocations(names);
                MessageBox.Show(this,
                    $"{names.Count:N0} location(s) generated successfully.\nExisting names were skipped.",
                    "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Generation failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { btnGenerate.Enabled = true; }
        }
    }
}
