-- ================================================================
-- TEST DATA - Supply Finishing Dashboard & After Washing
-- Database: ELWP_PRD + DB_SUPPLY_HOSE
-- Tanggal:  2026-03-03
-- ================================================================

-- ================================================================
-- BAGIAN 1: ELWP_PRD  (Mesin & Planning)
-- ================================================================
USE ELWP_PRD;
GO

-- Buat schema produksi jika belum ada
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'produksi')
    EXEC('CREATE SCHEMA produksi');
GO

-- Buat tabel mesin jika belum ada
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'produksi' AND TABLE_NAME = 'tb_elwp_produksi_mesins'
)
BEGIN
    CREATE TABLE produksi.tb_elwp_produksi_mesins (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        KodeMesin   NVARCHAR(50),
        NamaMesin   NVARCHAR(100),
        PlantId     INT,
        AreaId      INT,
        Keterangan  NVARCHAR(200),
        IsActive    BIT DEFAULT 1,
        CreatedAt   DATETIME DEFAULT GETDATE(),
        UpdatedAt   DATETIME DEFAULT GETDATE()
    );
    PRINT 'Tabel tb_elwp_produksi_mesins dibuat.';
END
GO

-- Buat tabel planning jika belum ada
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'produksi' AND TABLE_NAME = 'tb_elwp_produksi_plannings'
)
BEGIN
    CREATE TABLE produksi.tb_elwp_produksi_plannings (
        Id               INT IDENTITY(1,1) PRIMARY KEY,
        PlantId          INT,
        AreaId           INT,
        MesinId          INT,
        TanggalPlanning  DATETIME,
        PnSap            NVARCHAR(50),
        KodeItem         NVARCHAR(50),
        PartName         NVARCHAR(200),
        QtyPlanning      INT,
        Shift            NVARCHAR(10),
        LoadingTimeHours DECIMAL(18,2),
        CreatedAt        DATETIME DEFAULT GETDATE(),
        UpdatedAt        DATETIME DEFAULT GETDATE(),
        CreatedBy        INT,
        PlanningDate     DATE
    );
    PRINT 'Tabel tb_elwp_produksi_plannings dibuat.';
END
GO

-- -------------------------------------------------------
-- Insert Finishing Lines (skip jika KodeMesin sudah ada)
-- -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM produksi.tb_elwp_produksi_mesins WHERE KodeMesin = 'FL-01')
    INSERT INTO produksi.tb_elwp_produksi_mesins (KodeMesin, NamaMesin, PlantId, AreaId, Keterangan, IsActive, CreatedAt, UpdatedAt)
    VALUES ('FL-01', 'Finishing Line 1', 1, 1, 'Line Assembly Hose 1', 1, GETDATE(), GETDATE());

IF NOT EXISTS (SELECT 1 FROM produksi.tb_elwp_produksi_mesins WHERE KodeMesin = 'FL-02')
    INSERT INTO produksi.tb_elwp_produksi_mesins (KodeMesin, NamaMesin, PlantId, AreaId, Keterangan, IsActive, CreatedAt, UpdatedAt)
    VALUES ('FL-02', 'Finishing Line 2', 1, 1, 'Line Assembly Hose 2', 1, GETDATE(), GETDATE());

IF NOT EXISTS (SELECT 1 FROM produksi.tb_elwp_produksi_mesins WHERE KodeMesin = 'FL-03')
    INSERT INTO produksi.tb_elwp_produksi_mesins (KodeMesin, NamaMesin, PlantId, AreaId, Keterangan, IsActive, CreatedAt, UpdatedAt)
    VALUES ('FL-03', 'Finishing Line 3', 1, 1, 'Line Assembly Hose 3', 1, GETDATE(), GETDATE());

PRINT '3 Finishing Lines siap.';
GO

-- -------------------------------------------------------
-- Insert Planning hari ini (hapus dulu jika sudah ada)
-- -------------------------------------------------------
DELETE FROM produksi.tb_elwp_produksi_plannings
WHERE PlanningDate = '2026-03-03' AND KodeItem IN ('HSE-001','HSE-002','HSE-003','HSE-004','HSE-005','HSE-006');

DECLARE @IdLine1 INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE KodeMesin = 'FL-01');
DECLARE @IdLine2 INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE KodeMesin = 'FL-02');
DECLARE @IdLine3 INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE KodeMesin = 'FL-03');

INSERT INTO produksi.tb_elwp_produksi_plannings
    (PlantId, AreaId, MesinId, TanggalPlanning, PnSap, KodeItem, PartName, QtyPlanning, Shift, LoadingTimeHours, CreatedAt, UpdatedAt, CreatedBy, PlanningDate)
VALUES
--  PlantId  AreaId  MesinId     TanggalPlan     PnSap      KodeItem   PartName                        Qty   Shift   LoadTime
    (1, 1,   @IdLine1, '2026-03-03', 'PN-HSE001', 'HSE-001', 'Hose Assembly Type A - 5/16"',     200, 'A',  10.00, GETDATE(), GETDATE(), 1, '2026-03-03'),
    (1, 1,   @IdLine1, '2026-03-03', 'PN-HSE002', 'HSE-002', 'Hose Assembly Type B - 3/8"',      150, 'A',  13.00, GETDATE(), GETDATE(), 1, '2026-03-03'),
    (1, 1,   @IdLine2, '2026-03-03', 'PN-HSE003', 'HSE-003', 'Hose Connector Flex C - 1/2"',     300, 'A',  10.50, GETDATE(), GETDATE(), 1, '2026-03-03'),
    (1, 1,   @IdLine2, '2026-03-03', 'PN-HSE004', 'HSE-004', 'Hose Connector Flex D - 5/8"',     200, 'B',  14.00, GETDATE(), GETDATE(), 1, '2026-03-03'),
    (1, 1,   @IdLine3, '2026-03-03', 'PN-HSE005', 'HSE-005', 'Rubber Hose Standard E - Small',   180, 'A',  11.00, GETDATE(), GETDATE(), 1, '2026-03-03'),
    (1, 1,   @IdLine3, '2026-03-03', 'PN-HSE006', 'HSE-006', 'Rubber Hose Standard F - Large',   220, 'B',  15.50, GETDATE(), GETDATE(), 1, '2026-03-03');

PRINT '6 Planning entries untuk 2026-03-03 siap.';
GO

-- ================================================================
-- BAGIAN 2: DB_SUPPLY_HOSE  (Items_aw & storage_log_aw)
-- ================================================================
USE DB_SUPPLY_HOSE;
GO

-- -------------------------------------------------------
-- Insert Items_aw master (skip jika sudah ada)
-- -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Items_aw WHERE item_code = 'HSE-001')
    INSERT INTO Items_aw (item_code, mesin, qty_per_box, standard_exp, standard_min, standard_max)
    VALUES ('HSE-001', 'FL-01', 10, 30, 5, 50);

IF NOT EXISTS (SELECT 1 FROM Items_aw WHERE item_code = 'HSE-002')
    INSERT INTO Items_aw (item_code, mesin, qty_per_box, standard_exp, standard_min, standard_max)
    VALUES ('HSE-002', 'FL-01', 10, 30, 5, 50);

IF NOT EXISTS (SELECT 1 FROM Items_aw WHERE item_code = 'HSE-003')
    INSERT INTO Items_aw (item_code, mesin, qty_per_box, standard_exp, standard_min, standard_max)
    VALUES ('HSE-003', 'FL-02', 12, 30, 5, 60);

IF NOT EXISTS (SELECT 1 FROM Items_aw WHERE item_code = 'HSE-004')
    INSERT INTO Items_aw (item_code, mesin, qty_per_box, standard_exp, standard_min, standard_max)
    VALUES ('HSE-004', 'FL-02', 12, 30, 5, 60);

IF NOT EXISTS (SELECT 1 FROM Items_aw WHERE item_code = 'HSE-005')
    INSERT INTO Items_aw (item_code, mesin, qty_per_box, standard_exp, standard_min, standard_max)
    VALUES ('HSE-005', 'FL-03', 15, 30, 5, 75);

IF NOT EXISTS (SELECT 1 FROM Items_aw WHERE item_code = 'HSE-006')
    INSERT INTO Items_aw (item_code, mesin, qty_per_box, standard_exp, standard_min, standard_max)
    VALUES ('HSE-006', 'FL-03', 15, 30, 5, 75);

PRINT '6 Items_aw master siap.';
GO

-- -------------------------------------------------------
-- Insert storage_log_aw  (After Washing IN)
-- Ini yang muncul saat klik planning card -> auto-fill qty
-- -------------------------------------------------------
DELETE FROM storage_log_aw
WHERE item_code IN ('HSE-001','HSE-002','HSE-003','HSE-004','HSE-005','HSE-006')
  AND CAST(stored_at AS DATE) = '2026-03-03';

INSERT INTO storage_log_aw (item_code, full_qr, production_date, box_count, qty_pcs, stored_at, tanggal)
VALUES
--  item_code   full_qr                         prod_date      box  qty   stored_at                  tanggal
    ('HSE-001', 'HSE-001/20260303/001',          '2026-03-03',  20, 200, '2026-03-03 07:30:00',      '20260303'),
    ('HSE-002', 'HSE-002/20260303/001',          '2026-03-03',  15, 150, '2026-03-03 07:45:00',      '20260303'),
    ('HSE-003', 'HSE-003/20260303/001',          '2026-03-03',  25, 300, '2026-03-03 08:00:00',      '20260303'),
    ('HSE-004', 'HSE-004/20260303/001',          '2026-03-03',  20, 200, '2026-03-03 08:15:00',      '20260303'),
    ('HSE-005', 'HSE-005/20260303/001',          '2026-03-03',  18, 180, '2026-03-03 08:30:00',      '20260303'),
    ('HSE-006', 'HSE-006/20260303/001',          '2026-03-03',  22, 220, '2026-03-03 08:45:00',      '20260303');

PRINT '6 storage_log_aw (After Washing IN) siap.';

PRINT '';
PRINT '================================================================';
PRINT 'SELESAI! Ringkasan data test:';
PRINT '================================================================';
PRINT 'ELWP_PRD:';
PRINT '  - Finishing Line 1 (HSE-001 jam 10:00, HSE-002 jam 13:00)';
PRINT '  - Finishing Line 2 (HSE-003 jam 10:30, HSE-004 jam 14:00)';
PRINT '  - Finishing Line 3 (HSE-005 jam 11:00, HSE-006 jam 15:30)';
PRINT '';
PRINT 'DB_SUPPLY_HOSE:';
PRINT '  - 6 items master di Items_aw';
PRINT '  - 6 records di storage_log_aw dengan qty penuh';
PRINT '';
PRINT 'UJICOBA:';
PRINT '  1. Buka Supply Finishing Dashboard -> 6 planning card muncul di 3 line';
PRINT '  2. Buka After Washing INPUT OUT -> klik planning card -> qty auto-fill';
PRINT '================================================================';
GO
