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
                // 1. 確保資料庫存在 (連線到 master)
                using (var masterDb = new SqlConnection(MasterConnectionString))
                {
                    masterDb.Open();
                    var dbExists = masterDb.ExecuteScalar<int>("SELECT COUNT(*) FROM sys.databases WHERE name = 'BreakfastDB'");
                    if (dbExists == 0)
                    {
                        masterDb.Execute("CREATE DATABASE BreakfastDB");
                        Console.WriteLine("Database BreakfastDB created.");
                    }
                }

                // 2. 執行 SQL 腳本建立資料表
                using (var db = new SqlConnection(ConnectionString))
                {
                    db.Open();
                    string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DatabaseSetup.sql");
                    if (!File.Exists(scriptPath)) scriptPath = "DatabaseSetup.sql"; // 為了開發方便
                    
                    if (File.Exists(scriptPath))
                    {
                        string sql = File.ReadAllText(scriptPath);
                        db.Execute(sql);
                        Console.WriteLine("Database tables setup completed.");
                    }
                }

                // 3. 如果 mealcattable 是空的，則匯入 JSON
                ImportInitialData();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing database: {ex.Message}");
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
