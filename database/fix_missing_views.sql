-- ============================================================
-- FIX: Tambah tabel & view yang kurang di semua database
-- Jalankan script ini di SQL Server jika muncul error:
--   "DataTables warning: table id=stock-table - Unable to load table data"
--
-- Database yang dicover:
--   DB_SUPPLY_HOSE  -> vw_stock_summary  (halaman Home / Green Hose)
--   DB_SUPPLY_RVI   -> vw_stock_summary  (halaman RVI)
--   DB_SUPPLY_MOLDED-> vw_stock_summary  (halaman Molded)
-- ============================================================

SET NOCOUNT ON;

-- ============================================================
-- [1] DB_SUPPLY_HOSE  –  Items + storage_log + supply_log + vw_stock_summary
-- ============================================================
USE DB_SUPPLY_HOSE;
GO

-- Tabel Items (master item Green Hose)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Items')
BEGIN
    CREATE TABLE dbo.Items (
        item_code     NVARCHAR(100) NOT NULL PRIMARY KEY,
        mesin         NVARCHAR(100) NULL,
        qty_per_box   DECIMAL(10,2) NULL,
        standard_exp  INT NULL,
        standard_min  INT NULL,
        standard_max  INT NULL,
        created_at    DATETIME DEFAULT GETDATE(),
        updated_at    DATETIME DEFAULT GETDATE()
    );
    PRINT 'HOSE: tabel Items dibuat.';
END
GO

-- Tabel storage_log (INPUT IN untuk Green Hose)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'storage_log')
BEGIN
    CREATE TABLE dbo.storage_log (
        log_id          INT IDENTITY(1,1) PRIMARY KEY,
        item_code       NVARCHAR(100)  NOT NULL,
        full_qr         NVARCHAR(300)  NULL,
        production_date DATETIME       NULL,
        box_count       INT            DEFAULT 0,
        qty_pcs         INT            DEFAULT 0,
        stored_at       DATETIME       DEFAULT GETDATE(),
        tanggal         NVARCHAR(10)   NULL
    );
    PRINT 'HOSE: tabel storage_log dibuat.';
END
GO

-- Tabel supply_log (OUTPUT untuk Green Hose)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'supply_log')
BEGIN
    CREATE TABLE dbo.supply_log (
        log_id          INT IDENTITY(1,1) PRIMARY KEY,
        item_code       NVARCHAR(100)  NOT NULL,
        full_qr         NVARCHAR(300)  NULL,
        box_count       INT            DEFAULT 0,
        qty_pcs         INT            DEFAULT 0,
        supplied_at     DATETIME       DEFAULT GETDATE(),
        to_process      NVARCHAR(100)  NULL,
        tanggal         NVARCHAR(10)   NULL,
        storage_log_id  INT            NULL
    );
    PRINT 'HOSE: tabel supply_log dibuat.';
END
GO

-- View vw_stock_summary untuk Green Hose (stock = IN - OUT per storage record)
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_stock_summary')
    DROP VIEW dbo.vw_stock_summary;
GO
CREATE VIEW dbo.vw_stock_summary AS
SELECT
    sl.log_id,
    sl.item_code,
    sl.full_qr,
    -- Sisa box = yang masuk dikurangi yang sudah keluar
    (sl.box_count - ISNULL(SUM(sup.box_count), 0))   AS current_box_stock,
    -- last_updated: waktu dari supply terakhir atau waktu masuk
    CONVERT(VARCHAR(50),
        CASE
            WHEN MAX(sup.supplied_at) IS NOT NULL
                 AND MAX(sup.supplied_at) > sl.stored_at THEN MAX(sup.supplied_at)
            ELSE sl.stored_at
        END,
        120)                                           AS last_updated,
    -- status_expired berdasarkan production_date + standard_exp hari
    CASE
        WHEN sl.production_date IS NULL OR i.standard_exp IS NULL OR i.standard_exp = 0
             THEN 'Normal'
        WHEN DATEADD(DAY, i.standard_exp, sl.production_date) < CAST(GETDATE() AS DATE)
             THEN 'Expired'
        WHEN DATEADD(DAY, i.standard_exp, sl.production_date)
               <= DATEADD(DAY, 7, CAST(GETDATE() AS DATE))
             THEN 'Near Exp'
        ELSE 'Normal'
    END                                                AS status_expired
FROM dbo.storage_log sl
LEFT JOIN dbo.Items i  ON sl.item_code = i.item_code
LEFT JOIN dbo.supply_log sup ON sl.log_id = sup.storage_log_id
GROUP BY
    sl.log_id, sl.item_code, sl.full_qr,
    sl.box_count, sl.stored_at, sl.production_date,
    i.standard_exp
HAVING (sl.box_count - ISNULL(SUM(sup.box_count), 0)) > 0;
GO
PRINT 'HOSE: view vw_stock_summary dibuat/diperbarui.';
GO

-- ============================================================
-- [2] DB_SUPPLY_RVI  –  items + storage_log + supply_log + vw_stock_summary
-- ============================================================
USE DB_SUPPLY_RVI;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'items')
BEGIN
    CREATE TABLE dbo.items (
        item_code     NVARCHAR(100) NOT NULL PRIMARY KEY,
        mesin         NVARCHAR(100) NULL,
        qty_per_box   DECIMAL(10,2) NULL,
        standard_exp  INT NULL,
        standard_min  INT NULL,
        standard_max  INT NULL,
        created_at    DATETIME DEFAULT GETDATE(),
        updated_at    DATETIME DEFAULT GETDATE()
    );
    PRINT 'RVI: tabel items dibuat.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'storage_log')
BEGIN
    CREATE TABLE dbo.storage_log (
        log_id          INT IDENTITY(1,1) PRIMARY KEY,
        item_code       NVARCHAR(100)  NOT NULL,
        full_qr         NVARCHAR(300)  NULL,
        production_date DATETIME       NULL,
        box_count       INT            DEFAULT 0,
        qty_pcs         INT            DEFAULT 0,
        stored_at       DATETIME       DEFAULT GETDATE(),
        tanggal         NVARCHAR(10)   NULL
    );
    PRINT 'RVI: tabel storage_log dibuat.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'supply_log')
BEGIN
    CREATE TABLE dbo.supply_log (
        log_id          INT IDENTITY(1,1) PRIMARY KEY,
        item_code       NVARCHAR(100)  NOT NULL,
        full_qr         NVARCHAR(300)  NULL,
        box_count       INT            DEFAULT 0,
        qty_pcs         INT            DEFAULT 0,
        supplied_at     DATETIME       DEFAULT GETDATE(),
        to_process      NVARCHAR(100)  NULL,
        tanggal         NVARCHAR(10)   NULL,
        storage_log_id  INT            NULL
    );
    PRINT 'RVI: tabel supply_log dibuat.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'users')
BEGIN
    CREATE TABLE dbo.users (
        id           INT IDENTITY(1,1) PRIMARY KEY,
        username     NVARCHAR(50)  NOT NULL UNIQUE,
        password     NVARCHAR(255) NOT NULL,
        created_date DATETIME DEFAULT GETDATE(),
        last_login   DATETIME NULL,
        plant_id     INT NULL
    );
    PRINT 'RVI: tabel users dibuat.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'raks')
BEGIN
    CREATE TABLE dbo.raks (
        full_qr    NVARCHAR(300) NOT NULL PRIMARY KEY,
        location   NVARCHAR(100) NULL,
        item_code  NVARCHAR(100) NULL,
        created_at DATETIME DEFAULT GETDATE()
    );
    PRINT 'RVI: tabel raks dibuat.';
END
GO

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_stock_summary')
    DROP VIEW dbo.vw_stock_summary;
GO
CREATE VIEW dbo.vw_stock_summary AS
SELECT
    sl.log_id,
    sl.item_code,
    sl.full_qr,
    (sl.box_count - ISNULL(SUM(sup.box_count), 0))   AS current_box_stock,
    CONVERT(VARCHAR(50),
        CASE
            WHEN MAX(sup.supplied_at) IS NOT NULL
                 AND MAX(sup.supplied_at) > sl.stored_at THEN MAX(sup.supplied_at)
            ELSE sl.stored_at
        END,
        120)                                           AS last_updated,
    CASE
        WHEN sl.production_date IS NULL OR i.standard_exp IS NULL OR i.standard_exp = 0
             THEN 'Normal'
        WHEN DATEADD(DAY, i.standard_exp, sl.production_date) < CAST(GETDATE() AS DATE)
             THEN 'Expired'
        WHEN DATEADD(DAY, i.standard_exp, sl.production_date)
               <= DATEADD(DAY, 7, CAST(GETDATE() AS DATE))
             THEN 'Near Exp'
        ELSE 'Normal'
    END                                                AS status_expired
FROM dbo.storage_log sl
LEFT JOIN dbo.items i  ON sl.item_code = i.item_code
LEFT JOIN dbo.supply_log sup ON sl.log_id = sup.storage_log_id
GROUP BY
    sl.log_id, sl.item_code, sl.full_qr,
    sl.box_count, sl.stored_at, sl.production_date,
    i.standard_exp
HAVING (sl.box_count - ISNULL(SUM(sup.box_count), 0)) > 0;
GO
PRINT 'RVI: view vw_stock_summary dibuat/diperbarui.';
GO

-- ============================================================
-- [3] DB_SUPPLY_MOLDED  –  items + storage_log + supply_log + vw_stock_summary
-- ============================================================
USE DB_SUPPLY_MOLDED;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'items')
BEGIN
    CREATE TABLE dbo.items (
        item_code     NVARCHAR(100) NOT NULL PRIMARY KEY,
        mesin         NVARCHAR(100) NULL,
        qty_per_box   DECIMAL(10,2) NULL,
        standard_exp  INT NULL,
        standard_min  INT NULL,
        standard_max  INT NULL,
        created_at    DATETIME DEFAULT GETDATE(),
        updated_at    DATETIME DEFAULT GETDATE()
    );
    PRINT 'MOLDED: tabel items dibuat.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'storage_log')
BEGIN
    CREATE TABLE dbo.storage_log (
        log_id          INT IDENTITY(1,1) PRIMARY KEY,
        item_code       NVARCHAR(100)  NOT NULL,
        full_qr         NVARCHAR(300)  NULL,
        production_date DATETIME       NULL,
        box_count       INT            DEFAULT 0,
        qty_pcs         INT            DEFAULT 0,
        stored_at       DATETIME       DEFAULT GETDATE(),
        tanggal         NVARCHAR(10)   NULL
    );
    PRINT 'MOLDED: tabel storage_log dibuat.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'supply_log')
BEGIN
    CREATE TABLE dbo.supply_log (
        log_id          INT IDENTITY(1,1) PRIMARY KEY,
        item_code       NVARCHAR(100)  NOT NULL,
        full_qr         NVARCHAR(300)  NULL,
        box_count       INT            DEFAULT 0,
        qty_pcs         INT            DEFAULT 0,
        supplied_at     DATETIME       DEFAULT GETDATE(),
        to_process      NVARCHAR(100)  NULL,
        tanggal         NVARCHAR(10)   NULL,
        storage_log_id  INT            NULL
    );
    PRINT 'MOLDED: tabel supply_log dibuat.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'users')
BEGIN
    CREATE TABLE dbo.users (
        id           INT IDENTITY(1,1) PRIMARY KEY,
        username     NVARCHAR(50)  NOT NULL UNIQUE,
        password     NVARCHAR(255) NOT NULL,
        created_date DATETIME DEFAULT GETDATE(),
        last_login   DATETIME NULL,
        plant_id     INT NULL
    );
    PRINT 'MOLDED: tabel users dibuat.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'raks')
BEGIN
    CREATE TABLE dbo.raks (
        full_qr    NVARCHAR(300) NOT NULL PRIMARY KEY,
        location   NVARCHAR(100) NULL,
        item_code  NVARCHAR(100) NULL,
        created_at DATETIME DEFAULT GETDATE()
    );
    PRINT 'MOLDED: tabel raks dibuat.';
END
GO

IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_stock_summary')
    DROP VIEW dbo.vw_stock_summary;
GO
CREATE VIEW dbo.vw_stock_summary AS
SELECT
    sl.log_id,
    sl.item_code,
    sl.full_qr,
    (sl.box_count - ISNULL(SUM(sup.box_count), 0))   AS current_box_stock,
    CONVERT(VARCHAR(50),
        CASE
            WHEN MAX(sup.supplied_at) IS NOT NULL
                 AND MAX(sup.supplied_at) > sl.stored_at THEN MAX(sup.supplied_at)
            ELSE sl.stored_at
        END,
        120)                                           AS last_updated,
    CASE
        WHEN sl.production_date IS NULL OR i.standard_exp IS NULL OR i.standard_exp = 0
             THEN 'Normal'
        WHEN DATEADD(DAY, i.standard_exp, sl.production_date) < CAST(GETDATE() AS DATE)
             THEN 'Expired'
        WHEN DATEADD(DAY, i.standard_exp, sl.production_date)
               <= DATEADD(DAY, 7, CAST(GETDATE() AS DATE))
             THEN 'Near Exp'
        ELSE 'Normal'
    END                                                AS status_expired
FROM dbo.storage_log sl
LEFT JOIN dbo.items i  ON sl.item_code = i.item_code
LEFT JOIN dbo.supply_log sup ON sl.log_id = sup.storage_log_id
GROUP BY
    sl.log_id, sl.item_code, sl.full_qr,
    sl.box_count, sl.stored_at, sl.production_date,
    i.standard_exp
HAVING (sl.box_count - ISNULL(SUM(sup.box_count), 0)) > 0;
GO
PRINT 'MOLDED: view vw_stock_summary dibuat/diperbarui.';
GO

-- ============================================================
-- VERIFIKASI
-- ============================================================
PRINT '--- Verifikasi DB_SUPPLY_HOSE ---';
USE DB_SUPPLY_HOSE;
SELECT TABLE_NAME, TABLE_TYPE
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME IN ('Items','storage_log','supply_log','vw_stock_summary','Items_aw',
                     'storage_log_aw','supply_log_aw','vw_stock_summary_aw')
ORDER BY TABLE_TYPE, TABLE_NAME;
GO

PRINT '--- Verifikasi DB_SUPPLY_RVI ---';
USE DB_SUPPLY_RVI;
SELECT TABLE_NAME, TABLE_TYPE
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME IN ('items','storage_log','supply_log','vw_stock_summary','users','raks')
ORDER BY TABLE_TYPE, TABLE_NAME;
GO

PRINT '--- Verifikasi DB_SUPPLY_MOLDED ---';
USE DB_SUPPLY_MOLDED;
SELECT TABLE_NAME, TABLE_TYPE
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME IN ('items','storage_log','supply_log','vw_stock_summary','users','raks')
ORDER BY TABLE_TYPE, TABLE_NAME;
GO

PRINT '=== SELESAI! Semua tabel & view sudah lengkap. ===';
