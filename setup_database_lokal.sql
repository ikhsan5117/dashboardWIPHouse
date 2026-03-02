-- ============================================================
-- SCRIPT SETUP DATABASE LOKAL - VERSI LENGKAP DENGAN PLANT BTR
-- Jalankan SEKALI di SQL Server lokal (localhost) via SSMS
-- Mendukung: Input AW, Supply Finishing, Multi-Plant
-- ============================================================

SET NOCOUNT ON;
GO

-- ============================================================
-- [1] DATABASE UTAMA APLIKASI: DB_SUPPLY_HOSE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'DB_SUPPLY_HOSE')
BEGIN
    CREATE DATABASE DB_SUPPLY_HOSE;
    PRINT 'Database DB_SUPPLY_HOSE dibuat.';
END
GO

USE DB_SUPPLY_HOSE;
GO

-- Tabel storage_log_aw (INPUT IN)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'storage_log_aw')
BEGIN
    CREATE TABLE dbo.storage_log_aw (
        log_id       INT PRIMARY KEY IDENTITY(1,1),
        item_code    NVARCHAR(50),
        full_qr      NVARCHAR(200),
        box_count    INT DEFAULT 0,
        qty_pcs      INT DEFAULT 0,
        stored_at    DATETIME DEFAULT GETDATE(),
        tanggal      NVARCHAR(20),
        production_date DATETIME NULL
    );
    PRINT 'Tabel storage_log_aw dibuat.';
END
GO

-- Tabel supply_log_aw (INPUT OUT) - dengan plant_id!
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'supply_log_aw')
BEGIN
    CREATE TABLE dbo.supply_log_aw (
        log_id      INT PRIMARY KEY IDENTITY(1,1),
        item_code   NVARCHAR(50),
        full_qr     NVARCHAR(200),
        box_count   INT DEFAULT 0,
        qty_pcs     INT DEFAULT 0,
        supplied_at DATETIME DEFAULT GETDATE(),
        tanggal     NVARCHAR(20),
        plant_id    INT NULL   -- 1=HOSE, 2=RVI, 3=MOLDED, 4=BTR
    );
    PRINT 'Tabel supply_log_aw dibuat.';
END
ELSE
BEGIN
    -- Tambah kolom plant_id jika belum ada (untuk yang sudah punya tabel lama)
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'plant_id' AND Object_ID = Object_ID('supply_log_aw'))
    BEGIN
        ALTER TABLE dbo.supply_log_aw ADD plant_id INT NULL;
        PRINT 'Kolom plant_id ditambahkan ke supply_log_aw.';
    END
END
GO

-- Tabel items_aw (master item After Washing)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Items_aw')
BEGIN
    CREATE TABLE dbo.Items_aw (
        item_code       NVARCHAR(50) PRIMARY KEY,
        part_name       NVARCHAR(200),
        mesin           NVARCHAR(100),
        qty_per_box     DECIMAL(10,2),
        standard_exp    INT,
        standard_min    INT,
        standard_max    INT
    );
    INSERT INTO dbo.Items_aw (item_code, part_name, mesin, qty_per_box) VALUES
        ('TEST-BTR-001', 'BTR Part Alpha',  'Finishing Line 1', 100),
        ('TEST-BTR-002', 'BTR Part Beta',   'Finishing Line 2', 100),
        ('TEST-BTR-003', 'BTR Part Gamma',  'Finishing Line 3', 100),
        ('NA1120',       'REINFORCEMENT FR', 'Finishing Line 1', 48),
        ('NA1600',       'STAY FRONT BUMPER','Finishing Line 1', 20);
    PRINT 'Tabel Items_aw + data awal dibuat.';
END
GO

-- View vw_stock_summary_aw
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_stock_summary_aw')
    DROP VIEW dbo.vw_stock_summary_aw;
GO
CREATE VIEW dbo.vw_stock_summary_aw AS
SELECT
    s.item_code,
    s.full_qr,
    SUM(CASE WHEN s.stored_at IS NOT NULL THEN 1 ELSE 0 END) AS current_box_stock,
    MAX(CONVERT(VARCHAR(50), s.stored_at, 120)) AS last_updated
FROM dbo.storage_log_aw s
GROUP BY s.item_code, s.full_qr;
GO
PRINT 'View vw_stock_summary_aw dibuat.';
GO

-- ============================================================
-- [2] DATABASE ELWP LOKAL: ELWP_PRD (dengan PlantId)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'ELWP_PRD')
BEGIN
    CREATE DATABASE ELWP_PRD;
    PRINT 'Database ELWP_PRD dibuat.';
END
GO

USE ELWP_PRD;
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'produksi')
    EXEC('CREATE SCHEMA produksi');
GO

-- Tabel Master Mesin (dengan PlantId & KodeMesin)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tb_elwp_produksi_mesins' AND schema_id = SCHEMA_ID('produksi'))
BEGIN
    CREATE TABLE produksi.tb_elwp_produksi_mesins (
        Id          INT PRIMARY KEY IDENTITY(1,1),
        KodeMesin   NVARCHAR(50),
        NamaMesin   NVARCHAR(100),
        PlantId     INT DEFAULT 1,
        AreaId      INT NULL,
        Keterangan  NVARCHAR(200) NULL,
        IsActive    BIT DEFAULT 1,
        CreatedAt   DATETIME DEFAULT GETDATE(),
        UpdatedAt   DATETIME NULL
    );

    -- Masukkan Finishing Line 1-22 untuk setiap Plant
    -- Plant 1 (HOSE)
    DECLARE @i INT = 1;
    WHILE @i <= 22 BEGIN
        INSERT INTO produksi.tb_elwp_produksi_mesins (KodeMesin, NamaMesin, PlantId)
        VALUES ('HOSE-FL-' + RIGHT('0'+CAST(@i AS VARCHAR),2), 'Finishing Line ' + CAST(@i AS VARCHAR), 1);
        SET @i = @i + 1;
    END

    -- Plant 2 (RVI) - Line 1-5
    SET @i = 1;
    WHILE @i <= 5 BEGIN
        INSERT INTO produksi.tb_elwp_produksi_mesins (KodeMesin, NamaMesin, PlantId)
        VALUES ('RVI-FL-' + RIGHT('0'+CAST(@i AS VARCHAR),2), 'Finishing Line ' + CAST(@i AS VARCHAR), 2);
        SET @i = @i + 1;
    END

    -- Plant 4 (BTR) - Line 1-3 (untuk test)
    INSERT INTO produksi.tb_elwp_produksi_mesins (KodeMesin, NamaMesin, PlantId) VALUES
        ('BTR-FL-01', 'Finishing Line 1', 4),
        ('BTR-FL-02', 'Finishing Line 2', 4),
        ('BTR-FL-03', 'Finishing Line 3', 4);

    PRINT 'Tabel tb_elwp_produksi_mesins + data Finishing Lines (HOSE/RVI/BTR) dibuat.';
END
GO

-- Tabel Planning (dengan PlantId)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tb_elwp_produksi_plannings' AND schema_id = SCHEMA_ID('produksi'))
BEGIN
    CREATE TABLE produksi.tb_elwp_produksi_plannings (
        Id              INT PRIMARY KEY IDENTITY(1,1),
        PlantId         INT DEFAULT 1,
        AreaId          INT NULL,
        MesinId         INT,
        TanggalPlanning DATE,
        PnSap           NVARCHAR(50) NULL,
        KodeItem        NVARCHAR(50),
        PartName        NVARCHAR(200),
        QtyPlanning     INT,
        Shift           NVARCHAR(20),
        LoadingTimeHours DECIMAL(18,2),
        PlanningDate    DATE NULL,
        CreatedAt       DATETIME DEFAULT GETDATE(),
        UpdatedAt       DATETIME NULL,
        CreatedBy       INT NULL
    );
    PRINT 'Tabel tb_elwp_produksi_plannings dibuat.';
END
GO

-- ============================================================
-- [3] INSERT DATA TEST UNTUK HARI INI
-- ============================================================
USE ELWP_PRD;
GO

-- Hapus data test lama dulu
DELETE FROM produksi.tb_elwp_produksi_plannings
WHERE KodeItem LIKE 'TEST-BTR-%' OR KodeItem IN ('NA1120','NA1600','NA1620');

DECLARE @Today DATE = CAST(GETDATE() AS DATE);

-- Ambil ID mesin BTR
DECLARE @BTR1 INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE KodeMesin = 'BTR-FL-01');
DECLARE @BTR2 INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE KodeMesin = 'BTR-FL-02');
DECLARE @BTR3 INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE KodeMesin = 'BTR-FL-03');

-- Ambil ID mesin HOSE
DECLARE @HOSE1 INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE KodeMesin = 'HOSE-FL-01');
DECLARE @HOSE2 INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE KodeMesin = 'HOSE-FL-02');

-- Data Planning BTR (Plant 4) - untuk uji coba hari ini
INSERT INTO produksi.tb_elwp_produksi_plannings
    (PlantId, MesinId, TanggalPlanning, KodeItem, PartName, QtyPlanning, Shift, LoadingTimeHours, PlanningDate)
VALUES
    (4, @BTR1, @Today, 'TEST-BTR-001', 'BTR Part Alpha',  500, '1', 9.00,  @Today),  -- Jam 09:00 (sudah lewat = MERAH)
    (4, @BTR2, @Today, 'TEST-BTR-002', 'BTR Part Beta',   300, '1', 13.00, @Today),  -- Jam 13:00
    (4, @BTR3, @Today, 'TEST-BTR-003', 'BTR Part Gamma',  200, '2', 16.00, @Today);  -- Jam 16:00

-- Data Planning HOSE (Plant 1) - tambahan
INSERT INTO produksi.tb_elwp_produksi_plannings
    (PlantId, MesinId, TanggalPlanning, KodeItem, PartName, QtyPlanning, Shift, LoadingTimeHours, PlanningDate)
VALUES
    (1, @HOSE1, @Today, 'NA1120', 'REINFORCEMENT FR',    48, 'Shift 1', 10.00, @Today),
    (1, @HOSE2, @Today, 'NA1600', 'STAY FRONT BUMPER',   20, 'Shift 1', 11.30, @Today);

PRINT 'Data Planning BTR & HOSE untuk hari ini berhasil dimasukkan!';
GO

-- ============================================================
-- [4] DATABASE TAMBAHAN (agar aplikasi tidak error)
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'DB_SUPPLY_HOSE_TRIAL') CREATE DATABASE DB_SUPPLY_HOSE_TRIAL;
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'DB_SUPPLY_RVI')         CREATE DATABASE DB_SUPPLY_RVI;
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'DB_SUPPLY_MOLDED')      CREATE DATABASE DB_SUPPLY_MOLDED;
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'DB_SUPPLY_BTR')         CREATE DATABASE DB_SUPPLY_BTR;
GO

-- ============================================================
-- [5] VERIFIKASI AKHIR
-- ============================================================
USE ELWP_PRD;
SELECT 
    p.KodeItem, p.PartName, p.QtyPlanning,
    CAST(p.LoadingTimeHours AS VARCHAR) + ':00' AS JamRencana,
    m.NamaMesin, m.PlantId,
    CASE m.PlantId WHEN 1 THEN 'HOSE' WHEN 2 THEN 'RVI' WHEN 4 THEN 'BTR' ELSE '?' END AS Plant
FROM produksi.tb_elwp_produksi_plannings p
JOIN produksi.tb_elwp_produksi_mesins m ON m.Id = p.MesinId
WHERE p.TanggalPlanning = CAST(GETDATE() AS DATE)
ORDER BY m.PlantId, p.LoadingTimeHours;
GO

PRINT '=======================================================';
PRINT 'SETUP LOKAL SELESAI!';
PRINT '';
PRINT 'Langkah selanjutnya:';
PRINT '1. Ganti appsettings.json -> Server=localhost';
PRINT '2. Jalankan aplikasi (dotnet run)';
PRINT '3. Login sbg adminBTR -> Supply Finishing';
PRINT '4. Lakukan INPUT OUT item TEST-BTR-001 -> Plant BTR';
PRINT '5. Refresh dashboard -> Kartu jadi HIJAU!';
PRINT '=======================================================';
