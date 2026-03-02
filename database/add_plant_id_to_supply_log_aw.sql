-- =============================================
-- Script: Tambah kolom plant_id ke supply_log_aw
-- Jalankan script ini di SQL Server Management Studio
-- pada database DB_SUPPLY_HOSE (atau database yang relevan)
-- =============================================

USE [DB_SUPPLY_HOSE];
GO

-- Tambah kolom plant_id ke tabel supply_log_aw (jika belum ada)
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE Name = N'plant_id' 
    AND Object_ID = Object_ID(N'supply_log_aw')
)
BEGIN
    ALTER TABLE [supply_log_aw]
    ADD [plant_id] INT NULL;

    PRINT 'Kolom plant_id berhasil ditambahkan ke tabel supply_log_aw.';
END
ELSE
BEGIN
    PRINT 'Kolom plant_id sudah ada di tabel supply_log_aw.';
END
GO

PRINT '================================================';
PRINT 'Keterangan nilai plant_id:';
PRINT '  1 = Plant HOSE';
PRINT '  2 = Plant RVI';
PRINT '  3 = Plant MOLDED';
PRINT '  4 = Plant BTR';
PRINT '  NULL = Belum dipilih (data lama)';
PRINT '================================================';
