using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using JaneERP.Models;

namespace JaneERP
{
    public partial class OrderDetailsForm : Form
    {
        private readonly OrderDetails _details;

        public OrderDetailsForm(OrderDetails details)
        {
            _details = details ?? throw new ArgumentNullException(nameof(details));
            InitializeComponent();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            Populate();
        }

        private void Populate()
        {
            lblOrderNumber.Text = _details.Name ?? $"#{_details.OrderNumber}";
            lblDate.Text = _details.CreatedAt.ToString("u");
            lblCustomer.Text = _details.CustomerName ?? _details.ContactEmail ?? "—";

            // Style the email as a clickable mailto link when an email is present
            var email = _details.ContactEmail;
            if (!string.IsNullOrWhiteSpace(email))
            {
                lblEmail.Text            = email;
                lblEmail.ForeColor       = Theme.Teal;
                lblEmail.LinkColor       = Theme.Teal;
                lblEmail.ActiveLinkColor = Theme.Gold;
                lblEmail.LinkBehavior    = LinkBehavior.HoverUnderline;
                lblEmail.Click          += (_, _) =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName        = "mailto:" + email,
                            UseShellExecute = true
                        });
                    }
                    catch { /* ignore if no email client configured */ }
                };
            }
            else
            {
                lblEmail.Text      = "—";
                lblEmail.ForeColor = SystemColors.ControlText;
            }
            lblShipping.Text = _details.ShippingAddress ?? "—";
            lblTotal.Text = $"{_details.TotalPrice:N2} {_details.Currency}";

            dgvLineItems.AutoGenerateColumns = false;
            dgvLineItems.Columns.Clear();
            dgvLineItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "Title", DataPropertyName = "Title", Width = 300 });
            dgvLineItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "Qty", DataPropertyName = "Quantity", Width = 60 });
            dgvLineItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Price", HeaderText = "Price", DataPropertyName = "Price", Width = 80 });
            // new Part/sku column
            dgvLineItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "PartNumber", HeaderText = "Part#", DataPropertyName = "PartNumber", Width = 120 });

            dgvLineItems.DataSource = _details.LineItems.Select(li => new
            {
                li.Title,
                li.Quantity,
                Price = $"{li.Price:N2} {_details.Currency}",
                PartNumber = li.Sku
            }).ToList();
        }

        private void btnExport_Click(object? sender, EventArgs e)
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"{_details.Name ?? $"order_{_details.OrderNumber}"}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var csv = BuildCsv(_details);
                File.WriteAllText(dlg.FileName, csv, Encoding.UTF8);
                MessageBox.Show(this, "Export complete.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Export error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var v = value.Replace("\"", "\"\"");
            if (v.Contains(',') || v.Contains('"') || v.Contains('\n') || v.Contains('\r'))
                return $"\"{v}\"";
            return v;
        }

        private static string BuildCsv(OrderDetails d)
        {
            var sb = new StringBuilder();

            // Header block
            sb.AppendLine($"Order,{EscapeCsv(d.Name ?? d.OrderNumber.ToString())}");
            sb.AppendLine($"OrderNumber,{d.OrderNumber}");
            sb.AppendLine($"Date,{EscapeCsv(d.CreatedAt.ToString("u"))}");
            sb.AppendLine($"Customer,{EscapeCsv(d.CustomerName)}");
            sb.AppendLine($"Email,{EscapeCsv(d.ContactEmail)}");
            sb.AppendLine($"ShippingAddress,{EscapeCsv(d.ShippingAddress)}");
            sb.AppendLine($"Total,{d.TotalPrice.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine($"Currency,{EscapeCsv(d.Currency)}");
            sb.AppendLine(); // blank line

            // Line items header
            sb.AppendLine("Item Title,Quantity,Price,PartNumber,SKU");

            foreach (var li in d.LineItems)
            {
                var price = li.Price.ToString("F2", CultureInfo.InvariantCulture);
                sb.AppendLine($"{EscapeCsv(li.Title)},{li.Quantity},{price},{EscapeCsv(li.Sku)},{EscapeCsv(li.Sku)}");
            }

            return sb.ToString();
        }
    }
}
