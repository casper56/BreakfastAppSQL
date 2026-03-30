using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using Dapper;

namespace BreakfastApp
{
    public class CustomerSelectForm : Form
    {
        private string ConnectionString = @"Server=.\SQL2022;Database=BreakfastDB;User Id=sa;Password=1qaz@wsx;TrustServerCertificate=True;";
        private DataGridView dgvCustomers;
        private TextBox txtSearch;
        private Button btnSelect, btnCancel, btnNoCustomer;

        public Customer SelectedCustomer { get; private set; }

        public CustomerSelectForm()
        {
            this.Text = "選擇客戶";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Microsoft JhengHei", 10);

            SetupUI();
            LoadCustomerData();
        }

        private void SetupUI()
        {
            TableLayoutPanel tlp = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));

            // 搜尋區
            Panel pnlSearch = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            pnlSearch.Controls.Add(new Label { Text = "搜尋姓名/手機:", Location = new Point(10, 13), AutoSize = true });
            txtSearch = new TextBox { Location = new Point(120, 10), Width = 200 };
            txtSearch.TextChanged += (s, e) => LoadCustomerData(txtSearch.Text);
            pnlSearch.Controls.Add(txtSearch);
            tlp.Controls.Add(pnlSearch, 0, 0);

            // 列表區
            dgvCustomers = new DataGridView
            {
                Dock = DockStyle.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                BackgroundColor = Color.White,
                RowHeadersVisible = false
            };
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "姓名", Width = 120 });
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Mobile", HeaderText = "手機", Width = 150 });
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CustomerLevel", HeaderText = "等級", Width = 80 });
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "City", HeaderText = "城市", Width = 80 });
            dgvCustomers.Columns[dgvCustomers.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            dgvCustomers.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) SelectAndClose(); };
            tlp.Controls.Add(dgvCustomers, 0, 1);

            // 按鈕區
            FlowLayoutPanel pnlButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
            btnCancel = new Button { Text = "取消", Width = 80, Height = 35 };
            btnSelect = new Button { Text = "選擇", Width = 80, Height = 35, BackColor = Color.LightBlue };
            btnNoCustomer = new Button { Text = "不使用客戶", Width = 120, Height = 35 };

            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
            btnSelect.Click += (s, e) => SelectAndClose();
            btnNoCustomer.Click += (s, e) => { SelectedCustomer = null; this.DialogResult = DialogResult.OK; };

            pnlButtons.Controls.Add(btnCancel);
            pnlButtons.Controls.Add(btnSelect);
            pnlButtons.Controls.Add(btnNoCustomer);
            tlp.Controls.Add(pnlButtons, 0, 2);

            this.Controls.Add(tlp);
        }

        private void LoadCustomerData(string keyword = "")
        {
            try
            {
                using (var db = new SqlConnection(ConnectionString))
                {
                    string sql = "SELECT * FROM Customers WHERE (Name LIKE @Keyword OR Mobile LIKE @Keyword) AND Status = 1 ORDER BY Name";
                    var list = db.Query<Customer>(sql, new { Keyword = $"%{keyword}%" }).ToList();
                    dgvCustomers.DataSource = list;
                }
            }
            catch (Exception ex) { MessageBox.Show("載入客戶失敗: " + ex.Message); }
        }

        private void SelectAndClose()
        {
            if (dgvCustomers.SelectedRows.Count > 0)
            {
                SelectedCustomer = (Customer)dgvCustomers.SelectedRows[0].DataBoundItem;
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                MessageBox.Show("請選擇一位客戶");
            }
        }
    }
}
