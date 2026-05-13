namespace JaneERP
{
    /// <summary>
    /// Minimal dialog for setting a cycle count schedule frequency on a location.
    /// FrequencyDays == 0 means "remove schedule".
    /// </summary>
    public class FormScheduleEditor : Form
    {
        /// <summary>The chosen frequency in days. 0 = clear schedule.</summary>
        public int FrequencyDays { get; private set; }

        private NumericUpDown nudDays = new();

        public FormScheduleEditor(string locationName, int currentDays)
        {
            BuildUI(locationName, currentDays);
            Theme.Apply(this);
            Theme.MakeBorderless(this);
        }

        private void BuildUI(string locationName, int currentDays)
        {
            Text            = "Set Count Schedule";
            ClientSize      = new Size(320, 170);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;

            Controls.Add(new Label
            {
                Text      = $"Location: {locationName}",
                Font      = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Theme.Gold,
                Location  = new Point(16, 16),
                Size      = new Size(288, 20),
                AutoSize  = false
            });

            Controls.Add(new Label
            {
                Text     = "Count every (days):",
                Location = new Point(16, 52),
                AutoSize = true
            });

            nudDays.Location = new Point(160, 49);
            nudDays.Size     = new Size(80, 23);
            nudDays.Minimum  = 0;
            nudDays.Maximum  = 365;
            nudDays.Value    = currentDays;
            Controls.Add(nudDays);

            Controls.Add(new Label
            {
                Text      = "Set to 0 to remove the schedule.",
                Location  = new Point(16, 80),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = Color.DimGray
            });

            var btnOK = new Button { Text = "Save", Size = new Size(80, 28), Location = new Point(120, 120) };
            btnOK.Click += (_, _) =>
            {
                FrequencyDays = (int)nudDays.Value;
                DialogResult  = DialogResult.OK;
                Close();
            };
            Controls.Add(btnOK);

            var btnCancel = new Button
            {
                Text         = "Cancel",
                Size         = new Size(80, 28),
                Location     = new Point(212, 120),
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(btnCancel);
            CancelButton = btnCancel;
        }
    }
}
