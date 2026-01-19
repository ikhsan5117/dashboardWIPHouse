-- =============================================
-- RVI Complete Test Data Script
-- Database: DB_SUPPLY_RVI
-- Purpose: Complete setup for RVI dashboard testing
-- =============================================

-- =============================================
-- STEP 1: Clean up existing data (optional)
-- =============================================
/*
-- Uncomment if you want to start fresh
DELETE FROM stock_log;
DELETE FROM current_stock;
DELETE FROM items;
DROP VIEW IF EXISTS vw_stock_summary;
DROP TABLE IF EXISTS temp_stock_data;
*/

-- =============================================
-- STEP 2: Create tables and views
-- =============================================

-- Create stock_log table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='stock_log' AND xtype='U')
BEGIN
    CREATE TABLE stock_log (
        id INT IDENTITY(1,1) PRIMARY KEY,
        item_code VARCHAR(100) NOT NULL,
        full_qr VARCHAR(300),
        box_count INT NOT NULL,
        transaction_type VARCHAR(20) NOT NULL,
        transaction_date DATETIME DEFAULT GETDATE(),
        notes VARCHAR(500),
        created_by VARCHAR(100)
    );
END

-- Create current_stock table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='current_stock' AND xtype='U')
BEGIN
    CREATE TABLE current_stock (
        item_code VARCHAR(100) PRIMARY KEY,
        current_box_stock INT DEFAULT 0,
        last_updated DATETIME DEFAULT GETDATE()
    );
END

-- =============================================
-- STEP 3: Insert items data
-- =============================================

-- Insert items with logical standard_min and standard_max values
INSERT INTO items (item_code, qty_per_box, standard_min, standard_max) VALUES
-- SHORTAGE ITEMS (will have 0 or low stock)
('RVI001', 100.00, 100, 500),    -- StandardMin = 1 × 100 = 100
('RVI002', 50.00, 50, 200),      -- StandardMin = 1 × 50 = 50
('RVI003', 200.00, 200, 800),    -- StandardMin = 1 × 200 = 200
('RVI004', 75.00, 75, 300),      -- StandardMin = 1 × 75 = 75
('RVI005', 150.00, 150, 600),    -- StandardMin = 1 × 150 = 150
('RVI021', 100.00, 100, 500),    -- Exactly at minimum
('RVI026', 25.00, 25, 100),      -- Small quantity
('RVI028', 30.00, 30, 120),      -- Different ratio

-- NORMAL ITEMS (will have moderate stock)
('RVI006', 100.00, 100, 500),    -- StandardMin = 100, StandardMax = 500
('RVI007', 50.00, 50, 200),      -- StandardMin = 50, StandardMax = 200
('RVI008', 200.00, 200, 800),    -- StandardMin = 200, StandardMax = 800
('RVI009', 75.00, 75, 300),      -- StandardMin = 75, StandardMax = 300
('RVI010', 150.00, 150, 600),    -- StandardMin = 150, StandardMax = 600
('RVI011', 120.00, 120, 480),    -- StandardMin = 120, StandardMax = 480
('RVI012', 80.00, 80, 320),      -- StandardMin = 80, StandardMax = 320
('RVI013', 250.00, 250, 1000),   -- StandardMin = 250, StandardMax = 1000
('RVI014', 90.00, 90, 360),      -- StandardMin = 90, StandardMax = 360
('RVI015', 180.00, 180, 720),    -- StandardMin = 180, StandardMax = 720
('RVI023', 200.00, 200, 800),    -- Just above minimum
('RVI024', 75.00, 75, 300),      -- Just below maximum
('RVI025', 150.00, 150, 600),    -- Middle range
('RVI030', 60.00, 60, 240),      -- Medium quantity

-- OVER STOCK ITEMS (will have high stock)
('RVI016', 100.00, 100, 500),    -- StandardMin = 100, StandardMax = 500
('RVI017', 50.00, 50, 200),      -- StandardMin = 50, StandardMax = 200
('RVI018', 200.00, 200, 800),    -- StandardMin = 200, StandardMax = 800
('RVI019', 75.00, 75, 300),      -- StandardMin = 75, StandardMax = 300
('RVI020', 150.00, 150, 600),    -- StandardMin = 150, StandardMax = 600
('RVI022', 50.00, 50, 200),      -- Exactly at maximum
('RVI027', 500.00, 500, 2000),   -- Large quantity
('RVI029', 400.00, 400, 1600);   -- High volume

-- =============================================
-- STEP 4: Insert stock movements
-- =============================================

-- SHORTAGE ITEMS - No stock movements (remain at 0)
-- These items will show as Shortage status

-- NORMAL ITEMS - Moderate stock levels
INSERT INTO stock_log (item_code, full_qr, box_count, transaction_type, transaction_date, notes, created_by) VALUES
-- RVI006 - 3 boxes (300 pcs) - Normal
('RVI006', 'RVI_QR_RVI006_001', 5, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI006', 'RVI_QR_RVI006_002', -2, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI007 - 2 boxes (100 pcs) - Normal
('RVI007', 'RVI_QR_RVI007_001', 4, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI007', 'RVI_QR_RVI007_002', -2, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI008 - 2 boxes (400 pcs) - Normal
('RVI008', 'RVI_QR_RVI008_001', 3, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI008', 'RVI_QR_RVI008_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI009 - 2 boxes (150 pcs) - Normal
('RVI009', 'RVI_QR_RVI009_001', 4, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI009', 'RVI_QR_RVI009_002', -2, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI010 - 2 boxes (300 pcs) - Normal
('RVI010', 'RVI_QR_RVI010_001', 3, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI010', 'RVI_QR_RVI010_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI011 - 2 boxes (240 pcs) - Normal
('RVI011', 'RVI_QR_RVI011_001', 3, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI011', 'RVI_QR_RVI011_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI012 - 2 boxes (160 pcs) - Normal
('RVI012', 'RVI_QR_RVI012_001', 3, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI012', 'RVI_QR_RVI012_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI013 - 2 boxes (500 pcs) - Normal
('RVI013', 'RVI_QR_RVI013_001', 3, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI013', 'RVI_QR_RVI013_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI014 - 2 boxes (180 pcs) - Normal
('RVI014', 'RVI_QR_RVI014_001', 3, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI014', 'RVI_QR_RVI014_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI015 - 2 boxes (360 pcs) - Normal
('RVI015', 'RVI_QR_RVI015_001', 3, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI015', 'RVI_QR_RVI015_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI023 - 2 boxes (400 pcs) - Normal (just above minimum)
('RVI023', 'RVI_QR_RVI023_001', 3, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI023', 'RVI_QR_RVI023_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI024 - 3 boxes (225 pcs) - Normal (just below maximum)
('RVI024', 'RVI_QR_RVI024_001', 4, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI024', 'RVI_QR_RVI024_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI025 - 2 boxes (300 pcs) - Normal (middle range)
('RVI025', 'RVI_QR_RVI025_001', 3, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI025', 'RVI_QR_RVI025_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI030 - 2 boxes (120 pcs) - Normal
('RVI030', 'RVI_QR_RVI030_001', 3, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI030', 'RVI_QR_RVI030_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1');

-- OVER STOCK ITEMS - High stock levels
INSERT INTO stock_log (item_code, full_qr, box_count, transaction_type, transaction_date, notes, created_by) VALUES
-- RVI016 - 6 boxes (600 pcs) - Over Stock
('RVI016', 'RVI_QR_RVI016_001', 8, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI016', 'RVI_QR_RVI016_002', -2, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI017 - 5 boxes (250 pcs) - Over Stock
('RVI017', 'RVI_QR_RVI017_001', 6, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI017', 'RVI_QR_RVI017_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI018 - 5 boxes (1000 pcs) - Over Stock
('RVI018', 'RVI_QR_RVI018_001', 6, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI018', 'RVI_QR_RVI018_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI019 - 5 boxes (375 pcs) - Over Stock
('RVI019', 'RVI_QR_RVI019_001', 6, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI019', 'RVI_QR_RVI019_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI020 - 5 boxes (750 pcs) - Over Stock
('RVI020', 'RVI_QR_RVI020_001', 6, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI020', 'RVI_QR_RVI020_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI022 - 4 boxes (200 pcs) - Over Stock (exactly at maximum)
('RVI022', 'RVI_QR_RVI022_001', 5, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI022', 'RVI_QR_RVI022_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI027 - 3 boxes (1500 pcs) - Over Stock
('RVI027', 'RVI_QR_RVI027_001', 4, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI027', 'RVI_QR_RVI027_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),

-- RVI029 - 3 boxes (1200 pcs) - Over Stock
('RVI029', 'RVI_QR_RVI029_001', 4, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI029', 'RVI_QR_RVI029_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1');

-- =============================================
-- STEP 5: Calculate current stock
-- =============================================

-- Calculate current stock for each item
INSERT INTO current_stock (item_code, current_box_stock, last_updated)
SELECT 
    item_code,
    SUM(CASE 
        WHEN transaction_type = 'IN' THEN box_count
        WHEN transaction_type = 'OUT' THEN -box_count
        WHEN transaction_type = 'ADJUSTMENT' THEN box_count
        ELSE 0
    END) as current_box_stock,
    MAX(transaction_date) as last_updated
FROM stock_log
GROUP BY item_code;

-- Insert 0 stock for items with no movements
INSERT INTO current_stock (item_code, current_box_stock, last_updated)
SELECT 
    i.item_code,
    0 as current_box_stock,
    GETDATE() as last_updated
FROM items i
WHERE i.item_code NOT IN (SELECT item_code FROM current_stock);

-- =============================================
-- STEP 6: Create vw_stock_summary view
-- =============================================

-- Drop view if exists
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_stock_summary')
    DROP VIEW vw_stock_summary;

-- Create vw_stock_summary view
CREATE VIEW vw_stock_summary AS
SELECT 
    i.item_code,
    CONCAT('RVI_QR_', i.item_code, '_', FORMAT(cs.last_updated, 'yyyyMMddHHmmss')) as full_qr,
    cs.current_box_stock,
    FORMAT(cs.last_updated, 'yyyy-MM-dd HH:mm:ss') as last_updated
FROM items i
LEFT JOIN current_stock cs ON i.item_code = cs.item_code;

-- =============================================
-- STEP 7: Verification and Dashboard Preview
-- =============================================

-- Dashboard Summary Query (matches RVI controller logic)
SELECT 
    COUNT(*) as TotalItems,
    SUM(CASE WHEN cs.current_box_stock <= i.standard_min THEN 1 ELSE 0 END) as ShortageCount,
    SUM(CASE WHEN cs.current_box_stock > i.standard_min AND cs.current_box_stock < i.standard_max THEN 1 ELSE 0 END) as NormalCount,
    SUM(CASE WHEN cs.current_box_stock >= i.standard_max THEN 1 ELSE 0 END) as OverStockCount
FROM items i
LEFT JOIN current_stock cs ON i.item_code = cs.item_code;

-- Detailed Items Query (matches RVI table data)
SELECT 
    ROW_NUMBER() OVER (ORDER BY i.item_code) as No,
    i.item_code,
    i.standard_min,
    i.standard_max,
    ISNULL(cs.current_box_stock, 0) as CurrentBoxStock,
    ROUND(ISNULL(cs.current_box_stock, 0) * i.qty_per_box, 2) as Pcs,
    CASE 
        WHEN ISNULL(cs.current_box_stock, 0) <= i.standard_min THEN 'Shortage'
        WHEN ISNULL(cs.current_box_stock, 0) > i.standard_min AND ISNULL(cs.current_box_stock, 0) < i.standard_max THEN 'Normal'
        WHEN ISNULL(cs.current_box_stock, 0) >= i.standard_max THEN 'Over Stock'
        ELSE 'Normal'
    END as Status,
    ISNULL(cs.last_updated, GETDATE()) as LastUpdatedDate,
    i.qty_per_box
FROM items i
LEFT JOIN current_stock cs ON i.item_code = cs.item_code
ORDER BY i.item_code;

-- Status Summary
SELECT 
    CASE 
        WHEN cs.current_box_stock <= i.standard_min THEN 'Shortage'
        WHEN cs.current_box_stock > i.standard_min AND cs.current_box_stock < i.standard_max THEN 'Normal'
        WHEN cs.current_box_stock >= i.standard_max THEN 'Over Stock'
        ELSE 'Normal'
    END as Status,
    COUNT(*) as Count,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM items), 1) as Percentage
FROM items i
LEFT JOIN current_stock cs ON i.item_code = cs.item_code
GROUP BY 
    CASE 
        WHEN cs.current_box_stock <= i.standard_min THEN 'Shortage'
        WHEN cs.current_box_stock > i.standard_min AND cs.current_box_stock < i.standard_max THEN 'Normal'
        WHEN cs.current_box_stock >= i.standard_max THEN 'Over Stock'
        ELSE 'Normal'
    END
ORDER BY 
    CASE 
        WHEN cs.current_box_stock <= i.standard_min THEN 1
        WHEN cs.current_box_stock > i.standard_min AND cs.current_box_stock < i.standard_max THEN 2
        WHEN cs.current_box_stock >= i.standard_max THEN 3
        ELSE 2
    END;

-- =============================================
-- EXPECTED DASHBOARD RESULTS
-- =============================================
/*
After running this script, your RVI dashboard should show:

Total Items: 30
Shortage Count: 8 items (RVI001, RVI002, RVI003, RVI004, RVI005, RVI021, RVI026, RVI028)
Normal Count: 14 items (RVI006, RVI007, RVI008, RVI009, RVI010, RVI011, RVI012, RVI013, RVI014, RVI015, RVI023, RVI024, RVI025, RVI030)
Over Stock Count: 8 items (RVI016, RVI017, RVI018, RVI019, RVI020, RVI022, RVI027, RVI029)

This provides a good distribution for testing the dashboard functionality.
*/

-- =============================================
-- CLEANUP COMMANDS (if needed)
-- =============================================
/*
-- To reset everything:
DELETE FROM stock_log;
DELETE FROM current_stock;
DELETE FROM items;
DROP VIEW IF EXISTS vw_stock_summary;
*/

