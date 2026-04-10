using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using JaneERP.Models;

namespace JaneERP
{
    public partial class MultiOrderDetailsForm : Form
    {
        private readonly List<AggregatedLineItem> _items;

        public MultiOrderDetailsForm(List<AggregatedLineItem> items)
        {
            _items = items ?? new List<AggregatedLineItem>();
            InitializeComponent();
            Theme.Apply(this);
            Theme.MakeBorderless(this);
            PopulateGrid();
        }

        private void PopulateGrid()
        {
            dgvCombined.AutoGenerateColumns = false;
            dgvCombined.Columns.Clear();

            dgvCombined.Columns.Add(new DataGridViewTextBoxColumn { Name = "OrderNumber", HeaderText = "Order", DataPropertyName = "OrderNumber", Width = 120 });
            dgvCombined.Columns.Add(new DataGridViewTextBoxColumn { Name = "PartNumber", HeaderText = "Part#", DataPropertyName = "PartNumber", Width = 120 });
            dgvCombined.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "Title", DataPropertyName = "Title", Width = 300 });
            dgvCombined.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "Qty", DataPropertyName = "Quantity", Width = 60 });
            dgvCombined.Columns.Add(new DataGridViewTextBoxColumn { Name = "Price", HeaderText = "Price", DataPropertyName = "Price", Width = 80 });

            dgvCombined.DataSource = _items.Select(i => new
            {
                OrderNumber = i.OrderNumber,
                PartNumber = i.PartNumber,
                Title = i.Title,
                Quantity = i.Quantity,
                Price = i.Price.ToString("F2", CultureInfo.InvariantCulture)
            }).ToList();
        }

        private void btnExport_Click(object? sender, EventArgs e)
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"selected_orders_{DateTime.UtcNow:yyyyMMddHHmmss}.csv"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var csv = BuildCsv(_items);
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

        private static string BuildCsv(List<AggregatedLineItem> items)
        {
            var sb = new StringBuilder();
            // header columns
            sb.AppendLine("Order,PartNumber,Title,Quantity,Price");
            foreach (var li in items)
            {
                sb.AppendLine($"{EscapeCsv(li.OrderNumber)},{EscapeCsv(li.PartNumber)},{EscapeCsv(li.Title)},{li.Quantity},{li.Price.ToString("F2", CultureInfo.InvariantCulture)}");
            }
            return sb.ToString();
        }
    }
}
