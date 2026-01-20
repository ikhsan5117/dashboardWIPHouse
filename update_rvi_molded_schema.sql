-- SAFE UPDATE SCRIPT
-- Script ini aman dijalankan berulang kali.
-- Script ini hanya menambahkan kolom jika kolom tersebut belum ada.
-- Tidak akan menghapus data yang sudah ada.

-- =============================================
-- 1. UPDATE DATABASE RVI
-- =============================================
USE DB_SUPPLY_RVI;
GO

PRINT '=== Checking RVI storage_log ===';
IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'full_qr' AND Object_ID = Object_ID(N'storage_log'))
BEGIN
    PRINT 'Adding full_qr to storage_log...';
    ALTER TABLE storage_log ADD full_qr NVARCHAR(300) NOT NULL DEFAULT '-';
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'qty_pcs' AND Object_ID = Object_ID(N'storage_log'))
BEGIN
    PRINT 'Adding qty_pcs to storage_log...';
    ALTER TABLE storage_log ADD qty_pcs INT NULL;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'production_date' AND Object_ID = Object_ID(N'storage_log'))
BEGIN
    PRINT 'Adding production_date to storage_log...';
    ALTER TABLE storage_log ADD production_date DATETIME2 NULL;
END
GO

PRINT '=== Checking RVI supply_log ===';
IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'full_qr' AND Object_ID = Object_ID(N'supply_log'))
BEGIN
    PRINT 'Adding full_qr to supply_log...';
    ALTER TABLE supply_log ADD full_qr NVARCHAR(300) NULL;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'qty_pcs' AND Object_ID = Object_ID(N'supply_log'))
BEGIN
    PRINT 'Adding qty_pcs to supply_log...';
    ALTER TABLE supply_log ADD qty_pcs INT NULL;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'production_date' AND Object_ID = Object_ID(N'supply_log'))
BEGIN
    PRINT 'Adding production_date to supply_log...';
    ALTER TABLE supply_log ADD production_date DATETIME2 NULL;
END
GO

-- =============================================
-- 2. UPDATE DATABASE MOLDED
-- =============================================
USE DB_SUPPLY_MOLDED;
GO

PRINT '=== Checking MOLDED storage_log ===';
IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'full_qr' AND Object_ID = Object_ID(N'storage_log'))
BEGIN
    PRINT 'Adding full_qr to storage_log...';
    ALTER TABLE storage_log ADD full_qr NVARCHAR(300) NOT NULL DEFAULT '-';
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'qty_pcs' AND Object_ID = Object_ID(N'storage_log'))
BEGIN
    PRINT 'Adding qty_pcs to storage_log...';
    ALTER TABLE storage_log ADD qty_pcs INT NULL;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'production_date' AND Object_ID = Object_ID(N'storage_log'))
BEGIN
    PRINT 'Adding production_date to storage_log...';
    ALTER TABLE storage_log ADD production_date DATETIME2 NULL;
END
GO

PRINT '=== Checking MOLDED supply_log ===';
IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'full_qr' AND Object_ID = Object_ID(N'supply_log'))
BEGIN
    PRINT 'Adding full_qr to supply_log...';
    ALTER TABLE supply_log ADD full_qr NVARCHAR(300) NULL;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'qty_pcs' AND Object_ID = Object_ID(N'supply_log'))
BEGIN
    PRINT 'Adding qty_pcs to supply_log...';
    ALTER TABLE supply_log ADD qty_pcs INT NULL;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'production_date' AND Object_ID = Object_ID(N'supply_log'))
BEGIN
    PRINT 'Adding production_date to supply_log...';
    ALTER TABLE supply_log ADD production_date DATETIME2 NULL;
END
GO

PRINT '=== UPDATE COMPLETE ===';
