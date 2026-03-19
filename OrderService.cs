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

        public List<Order> SearchOrders(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return AllOrders;
            return GetAllOrders().Where(o => o.OrderId.Contains(query)).ToList();
        }
    }
}
