using System;
using System.Windows.Forms;

namespace BreakfastApp;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        // 1. 啟動時初始化資料庫 (建立資料庫、資料表並從 JSON 遷移資料)
        DbService.InitializeDatabase();

        // 2. [暫時腳本] 產生 100 筆模擬客戶資料
        try
        {
            var customerService = new CustomerService();
            customerService.SeedMockCustomers(100);
            // 執行完一次後，建議可以把這兩行刪除或註解掉
        }
        catch (Exception ex)
        {
            Console.WriteLine("模擬客戶資料匯入失敗: " + ex.Message);
        }

        Application.Run(new Form1());
    }    
}
