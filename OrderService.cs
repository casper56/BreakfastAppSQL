using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Linq;

namespace BreakfastApp
{
    public class OrderService
    {
        private string ConnectionString = @"Server=.\SQL2022;Database=BreakfastDB;User Id=sa;Password=1qaz@wsx;TrustServerCertificate=True;";

        // 提供給舊 UI 使用的屬性，改為即時從資料庫讀取
        public List<Order> AllOrders => GetAllOrders();

        public OrderService() { }

        public string GenerateOrderId()
        {
            string datePrefix = DateTime.Now.ToString("yyyyMMdd");
            using (var db = new SqlConnection(ConnectionString))
            {
                var lastOrderNo = db.QueryFirstOrDefault<string>(
                    "SELECT TOP 1 OrderNo FROM ordertable WHERE OrderNo LIKE @Prefix ORDER BY OrderNo DESC",
                    new { Prefix = datePrefix + "%" });

                int nextNum = 1;
                if (lastOrderNo != null)
                {
                    string suffix = lastOrderNo.Substring(8);
                    if (int.TryParse(suffix, out int lastNum)) nextNum = lastNum + 1;
                }
                return $"{datePrefix}{nextNum:D4}";
            }
        }

        public void AddOrder(Order order)
        {
            using (var db = new SqlConnection(ConnectionString))
            {
                db.Open();
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
                                SubTotal = item.Subtotal
                            }, trans);
                        }
                        trans.Commit();

                        // 成功後，同步備份到 JSON
                        BackupOrdersToJson();
                    }
                    catch { trans.Rollback(); throw; }
                }
            }
        }

        public void BackupOrdersToJson()
        {
            try
            {
                var orders = GetAllOrders();
                string jsonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "orders_backup.json");
                string jsonContent = System.Text.Json.JsonSerializer.Serialize(orders, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(jsonPath, jsonContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"備份訂單至 JSON 失敗: {ex.Message}");
            }
        }

        // --- 供 UI 相容使用的輔助方法 ---

        private List<Order> GetAllOrders()
        {
            using (var db = new SqlConnection(ConnectionString))
            {
                var masters = db.Query<OrderMaster>("SELECT * FROM ordertable ORDER BY OrderDate DESC").ToList();
                var results = new List<Order>();
                foreach (var m in masters)
                {
                    var details = db.Query<OrderDetail>("SELECT * FROM orderdetails WHERE OrderId = @Id", new { Id = m.Id });
                    results.Add(new Order
                    {
                        OrderId = m.OrderNo,
                        CustomerId = m.CustomerId, // 加入客戶ID對應
                        Timestamp = m.OrderDate,
                        Items = details.Select(d => new CartItem {
                            ItemId = d.MenuItemId,
                            Name = d.ItemName,
                            OptionName = d.Spec ?? "單點",
                            Price = d.UnitPrice,
                            Quantity = d.Quantity
                        }).ToList()
                    });
                }
                return results;
            }
        }

        public void SaveOrders() 
        { 
            // 呼叫新的備份方法
            BackupOrdersToJson();
        }

        /// <summary>
        /// 從 JSON 檔案還原/匯入訂單資料至 SQL Server
        /// </summary>
        public void RestoreOrdersFromJson(string jsonPath)
        {
            if (!System.IO.File.Exists(jsonPath)) return;

            try
            {
                string jsonContent = System.IO.File.ReadAllText(jsonPath);
                var orders = System.Text.Json.JsonSerializer.Deserialize<List<Order>>(jsonContent);
                if (orders == null) return;

                using (var db = new SqlConnection(ConnectionString))
                {
                    db.Open();
                    foreach (var order in orders)
                    {
                        // 1. 檢查該訂單號是否已存在
                        bool exists = db.ExecuteScalar<int>("SELECT COUNT(1) FROM ordertable WHERE OrderNo = @OrderNo", new { OrderNo = order.OrderId }) > 0;
                        if (exists) continue;

                        using (var trans = db.BeginTransaction())
                        {
                            try
                            {
                                // 2. 插入主檔
                                string sqlMaster = @"INSERT INTO ordertable (OrderNo, OrderDate, TotalAmount, TotalQuantity, Status)
                                                    OUTPUT INSERTED.Id VALUES (@OrderNo, @OrderDate, @TotalAmount, @TotalQuantity, @Status)";

                                int orderId = db.QuerySingle<int>(sqlMaster, new
                                {
                                    OrderNo = order.OrderId,
                                    OrderDate = order.Timestamp,
                                    TotalAmount = order.TotalAmount,
                                    TotalQuantity = order.Items.Sum(x => x.Quantity),
                                    Status = "Completed"
                                }, trans);

                                // 3. 插入明細
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
                                        SubTotal = item.Subtotal
                                    }, trans);
                                }
                                trans.Commit();
                            }
                            catch { trans.Rollback(); }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"還原訂單失敗: {ex.Message}");
            }
        }

        public List<Order> SearchOrders(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return AllOrders;
            return GetAllOrders().Where(o => o.OrderId.Contains(query)).ToList();
        }

        public void SeedMockOrders(int count)
        {
            try
            {
                using (var db = new SqlConnection(ConnectionString))
                {
                    db.Open();
                    
                    // 1. 取得現有的客戶 IDs
                    var customerIds = db.Query<int>("SELECT CustomerID FROM Customers").ToList();
                    if (customerIds.Count == 0) return;

                    // 2. 取得現有的餐點品項
                    var mealItems = db.Query<MealItem>("SELECT * FROM mealtable").ToList();
                    if (mealItems.Count == 0) return;

                    var random = new Random();
                    int successCount = 0;

                    for (int i = 0; i < count; i++)
                    {
                        using (var trans = db.BeginTransaction())
                        {
                            try
                            {
                                int customerId = customerIds[random.Next(customerIds.Count)];
                                DateTime orderDate = DateTime.Now.AddDays(-random.Next(1, 60));
                                string orderNo = orderDate.ToString("yyyyMMdd") + random.Next(1000, 9999).ToString();
                                
                                // 隨機挑選 1-5 個品項
                                int itemCount = random.Next(1, 6);
                                var selectedItems = new List<CartItem>();
                                int totalAmount = 0;
                                int totalQuantity = 0;

                                for (int j = 0; j < itemCount; j++)
                                {
                                    var meal = mealItems[random.Next(mealItems.Count)];
                                    int price = meal.PriceRegular ?? meal.Price ?? 30;
                                    int qty = random.Next(1, 4);
                                    
                                    selectedItems.Add(new CartItem
                                    {
                                        ItemId = meal.Id,
                                        Name = meal.Name,
                                        OptionName = "一般",
                                        Price = price,
                                        Quantity = qty
                                    });
                                    totalAmount += price * qty;
                                    totalQuantity += qty;
                                }

                                // 插入主檔 (包含 CustomerId)
                                string sqlMaster = @"INSERT INTO ordertable (OrderNo, OrderDate, CustomerId, TotalAmount, TotalQuantity, Status)
                                                    OUTPUT INSERTED.Id VALUES (@OrderNo, @OrderDate, @CustomerId, @TotalAmount, @TotalQuantity, @Status)";

                                int orderId = db.QuerySingle<int>(sqlMaster, new
                                {
                                    OrderNo = orderNo,
                                    OrderDate = orderDate,
                                    CustomerId = customerId,
                                    TotalAmount = totalAmount,
                                    TotalQuantity = totalQuantity,
                                    Status = "Completed"
                                }, trans);

                                // 插入明細
                                foreach (var item in selectedItems)
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
                            catch (Exception ex)
                            {
                                trans.Rollback();
                                Console.WriteLine($"模擬訂單產生失敗: {ex.Message}");
                            }
                        }
                    }
                    if (successCount > 0)
                        System.Windows.Forms.MessageBox.Show($"成功產生 {successCount} 筆模擬訂單資料！");
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("模擬訂單資料產生錯誤: " + ex.Message);
            }
        }
    }
}
