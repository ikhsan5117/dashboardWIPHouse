-- =============================================
-- RVI Items Insert Query
-- Database: DB_SUPPLY_RVI
-- Table: items
-- Purpose: Insert item codes with logical standard_min and standard_max
-- =============================================

-- Clear existing data (optional - uncomment if needed)
-- DELETE FROM items WHERE item_code IN ('NA3050', 'NA2000', 'NA1200', 'NA1220', 'NA1960', 'NA1980', 'IS1020', 'NA1990', 'NA1210', 'IS1040', 'TA0990', 'TA0890', 'TA0870', 'IS1030', 'IS1050', 'NA1710', 'TA0820', 'TA0880', 'TA0830', 'TA0860', 'TA0840', 'TA0850');

-- =============================================
-- INSERT ITEMS WITH LOGICAL STANDARD_MIN AND STANDARD_MAX
-- =============================================

-- Insert unique item codes with calculated standard_min and standard_max
-- StandardMin = 1 box × QtyPerBox
-- StandardMax = StandardMin × 4 (4x minimum for good stock level)

INSERT INTO items (item_code, qty_per_box, standard_min, standard_max) VALUES
-- NA3050 - Assuming 100 pcs per box
('NA3050', 100.00, 100, 400),

-- NA2000 - Assuming 50 pcs per box (common for smaller items)
('NA2000', 50.00, 50, 200),

-- NA1200 - Assuming 75 pcs per box
('NA1200', 75.00, 75, 300),

-- NA1220 - Assuming 80 pcs per box
('NA1220', 80.00, 80, 320),

-- NA1960 - Assuming 60 pcs per box
('NA1960', 60.00, 60, 240),

-- NA1980 - Assuming 90 pcs per box
('NA1980', 90.00, 90, 360),

-- IS1020 - Assuming 120 pcs per box
('IS1020', 120.00, 120, 480),

-- NA1990 - Assuming 70 pcs per box
('NA1990', 70.00, 70, 280),

-- NA1210 - Assuming 85 pcs per box
('NA1210', 85.00, 85, 340),

-- IS1040 - Assuming 150 pcs per box
('IS1040', 150.00, 150, 600),

-- TA0990 - Assuming 200 pcs per box
('TA0990', 200.00, 200, 800),

-- TA0890 - Assuming 180 pcs per box
('TA0890', 180.00, 180, 720),

-- TA0870 - Assuming 160 pcs per box
('TA0870', 160.00, 160, 640),

-- IS1030 - Assuming 110 pcs per box
('IS1030', 110.00, 110, 440),

-- IS1050 - Assuming 130 pcs per box
('IS1050', 130.00, 130, 520),

-- NA1710 - Assuming 95 pcs per box
('NA1710', 95.00, 95, 380),

-- TA0820 - Assuming 170 pcs per box
('TA0820', 170.00, 170, 680),

-- TA0880 - Assuming 190 pcs per box
('TA0880', 190.00, 190, 760),

-- TA0830 - Assuming 175 pcs per box
('TA0830', 175.00, 175, 700),

-- TA0860 - Assuming 185 pcs per box
('TA0860', 185.00, 185, 740),

-- TA0840 - Assuming 165 pcs per box
('TA0840', 165.00, 165, 660),

-- TA0850 - Assuming 155 pcs per box
('TA0850', 155.00, 155, 620);

-- =============================================
-- VERIFICATION QUERIES
-- =============================================

-- Check inserted items
SELECT 
    item_code,
    qty_per_box,
    standard_min,
    standard_max,
    'StandardMin = 1 box × ' + CAST(qty_per_box AS VARCHAR) + ' = ' + CAST(standard_min AS VARCHAR) as Calculation_Note
FROM items 
WHERE item_code IN ('NA3050', 'NA2000', 'NA1200', 'NA1220', 'NA1960', 'NA1980', 'IS1020', 'NA1990', 'NA1210', 'IS1040', 'TA0990', 'TA0890', 'TA0870', 'IS1030', 'IS1050', 'NA1710', 'TA0820', 'TA0880', 'TA0830', 'TA0860', 'TA0840', 'TA0850')
ORDER BY item_code;

-- Count total items
SELECT COUNT(*) as Total_Items_Inserted FROM items 
WHERE item_code IN ('NA3050', 'NA2000', 'NA1200', 'NA1220', 'NA1960', 'NA1980', 'IS1020', 'NA1990', 'NA1210', 'IS1040', 'TA0990', 'TA0890', 'TA0870', 'IS1030', 'IS1050', 'NA1710', 'TA0820', 'TA0880', 'TA0830', 'TA0860', 'TA0840', 'TA0850');

-- =============================================
-- ALTERNATIVE: INSERT WITH DIFFERENT QTY_PER_BOX VALUES
-- =============================================

-- If you want to use different qty_per_box values, uncomment and modify below:

/*
INSERT INTO items (item_code, qty_per_box, standard_min, standard_max) VALUES
-- NA3050 - Different qty_per_box values
('NA3050', 50.00, 50, 200),   -- 50 pcs per box
('NA2000', 25.00, 25, 100),   -- 25 pcs per box
('NA1200', 30.00, 30, 120),   -- 30 pcs per box
('NA1220', 40.00, 40, 160),   -- 40 pcs per box
('NA1960', 35.00, 35, 140),   -- 35 pcs per box
('NA1980', 45.00, 45, 180),   -- 45 pcs per box
('IS1020', 60.00, 60, 240),   -- 60 pcs per box
('NA1990', 55.00, 55, 220),   -- 55 pcs per box
('NA1210', 65.00, 65, 260),   -- 65 pcs per box
('IS1040', 75.00, 75, 300),   -- 75 pcs per box
('TA0990', 100.00, 100, 400), -- 100 pcs per box
('TA0890', 90.00, 90, 360),   -- 90 pcs per box
('TA0870', 80.00, 80, 320),   -- 80 pcs per box
('IS1030', 70.00, 70, 280),   -- 70 pcs per box
('IS1050', 85.00, 85, 340),   -- 85 pcs per box
('NA1710', 95.00, 95, 380),   -- 95 pcs per box
('TA0820', 110.00, 110, 440), -- 110 pcs per box
('TA0880', 120.00, 120, 480), -- 120 pcs per box
('TA0830', 105.00, 105, 420), -- 105 pcs per box
('TA0860', 115.00, 115, 460), -- 115 pcs per box
('TA0840', 125.00, 125, 500), -- 125 pcs per box
('TA0850', 135.00, 135, 540); -- 135 pcs per box
*/

-- =============================================
-- NOTES
-- =============================================
/*
1. StandardMin Calculation: 1 box × QtyPerBox
   - NA3050: 1 × 100 = 100
   - NA2000: 1 × 50 = 50
   - etc.

2. StandardMax Calculation: StandardMin × 4
   - This provides a good buffer for stock management
   - You can adjust the multiplier (4) based on your business needs

3. QtyPerBox Values:
   - I've assigned reasonable values based on typical manufacturing scenarios
   - You can modify these values based on your actual product specifications

4. The query only inserts unique item codes (no duplicates)
   - Even though your list has multiple entries for some items, the INSERT will only create one record per unique item_code

5. To modify qty_per_box values:
   - Update the values in the INSERT statement above
   - Or use the alternative INSERT statement with different values
*/

