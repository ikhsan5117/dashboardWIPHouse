-- ================================================================
-- FIX: Insert test data ke DB_SUPPLY_HOSE berdasarkan item code
--      yang SUDAH ADA di ELWP_PRD planning hari ini
-- Jalankan di SSMS sebagai single script (semua database otomatis)
-- ================================================================

USE DB_SUPPLY_HOSE;
GO

-- -------------------------------------------------------
-- STEP 1: Buat tabel temp dari ELWP_PRD planning hari ini
-- -------------------------------------------------------
IF OBJECT_ID('tempdb..#elwp_today') IS NOT NULL DROP TABLE #elwp_today;

SELECT DISTINCT
    p.KodeItem,
    p.PartName,
    p.QtyPlanning,
    m.KodeMesin
INTO #elwp_today
FROM ELWP_PRD.produksi.tb_elwp_produksi_plannings p
JOIN ELWP_PRD.produksi.tb_elwp_produksi_mesins m ON m.Id = p.MesinId
WHERE
    p.PlantId = 1
    AND m.NamaMesin LIKE '%Finishing Line%'
    AND (
        CAST(p.PlanningDate AS DATE) = CAST(GETDATE() AS DATE)
        OR CAST(p.TanggalPlanning AS DATE) = CAST(GETDATE() AS DATE)
    )
    AND p.KodeItem IS NOT NULL;

PRINT 'Item dari ELWP_PRD hari ini:';
SELECT * FROM #elwp_today;
GO

-- -------------------------------------------------------
-- STEP 2: Insert ke Items_aw (skip jika sudah ada)
-- -------------------------------------------------------
INSERT INTO Items_aw (item_code, mesin, qty_per_box, standard_exp, standard_min, standard_max)
SELECT
    e.KodeItem,
    e.KodeMesin,
    10,   -- qty_per_box default
    30,   -- standard_exp (hari)
    5,    -- standard_min (box)
    100   -- standard_max (box)
FROM #elwp_today e
WHERE NOT EXISTS (
    SELECT 1 FROM Items_aw aw WHERE aw.item_code = e.KodeItem
);

PRINT CAST(@@ROWCOUNT AS VARCHAR) + ' baris ditambahkan ke Items_aw.';
GO

-- -------------------------------------------------------
-- STEP 3: Insert ke storage_log_aw (After Washing IN)
--         Dengan qty yang cukup untuk ujicoba OUT
--         Skip item yang sudah punya storage hari ini
-- -------------------------------------------------------
INSERT INTO storage_log_aw (item_code, full_qr, production_date, box_count, qty_pcs, stored_at, tanggal)
SELECT
    e.KodeItem,
    -- Format full_qr: KODEITEM/YYYYMMDD/001
    e.KodeItem + '/' + FORMAT(GETDATE(), 'yyyyMMdd') + '/001',
    CAST(GETDATE() AS DATE),
    10,                              -- box_count
    e.QtyPlanning,                   -- qty_pcs = sama dengan qty planning
    DATEADD(HOUR, -2, GETDATE()),    -- stored 2 jam lalu
    FORMAT(GETDATE(), 'ddMMyyyy')
FROM #elwp_today e
WHERE NOT EXISTS (
    SELECT 1 FROM storage_log_aw sl
    WHERE sl.item_code = e.KodeItem
      AND CAST(sl.stored_at AS DATE) = CAST(GETDATE() AS DATE)
);

PRINT CAST(@@ROWCOUNT AS VARCHAR) + ' baris ditambahkan ke storage_log_aw.';
GO

-- -------------------------------------------------------
-- STEP 4: Buat tabel raks_aw jika belum ada, lalu insert
-- -------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'raks_aw' AND TABLE_SCHEMA = 'dbo'
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

INSERT INTO raks_aw (kode_rak, full_qr, item_code)
SELECT
    'RAK-AW-' + RIGHT('0' + CAST(ROW_NUMBER() OVER (ORDER BY sl.log_id) AS VARCHAR), 2),
    sl.full_qr,
    sl.item_code
FROM storage_log_aw sl
JOIN #elwp_today e ON e.KodeItem = sl.item_code
WHERE CAST(sl.stored_at AS DATE) = CAST(GETDATE() AS DATE)
  AND NOT EXISTS (
    SELECT 1 FROM raks_aw r WHERE r.full_qr = sl.full_qr
  );

PRINT CAST(@@ROWCOUNT AS VARCHAR) + ' baris ditambahkan ke raks_aw.';
GO

-- -------------------------------------------------------
-- STEP 5: Verifikasi hasil
-- -------------------------------------------------------
PRINT '';
PRINT '=== VERIFIKASI DATA ===';

SELECT
    sl.item_code,
    sl.full_qr,
    sl.box_count,
    sl.qty_pcs,
    sl.stored_at,
    r.kode_rak
FROM storage_log_aw sl
JOIN #elwp_today e ON e.KodeItem = sl.item_code
LEFT JOIN raks_aw r ON r.full_qr = sl.full_qr
WHERE CAST(sl.stored_at AS DATE) = CAST(GETDATE() AS DATE)
ORDER BY sl.item_code;

DROP TABLE IF EXISTS #elwp_today;
GO

PRINT '';
PRINT 'SELESAI! Sekarang:';
PRINT '1. Buka After Washing INPUT OUT';
PRINT '2. Isi Item Code -> klik item card -> Full QR auto-fill';
PRINT '3. Submit OUT -> Supply Finishing Dashboard langsung update via SignalR';
GO
