using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Linq;

namespace BreakfastApp
{
    // --- 基礎資料庫模型 ---

    public class Customer
    {
        public int CustomerID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? TaxID { get; set; }
        public string? ContactPerson { get; set; }
        public string? Mobile { get; set; }
        public string? Email { get; set; }
        public string? PostalCode { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Street { get; set; }
        public string? SubStreet { get; set; }
        public string? HouseNumber { get; set; }
        public string? Floor_Other { get; set; }
        public string? CustomerLevel { get; set; }
        public string? Payment { get; set; }
        public bool Status { get; set; } = true;
        public DateTime CreateDate { get; set; } = DateTime.Now;
        public DateTime? UpdateDate { get; set; }
    }

    public class MealCategory
    {
        public int Id { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int SortNo { get; set; }
    }

    public class MealItem
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? PriceRegular { get; set; }
        public int? PriceWithEgg { get; set; }
        public int? PriceSmall { get; set; }
        public int? PriceMedium { get; set; }
        public int? PriceLarge { get; set; }
        public int? PriceDanbing { get; set; }
        public int? PriceHefen { get; set; }
        public int? Price8Pcs { get; set; }
        public int? Price10Pcs { get; set; }
        public int? Price1Pc { get; set; }
        public int? Price2Pcs { get; set; }
        public int? PriceSingle { get; set; }
        public int? Price { get; set; }
        public string? Flavors { get; set; } 
        public string? Image { get; set; }
        public string? Content { get; set; }
        public string? CategoryNote { get; set; }
        public int SortNo { get; set; }
    }

    public class OrderMaster
    {
        public int Id { get; set; }
        public string OrderNo { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; } = DateTime.Now;
        public int? CustomerId { get; set; }
        public int TotalAmount { get; set; }
        public int TotalQuantity { get; set; }
        public string Status { get; set; } = "Completed";
        public string? Remark { get; set; }
    }

    public class OrderDetail
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int MenuItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Spec { get; set; }
        public int UnitPrice { get; set; }
        public int Quantity { get; set; }
        public int SubTotal { get; set; }
    }

    // --- 購物車與列印使用的訂單模型 ---
    public class Order
    {
        [JsonPropertyName("order_id")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("customer_id")]
        public int? CustomerId { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        [JsonPropertyName("items")]
        public List<CartItem> Items { get; set; } = new List<CartItem>();

        [JsonPropertyName("total_amount")]
        public int TotalAmount => Items.Sum(x => x.Subtotal);
    }

    // --- 相容原有程式碼的 MenuItem 類別 ---
    public class MenuItem
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("price_regular")] public int? PriceRegular { get; set; }
        [JsonPropertyName("price_with_egg")] public int? PriceWithEgg { get; set; }
        [JsonPropertyName("price_small")] public int? PriceSmall { get; set; }
        [JsonPropertyName("price_medium")] public int? PriceMedium { get; set; }
        [JsonPropertyName("price_large")] public int? PriceLarge { get; set; }
        [JsonPropertyName("price_danbing")] public int? PriceDanbing { get; set; }
        [JsonPropertyName("price_hefen")] public int? PriceHefen { get; set; }
        [JsonPropertyName("price_8pcs")] public int? Price8Pcs { get; set; }
        [JsonPropertyName("price_10pcs")] public int? Price10Pcs { get; set; }
        [JsonPropertyName("price_1pc")] public int? Price1Pc { get; set; }
        [JsonPropertyName("price_2pcs")] public int? Price2Pcs { get; set; }
        [JsonPropertyName("price_single")] public int? PriceSingle { get; set; }
        [JsonPropertyName("price")] public int? Price { get; set; }
        [JsonPropertyName("content")] public string? Content { get; set; }
        [JsonPropertyName("flavors")] public List<string>? Flavors { get; set; }
        [JsonPropertyName("category_note")] public string? CategoryNote { get; set; }
        [JsonPropertyName("image")] public string? Image { get; set; }

        public override string ToString()
        {
            var details = new List<string>();
            if (Price.HasValue) details.Add($"${Price}");
            if (PriceRegular.HasValue) details.Add($"原${PriceRegular}");
            if (PriceWithEgg.HasValue) details.Add($"加蛋${PriceWithEgg}");
            string detailStr = details.Count > 0 ? $" ({string.Join(", ", details)})" : "";
            return $"{Name}{detailStr}";
        }
    }

    public class MenuRoot
    {
        [JsonPropertyName("menu_name")]
        public string MenuName { get; set; } = string.Empty;
        [JsonPropertyName("categories")]
        public List<Category> Categories { get; set; } = new List<Category>();
    }

    public class Category
    {
        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; } = string.Empty;
        [JsonPropertyName("items")]
        public List<MenuItem> Items { get; set; } = new List<MenuItem>();
    }

    public class CartItem
    {
        public int ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string OptionName { get; set; } = "單點";
        public int Price { get; set; }
        public int Quantity { get; set; } = 1;
        public int Subtotal => Price * Quantity;
        public MenuItem Item { get; set; } = new MenuItem();
    }
}
