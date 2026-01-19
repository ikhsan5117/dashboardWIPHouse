-- =============================================
-- RVI Items Simple Insert Query
-- Database: DB_SUPPLY_RVI
-- Table: items
-- Purpose: Simple insert with same qty_per_box for all items
-- =============================================

-- =============================================
-- OPTION 1: All items with same qty_per_box (100 pcs per box)
-- =============================================

INSERT INTO items (item_code, qty_per_box, standard_min, standard_max) VALUES
('NA3050', 100.00, 100, 400),
('NA2000', 100.00, 100, 400),
('NA1200', 100.00, 100, 400),
('NA1220', 100.00, 100, 400),
('NA1960', 100.00, 100, 400),
('NA1980', 100.00, 100, 400),
('IS1020', 100.00, 100, 400),
('NA1990', 100.00, 100, 400),
('NA1210', 100.00, 100, 400),
('IS1040', 100.00, 100, 400),
('TA0990', 100.00, 100, 400),
('TA0890', 100.00, 100, 400),
('TA0870', 100.00, 100, 400),
('IS1030', 100.00, 100, 400),
('IS1050', 100.00, 100, 400),
('NA1710', 100.00, 100, 400),
('TA0820', 100.00, 100, 400),
('TA0880', 100.00, 100, 400),
('TA0830', 100.00, 100, 400),
('TA0860', 100.00, 100, 400),
('TA0840', 100.00, 100, 400),
('TA0850', 100.00, 100, 400);

-- =============================================
-- OPTION 2: All items with qty_per_box = 50 pcs per box
-- =============================================

/*
INSERT INTO items (item_code, qty_per_box, standard_min, standard_max) VALUES
('NA3050', 50.00, 50, 200),
('NA2000', 50.00, 50, 200),
('NA1200', 50.00, 50, 200),
('NA1220', 50.00, 50, 200),
('NA1960', 50.00, 50, 200),
('NA1980', 50.00, 50, 200),
('IS1020', 50.00, 50, 200),
('NA1990', 50.00, 50, 200),
('NA1210', 50.00, 50, 200),
('IS1040', 50.00, 50, 200),
('TA0990', 50.00, 50, 200),
('TA0890', 50.00, 50, 200),
('TA0870', 50.00, 50, 200),
('IS1030', 50.00, 50, 200),
('IS1050', 50.00, 50, 200),
('NA1710', 50.00, 50, 200),
('TA0820', 50.00, 50, 200),
('TA0880', 50.00, 50, 200),
('TA0830', 50.00, 50, 200),
('TA0860', 50.00, 50, 200),
('TA0840', 50.00, 50, 200),
('TA0850', 50.00, 50, 200);
*/

-- =============================================
-- OPTION 3: All items with qty_per_box = 25 pcs per box
-- =============================================

/*
INSERT INTO items (item_code, qty_per_box, standard_min, standard_max) VALUES
('NA3050', 25.00, 25, 100),
('NA2000', 25.00, 25, 100),
('NA1200', 25.00, 25, 100),
('NA1220', 25.00, 25, 100),
('NA1960', 25.00, 25, 100),
('NA1980', 25.00, 25, 100),
('IS1020', 25.00, 25, 100),
('NA1990', 25.00, 25, 100),
('NA1210', 25.00, 25, 100),
('IS1040', 25.00, 25, 100),
('TA0990', 25.00, 25, 100),
('TA0890', 25.00, 25, 100),
('TA0870', 25.00, 25, 100),
('IS1030', 25.00, 25, 100),
('IS1050', 25.00, 25, 100),
('NA1710', 25.00, 25, 100),
('TA0820', 25.00, 25, 100),
('TA0880', 25.00, 25, 100),
('TA0830', 25.00, 25, 100),
('TA0860', 25.00, 25, 100),
('TA0840', 25.00, 25, 100),
('TA0850', 25.00, 25, 100);
*/

-- =============================================
-- VERIFICATION
-- =============================================

-- Check inserted items
SELECT 
    item_code,
    qty_per_box,
    standard_min,
    standard_max,
    'StandardMin = 1 box × ' + CAST(qty_per_box AS VARCHAR) + ' = ' + CAST(standard_min AS VARCHAR) as Calculation
FROM items 
WHERE item_code IN ('NA3050', 'NA2000', 'NA1200', 'NA1220', 'NA1960', 'NA1980', 'IS1020', 'NA1990', 'NA1210', 'IS1040', 'TA0990', 'TA0890', 'TA0870', 'IS1030', 'IS1050', 'NA1710', 'TA0820', 'TA0880', 'TA0830', 'TA0860', 'TA0840', 'TA0850')
ORDER BY item_code;

-- Count total items
SELECT COUNT(*) as Total_Items_Inserted FROM items 
WHERE item_code IN ('NA3050', 'NA2000', 'NA1200', 'NA1220', 'NA1960', 'NA1980', 'IS1020', 'NA1990', 'NA1210', 'IS1040', 'TA0990', 'TA0890', 'TA0870', 'IS1030', 'IS1050', 'NA1710', 'TA0820', 'TA0880', 'TA0830', 'TA0860', 'TA0840', 'TA0850');

-- =============================================
-- NOTES
-- =============================================
/*
1. Choose one of the three options above:
   - Option 1: 100 pcs per box (recommended for most cases)
   - Option 2: 50 pcs per box (for smaller items)
   - Option 3: 25 pcs per box (for very small items)

2. StandardMin = 1 box × QtyPerBox
3. StandardMax = StandardMin × 4 (provides good stock buffer)

4. All 22 unique item codes will be inserted
5. Duplicate item codes in your list will be handled (only one record per unique item_code)
*/

