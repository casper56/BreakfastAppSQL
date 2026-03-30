using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using System.Linq;

namespace BreakfastApp
{
    public enum ReceiptType { Customer, Kitchen }

    public static class PrintService
    {
        public static void PreviewReceipt(Order order, ReceiptType type = ReceiptType.Customer)
        {
            // ... (existing code for PreviewReceipt) ...
            using (var printDoc = new PrintDocument())
            {
                printDoc.PrintPage += (s, e) =>
                {
                    Graphics g = e.Graphics;
                    float y = 30;
                    float margin = 30;
                    float width = 300; // 模擬 58mm 寬度

                    // 依據類型調整字體
                    Font fontTitle = new Font("Microsoft JhengHei", type == ReceiptType.Kitchen ? 20 : 16, FontStyle.Bold);
                    Font fontContent = new Font("Microsoft JhengHei", type == ReceiptType.Kitchen ? 14 : 10);
                    Font fontSmall = new Font("Microsoft JhengHei", 9);

                    // 1. 標題
                    string title = type == ReceiptType.Kitchen ? "【廚房製作單】" : "早餐店點餐收據";
                    g.DrawString(title, fontTitle, Brushes.Black, margin, y);
                    y += 45;

                    // 2. 基本資訊
                    g.DrawString($"單號: {order.OrderId}", fontContent, Brushes.Black, margin, y);
                    y += type == ReceiptType.Kitchen ? 30 : 20;

                    if (order.CustomerId.HasValue)
                    {
                        g.DrawString($"客戶ID: {order.CustomerId}", fontContent, Brushes.Black, margin, y);
                        y += type == ReceiptType.Kitchen ? 30 : 20;
                    }

                    g.DrawString($"時間: {order.Timestamp:HH:mm:ss}", fontSmall, Brushes.Black, margin, y);
                    y += 25;
                    g.DrawLine(Pens.Black, margin, y, width + margin, y);
                    y += 10;

                    // 3. 品項
                    foreach (var item in order.Items)
                    {
                        string nameStr = $"{item.Name}({item.OptionName})";
                        g.DrawString(nameStr, fontContent, Brushes.Black, margin, y);

                        if (type == ReceiptType.Customer)
                        {
                            // 客戶聯顯示數量與小計 (修正對齊問題)
                            string qtyStr = $"x{item.Quantity}";
                            string priceStr = $"${item.Subtotal}";

                            // 數量加大並加粗，固定於右側 200px 處
                            Font fontQty = new Font(fontContent.FontFamily, 12, FontStyle.Bold);
                            g.DrawString(qtyStr, fontQty, Brushes.Black, margin + 200, y - 2);

                            // 小計則靠右對齊
                            float pWidth = g.MeasureString(priceStr, fontContent).Width;
                            g.DrawString(priceStr, fontContent, Brushes.Black, width + margin - pWidth, y);
                        }
                        else
                        {
                            // 廚房聯強化數量顯示 (修正對齊問題)
                            string qtyStr = $"數量: {item.Quantity}";
                            // 使用固定位置對齊 (與客戶聯規格一致或稍作調整)
                            g.DrawString(qtyStr, new Font(fontContent, FontStyle.Bold), Brushes.Black, margin + 200, y);
                        }
                        y += type == ReceiptType.Kitchen ? 35 : 25;
                    }

                    // 4. 總計
                    y += 10;
                    g.DrawLine(Pens.Black, margin, y, width + margin, y);
                    y += 15;

                    int totalQty = order.Items.Sum(x => x.Quantity);
                    string qtyTotalStr = $"數量: {totalQty,7}";

                    if (type == ReceiptType.Customer)
                    {
                        // 1. 總數量顯示於上方 (靠右) - 字型加大
                        Font fontTotalQty = new Font(fontContent.FontFamily, 12, FontStyle.Bold);
                        float qWidth = g.MeasureString(qtyTotalStr, fontTotalQty).Width;
                        g.DrawString(qtyTotalStr, fontTotalQty, Brushes.Black, width + margin - qWidth, y);

                        y += 30; // 往下移位 (配合較大字型增加間距)

                        // 2. 總額顯示於下方 (靠右)
                        string totalAmountStr = $"總額: ${order.TotalAmount}";
                        float tWidth = g.MeasureString(totalAmountStr, fontTotalQty).Width;
                        g.DrawString(totalAmountStr, fontTotalQty, Brushes.DarkRed, width + margin - tWidth, y);
                    }
                    else
                    {
                        // 廚房聯：僅強化顯示總數量
                        g.DrawString(qtyTotalStr, fontTitle, Brushes.Black, margin, y);
                    }

                    y += 50;
                    g.DrawString("----- 結束 -----", fontSmall, Brushes.Gray, margin + 80, y);
                };

                using (var previewDlg = new PrintPreviewDialog())
                {
                    previewDlg.Document = printDoc;
                    previewDlg.Text = type == ReceiptType.Kitchen ? "廚房單預覽" : "收據預覽";
                    previewDlg.Width = 450;
                    previewDlg.Height = 600;
                    previewDlg.ShowDialog();
                }
            }
        }

        public static void PreviewMenu(List<Category> categories, int totalItemsCount)
        {
            if (categories == null || categories.Count == 0) return;

            int currentCatIdx = 0;
            int currentItemIdx = 0;
            int pageNum = 1;

            using (var printDoc = new PrintDocument())
            {
                printDoc.BeginPrint += (s, e) =>
                {
                    currentCatIdx = 0;
                    currentItemIdx = 0;
                    pageNum = 1;
                };

                printDoc.PrintPage += (s, e) =>
                {
                    Graphics g = e.Graphics;
                    Font fontTitle = new Font("Microsoft JhengHei", 18, FontStyle.Bold);
                    Font fontCat = new Font("Microsoft JhengHei", 14, FontStyle.Bold);
                    Font fontContent = new Font("Microsoft JhengHei", 11);
                    Font fontSmall = new Font("Microsoft JhengHei", 10);
                    Brush brush = Brushes.Black;

                    float y = 50;
                    float margin = 50;
                    float bottomMargin = e.PageBounds.Height - 80;

                    if (pageNum == 1)
                    {
                        g.DrawString("早餐店完整菜單明細", fontTitle, brush, margin, y);
                        y += 40;
                        g.DrawString($"列印時間: {DateTime.Now:yyyy/MM/dd HH:mm:ss} | 總品項數: {totalItemsCount}", fontSmall, brush, margin, y);
                        y += 30;
                        g.DrawLine(Pens.Black, margin, y, e.PageBounds.Width - margin, y);
                        y += 20;
                    }
                    else
                    {
                        g.DrawString("早餐店完整菜單明細 (續)", fontSmall, Brushes.Gray, margin, y);
                        y += 30;
                    }

                    while (currentCatIdx < categories.Count)
                    {
                        var cat = categories[currentCatIdx];

                        if (currentItemIdx == 0)
                        {
                            if (y + 70 > bottomMargin)
                            {
                                e.HasMorePages = true;
                                pageNum++;
                                return;
                            }
                            g.DrawString($"【{cat.CategoryName}】", fontCat, Brushes.DarkBlue, margin, y);
                            y += 35;
                        }

                        while (currentItemIdx < cat.Items.Count)
                        {
                            if (y + 30 > bottomMargin)
                            {
                                e.HasMorePages = true;
                                pageNum++;
                                return;
                            }

                            var item = cat.Items[currentItemIdx];
                            var prices = new List<string>();
                            if (item.PriceRegular.HasValue) prices.Add($"原${item.PriceRegular}");
                            if (item.PriceWithEgg.HasValue) prices.Add($"蛋${item.PriceWithEgg}");
                            if (item.PriceSmall.HasValue) prices.Add($"小${item.PriceSmall}");
                            if (item.PriceMedium.HasValue) prices.Add($"中${item.PriceMedium}");
                            if (item.PriceLarge.HasValue) prices.Add($"大${item.PriceLarge}");
                            if (item.PriceDanbing.HasValue) prices.Add($"餅${item.PriceDanbing}");
                            if (item.PriceHefen.HasValue) prices.Add($"河${item.PriceHefen}");
                            if (item.Price.HasValue) prices.Add($"${item.Price}");

                            string priceStr = string.Join(", ", prices);
                            if (string.IsNullOrEmpty(priceStr)) priceStr = "(詳見選項)";

                            g.DrawString($"[{item.Id:00}] {item.Name}", fontContent, brush, margin + 20, y);

                            float priceWidth = g.MeasureString(priceStr, fontContent).Width;
                            g.DrawString(priceStr, fontContent, Brushes.DimGray, e.PageBounds.Width - margin - priceWidth, y);

                            y += 25;
                            currentItemIdx++;
                        }

                        currentCatIdx++;
                        currentItemIdx = 0;
                        y += 15;
                    }

                    g.DrawString($"- 第 {pageNum} 頁 -", fontSmall, Brushes.Gray, e.PageBounds.Width / 2 - 30, e.PageBounds.Height - 50);
                    e.HasMorePages = false;
                };

                using (var previewDlg = new PrintPreviewDialog())
                {
                    previewDlg.Document = printDoc;
                    previewDlg.Width = 900;
                    previewDlg.Height = 1000;
                    previewDlg.StartPosition = FormStartPosition.CenterScreen;
                    previewDlg.ShowDialog();
                }
            }
        }
    }
}