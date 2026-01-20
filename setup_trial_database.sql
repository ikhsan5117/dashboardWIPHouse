-- ========================================
-- Setup Script for DB_SUPPLY_HOSE_TRIAL
-- ========================================
-- This script creates all necessary tables for testing the HOSE input system
-- Run this script in SQL Server Management Studio or Azure Data Studio
-- 
-- Server: 10.14.149.34
-- Database: DB_SUPPLY_HOSE_TRIAL
-- User: usrvelasto
-- Password: H1s@na2025!!
-- ========================================

USE DB_SUPPLY_HOSE_TRIAL;
GO

PRINT '========================================';
PRINT 'Starting Trial Database Setup...';
PRINT '========================================';
GO

-- ========================================
-- 1. Create Users Table (for Authentication)
-- ========================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(100) NOT NULL UNIQUE,
        Password NVARCHAR(100) NOT NULL,
        CreatedDate DATETIME NULL,
        LastLogin DATETIME NULL
    );
    
    PRINT '✓ Users table created';
END
ELSE
BEGIN
    PRINT '- Users table already exists';
END
GO

-- Insert default users
IF NOT EXISTS (SELECT * FROM Users WHERE Username = 'admin')
BEGIN
    INSERT INTO Users (Username, Password, CreatedDate, LastLogin)
    VALUES ('admin', 'admin123', GETDATE(), NULL);
    PRINT '✓ Admin user created';
END

IF NOT EXISTS (SELECT * FROM Users WHERE Username = 'user')
BEGIN
    INSERT INTO Users (Username, Password, CreatedDate, LastLogin)
    VALUES ('user', 'user123', GETDATE(), NULL);
    PRINT '✓ Regular user created';
END
GO

-- ========================================
-- 2. Create storage_log Table (GREEN HOSE - INPUT IN)
-- ========================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'storage_log')
BEGIN
    CREATE TABLE storage_log (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ItemCode NVARCHAR(100) NOT NULL,
        FullQR NVARCHAR(200) NOT NULL,
        ProductionDate DATE NULL,
        BoxCount INT NULL DEFAULT 0,
        QtyEcer INT NULL DEFAULT 0,
        Tanggal NVARCHAR(50) NULL,
        TransactionType NVARCHAR(10) NULL DEFAULT 'IN',
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME NULL
    );
    
    -- Create indexes for better performance
    CREATE INDEX IX_storage_log_ItemCode ON storage_log(ItemCode);
    CREATE INDEX IX_storage_log_TransactionType ON storage_log(TransactionType);
    CREATE INDEX IX_storage_log_CreatedAt ON storage_log(CreatedAt);
    
    PRINT '✓ storage_log table created with indexes';
END
ELSE
BEGIN
    PRINT '- storage_log table already exists';
END
GO

-- ========================================
-- 3. Create supply_log Table (GREEN HOSE - INPUT OUT)
-- ========================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'supply_log')
BEGIN
    CREATE TABLE supply_log (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ItemCode NVARCHAR(100) NOT NULL,
        FullQR NVARCHAR(200) NOT NULL,
        BoxCount INT NULL DEFAULT 0,
        QtyPcs INT NULL DEFAULT 0,
        Tanggal NVARCHAR(50) NULL,
        ToProcess NVARCHAR(100) NULL DEFAULT 'Production',
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME NULL
    );
    
    -- Create indexes
    CREATE INDEX IX_supply_log_ItemCode ON supply_log(ItemCode);
    CREATE INDEX IX_supply_log_CreatedAt ON supply_log(CreatedAt);
    
    PRINT '✓ supply_log table created with indexes';
END
ELSE
BEGIN
    PRINT '- supply_log table already exists';
END
GO

-- ========================================
-- 4. Create storage_log_aw Table (AFTER WASHING)
-- ========================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'storage_log_aw')
BEGIN
    CREATE TABLE storage_log_aw (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ItemCode NVARCHAR(100) NOT NULL,
        FullQr NVARCHAR(200) NOT NULL,
        ProductionDate DATE NULL,
        BoxCount INT NULL DEFAULT 0,
        QtyEcer INT NULL DEFAULT 0,
        Tanggal NVARCHAR(50) NULL,
        TransactionType NVARCHAR(10) NULL DEFAULT 'IN',
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME NULL
    );
    
    -- Create indexes
    CREATE INDEX IX_storage_log_aw_ItemCode ON storage_log_aw(ItemCode);
    CREATE INDEX IX_storage_log_aw_TransactionType ON storage_log_aw(TransactionType);
    CREATE INDEX IX_storage_log_aw_CreatedAt ON storage_log_aw(CreatedAt);
    
    PRINT '✓ storage_log_aw table created with indexes';
END
ELSE
BEGIN
    PRINT '- storage_log_aw table already exists';
END
GO

-- ========================================
-- 5. Verification Queries
-- ========================================
PRINT '';
PRINT '========================================';
PRINT 'Verification:';
PRINT '========================================';

-- Check Users
DECLARE @UserCount INT = (SELECT COUNT(*) FROM Users);
PRINT 'Users count: ' + CAST(@UserCount AS NVARCHAR(10));

-- Check Tables
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
    PRINT '✓ Users table exists';
    
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'storage_log')
    PRINT '✓ storage_log table exists';
    
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'supply_log')
    PRINT '✓ supply_log table exists';
    
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'storage_log_aw')
    PRINT '✓ storage_log_aw table exists';

PRINT '';
PRINT '========================================';
PRINT '✓ Trial Database Setup Completed!';
PRINT '========================================';
PRINT '';
PRINT 'You can now:';
PRINT '1. Login with username: user, password: user123';
PRINT '2. Login with username: admin, password: admin123';
PRINT '3. Test GREEN HOSE input (data goes to storage_log and supply_log)';
PRINT '4. Test AFTER WASHING input (data goes to storage_log_aw)';
PRINT '';
GO
