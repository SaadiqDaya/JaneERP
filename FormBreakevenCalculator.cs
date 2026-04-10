using JaneERP.Logging;
using JaneERP.Security;

namespace JaneERP
{
    /// <summary>
    /// Breakeven calculator — enter unit qty, unit cost, other direct costs, overhead,
    /// and an optional target selling price to see total cost, cost per unit, gross margin,
    /// profit per unit and total profit. Auto-recalculates on every input change.
    /// Includes a "Solve for" mode that disables one input and back-calculates it
    /// from a target total cost.
    /// </summary>
    public class FormBreakevenCalculator : Form
    {
        // ── Inputs ────────────────────────────────────────────────────────────
        private NumericUpDown nudUnitQty        = new();
        private TextBox       txtUnitCost       = new();
        private TextBox       txtOtherCosts     = new();
        private TextBox       txtOverhead       = new();
        private TextBox       txtTargetPrice    = new();

        // ── Solve-for controls ────────────────────────────────────────────────
        private ComboBox      cboSolveFor       = new();
        private TextBox       txtTargetTotal    = new();
        private Label         lblSolvedValue    = new();
        private Panel         pnlSolveRow       = new();

        // ── Output labels ─────────────────────────────────────────────────────
        private Label lblTotalCostVal     = new();
        private Label lblCostPerUnitVal   = new();
        private Label lblBreakevenVal     = new();
        private Label lblGrossMarginVal   = new();
        private Label lblProfitPerUnitVal = new();
        private Label lblTotalProfitVal   = new();

        // ── Buttons ───────────────────────────────────────────────────────────
        private Button btnCalculate = new();
        private Button btnClear     = new();

        // Suppress re-entrant recalc while we programmatically fill the solved field
        private bool _recalcSuppressed;

        public FormBreakevenCalculator()
        {
            BuildUI();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Theme.AddCloseButton(this);
            Theme.MakeResizable(this);

            AppLogger.Audit(
                AppSession.CurrentUser?.Username ?? "system",
                "OpenBreakevenCalc", "");
        }

        // ─────────────────────────────────────────────────────────────────────
        // UI construction
        // ─────────────────────────────────────────────────────────────────────
        private void BuildUI()
        {
            Text          = "Breakeven Calculator";
            ClientSize    = new Size(520, 600);
            MinimumSize   = new Size(480, 560);
            StartPosition = FormStartPosition.CenterParent;

            // ── Header ───────────────────────────────────────────────────────
            var pnlHeader = new Panel
            {
                Tag       = "header",
                Dock      = DockStyle.Top,
                Height    = 52,
                BackColor = Theme.Header
            };
            var lblTitle = new Label
            {
                Text      = "Breakeven Calculator",
                Font      = new Font("Segoe UI", 15F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                AutoSize  = false,
                Size      = new Size(400, 36),
                Location  = new Point(14, 8),
                BackColor = Color.Transparent
            };
            pnlHeader.Controls.Add(lblTitle);

            // ── Scroll panel (holds everything below header) ──────────────────
            var pnlScroll = new Panel
            {
                AutoScroll = true,
                Dock       = DockStyle.Fill
            };

            // ── Content panel (fixed width, inside scroll) ────────────────────
            var pnl = new Panel
            {
                Location  = new Point(0, 0),
                Size      = new Size(500, 560),
                BackColor = Color.Transparent
            };

            int y = 16;
            const int ctlX  = 190;
            const int ctlW  = 200;
            const int rowH  = 36;
            const int secGap = 14;

            // ── Section: Inputs ───────────────────────────────────────────────
            pnl.Controls.Add(MakeSectionLabel("INPUTS", ref y));

            pnl.Controls.Add(MakeLabel("Unit Quantity:",  y));
            nudUnitQty = new NumericUpDown
            {
                Location  = new Point(ctlX, y),
                Size      = new Size(ctlW, 24),
                Minimum   = 0,
                Maximum   = 1_000_000,
                Value     = 100,
                DecimalPlaces = 0,
                BackColor = Theme.InputBg,
                ForeColor = Theme.TextPrimary
            };
            nudUnitQty.ValueChanged += (_, _) => Recalculate();
            pnl.Controls.Add(nudUnitQty);
            y += rowH;

            pnl.Controls.Add(MakeLabel("Unit Cost (R):",  y));
            txtUnitCost = MakeTextBox(ctlX, y, ctlW);
            pnl.Controls.Add(txtUnitCost);
            y += rowH;

            pnl.Controls.Add(MakeLabel("Other Direct Costs (R):", y));
            txtOtherCosts = MakeTextBox(ctlX, y, ctlW);
            pnl.Controls.Add(txtOtherCosts);
            y += rowH;

            pnl.Controls.Add(MakeLabel("Overhead (R):", y));
            txtOverhead = MakeTextBox(ctlX, y, ctlW);
            pnl.Controls.Add(txtOverhead);
            y += rowH;

            pnl.Controls.Add(MakeLabel("Target Selling Price (R):", y));
            txtTargetPrice = MakeTextBox(ctlX, y, ctlW);
            pnl.Controls.Add(txtTargetPrice);
            y += rowH + secGap;

            // ── Section: Solve for ────────────────────────────────────────────
            pnl.Controls.Add(MakeSectionLabel("SOLVE FOR (OPTIONAL)", ref y));

            pnl.Controls.Add(MakeLabel("Solve for:", y));
            cboSolveFor = new ComboBox
            {
                Location      = new Point(ctlX, y),
                Size          = new Size(ctlW, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = Theme.InputBg,
                ForeColor     = Theme.TextPrimary,
                FlatStyle     = FlatStyle.Flat
            };
            cboSolveFor.Items.AddRange(new object[]
            {
                "None", "Unit Cost", "Other Direct Costs", "Overhead"
            });
            cboSolveFor.SelectedIndex = 0;
            cboSolveFor.SelectedIndexChanged += CboSolveFor_Changed;
            pnl.Controls.Add(cboSolveFor);
            y += rowH;

            // Target total cost + solved value — shown when solve-for != None
            pnlSolveRow = new Panel
            {
                Location  = new Point(0, y),
                Size      = new Size(500, rowH * 2),
                Visible   = false,
                BackColor = Color.Transparent
            };

            pnlSolveRow.Controls.Add(MakeLabel("Target Total Cost (R):", 0));
            txtTargetTotal = MakeTextBox(ctlX, 0, ctlW);
            pnlSolveRow.Controls.Add(txtTargetTotal);

            pnlSolveRow.Controls.Add(MakeLabel("Solved Value:", rowH));
            lblSolvedValue = new Label
            {
                Location  = new Point(ctlX, rowH),
                Size      = new Size(ctlW, 22),
                Font      = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                BackColor = Color.Transparent,
                Text      = "—"
            };
            pnlSolveRow.Controls.Add(lblSolvedValue);
            pnl.Controls.Add(pnlSolveRow);
            y += rowH * 2 + secGap;

            // ── Separator ─────────────────────────────────────────────────────
            var sep = new Panel
            {
                Location  = new Point(0, y),
                Size      = new Size(490, 1),
                BackColor = Theme.Border
            };
            pnl.Controls.Add(sep);
            y += 10;

            // ── Section: Outputs ──────────────────────────────────────────────
            pnl.Controls.Add(MakeSectionLabel("RESULTS", ref y));

            (lblTotalCostVal,     y) = AddOutputRow(pnl, "Total Cost (R):",         y, ctlX);
            (lblCostPerUnitVal,   y) = AddOutputRow(pnl, "Cost Per Unit (R):",       y, ctlX);
            (lblBreakevenVal,     y) = AddOutputRow(pnl, "Breakeven Price/Unit (R):", y, ctlX);
            (lblGrossMarginVal,   y) = AddOutputRow(pnl, "Gross Margin:",             y, ctlX);
            (lblProfitPerUnitVal, y) = AddOutputRow(pnl, "Profit Per Unit (R):",     y, ctlX);
            (lblTotalProfitVal,   y) = AddOutputRow(pnl, "Total Profit (R):",        y, ctlX);

            y += secGap;

            // ── Separator ─────────────────────────────────────────────────────
            var sep2 = new Panel
            {
                Location  = new Point(0, y),
                Size      = new Size(490, 1),
                BackColor = Theme.Border
            };
            pnl.Controls.Add(sep2);
            y += 10;

            // ── Buttons ───────────────────────────────────────────────────────
            btnCalculate = new Button
            {
                Text     = "Calculate",
                Size     = new Size(120, 34),
                Location = new Point(ctlX, y),
                Font     = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            Theme.StyleButton(btnCalculate);
            btnCalculate.Click += (_, _) => Recalculate();

            btnClear = new Button
            {
                Text     = "Clear",
                Size     = new Size(80, 34),
                Location = new Point(ctlX + 128, y),
                Font     = new Font("Segoe UI", 10F)
            };
            Theme.StyleSecondaryButton(btnClear);
            btnClear.Click += BtnClear_Click;

            pnl.Controls.Add(btnCalculate);
            pnl.Controls.Add(btnClear);

            y += 44;
            pnl.Height = y + 16;

            pnlScroll.Controls.Add(pnl);

            Controls.Add(pnlHeader);
            Controls.Add(pnlScroll);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helper: section heading
        // ─────────────────────────────────────────────────────────────────────
        private static Label MakeSectionLabel(string text, ref int y)
        {
            var lbl = new Label
            {
                Text      = text,
                Location  = new Point(14, y),
                Size      = new Size(460, 20),
                Font      = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Theme.TextMuted,
                BackColor = Color.Transparent
            };
            y += 24;
            return lbl;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helper: row label
        // ─────────────────────────────────────────────────────────────────────
        private static Label MakeLabel(string text, int y)
            => new Label
            {
                Text      = text,
                Location  = new Point(14, y + 3),
                Size      = new Size(172, 20),
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight
            };

        // ─────────────────────────────────────────────────────────────────────
        // Helper: input TextBox
        // ─────────────────────────────────────────────────────────────────────
        private TextBox MakeTextBox(int x, int y, int w)
        {
            var txt = new TextBox
            {
                Location    = new Point(x, y),
                Size        = new Size(w, 24),
                BackColor   = Theme.InputBg,
                ForeColor   = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Text        = "0"
            };
            txt.TextChanged += (_, _) => Recalculate();
            return txt;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helper: output row (label + value label)
        // ─────────────────────────────────────────────────────────────────────
        private static (Label val, int nextY) AddOutputRow(Panel parent, string caption, int y, int ctlX)
        {
            parent.Controls.Add(new Label
            {
                Text      = caption,
                Location  = new Point(14, y + 3),
                Size      = new Size(172, 20),
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Theme.TextSecondary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight
            });

            var val = new Label
            {
                Text      = "—",
                Location  = new Point(ctlX, y + 3),
                Size      = new Size(220, 20),
                Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Theme.TextPrimary,
                BackColor = Color.Transparent
            };
            parent.Controls.Add(val);
            return (val, y + 32);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Solve-for combo changed
        // ─────────────────────────────────────────────────────────────────────
        private void CboSolveFor_Changed(object? sender, EventArgs e)
        {
            bool solving = cboSolveFor.SelectedIndex > 0;
            pnlSolveRow.Visible = solving;

            // Re-enable all inputs, then disable the chosen one
            txtUnitCost.Enabled   = true;
            txtOtherCosts.Enabled = true;
            txtOverhead.Enabled   = true;

            if (solving)
            {
                switch (cboSolveFor.SelectedItem?.ToString())
                {
                    case "Unit Cost":          txtUnitCost.Enabled   = false; break;
                    case "Other Direct Costs": txtOtherCosts.Enabled = false; break;
                    case "Overhead":           txtOverhead.Enabled   = false; break;
                }
            }

            Recalculate();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Core calculation
        // ─────────────────────────────────────────────────────────────────────
        private void Recalculate()
        {
            if (_recalcSuppressed) return;

            decimal unitQty    = (decimal)nudUnitQty.Value;
            decimal unitCost   = ParseDecimal(txtUnitCost.Text);
            decimal otherCosts = ParseDecimal(txtOtherCosts.Text);
            decimal overhead   = ParseDecimal(txtOverhead.Text);
            decimal targetPrice = ParseDecimal(txtTargetPrice.Text);

            // Solve for missing field
            string solveFor = cboSolveFor.SelectedItem?.ToString() ?? "None";
            if (solveFor != "None" && pnlSolveRow.Visible)
            {
                decimal targetTotal = ParseDecimal(txtTargetTotal.Text);
                decimal solved;
                bool    ok = false;

                switch (solveFor)
                {
                    case "Unit Cost":
                        if (unitQty > 0)
                        {
                            solved = (targetTotal - otherCosts - overhead) / unitQty;
                            unitCost = solved;
                            ok = true;
                            ShowSolved(solved, "R");
                        }
                        break;

                    case "Other Direct Costs":
                        solved = targetTotal - (unitQty * unitCost) - overhead;
                        otherCosts = solved;
                        ok = true;
                        ShowSolved(solved, "R");
                        break;

                    case "Overhead":
                        solved = targetTotal - (unitQty * unitCost) - otherCosts;
                        overhead = solved;
                        ok = true;
                        ShowSolved(solved, "R");
                        break;
                }

                if (!ok) lblSolvedValue.Text = "—";
            }

            // Derived values
            decimal totalCost   = (unitQty * unitCost) + otherCosts + overhead;
            decimal costPerUnit = unitQty > 0 ? totalCost / unitQty : 0;

            lblTotalCostVal.Text   = Fmt(totalCost);
            lblCostPerUnitVal.Text = unitQty > 0 ? Fmt(costPerUnit) : "—";
            lblBreakevenVal.Text   = unitQty > 0 ? Fmt(costPerUnit) : "—";

            if (targetPrice > 0 && unitQty > 0)
            {
                decimal profitPerUnit = targetPrice - costPerUnit;
                decimal grossMargin   = (profitPerUnit / targetPrice) * 100m;
                decimal totalProfit   = profitPerUnit * unitQty;

                lblGrossMarginVal.Text   = $"{grossMargin:F1} %";
                lblProfitPerUnitVal.Text = Fmt(profitPerUnit);
                lblTotalProfitVal.Text   = Fmt(totalProfit);

                // Colour code margin
                lblGrossMarginVal.ForeColor = grossMargin >= 0 ? Theme.Teal : Theme.Danger;
                lblProfitPerUnitVal.ForeColor = profitPerUnit >= 0 ? Theme.TextPrimary : Theme.Danger;
                lblTotalProfitVal.ForeColor   = totalProfit >= 0 ? Theme.TextPrimary : Theme.Danger;
            }
            else
            {
                lblGrossMarginVal.Text       = "—";
                lblProfitPerUnitVal.Text     = "—";
                lblTotalProfitVal.Text       = "—";
                lblGrossMarginVal.ForeColor  = Theme.TextPrimary;
                lblProfitPerUnitVal.ForeColor = Theme.TextPrimary;
                lblTotalProfitVal.ForeColor  = Theme.TextPrimary;
            }
        }

        private void ShowSolved(decimal value, string prefix)
            => lblSolvedValue.Text = $"{prefix} {value:N2}";

        // ─────────────────────────────────────────────────────────────────────
        // Clear button
        // ─────────────────────────────────────────────────────────────────────
        private void BtnClear_Click(object? sender, EventArgs e)
        {
            _recalcSuppressed = true;
            nudUnitQty.Value     = 100;
            txtUnitCost.Text     = "0";
            txtOtherCosts.Text   = "0";
            txtOverhead.Text     = "0";
            txtTargetPrice.Text  = "0";
            txtTargetTotal.Text  = "0";
            cboSolveFor.SelectedIndex = 0;
            _recalcSuppressed = false;
            Recalculate();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────
        private static decimal ParseDecimal(string s)
            => decimal.TryParse(s.Trim(), out var v) ? v : 0m;

        private static string Fmt(decimal v)
            => v.ToString("N2");
    }
}
