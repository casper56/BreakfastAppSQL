using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;

namespace BreakfastApp
{
    public class OrderHistoryForm : Form
    {
        private OrderService _orderService;
        private DataGridView dgvOrders;
        private TextBox txtSearch;

        public OrderHistoryForm(OrderService orderService)
        {
            _orderService = orderService;
            this.Text = "歷史訂單查詢";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Microsoft JhengHei", 10);

            var layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.RowCount = 2;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            this.Controls.Add(layout);

            // 搜尋區
            var pnlSearch = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            pnlSearch.Controls.Add(new Label { Text = "訂單編號搜尋:", AutoSize = true, Margin = new Padding(0, 5, 0, 0) });
            txtSearch = new TextBox { Width = 150 };
            txtSearch.TextChanged += (s, e) => LoadGrid();
            pnlSearch.Controls.Add(txtSearch);
            
            var btnToday = new Button { Text = "📊 今日營收統計", AutoSize = true, BackColor = Color.LightGreen, FlatStyle = FlatStyle.Flat };
            btnToday.Click += (s, e) => ShowDailySummary();
            pnlSearch.Controls.Add(btnToday);

            layout.Controls.Add(pnlSearch, 0, 0);

            // 訂單列表
            dgvOrders = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = true,
                RowHeadersWidth = 30,
                BackgroundColor = Color.White
            };
            dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "OrderId", HeaderText = "訂單編號", Width = 150 });
            dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CustomerId", HeaderText = "客戶ID", Width = 80 }); // 新增客戶ID欄位
            dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Timestamp", HeaderText = "時間", Width = 180 });
            dgvOrders.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TotalAmount", HeaderText = "總金額", Width = 80 });
            
            var btnPrintCol = new DataGridViewButtonColumn 
            {
                HeaderText = "列印", 
                Text = "預覽收據", 
                UseColumnTextForButtonValue = true, 
                Width = 100 
            };
            dgvOrders.Columns.Add(btnPrintCol);

            // 支援 Delete 鍵刪除訂單
            dgvOrders.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Delete && dgvOrders.SelectedRows.Count > 0)
                {
                    if (MessageBox.Show("確定要刪除選取的訂單嗎？這將無法復原。", "確認刪除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        foreach (DataGridViewRow row in dgvOrders.SelectedRows)
                        {
                            if (row.DataBoundItem is Order order) _orderService.AllOrders.Remove(order);
                        }
                        _orderService.SaveOrders();
                        LoadGrid();
                    }
                }
            };

            dgvOrders.CellContentClick += (s, e) => {
                if (e.RowIndex >= 0 && e.ColumnIndex == dgvOrders.Columns.Count - 1)
                {
                    var order = dgvOrders.Rows[e.RowIndex].DataBoundItem as Order;
                    if (order != null) PrintService.PreviewReceipt(order);
                }
            };

            layout.Controls.Add(dgvOrders, 0, 1);
            LoadGrid();
        }

        private void ShowDailySummary()
        {
            string date = DateTime.Now.ToString("yyyy/MM/dd");
            var todayOrders = _orderService.AllOrders.Where(o => o.Timestamp.Date == DateTime.Today).ToList();
            int total = todayOrders.Sum(o => o.TotalAmount);
            
            MessageBox.Show($"今日 ({date}) 統計：\n\n總訂單數：{todayOrders.Count}\n總營收金額：${total}", "營收統計");
        }

        private void LoadGrid()
        {
            dgvOrders.DataSource = null;
            dgvOrders.DataSource = _orderService.SearchOrders(txtSearch.Text).OrderByDescending(o => o.OrderId).ToList();
        }
    }
}
