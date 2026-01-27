-- =============================================
-- Update Default User for BTR Module
-- Username: adminBTR
-- Password: BTR123
-- =============================================

USE DB_SUPPLY_BTR;
GO

-- Delete existing admin user if exists
DELETE FROM users WHERE username = 'admin';
GO

-- Insert new adminBTR user
IF NOT EXISTS (SELECT * FROM users WHERE username = 'adminBTR')
BEGIN
    INSERT INTO users (username, password, created_date)
    VALUES ('adminBTR', 'BTR123', GETDATE());
    PRINT 'User adminBTR created successfully.';
END
ELSE
BEGIN
    UPDATE users 
    SET password = 'BTR123', 
        created_date = GETDATE()
    WHERE username = 'adminBTR';
    PRINT 'User adminBTR updated successfully.';
END
GO

-- Verify
SELECT id, username, created_date, last_login 
FROM users 
WHERE username = 'adminBTR';
GO

PRINT '==============================================';
PRINT 'BTR User Setup Complete!';
PRINT 'Username: adminBTR';
PRINT 'Password: BTR123';
PRINT '==============================================';
GO
