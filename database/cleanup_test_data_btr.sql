-- =============================================
-- CLEANUP SCRIPT: Hapus semua data TEST BTR
-- Jalankan setelah selesai uji coba
-- =============================================

USE [ELWP_PRD];
GO

PRINT '=== Memulai Cleanup Data Test BTR ===';

-- Hapus data planning test
DELETE FROM produksi.tb_elwp_produksi_plannings
WHERE KodeItem LIKE 'TEST-BTR-%';
PRINT 'Planning test dihapus: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' baris.';

-- Hapus mesin test BTR
DELETE FROM produksi.tb_elwp_produksi_mesins
WHERE KodeMesin LIKE 'BTR-FL-%';
PRINT 'Mesin test dihapus: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' baris.';
GO

-- Hapus dari supply_log_aw (DB_SUPPLY_HOSE)
USE [DB_SUPPLY_HOSE];
GO

DELETE FROM supply_log_aw
WHERE item_code LIKE 'TEST-BTR-%';
PRINT 'Supply log test dihapus: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' baris.';
GO

PRINT '=== Cleanup Selesai! Data kembali bersih ===';
