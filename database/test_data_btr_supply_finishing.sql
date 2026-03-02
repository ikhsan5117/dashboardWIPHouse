-- =============================================
-- SCRIPT TEST DATA: Supply Finishing untuk BTR
-- Jalankan di database ELWP_PRD (produksi)
-- Tanggal test: 2026-03-02 (sesuaikan jika perlu)
-- =============================================

USE [ELWP_PRD];
GO

-- ==================================================
-- STEP 1: Tambah Mesin "Finishing Line" untuk BTR
-- PlantId = 4 (BTR)
-- Nama harus mengandung "Finishing Line" agar tampil
-- ==================================================
IF NOT EXISTS (SELECT 1 FROM produksi.tb_elwp_produksi_mesins WHERE KodeMesin = 'BTR-FL-01')
BEGIN
    INSERT INTO produksi.tb_elwp_produksi_mesins 
        (KodeMesin, NamaMesin, PlantId, AreaId, Keterangan, IsActive, CreatedAt)
    VALUES 
        ('BTR-FL-01', 'Finishing Line 1', 4, NULL, 'Test Mesin BTR Line 1', 1, GETDATE()),
        ('BTR-FL-02', 'Finishing Line 2', 4, NULL, 'Test Mesin BTR Line 2', 1, GETDATE()),
        ('BTR-FL-03', 'Finishing Line 3', 4, NULL, 'Test Mesin BTR Line 3', 1, GETDATE());

    PRINT 'Mesin BTR berhasil ditambahkan!';
END
ELSE
BEGIN
    PRINT 'Mesin BTR sudah ada, langsung ke step 2.';
END
GO

-- Cek ID mesin yang baru ditambahkan (catat IdMesin-nya!)
SELECT Id, KodeMesin, NamaMesin, PlantId 
FROM produksi.tb_elwp_produksi_mesins 
WHERE PlantId = 4;
GO

-- ==================================================
-- STEP 2: Tambah Data Planning untuk hari ini
-- Ganti @MesinId dengan ID yang didapat dari step 1!
-- LoadingTimeHours = jam rencana supply (format desimal)
-- Contoh: 10.5 = pukul 10:30, 14.0 = pukul 14:00
-- ==================================================

-- Ambil ID mesin otomatis
DECLARE @MesinId1 INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE KodeMesin = 'BTR-FL-01');
DECLARE @MesinId2 INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE KodeMesin = 'BTR-FL-02');
DECLARE @MesinId3 INT = (SELECT TOP 1 Id FROM produksi.tb_elwp_produksi_mesins WHERE KodeMesin = 'BTR-FL-03');
DECLARE @TodayDate DATE = CAST(GETDATE() AS DATE);

-- Hapus data test hari ini dulu jika ada (biar bersih)
DELETE FROM produksi.tb_elwp_produksi_plannings 
WHERE PlantId = 4 
  AND TanggalPlanning = @TodayDate
  AND KodeItem LIKE 'TEST-BTR-%';

-- Insert planning baru untuk hari ini
INSERT INTO produksi.tb_elwp_produksi_plannings
    (PlantId, MesinId, TanggalPlanning, KodeItem, PartName, QtyPlanning, Shift, LoadingTimeHours, PlanningDate, CreatedAt)
VALUES
    -- Line 1: Jadwal jam 09:00 (sudah lewat, status MERAH)
    (4, @MesinId1, @TodayDate, 'TEST-BTR-001', 'BTR Part Alpha', 500, '1', 9.00, @TodayDate, GETDATE()),
    -- Line 2: Jadwal jam 11:00 (sesuaikan waktu saat test)
    (4, @MesinId2, @TodayDate, 'TEST-BTR-002', 'BTR Part Beta',  300, '1', 11.00, @TodayDate, GETDATE()),
    -- Line 3: Jadwal jam 14:00 (masih lama, status PUTIH)
    (4, @MesinId3, @TodayDate, 'TEST-BTR-003', 'BTR Part Gamma', 200, '2', 14.00, @TodayDate, GETDATE());

PRINT 'Data Planning BTR untuk hari ini berhasil ditambahkan!';
GO

-- ==================================================
-- STEP 3: Verifikasi data test
-- ==================================================
SELECT 
    p.Id,
    p.KodeItem,
    p.PartName,
    p.QtyPlanning,
    CAST(p.LoadingTimeHours AS VARCHAR) + ':00' AS JamRencana,
    m.NamaMesin,
    m.PlantId,
    p.TanggalPlanning
FROM produksi.tb_elwp_produksi_plannings p
JOIN produksi.tb_elwp_produksi_mesins m ON m.Id = p.MesinId
WHERE p.PlantId = 4
  AND p.TanggalPlanning = CAST(GETDATE() AS DATE)
ORDER BY p.LoadingTimeHours;
GO

PRINT '============================================';
PRINT 'Data siap! Langkah selanjutnya:';
PRINT '1. Buka dashboard Supply Finishing sebagai adminBTR';
PRINT '2. Harusnya muncul 3 kartu item TEST-BTR';
PRINT '3. Lakukan INPUT OUT dari form After Washing';
PRINT '4. Pilih Plant 4 - BTR, Item Code = TEST-BTR-001';
PRINT '5. Refresh dashboard -> kartu harus jadi HIJAU!';
PRINT '============================================';
