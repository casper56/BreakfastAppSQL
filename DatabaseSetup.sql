-- 1. 建立客戶資料表
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Customers]') AND type in (N'U'))
BEGIN
    CREATE TABLE Customers (
        CustomerID INT PRIMARY KEY IDENTITY(1,1),
        Name NVARCHAR(100) NOT NULL,
        TaxID NVARCHAR(20),
        ContactPerson NVARCHAR(50),
        Mobile NVARCHAR(20),
        Email NVARCHAR(100),
        PostalCode VARCHAR(5),
        City NVARCHAR(10),
        District NVARCHAR(10),
        Street NVARCHAR(50),
        SubStreet NVARCHAR(30),
        HouseNumber NVARCHAR(20),
        Floor_Other NVARCHAR(50),
        CustomerLevel NVARCHAR(10),
        Status BIT DEFAULT 1,
        CreateDate DATETIME DEFAULT GETDATE(),
        UpdateDate DATETIME
    );
END

-- 2. 建立餐點分類表
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[mealcattable]') AND type in (N'U'))
BEGIN
    CREATE TABLE mealcattable (
        Id INT PRIMARY KEY IDENTITY(1,1),
        CategoryName NVARCHAR(50) NOT NULL,
        SortNo INT DEFAULT 0
    );
END

-- 3. 建立餐點品項表
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[mealtable]') AND type in (N'U'))
BEGIN
    CREATE TABLE mealtable (
        Id INT PRIMARY KEY IDENTITY(1,1),
        CategoryId INT,
        Name NVARCHAR(100) NOT NULL,
        PriceRegular INT,
        PriceWithEgg INT,
        PriceSmall INT,
        PriceMedium INT,
        PriceLarge INT,
        PriceDanbing INT,
        PriceHefen INT,
        Flavors NVARCHAR(MAX), -- 存儲 JSON 字串或逗號分隔
        Image NVARCHAR(255),
        SortNo INT DEFAULT 0,
        FOREIGN KEY (CategoryId) REFERENCES mealcattable(Id)
    );
END

-- 4. 建立銷售主檔 (訂單表)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ordertable]') AND type in (N'U'))
BEGIN
    CREATE TABLE ordertable (
        Id INT PRIMARY KEY IDENTITY(1,1),
        OrderNo NVARCHAR(20) NOT NULL,
        OrderDate DATETIME DEFAULT GETDATE(),
        CustomerId INT NULL,
        TotalAmount INT,
        TotalQuantity INT,
        Status NVARCHAR(20) DEFAULT 'Completed',
        Remark NVARCHAR(MAX)
    );
END

-- 5. 建立銷售明細檔
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[orderdetails]') AND type in (N'U'))
BEGIN
    CREATE TABLE orderdetails (
        Id INT PRIMARY KEY IDENTITY(1,1),
        OrderId INT,
        MenuItemId INT,
        ItemName NVARCHAR(100),
        Spec NVARCHAR(50),
        UnitPrice INT,
        Quantity INT,
        SubTotal INT,
        FOREIGN KEY (OrderId) REFERENCES ordertable(Id)
    );
END
