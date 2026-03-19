using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Text.Json;

namespace BreakfastApp
{
    public class MenuService
    {
        private string ConnectionString = @"Server=.\SQL2022;Database=BreakfastDB;User Id=sa;Password=1qaz@wsx;TrustServerCertificate=True;";

        public List<MenuItem> AllItems { get; private set; } = new List<MenuItem>();
        public List<Category> Categories { get; private set; } = new List<Category>();

        public MenuService(string filePath)
        {
            // 如果資料庫是空的，可以考慮呼叫 ImportFromJson(filePath);
            LoadData();
        }

        /// <summary>
        /// 從 JSON 檔案匯入資料到 SQL Server
        /// </summary>
        public void ImportFromJson(string jsonPath)
        {
            if (!File.Exists(jsonPath)) return;

            string json = File.ReadAllText(jsonPath);
            var menuRoot = JsonSerializer.Deserialize<MenuRoot>(json);
            if (menuRoot == null) return;

            using (var db = new SqlConnection(ConnectionString))
            {
                db.Open();
                using (var trans = db.BeginTransaction())
                {
                    try
                    {
                        // 1. 先清空舊資料 (注意順序，先刪品項再刪分類)
                        db.Execute("DELETE FROM orderdetails", null, trans);
                        db.Execute("DELETE FROM ordertable", null, trans);
                        db.Execute("DELETE FROM mealtable", null, trans);
                        db.Execute("DELETE FROM mealcattable", null, trans);

                        // 2. 匯入分類與品項
                        int catSort = 0;
                        foreach (var cat in menuRoot.Categories)
                        {
                            catSort++;
                            // 插入分類並取得 ID
                            string sqlCat = "INSERT INTO mealcattable (CategoryName, SortNo) VALUES (@CategoryName, @SortNo); SELECT CAST(SCOPE_IDENTITY() as int)";
                            int categoryId = db.Query<int>(sqlCat, new { CategoryName = cat.CategoryName, SortNo = catSort }, trans).Single();

                            int itemSort = 0;
                            foreach (var item in cat.Items)
                            {
                                itemSort++;
                                string sqlItem = @"INSERT INTO mealtable (
                                    CategoryId, Name, PriceRegular, PriceWithEgg, PriceSmall, PriceMedium, PriceLarge, 
                                    PriceDanbing, PriceHefen, Price8Pcs, Price10Pcs, Price1Pc, Price2Pcs, PriceSingle, 
                                    Price, Flavors, Image, Content, CategoryNote, SortNo) 
                                    VALUES (
                                    @CategoryId, @Name, @PriceRegular, @PriceWithEgg, @PriceSmall, @PriceMedium, @PriceLarge, 
                                    @PriceDanbing, @PriceHefen, @Price8Pcs, @Price10Pcs, @Price1Pc, @Price2Pcs, @PriceSingle, 
                                    @Price, @Flavors, @Image, @Content, @CategoryNote, @SortNo)";

                                db.Execute(sqlItem, new
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
                                    Price8Pcs = item.Price8Pcs,
                                    Price10Pcs = item.Price10Pcs,
                                    Price1Pc = item.Price1Pc,
                                    Price2Pcs = item.Price2Pcs,
                                    PriceSingle = item.PriceSingle,
                                    Price = item.Price,
                                    Flavors = item.Flavors != null ? JsonSerializer.Serialize(item.Flavors) : null,
                                    Image = item.Image, // 這裡會存入 "images/三明治/總匯三明治.jpg"
                                    Content = item.Content,
                                    CategoryNote = item.CategoryNote,
                                    SortNo = itemSort
                                }, trans);
                            }
                        }

                        trans.Commit();
                    }
                    catch (Exception ex)
                    {
                        trans.Rollback();
                        throw new Exception("匯入失敗: " + ex.Message);
                    }
                }
            }
            LoadData(); // 匯入完重新載入記憶體
        }

        public void LoadData()
        {
            using (var db = new SqlConnection(ConnectionString))
            {
                db.Open();
                var dbCats = db.Query<MealCategory>("SELECT * FROM mealcattable ORDER BY SortNo").ToList();
                var dbItems = db.Query<MealItem>("SELECT * FROM mealtable ORDER BY SortNo").ToList();

                var categories = new List<Category>();
                var allItems = new List<MenuItem>();

                foreach (var dbCat in dbCats)
                {
                    var catItems = dbItems.Where(i => i.CategoryId == dbCat.Id).Select(i => MapToMenuItem(i)).ToList();
                    categories.Add(new Category { CategoryName = dbCat.CategoryName, Items = catItems });
                    allItems.AddRange(catItems);
                }

                Categories = categories;
                AllItems = allItems;
            }
        }

        private MenuItem MapToMenuItem(MealItem m)
        {
            return new MenuItem
            {
                Id = m.Id,
                Name = m.Name,
                PriceRegular = m.PriceRegular,
                PriceWithEgg = m.PriceWithEgg,
                PriceSmall = m.PriceSmall,
                PriceMedium = m.PriceMedium,
                PriceLarge = m.PriceLarge,
                PriceDanbing = m.PriceDanbing,
                PriceHefen = m.PriceHefen,
                Price8Pcs = m.Price8Pcs,
                Price10Pcs = m.Price10Pcs,
                Price1Pc = m.Price1Pc,
                Price2Pcs = m.Price2Pcs,
                PriceSingle = m.PriceSingle,
                Price = m.Price,
                Content = m.Content,
                CategoryNote = m.CategoryNote,
                Image = m.Image,
                Flavors = !string.IsNullOrEmpty(m.Flavors) ? JsonSerializer.Deserialize<List<string>>(m.Flavors) : new List<string>()
            };
        }

        public void SaveData(string targetPath)
        {
            // 在資料庫模式下，此按鈕功能改為「同步資料」(目前僅需 LoadData)
            LoadData();
        }

        public Image? GetThumbnail(string relativePath, int targetWidth = 300)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            string imgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
            if (!File.Exists(imgPath)) return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(imgPath);
                using (var ms = new MemoryStream(bytes))
                {
                    using (var tempImg = Image.FromStream(ms))
                    {
                        int newHeight = (int)((double)tempImg.Height / tempImg.Width * targetWidth);
                        var thumb = new Bitmap(targetWidth, newHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        using (var g = Graphics.FromImage(thumb))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.DrawImage(tempImg, 0, 0, targetWidth, newHeight);
                        }
                        return thumb;
                    }
                }
            }
            catch { return null; }
        }

        public void SortCategory(string categoryName, bool ascending)
        {
            var cat = Categories.FirstOrDefault(c => c.CategoryName == categoryName);
            if (cat != null && cat.Items != null)
            {
                int GetBasePrice(MenuItem item) => item.PriceRegular ?? item.PriceSmall ?? item.Price ?? 0;
                if (ascending) cat.Items.Sort((x, y) => GetBasePrice(x).CompareTo(GetBasePrice(y)));
                else cat.Items.Sort((x, y) => GetBasePrice(y).CompareTo(GetBasePrice(x)));
            }
        }

        public MenuItem? GetItemById(int id) => AllItems.FirstOrDefault(x => x.Id == id);

        public void AddItem(MenuItem item, string categoryName) { LoadData(); }
        public void UpdateItem(int index, MenuItem newItem, string newCategoryName) { LoadData(); }
        public void RemoveItem(MenuItem item) { LoadData(); }
    }
}
