using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using Dapper;

namespace BreakfastApp
{
    public class CustomerForm : Form
    {
        private string ConnectionString = @"Server=.\SQL2022;Database=BreakfastDB;User Id=sa;Password=1qaz@wsx;TrustServerCertificate=True;";
        private Dictionary<string, Dictionary<string, List<string>>> _addressData = new Dictionary<string, Dictionary<string, List<string>>>();

        private DataGridView dgvCustomers;
        private TextBox txtName, txtTaxID, txtContact, txtMobile, txtEmail, txtSubStreet, txtHouseNum, txtFloor;
        private ComboBox cmbCity, cmbDistrict, cmbStreet, cmbLevel;
        private CheckBox chkStatus;
        private Button btnAdd, btnUpdate, btnDelete, btnClear;
        private TextBox txtSearch;

        public CustomerForm()
        {
            this.Text = "客戶資料維護 (支援上下拉動測試)";
            this.Size = new Size(1100, 800); // 放大視窗
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Microsoft JhengHei", 9);

            _addressData = DbService.LoadAddressData();
            SetupUI();
            LoadCustomerData();
        }

        private void SetupUI()
        {
            SplitContainer split = new SplitContainer { 
                Dock = DockStyle.Fill, 
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6,
                BorderStyle = BorderStyle.Fixed3D
            };
            this.Controls.Add(split);
            
            // 強制設定分隔線位置在 200 (往下調 50)，平衡編輯區與列表空間
            split.SplitterDistance = 200; 
            split.FixedPanel = FixedPanel.Panel1; // 固定上方高度，視窗放大時下方列表跟著變大

            // --- 上方編輯區 (Panel1) ---
            GroupBox gbEdit = new GroupBox { Text = "客戶詳情編輯", Dock = DockStyle.Fill, Padding = new Padding(10) };
            split.Panel1.Controls.Add(gbEdit);

            TableLayoutPanel tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5 };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

            int row = 0;
            tlp.Controls.Add(new Label { Text = "名稱:", Anchor = AnchorStyles.Right }, 0, row);
            txtName = new TextBox { Dock = DockStyle.Fill };
            tlp.Controls.Add(txtName, 1, row);
            tlp.Controls.Add(new Label { Text = "統一編號:", Anchor = AnchorStyles.Right }, 2, row);
            txtTaxID = new TextBox { Dock = DockStyle.Fill };
            tlp.Controls.Add(txtTaxID, 3, row);

            row++;
            tlp.Controls.Add(new Label { Text = "聯絡人:", Anchor = AnchorStyles.Right }, 0, row);
            txtContact = new TextBox { Dock = DockStyle.Fill };
            tlp.Controls.Add(txtContact, 1, row);
            tlp.Controls.Add(new Label { Text = "手機:", Anchor = AnchorStyles.Right }, 2, row);
            txtMobile = new TextBox { Dock = DockStyle.Fill };
            tlp.Controls.Add(txtMobile, 3, row);

            row++;
            tlp.Controls.Add(new Label { Text = "Email:", Anchor = AnchorStyles.Right }, 0, row);
            txtEmail = new TextBox { Dock = DockStyle.Fill };
            tlp.Controls.Add(txtEmail, 1, row);
            tlp.Controls.Add(new Label { Text = "等級:", Anchor = AnchorStyles.Right }, 2, row);
            cmbLevel = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbLevel.Items.AddRange(new string[] { "一般", "VIP", "黑名單" });
            cmbLevel.SelectedIndex = 0;
            tlp.Controls.Add(cmbLevel, 3, row);

            row++;
            tlp.Controls.Add(new Label { Text = "地址:", Anchor = AnchorStyles.Right }, 0, row);
            FlowLayoutPanel pnlAddress = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            cmbCity = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbDistrict = new ComboBox { Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbStreet = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDown };
            pnlAddress.Controls.Add(cmbCity);
            pnlAddress.Controls.Add(cmbDistrict);
            pnlAddress.Controls.Add(cmbStreet);
            tlp.Controls.Add(pnlAddress, 1, row);

            FlowLayoutPanel pnlAddress2 = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            txtSubStreet = new TextBox { Width = 80, PlaceholderText = "巷/弄" };
            txtHouseNum = new TextBox { Width = 60, PlaceholderText = "號" };
            txtFloor = new TextBox { Width = 100, PlaceholderText = "樓層/其餘" };
            pnlAddress2.Controls.Add(txtSubStreet);
            pnlAddress2.Controls.Add(txtHouseNum);
            pnlAddress2.Controls.Add(txtFloor);
            tlp.Controls.Add(pnlAddress2, 3, row);

            row++;
            chkStatus = new CheckBox { Text = "啟用狀態", Checked = true };
            tlp.Controls.Add(chkStatus, 1, row);

            FlowLayoutPanel pnlButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            btnClear = new Button { Text = "清除", Width = 80 };
            btnDelete = new Button { Text = "刪除", Width = 80 };
            btnUpdate = new Button { Text = "修改", Width = 80 };
            btnAdd = new Button { Text = "新增", Width = 80 };
            pnlButtons.Controls.Add(btnClear);
            pnlButtons.Controls.Add(btnDelete);
            pnlButtons.Controls.Add(btnUpdate);
            pnlButtons.Controls.Add(btnAdd);
            tlp.Controls.Add(pnlButtons, 3, row);

            gbEdit.Controls.Add(tlp);

            // --- 下方列表區 (Panel2) ---
            Panel pnlGrid = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
            split.Panel2.Controls.Add(pnlGrid);

            Panel pnlSearch = new Panel { Dock = DockStyle.Top, Height = 40 };
            pnlSearch.Controls.Add(new Label { Text = "🔍 搜尋客戶:", Location = new Point(5, 10), AutoSize = true });
            txtSearch = new TextBox { Location = new Point(85, 7), Width = 250 };
            txtSearch.TextChanged += (s, e) => LoadCustomerData(txtSearch.Text);
            pnlSearch.Controls.Add(txtSearch);
            pnlGrid.Controls.Add(pnlSearch);

            dgvCustomers = new DataGridView { 
                Dock = DockStyle.Fill, 
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                BackgroundColor = Color.White,
                ColumnHeadersVisible = true,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 45, // 標高加大
                RowHeadersVisible = false,
                GridColor = Color.LightGray,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            // 設定標頭字體與顏色
            dgvCustomers.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(220, 220, 220);
            dgvCustomers.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            dgvCustomers.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft JhengHei", 10, FontStyle.Bold);
            
            // 防止標頭在點擊時變色 (維持原色)
            dgvCustomers.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 220, 220);
            dgvCustomers.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.Black;
            dgvCustomers.EnableHeadersVisualStyles = false;

            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "客戶名稱", Width = 150 });
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Mobile", HeaderText = "手機", Width = 130 });
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "City", HeaderText = "縣市", Width = 80 });
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "District", HeaderText = "行政區", Width = 100 });
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TaxID", HeaderText = "統編", Width = 100 });
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CustomerLevel", HeaderText = "等級", Width = 80 });
            dgvCustomers.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreateDate", HeaderText = "註冊日期", Width = 150 });
            dgvCustomers.Columns[dgvCustomers.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            pnlGrid.Controls.Add(dgvCustomers);
            dgvCustomers.BringToFront();

            // 地址連動事件... (保持原樣)
            cmbCity.Items.AddRange(_addressData.Keys.ToArray());
            cmbCity.SelectedIndexChanged += (s, e) => {
                cmbDistrict.Items.Clear();
                if (cmbCity.SelectedItem != null) {
                    var dists = _addressData[cmbCity.SelectedItem.ToString()!].Keys.ToArray();
                    cmbDistrict.Items.AddRange(dists);
                    if (cmbDistrict.Items.Count > 0) cmbDistrict.SelectedIndex = 0;
                }
            };
            cmbDistrict.SelectedIndexChanged += (s, e) => {
                cmbStreet.Items.Clear();
                if (cmbCity.SelectedItem != null && cmbDistrict.SelectedItem != null) {
                    var streets = _addressData[cmbCity.SelectedItem.ToString()!][cmbDistrict.SelectedItem.ToString()!];
                    cmbStreet.Items.AddRange(streets.ToArray());
                }
            };

            btnAdd.Click += (s, e) => SaveCustomer(true);
            btnUpdate.Click += (s, e) => SaveCustomer(false);
            btnDelete.Click += (s, e) => DeleteCustomer();
            btnClear.Click += (s, e) => ClearFields();
            dgvCustomers.CellClick += (s, e) => { if (e.RowIndex >= 0) BindSelectedCustomer(); };
        }

        private void LoadCustomerData(string keyword = "")
        {
            try
            {
                using (var db = new SqlConnection(ConnectionString))
                {
                    string sql = "SELECT * FROM Customers WHERE Name LIKE @Keyword OR Mobile LIKE @Keyword OR TaxID LIKE @Keyword ORDER BY CreateDate DESC";
                    var list = db.Query<Customer>(sql, new { Keyword = $"%{keyword}%" }).ToList();
                    dgvCustomers.DataSource = list;
                }
            } catch { }
        }

        private void BindSelectedCustomer()
        {
            if (dgvCustomers.SelectedRows.Count == 0) return;
            var c = (Customer)dgvCustomers.SelectedRows[0].DataBoundItem;
            
            txtName.Text = c.Name;
            txtTaxID.Text = c.TaxID;
            txtContact.Text = c.ContactPerson;
            txtMobile.Text = c.Mobile;
            txtEmail.Text = c.Email;
            cmbLevel.SelectedItem = c.CustomerLevel;
            cmbCity.SelectedItem = c.City;
            // 先觸發 City 改變事件
            if (cmbCity.SelectedItem != null)
            {
                cmbDistrict.SelectedItem = c.District;
                if (cmbDistrict.SelectedItem != null)
                {
                    cmbStreet.Text = c.Street; // 允許手動輸入的值
                }
            }
            txtSubStreet.Text = c.SubStreet;
            txtHouseNum.Text = c.HouseNumber;
            txtFloor.Text = c.Floor_Other;
            chkStatus.Checked = c.Status;

            dgvCustomers.Tag = c.CustomerID;
        }

        private void SaveCustomer(bool isNew)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text)) { MessageBox.Show("請輸入名稱"); return; }

            try
            {
                using (var db = new SqlConnection(ConnectionString))
                {
                    string sql;
                    var param = new {
                        Name = txtName.Text,
                        TaxID = txtTaxID.Text,
                        ContactPerson = txtContact.Text,
                        Mobile = txtMobile.Text,
                        Email = txtEmail.Text,
                                            City = cmbCity.SelectedItem?.ToString(),
                                            District = cmbDistrict.SelectedItem?.ToString(),
                                            Street = cmbStreet.Text,
                                            SubStreet = txtSubStreet.Text,
                        
                        HouseNumber = txtHouseNum.Text,
                        Floor_Other = txtFloor.Text,
                        CustomerLevel = cmbLevel.SelectedItem?.ToString(),
                        Status = chkStatus.Checked,
                        UpdateDate = DateTime.Now,
                        CustomerID = (int?)(dgvCustomers.Tag ?? 0)
                    };

                    if (isNew)
                    {
                        sql = @"INSERT INTO Customers (Name, TaxID, ContactPerson, Mobile, Email, City, District, Street, SubStreet, HouseNumber, Floor_Other, CustomerLevel, Status, CreateDate)
                                VALUES (@Name, @TaxID, @ContactPerson, @Mobile, @Email, @City, @District, @Street, @SubStreet, @HouseNumber, @Floor_Other, @CustomerLevel, @Status, GETDATE())";
                    }
                    else
                    {
                        if (dgvCustomers.Tag == null) return;
                        sql = @"UPDATE Customers SET Name=@Name, TaxID=@TaxID, ContactPerson=@ContactPerson, Mobile=@Mobile, Email=@Email, 
                                City=@City, District=@District, Street=@Street, SubStreet=@SubStreet, HouseNumber=@HouseNumber, 
                                Floor_Other=@Floor_Other, CustomerLevel=@CustomerLevel, Status=@Status, UpdateDate=@UpdateDate 
                                WHERE CustomerID=@CustomerID";
                    }

                    db.Execute(sql, param);
                    MessageBox.Show(isNew ? "新增成功" : "修改成功");
                    LoadCustomerData();
                    ClearFields();
                }
            }
            catch (Exception ex) { MessageBox.Show("儲存失敗: " + ex.Message); }
        }

        private void DeleteCustomer()
        {
            if (dgvCustomers.Tag == null) return;
            if (MessageBox.Show("確定要刪除嗎?", "詢問", MessageBoxButtons.YesNo) == DialogResult.No) return;

            try
            {
                using (var db = new SqlConnection(ConnectionString))
                {
                    db.Execute("DELETE FROM Customers WHERE CustomerID=@Id", new { Id = (int)dgvCustomers.Tag });
                    LoadCustomerData();
                    ClearFields();
                }
            } catch { }
        }

        private void ClearFields()
        {
            txtName.Clear(); txtTaxID.Clear(); txtContact.Clear(); txtMobile.Clear(); txtEmail.Clear();
            cmbStreet.Text = ""; txtSubStreet.Clear(); txtHouseNum.Clear(); txtFloor.Clear();
            cmbCity.SelectedIndex = -1; cmbDistrict.Items.Clear(); cmbStreet.Items.Clear();
            cmbLevel.SelectedIndex = 0; chkStatus.Checked = true;
            dgvCustomers.Tag = null;
        }
    }
}
