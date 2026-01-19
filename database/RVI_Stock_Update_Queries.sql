-- =============================================
-- RVI Stock Update Queries
-- Database: DB_SUPPLY_RVI
-- Purpose: Update stock data to show different statuses in dashboard
-- =============================================

-- =============================================
-- OPTION 1: If you have a stock tracking table
-- =============================================

-- Create a temporary stock tracking table (if it doesn't exist)
-- This simulates the stock data that would come from your actual stock system
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='temp_stock_data' AND xtype='U')
BEGIN
    CREATE TABLE temp_stock_data (
        item_code VARCHAR(100) PRIMARY KEY,
        current_box_stock INT DEFAULT 0,
        last_updated DATETIME DEFAULT GETDATE()
    );
END

-- Clear existing temp data
DELETE FROM temp_stock_data;

-- Insert stock data to show different statuses
-- SHORTAGE ITEMS (Current Stock ≤ StandardMin)
INSERT INTO temp_stock_data (item_code, current_box_stock, last_updated) VALUES
('RVI001', 0, '2024-01-15 10:00:00'),    -- 0 ≤ 100 (Shortage)
('RVI002', 0, '2024-01-15 10:00:00'),    -- 0 ≤ 50 (Shortage)
('RVI003', 0, '2024-01-15 10:00:00'),    -- 0 ≤ 200 (Shortage)
('RVI004', 0, '2024-01-15 10:00:00'),    -- 0 ≤ 75 (Shortage)
('RVI005', 0, '2024-01-15 10:00:00'),    -- 0 ≤ 150 (Shortage)
('RVI021', 1, '2024-01-15 10:00:00');    -- 1 × 100 = 100, exactly at minimum (Shortage)

-- NORMAL ITEMS (StandardMin < Current Stock < StandardMax)
INSERT INTO temp_stock_data (item_code, current_box_stock, last_updated) VALUES
('RVI006', 3, '2024-01-15 10:00:00'),    -- 3 × 100 = 300, 100 < 300 < 500 (Normal)
('RVI007', 2, '2024-01-15 10:00:00'),    -- 2 × 50 = 100, 50 < 100 < 200 (Normal)
('RVI008', 2, '2024-01-15 10:00:00'),    -- 2 × 200 = 400, 200 < 400 < 800 (Normal)
('RVI009', 2, '2024-01-15 10:00:00'),    -- 2 × 75 = 150, 75 < 150 < 300 (Normal)
('RVI010', 2, '2024-01-15 10:00:00'),    -- 2 × 150 = 300, 150 < 300 < 600 (Normal)
('RVI011', 2, '2024-01-15 10:00:00'),    -- 2 × 120 = 240, 120 < 240 < 480 (Normal)
('RVI012', 2, '2024-01-15 10:00:00'),    -- 2 × 80 = 160, 80 < 160 < 320 (Normal)
('RVI013', 2, '2024-01-15 10:00:00'),    -- 2 × 250 = 500, 250 < 500 < 1000 (Normal)
('RVI014', 2, '2024-01-15 10:00:00'),    -- 2 × 90 = 180, 90 < 180 < 360 (Normal)
('RVI015', 2, '2024-01-15 10:00:00'),    -- 2 × 180 = 360, 180 < 360 < 720 (Normal)
('RVI023', 1, '2024-01-15 10:00:00'),    -- 1 × 200 = 200, 200 < 200 < 800 (Normal) - Wait, this should be 201
('RVI024', 3, '2024-01-15 10:00:00'),    -- 3 × 75 = 225, 75 < 225 < 300 (Normal)
('RVI025', 2, '2024-01-15 10:00:00'),    -- 2 × 150 = 300, 150 < 300 < 600 (Normal)
('RVI026', 2, '2024-01-15 10:00:00'),    -- 2 × 25 = 50, 25 < 50 < 100 (Normal)
('RVI027', 2, '2024-01-15 10:00:00'),    -- 2 × 500 = 1000, 500 < 1000 < 2000 (Normal)
('RVI028', 2, '2024-01-15 10:00:00'),    -- 2 × 30 = 60, 30 < 60 < 120 (Normal)
('RVI029', 2, '2024-01-15 10:00:00'),    -- 2 × 400 = 800, 400 < 800 < 1600 (Normal)
('RVI030', 2, '2024-01-15 10:00:00');    -- 2 × 60 = 120, 60 < 120 < 240 (Normal)

-- OVER STOCK ITEMS (Current Stock ≥ StandardMax)
INSERT INTO temp_stock_data (item_code, current_box_stock, last_updated) VALUES
('RVI016', 6, '2024-01-15 10:00:00'),    -- 6 × 100 = 600, 600 ≥ 500 (Over Stock)
('RVI017', 5, '2024-01-15 10:00:00'),    -- 5 × 50 = 250, 250 ≥ 200 (Over Stock)
('RVI018', 5, '2024-01-15 10:00:00'),    -- 5 × 200 = 1000, 1000 ≥ 800 (Over Stock)
('RVI019', 5, '2024-01-15 10:00:00'),    -- 5 × 75 = 375, 375 ≥ 300 (Over Stock)
('RVI020', 5, '2024-01-15 10:00:00'),    -- 5 × 150 = 750, 750 ≥ 600 (Over Stock)
('RVI022', 4, '2024-01-15 10:00:00');    -- 4 × 50 = 200, 200 ≥ 200 (Over Stock) - exactly at maximum

-- Fix RVI023 to be just above minimum
UPDATE temp_stock_data SET current_box_stock = 1 WHERE item_code = 'RVI023';
-- Actually, let's make it 2 boxes = 400 pcs, which is 200 < 400 < 800 (Normal)
UPDATE temp_stock_data SET current_box_stock = 2 WHERE item_code = 'RVI023';

-- =============================================
-- VERIFICATION QUERY WITH ACTUAL STOCK DATA
-- =============================================

-- Query to show items with their calculated status based on actual stock
SELECT 
    i.item_code,
    i.qty_per_box,
    i.standard_min,
    i.standard_max,
    ISNULL(s.current_box_stock, 0) as current_box_stock,
    ISNULL(s.current_box_stock * i.qty_per_box, 0) as current_pcs_stock,
    CASE 
        WHEN ISNULL(s.current_box_stock, 0) <= i.standard_min THEN 'Shortage'
        WHEN ISNULL(s.current_box_stock, 0) > i.standard_min AND ISNULL(s.current_box_stock, 0) < i.standard_max THEN 'Normal'
        WHEN ISNULL(s.current_box_stock, 0) >= i.standard_max THEN 'Over Stock'
        ELSE 'Normal'
    END as Status,
    s.last_updated
FROM items i
LEFT JOIN temp_stock_data s ON i.item_code = s.item_code
ORDER BY 
    CASE 
        WHEN ISNULL(s.current_box_stock, 0) <= i.standard_min THEN 1
        WHEN ISNULL(s.current_box_stock, 0) > i.standard_min AND ISNULL(s.current_box_stock, 0) < i.standard_max THEN 2
        WHEN ISNULL(s.current_box_stock, 0) >= i.standard_max THEN 3
        ELSE 2
    END,
    i.item_code;

-- =============================================
-- SUMMARY BY STATUS
-- =============================================

SELECT 
    CASE 
        WHEN ISNULL(s.current_box_stock, 0) <= i.standard_min THEN 'Shortage'
        WHEN ISNULL(s.current_box_stock, 0) > i.standard_min AND ISNULL(s.current_box_stock, 0) < i.standard_max THEN 'Normal'
        WHEN ISNULL(s.current_box_stock, 0) >= i.standard_max THEN 'Over Stock'
        ELSE 'Normal'
    END as Status,
    COUNT(*) as Count,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM items), 1) as Percentage
FROM items i
LEFT JOIN temp_stock_data s ON i.item_code = s.item_code
GROUP BY 
    CASE 
        WHEN ISNULL(s.current_box_stock, 0) <= i.standard_min THEN 'Shortage'
        WHEN ISNULL(s.current_box_stock, 0) > i.standard_min AND ISNULL(s.current_box_stock, 0) < i.standard_max THEN 'Normal'
        WHEN ISNULL(s.current_box_stock, 0) >= i.standard_max THEN 'Over Stock'
        ELSE 'Normal'
    END
ORDER BY 
    CASE 
        WHEN ISNULL(s.current_box_stock, 0) <= i.standard_min THEN 1
        WHEN ISNULL(s.current_box_stock, 0) > i.standard_min AND ISNULL(s.current_box_stock, 0) < i.standard_max THEN 2
        WHEN ISNULL(s.current_box_stock, 0) >= i.standard_max THEN 3
        ELSE 2
    END;

-- =============================================
-- OPTION 2: Direct Update to items table (if you want to simulate stock in items table)
-- =============================================

-- If you want to add a current_stock column to items table for testing
-- Uncomment the following:

/*
-- Add current_stock column to items table (if it doesn't exist)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('items') AND name = 'current_stock')
BEGIN
    ALTER TABLE items ADD current_stock INT DEFAULT 0;
END

-- Update items with stock data
UPDATE items SET current_stock = 0 WHERE item_code IN ('RVI001', 'RVI002', 'RVI003', 'RVI004', 'RVI005', 'RVI021');
UPDATE items SET current_stock = 3 WHERE item_code = 'RVI006';
UPDATE items SET current_stock = 2 WHERE item_code IN ('RVI007', 'RVI008', 'RVI009', 'RVI010', 'RVI011', 'RVI012', 'RVI013', 'RVI014', 'RVI015', 'RVI024', 'RVI025', 'RVI026', 'RVI027', 'RVI028', 'RVI029', 'RVI030');
UPDATE items SET current_stock = 2 WHERE item_code = 'RVI023';
UPDATE items SET current_stock = 6 WHERE item_code = 'RVI016';
UPDATE items SET current_stock = 5 WHERE item_code IN ('RVI017', 'RVI018', 'RVI019', 'RVI020');
UPDATE items SET current_stock = 4 WHERE item_code = 'RVI022';

-- Query with current_stock from items table
SELECT 
    item_code,
    qty_per_box,
    standard_min,
    standard_max,
    current_stock,
    current_stock * qty_per_box as current_pcs_stock,
    CASE 
        WHEN current_stock <= standard_min THEN 'Shortage'
        WHEN current_stock > standard_min AND current_stock < standard_max THEN 'Normal'
        WHEN current_stock >= standard_max THEN 'Over Stock'
        ELSE 'Normal'
    END as Status
FROM items
ORDER BY 
    CASE 
        WHEN current_stock <= standard_min THEN 1
        WHEN current_stock > standard_min AND current_stock < standard_max THEN 2
        WHEN current_stock >= standard_max THEN 3
        ELSE 2
    END,
    item_code;
*/

-- =============================================
-- CLEANUP (optional)
-- =============================================

-- Uncomment to remove temp table after testing
-- DROP TABLE temp_stock_data;

-- =============================================
-- NOTES
-- =============================================
/*
1. This script creates realistic stock data that will show different statuses in the dashboard:
   - 6 items with Shortage status
   - 18 items with Normal status  
   - 6 items with Over Stock status

2. The stock data is calculated as: Current Box Stock × Qty Per Box = Current PCS Stock

3. Status Logic:
   - Shortage: Current Box Stock ≤ StandardMin
   - Normal: StandardMin < Current Box Stock < StandardMax
   - Over Stock: Current Box Stock ≥ StandardMax

4. You can modify the current_box_stock values to test different scenarios

5. In a real system, this stock data would come from your actual inventory tracking system
*/

