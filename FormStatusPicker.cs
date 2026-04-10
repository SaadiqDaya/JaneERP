namespace JaneERP
{
    /// <summary>Small dialog to pick an order status (Draft, Live, WIP, Complete).</summary>
    internal class FormStatusPicker : Form
    {
        public string ChosenStatus { get; private set; } = "";

        public FormStatusPicker(string currentStatus)
        {
            Text          = "Change Order Status";
            ClientSize    = new Size(300, 210);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox   = false;
            MinimizeBox   = false;

            int y = 20;
            Controls.Add(new Label
            {
                Text      = $"Current status: {currentStatus}",
                Location  = new Point(16, y),
                AutoSize  = true,
                ForeColor = Color.Gray
            });
            y += 30;

            Controls.Add(new Label { Text = "New status:", Location = new Point(16, y), AutoSize = true });
            y += 20;

            foreach (var status in new[] { "Draft", "Live", "WIP", "Complete" })
            {
                var btn = new Button
                {
                    Text     = status,
                    Size     = new Size(260, 28),
                    Location = new Point(16, y)
                };
                var captured = status;
                btn.Click += (_, _) => { ChosenStatus = captured; DialogResult = DialogResult.OK; Close(); };
                Controls.Add(btn);
                y += 34;
            }

            Theme.Apply(this);
        }
    }
}
