-- =============================================
-- RVI Stock Summary Integration
-- Database: DB_SUPPLY_RVI
-- Purpose: Create/Update vw_stock_summary view for RVI dashboard
-- =============================================

-- =============================================
-- OPTION 1: Create vw_stock_summary view (if it doesn't exist)
-- =============================================

-- Drop view if exists
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_stock_summary')
    DROP VIEW vw_stock_summary;

-- Create vw_stock_summary view
CREATE VIEW vw_stock_summary AS
SELECT 
    i.item_code,
    CONCAT('RVI_QR_', i.item_code) as full_qr,
    ISNULL(s.current_box_stock, 0) as current_box_stock,
    FORMAT(ISNULL(s.last_updated, GETDATE()), 'yyyy-MM-dd HH:mm:ss') as last_updated
FROM items i
LEFT JOIN temp_stock_data s ON i.item_code = s.item_code;

-- =============================================
-- OPTION 2: Create actual stock tracking table (recommended)
-- =============================================

-- Create stock_log table to track stock movements
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='stock_log' AND xtype='U')
BEGIN
    CREATE TABLE stock_log (
        id INT IDENTITY(1,1) PRIMARY KEY,
        item_code VARCHAR(100) NOT NULL,
        full_qr VARCHAR(300),
        box_count INT NOT NULL,
        transaction_type VARCHAR(20) NOT NULL, -- 'IN', 'OUT', 'ADJUSTMENT'
        transaction_date DATETIME DEFAULT GETDATE(),
        notes VARCHAR(500),
        created_by VARCHAR(100),
        FOREIGN KEY (item_code) REFERENCES items(item_code)
    );
END

-- Create current_stock table to maintain current stock levels
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='current_stock' AND xtype='U')
BEGIN
    CREATE TABLE current_stock (
        item_code VARCHAR(100) PRIMARY KEY,
        current_box_stock INT DEFAULT 0,
        last_updated DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (item_code) REFERENCES items(item_code)
    );
END

-- =============================================
-- INSERT SAMPLE STOCK LOG DATA
-- =============================================

-- Clear existing data
DELETE FROM stock_log;
DELETE FROM current_stock;

-- Insert stock movements for different items
-- SHORTAGE ITEMS - No stock movements (all at 0)
-- These will remain at 0 stock

-- NORMAL ITEMS - Stock in movements
INSERT INTO stock_log (item_code, full_qr, box_count, transaction_type, transaction_date, notes, created_by) VALUES
-- RVI006 - Normal stock
('RVI006', 'RVI_QR_RVI006_001', 5, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI006', 'RVI_QR_RVI006_002', -2, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),
-- RVI007 - Normal stock
('RVI007', 'RVI_QR_RVI007_001', 4, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI007', 'RVI_QR_RVI007_002', -2, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),
-- RVI008 - Normal stock
('RVI008', 'RVI_QR_RVI008_001', 3, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI008', 'RVI_QR_RVI008_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),
-- RVI009 - Normal stock
('RVI009', 'RVI_QR_RVI009_001', 4, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI009', 'RVI_QR_RVI009_002', -2, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),
-- RVI010 - Normal stock
('RVI010', 'RVI_QR_RVI010_001', 3, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI010', 'RVI_QR_RVI010_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1');

-- OVER STOCK ITEMS - High stock levels
INSERT INTO stock_log (item_code, full_qr, box_count, transaction_type, transaction_date, notes, created_by) VALUES
-- RVI016 - Over stock
('RVI016', 'RVI_QR_RVI016_001', 8, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI016', 'RVI_QR_RVI016_002', -2, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),
-- RVI017 - Over stock
('RVI017', 'RVI_QR_RVI017_001', 6, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI017', 'RVI_QR_RVI017_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1'),
-- RVI018 - Over stock
('RVI018', 'RVI_QR_RVI018_001', 6, 'IN', '2024-01-10 08:00:00', 'Initial stock', 'System'),
('RVI018', 'RVI_QR_RVI018_002', -1, 'OUT', '2024-01-12 14:30:00', 'Production usage', 'Operator1');

-- =============================================
-- CALCULATE CURRENT STOCK FROM STOCK LOG
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
-- UPDATE vw_stock_summary VIEW TO USE REAL DATA
-- =============================================

-- Drop and recreate view with real data
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_stock_summary')
    DROP VIEW vw_stock_summary;

CREATE VIEW vw_stock_summary AS
SELECT 
    i.item_code,
    CONCAT('RVI_QR_', i.item_code, '_', FORMAT(cs.last_updated, 'yyyyMMddHHmmss')) as full_qr,
    cs.current_box_stock,
    FORMAT(cs.last_updated, 'yyyy-MM-dd HH:mm:ss') as last_updated
FROM items i
LEFT JOIN current_stock cs ON i.item_code = cs.item_code;

-- =============================================
-- VERIFICATION QUERIES
-- =============================================

-- Check stock log data
SELECT 
    item_code,
    transaction_type,
    SUM(box_count) as total_boxes,
    COUNT(*) as transaction_count
FROM stock_log
GROUP BY item_code, transaction_type
ORDER BY item_code, transaction_type;

-- Check current stock levels
SELECT 
    cs.item_code,
    cs.current_box_stock,
    cs.last_updated,
    i.standard_min,
    i.standard_max,
    CASE 
        WHEN cs.current_box_stock <= i.standard_min THEN 'Shortage'
        WHEN cs.current_box_stock > i.standard_min AND cs.current_box_stock < i.standard_max THEN 'Normal'
        WHEN cs.current_box_stock >= i.standard_max THEN 'Over Stock'
        ELSE 'Normal'
    END as Status
FROM current_stock cs
JOIN items i ON cs.item_code = i.item_code
ORDER BY 
    CASE 
        WHEN cs.current_box_stock <= i.standard_min THEN 1
        WHEN cs.current_box_stock > i.standard_min AND cs.current_box_stock < i.standard_max THEN 2
        WHEN cs.current_box_stock >= i.standard_max THEN 3
        ELSE 2
    END,
    cs.item_code;

-- Check vw_stock_summary view
SELECT * FROM vw_stock_summary ORDER BY item_code;

-- Summary by status
SELECT 
    CASE 
        WHEN cs.current_box_stock <= i.standard_min THEN 'Shortage'
        WHEN cs.current_box_stock > i.standard_min AND cs.current_box_stock < i.standard_max THEN 'Normal'
        WHEN cs.current_box_stock >= i.standard_max THEN 'Over Stock'
        ELSE 'Normal'
    END as Status,
    COUNT(*) as Count,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM items), 1) as Percentage
FROM current_stock cs
JOIN items i ON cs.item_code = i.item_code
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
-- STOCK MOVEMENT FUNCTIONS (Optional)
-- =============================================

-- Function to add stock
/*
CREATE PROCEDURE sp_add_stock
    @item_code VARCHAR(100),
    @box_count INT,
    @notes VARCHAR(500) = '',
    @created_by VARCHAR(100) = 'System'
AS
BEGIN
    INSERT INTO stock_log (item_code, full_qr, box_count, transaction_type, notes, created_by)
    VALUES (@item_code, CONCAT('RVI_QR_', @item_code, '_', FORMAT(GETDATE(), 'yyyyMMddHHmmss')), @box_count, 'IN', @notes, @created_by);
    
    -- Update current stock
    MERGE current_stock AS target
    USING (SELECT @item_code as item_code, @box_count as box_count) AS source
    ON target.item_code = source.item_code
    WHEN MATCHED THEN
        UPDATE SET current_box_stock = current_box_stock + source.box_count, last_updated = GETDATE()
    WHEN NOT MATCHED THEN
        INSERT (item_code, current_box_stock, last_updated)
        VALUES (source.item_code, source.box_count, GETDATE());
END;
*/

-- Function to remove stock
/*
CREATE PROCEDURE sp_remove_stock
    @item_code VARCHAR(100),
    @box_count INT,
    @notes VARCHAR(500) = '',
    @created_by VARCHAR(100) = 'System'
AS
BEGIN
    INSERT INTO stock_log (item_code, full_qr, box_count, transaction_type, notes, created_by)
    VALUES (@item_code, CONCAT('RVI_QR_', @item_code, '_', FORMAT(GETDATE(), 'yyyyMMddHHmmss')), @box_count, 'OUT', @notes, @created_by);
    
    -- Update current stock
    UPDATE current_stock 
    SET current_box_stock = current_box_stock - @box_count, 
        last_updated = GETDATE()
    WHERE item_code = @item_code;
END;
*/

-- =============================================
-- NOTES
-- =============================================
/*
1. This script creates a complete stock tracking system for RVI:
   - stock_log: Tracks all stock movements (IN/OUT/ADJUSTMENT)
   - current_stock: Maintains current stock levels
   - vw_stock_summary: View that matches the expected structure

2. The system will show realistic status distribution:
   - Items with no stock movements = Shortage (0 stock)
   - Items with moderate stock = Normal
   - Items with high stock = Over Stock

3. You can add more stock movements to test different scenarios

4. The vw_stock_summary view now provides real data that the RVI dashboard can use

5. Uncomment the stored procedures if you want to add/remove stock programmatically
*/

