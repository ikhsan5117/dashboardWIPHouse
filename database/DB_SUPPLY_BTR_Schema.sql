-- =============================================
-- Database: DB_SUPPLY_BTR
-- Description: Supply Chain Management for BTR (Before Trimming) Process
-- Created: 2026-01-27
-- =============================================

USE master;
GO

-- Create Database if not exists
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'DB_SUPPLY_BTR')
BEGIN
    CREATE DATABASE DB_SUPPLY_BTR;
    PRINT 'Database DB_SUPPLY_BTR created successfully.';
END
ELSE
BEGIN
    PRINT 'Database DB_SUPPLY_BTR already exists.';
END
GO

USE DB_SUPPLY_BTR;
GO

-- =============================================
-- Table: items
-- Description: Master data for BTR items with machine assignments and standards
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'items')
BEGIN
    CREATE TABLE items (
        item_code NVARCHAR(50) NOT NULL PRIMARY KEY,
        mesin NVARCHAR(20) NULL,                    -- Machine assignment (e.g., M01, M02, or NULL for general items)
        qty_per_box INT NOT NULL DEFAULT 0,         -- Quantity per box
        standard_exp INT NOT NULL DEFAULT 0,        -- Standard expiry in days
        standard_min INT NOT NULL DEFAULT 0,        -- Minimum stock threshold
        standard_max INT NOT NULL DEFAULT 0,        -- Maximum stock threshold
        created_at DATETIME DEFAULT GETDATE(),
        updated_at DATETIME DEFAULT GETDATE()
    );
    PRINT 'Table [items] created successfully.';
END
ELSE
BEGIN
    PRINT 'Table [items] already exists.';
END
GO

-- =============================================
-- Table: raks
-- Description: Rack/location management for physical storage
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'raks')
BEGIN
    CREATE TABLE raks (
        full_qr NVARCHAR(100) NOT NULL PRIMARY KEY, -- Full QR code identifier
        location NVARCHAR(50) NULL,                 -- Physical rack location
        item_code NVARCHAR(50) NOT NULL,            -- Reference to items table
        created_at DATETIME DEFAULT GETDATE(),
        CONSTRAINT FK_raks_items FOREIGN KEY (item_code) REFERENCES items(item_code)
            ON UPDATE CASCADE
            ON DELETE CASCADE
    );
    PRINT 'Table [raks] created successfully.';
END
ELSE
BEGIN
    PRINT 'Table [raks] already exists.';
END
GO

-- =============================================
-- Table: storage_log
-- Description: Log of all incoming materials stored in BTR area
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'storage_log')
BEGIN
    CREATE TABLE storage_log (
        log_id INT IDENTITY(1,1) PRIMARY KEY,
        item_code NVARCHAR(50) NOT NULL,            -- Item code
        full_qr NVARCHAR(100) NULL,                 -- Full QR code
        production_date DATE NULL,                  -- Production date of the material
        box_count INT NOT NULL DEFAULT 0,           -- Number of boxes stored
        qty_pcs INT NOT NULL DEFAULT 0,             -- Total quantity in pieces
        stored_at DATETIME DEFAULT GETDATE(),       -- Timestamp when stored
        tanggal DATE DEFAULT CAST(GETDATE() AS DATE), -- Date only (for easier querying)
        CONSTRAINT FK_storage_log_items FOREIGN KEY (item_code) REFERENCES items(item_code)
            ON UPDATE CASCADE
    );
    PRINT 'Table [storage_log] created successfully.';
END
ELSE
BEGIN
    PRINT 'Table [storage_log] already exists.';
END
GO

-- =============================================
-- Table: supply_log
-- Description: Log of all materials supplied/dispatched from BTR area
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'supply_log')
BEGIN
    CREATE TABLE supply_log (
        log_id INT IDENTITY(1,1) PRIMARY KEY,
        item_code NVARCHAR(50) NOT NULL,            -- Item code
        full_qr NVARCHAR(100) NULL,                 -- Full QR code
        production_date DATE NULL,                  -- Production date of the material
        box_count INT NOT NULL DEFAULT 0,           -- Number of boxes supplied
        qty_pcs INT NOT NULL DEFAULT 0,             -- Total quantity in pieces
        supplied_at DATETIME DEFAULT GETDATE(),     -- Timestamp when supplied
        to_process NVARCHAR(50) NULL,               -- Destination process/area
        tanggal DATE DEFAULT CAST(GETDATE() AS DATE), -- Date only (for easier querying)
        storage_log_id INT NULL,                    -- Reference to original storage log (FIFO tracking)
        CONSTRAINT FK_supply_log_items FOREIGN KEY (item_code) REFERENCES items(item_code)
            ON UPDATE CASCADE,
        CONSTRAINT FK_supply_log_storage FOREIGN KEY (storage_log_id) REFERENCES storage_log(log_id)
    );
    PRINT 'Table [supply_log] created successfully.';
END
ELSE
BEGIN
    PRINT 'Table [supply_log] already exists.';
END
GO

-- =============================================
-- Table: users
-- Description: User management for BTR module access control
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'users')
BEGIN
    CREATE TABLE users (
        id INT IDENTITY(1,1) PRIMARY KEY,
        username NVARCHAR(50) NOT NULL UNIQUE,
        password NVARCHAR(255) NOT NULL,            -- Should be hashed in production
        created_date DATETIME DEFAULT GETDATE(),
        last_login DATETIME NULL
    );
    PRINT 'Table [users] created successfully.';
END
ELSE
BEGIN
    PRINT 'Table [users] already exists.';
END
GO

-- =============================================
-- View: vw_stok_summary
-- Description: Real-time stock summary with expiry status monitoring
-- =============================================
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_stok_summary')
BEGIN
    DROP VIEW vw_stok_summary;
    PRINT 'Existing view [vw_stok_summary] dropped.';
END
GO

CREATE VIEW vw_stok_summary AS
SELECT 
    sl.log_id,
    sl.item_code,
    sl.full_qr,
    sl.stored_at,
    
    -- Current Box Stock (Stored - Supplied)
    (sl.box_count - ISNULL(SUM(sup.box_count), 0)) AS current_box_stock,
    
    -- Standard Expiry from items table
    i.standard_exp,
    
    -- Expired Date Calculation
    DATEADD(DAY, i.standard_exp, sl.production_date) AS expired_date,
    
    -- Status Expired (Expired, Near Exp, Normal)
    CASE 
        WHEN DATEADD(DAY, i.standard_exp, sl.production_date) < CAST(GETDATE() AS DATE) 
            THEN 'Expired'
        WHEN DATEADD(DAY, i.standard_exp, sl.production_date) <= DATEADD(DAY, 7, CAST(GETDATE() AS DATE)) 
            THEN 'Near Exp'
        ELSE 'Normal'
    END AS status_expired,
    
    -- Last Update (latest supply or storage time)
    CASE 
        WHEN MAX(sup.supplied_at) IS NOT NULL AND MAX(sup.supplied_at) > sl.stored_at 
            THEN MAX(sup.supplied_at)
        ELSE sl.stored_at
    END AS last_update

FROM storage_log sl
INNER JOIN items i ON sl.item_code = i.item_code
LEFT JOIN supply_log sup ON sl.log_id = sup.storage_log_id

GROUP BY 
    sl.log_id,
    sl.item_code,
    sl.full_qr,
    sl.stored_at,
    sl.box_count,
    sl.production_date,
    i.standard_exp

-- Only show records with remaining stock
HAVING (sl.box_count - ISNULL(SUM(sup.box_count), 0)) > 0;
GO

PRINT 'View [vw_stok_summary] created successfully.';
GO

-- =============================================
-- Create Indexes for Performance
-- =============================================

-- Index on storage_log for faster querying
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_storage_log_item_code')
BEGIN
    CREATE INDEX IX_storage_log_item_code ON storage_log(item_code);
    PRINT 'Index [IX_storage_log_item_code] created.';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_storage_log_tanggal')
BEGIN
    CREATE INDEX IX_storage_log_tanggal ON storage_log(tanggal);
    PRINT 'Index [IX_storage_log_tanggal] created.';
END

-- Index on supply_log for faster querying
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_supply_log_item_code')
BEGIN
    CREATE INDEX IX_supply_log_item_code ON supply_log(item_code);
    PRINT 'Index [IX_supply_log_item_code] created.';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_supply_log_storage_log_id')
BEGIN
    CREATE INDEX IX_supply_log_storage_log_id ON supply_log(storage_log_id);
    PRINT 'Index [IX_supply_log_storage_log_id] created.';
END

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_supply_log_tanggal')
BEGIN
    CREATE INDEX IX_supply_log_tanggal ON supply_log(tanggal);
    PRINT 'Index [IX_supply_log_tanggal] created.';
END

GO

-- =============================================
-- Insert Sample Data (Optional - for testing)
-- =============================================

-- Sample Items
IF NOT EXISTS (SELECT * FROM items WHERE item_code = 'BTR-001')
BEGIN
    INSERT INTO items (item_code, mesin, qty_per_box, standard_exp, standard_min, standard_max)
    VALUES 
        ('BTR-001', 'M01', 100, 30, 50, 200),
        ('BTR-002', 'M02', 150, 45, 60, 250),
        ('BTR-003', NULL, 200, 60, 100, 300);
    PRINT 'Sample items inserted.';
END
GO

-- Sample Racks
IF NOT EXISTS (SELECT * FROM raks WHERE full_qr = 'BTR-001-R01-001')
BEGIN
    INSERT INTO raks (full_qr, location, item_code)
    VALUES 
        ('BTR-001-R01-001', 'R01-A1', 'BTR-001'),
        ('BTR-002-R02-001', 'R02-B2', 'BTR-002'),
        ('BTR-003-R03-001', 'R03-C3', 'BTR-003');
    PRINT 'Sample racks inserted.';
END
GO

-- Sample Storage Log
IF NOT EXISTS (SELECT * FROM storage_log WHERE item_code = 'BTR-001')
BEGIN
    INSERT INTO storage_log (item_code, full_qr, production_date, box_count, qty_pcs, tanggal)
    VALUES 
        ('BTR-001', 'BTR-001-R01-001', DATEADD(DAY, -10, CAST(GETDATE() AS DATE)), 10, 1000, CAST(GETDATE() AS DATE)),
        ('BTR-002', 'BTR-002-R02-001', DATEADD(DAY, -5, CAST(GETDATE() AS DATE)), 15, 2250, CAST(GETDATE() AS DATE)),
        ('BTR-003', 'BTR-003-R03-001', DATEADD(DAY, -2, CAST(GETDATE() AS DATE)), 20, 4000, CAST(GETDATE() AS DATE));
    PRINT 'Sample storage logs inserted.';
END
GO

-- Sample Default User (username: admin, password: admin123 - CHANGE IN PRODUCTION!)
IF NOT EXISTS (SELECT * FROM users WHERE username = 'admin')
BEGIN
    INSERT INTO users (username, password)
    VALUES ('admin', 'admin123'); -- TODO: Hash password in production
    PRINT 'Default admin user created (username: admin, password: admin123).';
    PRINT 'WARNING: Please change the default password in production!';
END
GO

-- =============================================
-- Verification Query
-- =============================================
PRINT '==============================================';
PRINT 'Database DB_SUPPLY_BTR Setup Complete!';
PRINT '==============================================';
PRINT 'Tables Created:';
PRINT '  - items';
PRINT '  - raks';
PRINT '  - storage_log';
PRINT '  - supply_log';
PRINT '  - users';
PRINT '';
PRINT 'Views Created:';
PRINT '  - vw_stok_summary';
PRINT '';
PRINT 'Sample Data Inserted (for testing)';
PRINT '==============================================';
GO

-- Test the view
SELECT * FROM vw_stok_summary;
GO
