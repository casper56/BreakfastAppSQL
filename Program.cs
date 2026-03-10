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

        // 啟動時初始化資料庫 (建立資料庫、資料表並從 JSON 遷移資料)
        DbService.InitializeDatabase();

        Application.Run(new Form1());
    }    
}
