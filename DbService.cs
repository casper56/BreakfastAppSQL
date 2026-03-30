using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using Microsoft.Data.SqlClient;
using Dapper;

namespace BreakfastApp
{
    public class DbService
    {
        private static string ConnectionString = @"Server=.\SQL2022;Database=BreakfastDB;User Id=sa;Password=1qaz@wsx;TrustServerCertificate=True;";
        private static string MasterConnectionString = @"Server=.\SQL2022;Database=master;User Id=sa;Password=1qaz@wsx;TrustServerCertificate=True;";

        // 初始化資料庫 (建立資料庫與資料表)
        public static void InitializeDatabase()
        {
            try
            {
                // 1. 確保資料庫存在
                using (var masterDb = new SqlConnection(MasterConnectionString))
                {
                    masterDb.Open();
                    var dbExists = masterDb.ExecuteScalar<int>("SELECT COUNT(*) FROM sys.databases WHERE name = 'BreakfastDB'");
                    if (dbExists == 0) masterDb.Execute("CREATE DATABASE BreakfastDB");
                }

                // 2. 執行 SQL 腳本建立資料表
                using (var db = new SqlConnection(ConnectionString))
                {
                    db.Open();
                    string scriptPath = FindFile("DatabaseSetup.sql");
                    if (File.Exists(scriptPath))
                    {
                        string sql = File.ReadAllText(scriptPath);
                        // 移除 GO 並拆分執行，確保 Dapper 不報錯
                        var commands = sql.Split(new[] { "GO" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var cmd in commands) if (!string.IsNullOrWhiteSpace(cmd)) db.Execute(cmd);
                    }
                }

                // 3. 匯入 100 筆模擬客戶 (強行匯入)
                SeedCustomers();

                // 4. 匯入菜單
                ImportInitialData();

                // 5. 匯入模擬訂單
                SeedOrders();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"初始化失敗：{ex.Message}");
            }
        }

        private static void SeedOrders()
        {
            using (var db = new SqlConnection(ConnectionString))
            {
                db.Open();
                int count = db.ExecuteScalar<int>("SELECT COUNT(*) FROM ordertable");
                if (count >= 100) return; // 已有資料就不重複產生

                var service = new OrderService();
                service.SeedMockOrders(100);
            }
        }

        private static void SeedCustomers()
        {
            using (var db = new SqlConnection(ConnectionString))
            {
                db.Open();
                int count = db.ExecuteScalar<int>("SELECT COUNT(*) FROM Customers");
                if (count >= 100) return; // 已有資料就不重複產生

                var service = new CustomerService();
                service.SeedMockCustomers(100);
            }
        }

        private static string FindFile(string fileName)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] paths = {
                Path.Combine(baseDir, fileName),
                fileName,
                // 開發環境往上找 (Debug)
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\" + fileName)),
                // 嘗試找 Release 目錄 (如果當前是 Debug)
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\bin\Release\net10.0-windows\" + fileName)),
                // 嘗試找 Debug 目錄 (如果當前是 Release)
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\bin\Debug\net10.0-windows\" + fileName)),
                // 專案根目錄
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\" + fileName))
            };
            
            foreach (var p in paths)
            {
                try { if (File.Exists(p)) return p; } catch { }
            }
            return "";
        }

        private static void ImportOrdersFromJson()
        {
            using (var db = new SqlConnection(ConnectionString))
            {
                db.Open();
                var count = db.ExecuteScalar<int>("SELECT COUNT(*) FROM ordertable");
                if (count > 0) return; // 已經有資料

                // 嘗試多個檔名
                string[] filesToTry = { "order.json", "orders.json", "orders_backup.json", "Order.json" };
                string jsonPath = "";
                foreach (var f in filesToTry)
                {
                    jsonPath = FindFile(f);
                    if (!string.IsNullOrEmpty(jsonPath)) break;
                }
                
                if (string.IsNullOrEmpty(jsonPath))
                {
                    // 暫時取消此處彈窗，避免干擾啟動，但如果您確定檔案存在卻沒匯入，請檢查此處
                    return;
                }

                try
                {
                    string jsonContent = File.ReadAllText(jsonPath);
                    // 根據您 DataModels.cs 中的定義解析
                    var orders = JsonSerializer.Deserialize<List<Order>>(jsonContent);
                    if (orders == null || orders.Count == 0) return;

                    int successCount = 0;
                    foreach (var order in orders)
                    {
                        using (var trans = db.BeginTransaction())
                        {
                            try
                            {
                                string sqlMaster = @"INSERT INTO ordertable (OrderNo, OrderDate, CustomerId, TotalAmount, TotalQuantity, Status)
                                                    OUTPUT INSERTED.Id VALUES (@OrderNo, @OrderDate, @CustomerId, @TotalAmount, @TotalQuantity, @Status)";

                                int orderId = db.QuerySingle<int>(sqlMaster, new
                                {
                                    OrderNo = order.OrderId,
                                    OrderDate = order.Timestamp,
                                    CustomerId = order.CustomerId,
                                    TotalAmount = order.TotalAmount,
                                    TotalQuantity = order.Items.Sum(x => x.Quantity),
                                    Status = "Completed"
                                }, trans);

                                foreach (var item in order.Items)
                                {
                                    string sqlDetail = @"INSERT INTO orderdetails (OrderId, MenuItemId, ItemName, Spec, UnitPrice, Quantity, SubTotal)
                                                        VALUES (@OrderId, @MenuItemId, @ItemName, @Spec, @UnitPrice, @Quantity, @SubTotal)";
                                    db.Execute(sqlDetail, new
                                    {
                                        OrderId = orderId,
                                        MenuItemId = item.ItemId,
                                        ItemName = item.Name,
                                        Spec = item.OptionName,
                                        UnitPrice = item.Price,
                                        Quantity = item.Quantity,
                                        SubTotal = item.Price * item.Quantity
                                    }, trans);
                                }
                                trans.Commit();
                                successCount++;
                            }
                            catch (Exception exTrans)
                            {
                                trans.Rollback();
                                Console.WriteLine($"單筆訂單匯入失敗: {exTrans.Message}");
                            }
                        }
                    }
                    if (successCount > 0)
                        System.Windows.Forms.MessageBox.Show($"成功從 {Path.GetFileName(jsonPath)} 匯入 {successCount} 筆訂單記錄！");
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show($"解析訂單 JSON 失敗 ({Path.GetFileName(jsonPath)}): {ex.Message}");
                }
            }
        }

        private static void ImportInitialData()
        {
            using (var db = new SqlConnection(ConnectionString))
            {
                db.Open();
                var count = db.ExecuteScalar<int>("SELECT COUNT(*) FROM mealcattable");
                if (count > 0) return; // 已經有資料

                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "category_all.json");
                if (!File.Exists(jsonPath)) return;

                string jsonContent = File.ReadAllText(jsonPath);
                var menuRoot = JsonSerializer.Deserialize<MenuRoot>(jsonContent);

                if (menuRoot?.Categories != null)
                {
                    int sortNo = 1;
                    foreach (var category in menuRoot.Categories)
                    {
                        // 1. 插入分類
                        string insertCatSql = "INSERT INTO mealcattable (CategoryName, SortNo) OUTPUT INSERTED.Id VALUES (@CategoryName, @SortNo)";
                        int categoryId = db.QuerySingle<int>(insertCatSql, new { CategoryName = category.CategoryName, SortNo = sortNo++ });

                        // 2. 插入品項
                        if (category.Items != null)
                        {
                            int itemSortNo = 1;
                            foreach (var item in category.Items)
                            {
                                string insertItemSql = @"
                                    INSERT INTO mealtable (CategoryId, Name, PriceRegular, PriceWithEgg, PriceSmall, PriceMedium, PriceLarge, PriceDanbing, PriceHefen, Flavors, Image, SortNo)
                                    VALUES (@CategoryId, @Name, @PriceRegular, @PriceWithEgg, @PriceSmall, @PriceMedium, @PriceLarge, @PriceDanbing, @PriceHefen, @Flavors, @Image, @SortNo)";

                                db.Execute(insertItemSql, new
                                {
                                    CategoryId = categoryId,
                                    Name = item.Name,
                                    PriceRegular = item.PriceRegular,
                                    PriceWithEgg = item.PriceWithEgg,
                                    PriceSmall = item.PriceSmall,
                                    PriceMedium = item.PriceMedium,
                                    PriceLarge = item.PriceLarge,
                                    PriceDanbing = item.PriceDanbing,
                                    PriceHefen = item.PriceHefen,
                                    Flavors = item.Flavors != null ? JsonSerializer.Serialize(item.Flavors) : null,
                                    Image = item.Image,
                                    SortNo = itemSortNo++
                                });
                            }
                        }
                    }
                    Console.WriteLine("Imported category_all.json to database.");
                }
            }
        }

        // 地址解析服務 (相容不規則結構)
        public static Dictionary<string, Dictionary<string, List<string>>> LoadAddressData()
        {
            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "area.json");
            if (!File.Exists(jsonPath)) jsonPath = "area.json";
            
            if (!File.Exists(jsonPath)) return new Dictionary<string, Dictionary<string, List<string>>>();
            
            try
            {
                string jsonContent = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                
                // 改用 JsonDocument 進行手動解析，以處理型別不一致的問題
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
                            {
                                foreach (var item in dist.Value.EnumerateArray()) 
                                    streets.Add(item.ToString());
                            }
                            else if (dist.Value.ValueKind == JsonValueKind.String)
                            {
                                streets.Add(dist.Value.GetString() ?? "");
                            }
                            districts.Add(dist.Key, streets);
                        }
                        result.Add(city.Key, districts);
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("地址資料解析警告 (已嘗試相容模式): " + ex.Message);
                return new Dictionary<string, Dictionary<string, List<string>>>();
            }
        }
    }
}
