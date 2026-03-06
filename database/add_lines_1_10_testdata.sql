-- ================================================================
-- Tambah data Line 1-10 untuk Supply Finishing + Input OUT test
-- ELWP_PRD  : mesin Finishing Line 1-10 + planning hari ini
-- DB_SUPPLY_HOSE: Items_aw + storage_log_aw + raks_aw untuk tiap item
-- ================================================================

-- ================================================================
-- BAGIAN 1 : ELWP_PRD - Finishing Line 1-10 + Planning
-- ================================================================
USE ELWP_PRD;
GO

-- Tambah Finishing Line 1-10 (skip jika sudah ada)
DECLARE @lines TABLE (kode NVARCHAR(20), nama NVARCHAR(100));
INSERT INTO @lines VALUES
    ('FL-01','Finishing Line 1'),
    ('FL-02','Finishing Line 2'),
    ('FL-03','Finishing Line 3'),
    ('FL-04','Finishing Line 4'),
    ('FL-05','Finishing Line 5'),
    ('FL-06','Finishing Line 6'),
    ('FL-07','Finishing Line 7'),
    ('FL-08','Finishing Line 8'),
    ('FL-09','Finishing Line 9'),
    ('FL-10','Finishing Line 10');

INSERT INTO produksi.tb_elwp_produksi_mesins
    (KodeMesin, NamaMesin, PlantId, AreaId, Keterangan, IsActive, CreatedAt, UpdatedAt)
SELECT l.kode, l.nama, 1, 1, 'Line Finishing Hose', 1, GETDATE(), GETDATE()
FROM @lines l
WHERE NOT EXISTS (
    SELECT 1 FROM produksi.tb_elwp_produksi_mesins m WHERE m.NamaMesin = l.nama
);
PRINT CAST(@@ROWCOUNT AS VARCHAR) + ' mesin Finishing Line 1-10 ditambahkan.';
GO

-- Hapus planning line 1-10 hari ini jika sudah ada (biar tidak duplikat)
DELETE p
FROM produksi.tb_elwp_produksi_plannings p
JOIN produksi.tb_elwp_produksi_mesins m ON m.Id = p.MesinId
WHERE m.NamaMesin IN (
    'Finishing Line 1','Finishing Line 2','Finishing Line 3','Finishing Line 4',
    'Finishing Line 5','Finishing Line 6','Finishing Line 7','Finishing Line 8',
    'Finishing Line 9','Finishing Line 10'
)
AND CAST(p.PlanningDate AS DATE) = CAST(GETDATE() AS DATE);

-- Insert planning Line 1-10 untuk hari ini
-- LoadingTimeHours = jam relatif dari shift start (07:00)
-- 0.25 = 07:15, 0.5 = 07:30, dst
DECLARE @l1  INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE NamaMesin = 'Finishing Line 1'  ORDER BY Id);
DECLARE @l2  INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE NamaMesin = 'Finishing Line 2'  ORDER BY Id);
DECLARE @l3  INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE NamaMesin = 'Finishing Line 3'  ORDER BY Id);
DECLARE @l4  INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE NamaMesin = 'Finishing Line 4'  ORDER BY Id);
DECLARE @l5  INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE NamaMesin = 'Finishing Line 5'  ORDER BY Id);
DECLARE @l6  INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE NamaMesin = 'Finishing Line 6'  ORDER BY Id);
DECLARE @l7  INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE NamaMesin = 'Finishing Line 7'  ORDER BY Id);
DECLARE @l8  INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE NamaMesin = 'Finishing Line 8'  ORDER BY Id);
DECLARE @l9  INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE NamaMesin = 'Finishing Line 9'  ORDER BY Id);
DECLARE @l10 INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE NamaMesin = 'Finishing Line 10' ORDER BY Id);

DECLARE @today DATE = CAST(GETDATE() AS DATE);

INSERT INTO produksi.tb_elwp_produksi_plannings
    (PlantId, AreaId, MesinId, TanggalPlanning, PnSap, KodeItem, PartName, QtyPlanning, Shift, LoadingTimeHours, CreatedAt, UpdatedAt, CreatedBy, PlanningDate)
VALUES
-- Line 1 - 2 item (jam 10:00 & 13:00)
(1,1, @l1,  @today,'PN-A001','HOSE-A001','RADIATOR HOSE UPPER 5/16"',   48, 'Shift 1', 3.00, GETDATE(),GETDATE(),1, @today),
(1,1, @l1,  @today,'PN-A002','HOSE-A002','RADIATOR HOSE LOWER 3/8"',    36, 'Shift 1', 6.00, GETDATE(),GETDATE(),1, @today),
-- Line 2
(1,1, @l2,  @today,'PN-A003','HOSE-A003','HEATER HOSE INLET 1/2"',      60, 'Shift 1', 3.25, GETDATE(),GETDATE(),1, @today),
(1,1, @l2,  @today,'PN-A004','HOSE-A004','HEATER HOSE OUTLET 5/8"',     42, 'Shift 1', 6.25, GETDATE(),GETDATE(),1, @today),
-- Line 3
(1,1, @l3,  @today,'PN-A005','HOSE-A005','BYPASS HOSE 3/4"',            30, 'Shift 1', 3.50, GETDATE(),GETDATE(),1, @today),
(1,1, @l3,  @today,'PN-A006','HOSE-A006','COOLANT OVERFLOW HOSE',       24, 'Shift 1', 6.50, GETDATE(),GETDATE(),1, @today),
-- Line 4
(1,1, @l4,  @today,'PN-A007','HOSE-A007','TURBO HOSE INLET',            36, 'Shift 1', 3.75, GETDATE(),GETDATE(),1, @today),
(1,1, @l4,  @today,'PN-A008','HOSE-A008','TURBO HOSE OUTLET',           36, 'Shift 1', 6.75, GETDATE(),GETDATE(),1, @today),
-- Line 5
(1,1, @l5,  @today,'PN-A009','HOSE-A009','INTERCOOLER UPPER HOSE',      48, 'Shift 1', 4.00, GETDATE(),GETDATE(),1, @today),
(1,1, @l5,  @today,'PN-A010','HOSE-A010','INTERCOOLER LOWER HOSE',      48, 'Shift 1', 7.00, GETDATE(),GETDATE(),1, @today),
-- Line 6
(1,1, @l6,  @today,'PN-A011','HOSE-A011','TRANSMISSION COOLER HOSE',    60, 'Shift 1', 4.25, GETDATE(),GETDATE(),1, @today),
(1,1, @l6,  @today,'PN-A012','HOSE-A012','POWER STEERING HOSE HIGH',    30, 'Shift 1', 7.25, GETDATE(),GETDATE(),1, @today),
-- Line 7
(1,1, @l7,  @today,'PN-A013','HOSE-A013','POWER STEERING HOSE LOW',     30, 'Shift 1', 4.50, GETDATE(),GETDATE(),1, @today),
(1,1, @l7,  @today,'PN-A014','HOSE-A014','FUEL SUPPLY HOSE',            54, 'Shift 1', 7.50, GETDATE(),GETDATE(),1, @today),
-- Line 8
(1,1, @l8,  @today,'PN-A015','HOSE-A015','FUEL RETURN HOSE',            54, 'Shift 1', 4.75, GETDATE(),GETDATE(),1, @today),
(1,1, @l8,  @today,'PN-A016','HOSE-A016','BRAKE VACUUM HOSE',           42, 'Shift 1', 7.75, GETDATE(),GETDATE(),1, @today),
-- Line 9
(1,1, @l9,  @today,'PN-A017','HOSE-A017','AIR INTAKE HOSE',             36, 'Shift 1', 5.00, GETDATE(),GETDATE(),1, @today),
(1,1, @l9,  @today,'PN-A018','HOSE-A018','AIR CLEANER HOSE',            36, 'Shift 1', 8.00, GETDATE(),GETDATE(),1, @today),
-- Line 10
(1,1, @l10, @today,'PN-A019','HOSE-A019','WASHER HOSE FRONT',           72, 'Shift 1', 5.25, GETDATE(),GETDATE(),1, @today),
(1,1, @l10, @today,'PN-A020','HOSE-A020','WASHER HOSE REAR',            72, 'Shift 1', 8.25, GETDATE(),GETDATE(),1, @today);

PRINT CAST(@@ROWCOUNT AS VARCHAR) + ' planning entries untuk Line 1-10 ditambahkan.';
GO


-- ================================================================
-- BAGIAN 2 : DB_SUPPLY_HOSE - Items_aw + storage_log_aw + raks_aw
-- ================================================================
USE DB_SUPPLY_HOSE;
GO

-- Buat raks_aw jika belum ada
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'raks_aw'
)
BEGIN
    CREATE TABLE raks_aw (
        id        INT IDENTITY(1,1) PRIMARY KEY,
        kode_rak  NVARCHAR(50),
        full_qr   NVARCHAR(200),
        item_code NVARCHAR(50)
    );
    PRINT 'Tabel raks_aw dibuat.';
END
GO

-- Daftar item Line 1-10
DECLARE @items TABLE (item_code NVARCHAR(50), part_name NVARCHAR(200), qty INT);
INSERT INTO @items VALUES
    ('HOSE-A001','RADIATOR HOSE UPPER 5/16"',   48),
    ('HOSE-A002','RADIATOR HOSE LOWER 3/8"',    36),
    ('HOSE-A003','HEATER HOSE INLET 1/2"',      60),
    ('HOSE-A004','HEATER HOSE OUTLET 5/8"',     42),
    ('HOSE-A005','BYPASS HOSE 3/4"',            30),
    ('HOSE-A006','COOLANT OVERFLOW HOSE',       24),
    ('HOSE-A007','TURBO HOSE INLET',            36),
    ('HOSE-A008','TURBO HOSE OUTLET',           36),
    ('HOSE-A009','INTERCOOLER UPPER HOSE',      48),
    ('HOSE-A010','INTERCOOLER LOWER HOSE',      48),
    ('HOSE-A011','TRANSMISSION COOLER HOSE',    60),
    ('HOSE-A012','POWER STEERING HOSE HIGH',    30),
    ('HOSE-A013','POWER STEERING HOSE LOW',     30),
    ('HOSE-A014','FUEL SUPPLY HOSE',            54),
    ('HOSE-A015','FUEL RETURN HOSE',            54),
    ('HOSE-A016','BRAKE VACUUM HOSE',           42),
    ('HOSE-A017','AIR INTAKE HOSE',             36),
    ('HOSE-A018','AIR CLEANER HOSE',            36),
    ('HOSE-A019','WASHER HOSE FRONT',           72),
    ('HOSE-A020','WASHER HOSE REAR',            72);

-- Insert Items_aw (skip jika sudah ada)
INSERT INTO Items_aw (item_code, mesin, qty_per_box, standard_exp, standard_min, standard_max)
SELECT item_code, 'FL-HOSE', 10, 30, 5, 100
FROM @items i
WHERE NOT EXISTS (SELECT 1 FROM Items_aw aw WHERE aw.item_code = i.item_code);
PRINT CAST(@@ROWCOUNT AS VARCHAR) + ' baris ditambahkan ke Items_aw.';

-- Insert storage_log_aw (skip jika sudah ada hari ini)
INSERT INTO storage_log_aw (item_code, full_qr, production_date, box_count, qty_pcs, stored_at, tanggal)
SELECT
    i.item_code,
    i.item_code + '/' + FORMAT(GETDATE(),'yyyyMMdd') + '/001',
    CAST(GETDATE() AS DATE),
    CEILING(i.qty / 10.0),   -- box_count = qty / 10 (rounded up)
    i.qty,                    -- qty_pcs = qty planning penuh
    DATEADD(HOUR, -3, GETDATE()),
    FORMAT(GETDATE(),'yyyyMMdd')
FROM @items i
WHERE NOT EXISTS (
    SELECT 1 FROM storage_log_aw sl
    WHERE sl.item_code = i.item_code
      AND CAST(sl.stored_at AS DATE) = CAST(GETDATE() AS DATE)
);
PRINT CAST(@@ROWCOUNT AS VARCHAR) + ' baris ditambahkan ke storage_log_aw.';

-- Insert raks_aw (supaya Full QR autocomplete kerja di form INPUT OUT)
INSERT INTO raks_aw (kode_rak, full_qr, item_code)
SELECT
    'RAK-AW-' + RIGHT('000' + CAST(ROW_NUMBER() OVER (ORDER BY sl.log_id) AS VARCHAR), 3),
    sl.full_qr,
    sl.item_code
FROM storage_log_aw sl
JOIN @items i ON i.item_code = sl.item_code
WHERE CAST(sl.stored_at AS DATE) = CAST(GETDATE() AS DATE)
  AND NOT EXISTS (SELECT 1 FROM raks_aw r WHERE r.full_qr = sl.full_qr);
PRINT CAST(@@ROWCOUNT AS VARCHAR) + ' baris ditambahkan ke raks_aw.';
GO

-- ================================================================
-- VERIFIKASI
-- ================================================================
PRINT '';
PRINT '=== STOK TERSEDIA UNTUK INPUT OUT ===';
SELECT
    sl.item_code,
    sl.full_qr,
    sl.qty_pcs   AS [Qty Tersedia],
    sl.box_count AS [Box],
    r.kode_rak
FROM storage_log_aw sl
JOIN raks_aw r ON r.full_qr = sl.full_qr
WHERE sl.item_code LIKE 'HOSE-A%'
  AND CAST(sl.stored_at AS DATE) = CAST(GETDATE() AS DATE)
ORDER BY sl.item_code;
GO
