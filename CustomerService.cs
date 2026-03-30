using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Linq;
using System.Text.Json;
using System.IO;

namespace BreakfastApp
{
    public class CustomerService
    {
        private string ConnectionString = @"Server=.\SQL2022;Database=BreakfastDB;User Id=sa;Password=1qaz@wsx;TrustServerCertificate=True;";

        private string[] LastNames = { "陳", "林", "李", "王", "張", "劉", "黃", "蔡", "吳", "鄭", "謝", "郭", "洪", "邱", "曾", "廖", "賴", "徐", "周", "葉" };
        private string[] FirstNames = { "嘉玲", "淑芬", "雅婷", "美玲", "惠珍", "志明", "俊傑", "家豪", "建宏", "冠宇", "欣怡", "宜君", "心怡", "曉薇", "建良", "正雄", "志強", "子豪", "夢瑤", "天宇" };
        private string[] Levels = { "一般", "VIP", "白金", "黃金" };
        private string[] PaymentMethods = { "信用卡付款", "電子支付", "現金" };

        public void SeedMockCustomers(int count)
        {
            try
            {
                // 1. 檢查是否已經有客戶
                using (var db = new SqlConnection(ConnectionString))
                {
                    db.Open();
                    int existingCount = db.ExecuteScalar<int>("SELECT COUNT(*) FROM Customers");
                    // 如果已經有資料，詢問是否要繼續產生 (避免誤刪或重複)
                    if (existingCount > 0)
                    {
                        var result = System.Windows.Forms.MessageBox.Show($"資料庫已有 {existingCount} 筆客戶，是否要再產生 {count} 筆模擬資料？", "提示", System.Windows.Forms.MessageBoxButtons.YesNo);
                        if (result == System.Windows.Forms.DialogResult.No) return;
                    }
                }

                // 2. 讀取區域資料 (加強路徑尋找)
                var cities = LoadAddressData();
                if (cities == null || cities.Count == 0)
                {
                    System.Windows.Forms.MessageBox.Show("找不到 area.json 檔案，無法產生模擬資料！\n請檢查檔案是否在程式目錄或專案根目錄。");
                    return;
                }

                var random = new Random();
                var customers = new List<Customer>();

                for (int i = 0; i < count; i++)
                {
                    string city = cities.Keys.ElementAt(random.Next(cities.Count));
                    var districts = cities[city];
                    string district = districts.Keys.ElementAt(random.Next(districts.Count));
                    var streets = districts[district];
                    string street = (streets != null && streets.Count > 0) ? streets[random.Next(streets.Count)] : "中山路";

                    // 隨機選取 1-3 種付款方式
                    var selectedPayments = PaymentMethods.OrderBy(x => random.Next()).Take(random.Next(1, 4)).ToList();
                    string payment = string.Join(",", selectedPayments);

                    string lastName = LastNames[random.Next(LastNames.Length)];
                    string firstName = FirstNames[random.Next(FirstNames.Length)];

                    customers.Add(new Customer
                    {
                        Name = lastName + firstName,
                        ContactPerson = lastName + "先生/小姐",
                        Mobile = $"09{random.Next(10, 99)}{random.Next(100, 999)}{random.Next(100, 999)}",
                        Email = $"user{DateTime.Now.Ticks + i}@example.com",
                        City = city,
                        District = district,
                        Street = street,
                        HouseNumber = $"{random.Next(1, 200)}號",
                        CustomerLevel = Levels[random.Next(Levels.Length)],
                        Payment = payment,
                        Status = true,
                        CreateDate = DateTime.Now.AddDays(-random.Next(30, 365))
                    });
                }

                // 3. 插入資料庫
                using (var db = new SqlConnection(ConnectionString))
                {
                    db.Open();
                    string sql = @"INSERT INTO Customers (
                                    Name, ContactPerson, Mobile, Email, City, District, Street, HouseNumber, CustomerLevel, Payment, Status, CreateDate
                                    ) VALUES (
                                    @Name, @ContactPerson, @Mobile, @Email, @City, @District, @Street, @HouseNumber, @CustomerLevel, @Payment, @Status, @CreateDate)";
                    
                    int rows = db.Execute(sql, customers);
                    System.Windows.Forms.MessageBox.Show($"成功匯入 {rows} 筆模擬客戶資料！");
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("模擬資料匯入錯誤: " + ex.Message);
            }
        }

        private Dictionary<string, Dictionary<string, List<string>>> LoadAddressData()
        {
            // 嘗試多個可能路徑
            string[] possiblePaths = {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "area.json"),
                "area.json",
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\area.json")) // 開發環境路徑
            };

            string jsonPath = possiblePaths.FirstOrDefault(p => File.Exists(p));
            if (string.IsNullOrEmpty(jsonPath)) return null;
            
            try
            {
                string jsonContent = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var rawData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(jsonContent, options);
                var result = new Dictionary<string, Dictionary<string, List<string>>>();
                if (rawData != null)
                {
                    foreach (var city in rawData)
                    {
                        var districts = new Dictionary<string, List<string>>();
                        foreach (var dist in city.Value)
                        {
                            var streets = new List<string>();
                            if (dist.Value.ValueKind == JsonValueKind.Array)
                                foreach (var item in dist.Value.EnumerateArray()) streets.Add(item.ToString());
                            else if (dist.Value.ValueKind == JsonValueKind.String)
                                streets.Add(dist.Value.GetString() ?? "");
                            districts.Add(dist.Key, streets);
                        }
                        result.Add(city.Key, districts);
                    }
                }
                return result;
            }
            catch { return null; }
        }

        public List<Customer> GetAllCustomers()
        {
            using (var db = new SqlConnection(ConnectionString))
            {
                return db.Query<Customer>("SELECT * FROM Customers ORDER BY CreateDate DESC").ToList();
            }
        }
    }
}
