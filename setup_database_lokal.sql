-- ============================================================
-- SCRIPT SETUP DATABASE LOKAL UNTUK DASHBOARD PLANNING (ROBUST VERSION)
-- Jalankan script ini di SQL Server (SSMS) laptop rumah Anda.
-- ============================================================

SET NOCOUNT ON;
GO

-- 1. SETUP DATABASE LOKAL APLIKASI (UTAMA)
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'DB_SUPPLY_HOSE')
BEGIN
    CREATE DATABASE DB_SUPPLY_HOSE;
    PRINT 'Database DB_SUPPLY_HOSE berhasil dibuat.';
END
GO

USE DB_SUPPLY_HOSE;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SupplyLogAW')
BEGIN
    CREATE TABLE dbo.SupplyLogAW (
        Id INT PRIMARY KEY IDENTITY(1,1),
        ItemCode NVARCHAR(50),
        SuppliedAt DATETIME DEFAULT GETDATE()
    );
    PRINT 'Tabel SupplyLogAW berhasil dibuat.';
END
GO

-- 2. SETUP DATABASE PUSAT SIMULASI (ELWP_PRD)
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'ELWP_PRD')
BEGIN
    CREATE DATABASE ELWP_PRD;
    PRINT 'Database ELWP_PRD berhasil dibuat.';
END
GO

USE ELWP_PRD;
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'produksi')
BEGIN
    EXEC('CREATE SCHEMA produksi');
    PRINT 'Schema produksi berhasil dibuat.';
END
GO

-- Tabel Master Mesin
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tb_elwp_produksi_mesins' AND schema_id = SCHEMA_ID('produksi'))
BEGIN
    CREATE TABLE produksi.tb_elwp_produksi_mesins (
        Id INT PRIMARY KEY IDENTITY(1,1),
        NamaMesin NVARCHAR(100),
        IsActive BIT DEFAULT 1
    );

    -- Isi Mesin Finishing Line 1 s/d 22
    DECLARE @i INT = 1;
    WHILE @i <= 22
    BEGIN
        INSERT INTO produksi.tb_elwp_produksi_mesins (NamaMesin) VALUES ('Finishing Line ' + CAST(@i AS VARCHAR));
        SET @i = @i + 1;
    END
    PRINT 'Tabel tb_elwp_produksi_mesins & Data Awal berhasil dibuat.';
END
GO

-- Tabel Planning
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tb_elwp_produksi_plannings' AND schema_id = SCHEMA_ID('produksi'))
BEGIN
    CREATE TABLE produksi.tb_elwp_produksi_plannings (
        Id INT PRIMARY KEY IDENTITY(1,1),
        MesinId INT,
        KodeItem NVARCHAR(50),
        PartName NVARCHAR(200),
        QtyPlanning INT,
        Shift NVARCHAR(20),
        PlanningDate DATETIME,
        TanggalPlanning DATETIME,
        LoadingTimeHours FLOAT,
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME
    );
    PRINT 'Tabel tb_elwp_produksi_plannings berhasil dibuat.';
END
GO

-- View Finishing (Selalu Update)
IF EXISTS (SELECT * FROM sys.views WHERE name = 'View_ELWP_Planning_Finishing' AND schema_id = SCHEMA_ID('produksi'))
BEGIN
    DROP VIEW produksi.View_ELWP_Planning_Finishing;
END
GO

CREATE VIEW produksi.View_ELWP_Planning_Finishing AS
SELECT 
    p.Id AS PlanningId,
    p.MesinId,
    m.NamaMesin,
    TRY_CAST(REPLACE(m.NamaMesin, 'Finishing Line ', '') AS INT) AS LineNumber,
    p.KodeItem,
    p.PartName,
    p.QtyPlanning,
    p.Shift,
    CAST(COALESCE(p.PlanningDate, p.TanggalPlanning) AS DATE) AS Tanggal,
    p.LoadingTimeHours AS RawLoadingTime
FROM produksi.tb_elwp_produksi_plannings p
JOIN produksi.tb_elwp_produksi_mesins m ON p.MesinId = m.Id
WHERE m.NamaMesin LIKE 'Finishing Line %';
GO

-- 3. SETUP DATABASE TAMBAHAN (HANYA AGAR APLIKASI TIDAK ERROR SAAT STARTUP)
-- Jika appsettings Anda butuh database lain, kita buatkan kosongan saja.
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'DB_SUPPLY_HOSE_TRIAL') CREATE DATABASE DB_SUPPLY_HOSE_TRIAL;
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'DB_SUPPLY_RVI') CREATE DATABASE DB_SUPPLY_RVI;
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'DB_SUPPLY_MOLDED') CREATE DATABASE DB_SUPPLY_MOLDED;
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'DB_SUPPLY_BTR') CREATE DATABASE DB_SUPPLY_BTR;
GO

-- 4. ISI DATA CONTOH (SAMPEL)
USE ELWP_PRD;
DELETE FROM produksi.tb_elwp_produksi_plannings;

INSERT INTO produksi.tb_elwp_produksi_plannings 
(MesinId, KodeItem, PartName, QtyPlanning, Shift, TanggalPlanning, LoadingTimeHours)
VALUES 
(1, 'NA1120', 'REINFORCEMENT FR', 48, 'Shift 1', GETDATE(), 0.5),
(1, 'NA1600', 'STAY FRONT BUMPER', 20, 'Shift 1', GETDATE(), 1.0),
(2, 'NA1620', 'REINFORCEMENT RR', 33, 'Shift 1', GETDATE(), 0.7),
(4, 'PM1140', 'PLATE BINDING', 24, 'Shift 1', GETDATE(), 1.5),
(12, 'TA0570', 'BRACKET COMP R', 175, 'Shift 1', GETDATE(), 2.2);

USE DB_SUPPLY_HOSE;
TRUNCATE TABLE dbo.SupplyLogAW;
INSERT INTO dbo.SupplyLogAW (ItemCode, SuppliedAt) VALUES ('NA1120', GETDATE());

PRINT '-------------------------------------------------------';
PRINT 'SEMUA DB BERHASIL DISIAPKAN!';
PRINT 'Jangan lupa ganti Server=localhost di appsettings.json';
PRINT '-------------------------------------------------------';
