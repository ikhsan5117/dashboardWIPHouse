-- =============================================
-- RVI Sample Data Insert Script
-- Database: DB_SUPPLY_RVI
-- Table: items
-- =============================================

-- Clear existing data (optional - uncomment if needed)
-- DELETE FROM items;

-- Insert sample data for RVI items table
-- Format: ItemCode, QtyPerBox, StandardMin, StandardMax
-- Logic: StandardMin = 1 box × QtyPerBox, StandardMax should be > StandardMin

-- =============================================
-- SHORTAGE ITEMS (Current Stock ≤ StandardMin)
-- =============================================
INSERT INTO items (item_code, qty_per_box, standard_min, standard_max) VALUES
('RVI001', 100.00, 100, 500),    -- StandardMin = 1 × 100 = 100, Current Stock = 0 (Shortage)
('RVI002', 50.00, 50, 200),      -- StandardMin = 1 × 50 = 50, Current Stock = 0 (Shortage)
('RVI003', 200.00, 200, 800),    -- StandardMin = 1 × 200 = 200, Current Stock = 0 (Shortage)
('RVI004', 75.00, 75, 300),      -- StandardMin = 1 × 75 = 75, Current Stock = 0 (Shortage)
('RVI005', 150.00, 150, 600);    -- StandardMin = 1 × 150 = 150, Current Stock = 0 (Shortage)

-- =============================================
-- NORMAL ITEMS (StandardMin < Current Stock < StandardMax)
-- =============================================
INSERT INTO items (item_code, qty_per_box, standard_min, standard_max) VALUES
('RVI006', 100.00, 100, 500),    -- StandardMin = 100, StandardMax = 500, Current Stock = 300 (Normal)
('RVI007', 50.00, 50, 200),      -- StandardMin = 50, StandardMax = 200, Current Stock = 125 (Normal)
('RVI008', 200.00, 200, 800),    -- StandardMin = 200, StandardMax = 800, Current Stock = 500 (Normal)
('RVI009', 75.00, 75, 300),      -- StandardMin = 75, StandardMax = 300, Current Stock = 200 (Normal)
('RVI010', 150.00, 150, 600),    -- StandardMin = 150, StandardMax = 600, Current Stock = 400 (Normal)
('RVI011', 120.00, 120, 480),    -- StandardMin = 120, StandardMax = 480, Current Stock = 300 (Normal)
('RVI012', 80.00, 80, 320),      -- StandardMin = 80, StandardMax = 320, Current Stock = 200 (Normal)
('RVI013', 250.00, 250, 1000),   -- StandardMin = 250, StandardMax = 1000, Current Stock = 600 (Normal)
('RVI014', 90.00, 90, 360),      -- StandardMin = 90, StandardMax = 360, Current Stock = 225 (Normal)
('RVI015', 180.00, 180, 720);    -- StandardMin = 180, StandardMax = 720, Current Stock = 450 (Normal)

-- =============================================
-- OVER STOCK ITEMS (Current Stock ≥ StandardMax)
-- =============================================
INSERT INTO items (item_code, qty_per_box, standard_min, standard_max) VALUES
('RVI016', 100.00, 100, 500),    -- StandardMin = 100, StandardMax = 500, Current Stock = 600 (Over Stock)
('RVI017', 50.00, 50, 200),      -- StandardMin = 50, StandardMax = 200, Current Stock = 250 (Over Stock)
('RVI018', 200.00, 200, 800),    -- StandardMin = 200, StandardMax = 800, Current Stock = 1000 (Over Stock)
('RVI019', 75.00, 75, 300),      -- StandardMin = 75, StandardMax = 300, Current Stock = 400 (Over Stock)
('RVI020', 150.00, 150, 600);    -- StandardMin = 150, StandardMax = 600, Current Stock = 750 (Over Stock)

-- =============================================
-- EDGE CASE ITEMS (Boundary conditions)
-- =============================================
INSERT INTO items (item_code, qty_per_box, standard_min, standard_max) VALUES
('RVI021', 100.00, 100, 500),    -- StandardMin = 100, Current Stock = 100 (Exactly at minimum - Shortage)
('RVI022', 50.00, 50, 200),      -- StandardMax = 200, Current Stock = 200 (Exactly at maximum - Over Stock)
('RVI023', 200.00, 200, 800),    -- StandardMin = 200, Current Stock = 201 (Just above minimum - Normal)
('RVI024', 75.00, 75, 300),      -- StandardMax = 300, Current Stock = 299 (Just below maximum - Normal)
('RVI025', 150.00, 150, 600);    -- StandardMin = 150, StandardMax = 600, Current Stock = 375 (Middle - Normal)

-- =============================================
-- ADDITIONAL VARIETY ITEMS
-- =============================================
INSERT INTO items (item_code, qty_per_box, standard_min, standard_max) VALUES
('RVI026', 25.00, 25, 100),      -- Small quantity items
('RVI027', 500.00, 500, 2000),   -- Large quantity items
('RVI028', 30.00, 30, 120),      -- Different ratios
('RVI029', 400.00, 400, 1600),   -- High volume items
('RVI030', 60.00, 60, 240);      -- Medium quantity items

-- =============================================
-- VERIFICATION QUERIES
-- =============================================

-- Check total items inserted
SELECT COUNT(*) as Total_Items FROM items;

-- Check items by status logic (assuming current stock = 0 for all items initially)
-- Note: In real scenario, current stock would come from vw_stock_summary or actual stock data
SELECT 
    item_code,
    qty_per_box,
    standard_min,
    standard_max,
    CASE 
        WHEN 0 <= standard_min THEN 'Shortage'  -- Current stock = 0 (default)
        WHEN 0 > standard_min AND 0 < standard_max THEN 'Normal'
        WHEN 0 >= standard_max THEN 'Over Stock'
        ELSE 'Normal'
    END as Calculated_Status
FROM items
ORDER BY item_code;

-- Summary by status
SELECT 
    CASE 
        WHEN 0 <= standard_min THEN 'Shortage'
        WHEN 0 > standard_min AND 0 < standard_max THEN 'Normal'
        WHEN 0 >= standard_max THEN 'Over Stock'
        ELSE 'Normal'
    END as Status,
    COUNT(*) as Count
FROM items
GROUP BY 
    CASE 
        WHEN 0 <= standard_min THEN 'Shortage'
        WHEN 0 > standard_min AND 0 < standard_max THEN 'Normal'
        WHEN 0 >= standard_max THEN 'Over Stock'
        ELSE 'Normal'
    END
ORDER BY Status;

-- =============================================
-- SAMPLE STOCK DATA (if vw_stock_summary exists)
-- =============================================
-- Uncomment and modify if you have stock tracking data

/*
-- Insert sample stock data to vw_stock_summary (if table exists)
INSERT INTO vw_stock_summary (item_code, full_qr, current_box_stock, last_updated) VALUES
('RVI001', 'QR001', 0, '2024-01-15 10:00:00'),      -- Shortage
('RVI002', 'QR002', 0, '2024-01-15 10:00:00'),      -- Shortage
('RVI003', 'QR003', 0, '2024-01-15 10:00:00'),      -- Shortage
('RVI006', 'QR006', 3, '2024-01-15 10:00:00'),      -- Normal (3 boxes = 300 pcs)
('RVI007', 'QR007', 2, '2024-01-15 10:00:00'),      -- Normal (2 boxes = 100 pcs)
('RVI008', 'QR008', 2, '2024-01-15 10:00:00'),      -- Normal (2 boxes = 400 pcs)
('RVI016', 'QR016', 6, '2024-01-15 10:00:00'),      -- Over Stock (6 boxes = 600 pcs)
('RVI017', 'QR017', 5, '2024-01-15 10:00:00'),      -- Over Stock (5 boxes = 250 pcs)
('RVI018', 'QR018', 5, '2024-01-15 10:00:00');      -- Over Stock (5 boxes = 1000 pcs)
*/

-- =============================================
-- NOTES
-- =============================================
/*
1. StandardMin Calculation: 1 box × QtyPerBox
   - RVI001: 1 × 100 = 100
   - RVI002: 1 × 50 = 50
   - etc.

2. StandardMax should be > StandardMin for logical consistency

3. Current Stock Logic (for dashboard display):
   - Shortage: Current Stock ≤ StandardMin
   - Normal: StandardMin < Current Stock < StandardMax  
   - Over Stock: Current Stock ≥ StandardMax

4. In real scenario, current stock would come from actual stock tracking system
   For this sample, we assume current stock = 0 (all items show as Shortage initially)

5. To test different statuses, you would need to update the stock data
   or modify the current stock values in the verification queries above.
*/

