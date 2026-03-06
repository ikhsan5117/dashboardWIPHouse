-- =============================================================
-- TEST DATA: Buat INPUT IN otomatis dari planning hari ini
-- Jalankan di SQL Server LOKAL
-- Tujuan: simulasi barang sudah masuk After Washing
--         supaya bisa dicoba INPUT OUT di dashboard
-- =============================================================
USE DB_SUPPLY_HOSE;
GO

DECLARE @today DATE = CAST(GETDATE() AS DATE);
DECLARE @todayStr VARCHAR(10) = CONVERT(VARCHAR, GETDATE(), 23);

-- Ambil semua item dari planning hari ini yang BELUM ada di storage_log_aw hari ini
DECLARE @items TABLE (
    item_code  VARCHAR(50),
    part_name  NVARCHAR(200),
    qty        INT,
    mesin      NVARCHAR(200)
);

INSERT INTO @items (item_code, part_name, qty, mesin)
SELECT DISTINCT
    p.KodeItem,
    ISNULL(p.PartName, p.KodeItem),
    p.QtyPlanning,
    m.NamaMesin
FROM ELWP_PRD.produksi.tb_elwp_produksi_plannings p
JOIN ELWP_PRD.produksi.tb_elwp_produksi_mesins m ON p.MesinId = m.Id
WHERE p.PlanningDate = @today
  AND m.NamaMesin LIKE '%Finishing Line%'
  AND p.KodeItem NOT IN (
      SELECT DISTINCT item_code
      FROM storage_log_aw
      WHERE production_date = @today
  );

DECLARE @cnt INT = (SELECT COUNT(*) FROM @items);
PRINT CONCAT('Item yang akan dibuat stok-nya: ', @cnt);

-- =============================================================
-- 1. Tambah ke Items_aw jika belum ada
-- =============================================================
INSERT INTO Items_aw (item_code, part_name, mesin, qty_per_box, standard_exp, standard_min, standard_max)
SELECT
    i.item_code,
    i.part_name,
    i.mesin,
    10,   -- default qty per box
    30,   -- standard exp (hari)
    0,
    0
FROM @items i
WHERE NOT EXISTS (SELECT 1 FROM Items_aw WHERE item_code = i.item_code);

PRINT CONCAT('Items_aw baru ditambahkan: ', @@ROWCOUNT);

-- =============================================================
-- 2. Tambah ke storage_log_aw (simulasi INPUT IN)
-- Full QR format: item_code/YYYYMMDD/001
-- =============================================================
SET IDENTITY_INSERT storage_log_aw OFF; -- pastikan IDENTITY aktif

INSERT INTO storage_log_aw (item_code, full_qr, production_date, box_count, qty_pcs, stored_at, tanggal, transaction_type)
SELECT
    i.item_code,
    i.item_code + '/' + REPLACE(@todayStr, '-', '') + '/001' AS full_qr,
    @today,
    CEILING(CAST(i.qty AS FLOAT) / 10.0),  -- box = qty / 10 (dibulatkan ke atas)
    i.qty,
    GETDATE(),
    @todayStr,
    'IN'
FROM @items i;

PRINT CONCAT('storage_log_aw entries dibuat: ', @@ROWCOUNT);

-- =============================================================
-- 3. Tambah ke raks_aw (lokasi rak default)
-- =============================================================
INSERT INTO raks_aw (full_qr, kode_rak, item_code)
SELECT
    i.item_code + '/' + REPLACE(@todayStr, '-', '') + '/001',
    'RACK-TEST',
    i.item_code
FROM @items i
WHERE NOT EXISTS (
    SELECT 1 FROM raks_aw
    WHERE full_qr = i.item_code + '/' + REPLACE(@todayStr, '-', '') + '/001'
);

PRINT CONCAT('raks_aw entries dibuat: ', @@ROWCOUNT);

-- =============================================================
-- VERIFIKASI
-- =============================================================
SELECT
    s.item_code,
    s.full_qr,
    s.qty_pcs,
    s.box_count,
    s.transaction_type,
    s.stored_at
FROM storage_log_aw s
WHERE s.production_date = @today
  AND s.transaction_type = 'IN'
ORDER BY s.stored_at DESC;
GO
