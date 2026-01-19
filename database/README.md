# 🗄️ Database Scripts

This folder contains SQL scripts for database setup, testing, and data seeding.

---

## 📋 SQL Files Index

### **MOLDED Database**
- **[MOLDED_Admin_User_Insert.sql](MOLDED_Admin_User_Insert.sql)** - Insert admin users for MOLDED database
  - Creates `adminMolded` and `adminMolded321` users
  - Password: `molded321`

### **RVI Database**
- **[RVI_Items_Insert_Query.sql](RVI_Items_Insert_Query.sql)** - Insert RVI items master data
- **[RVI_Items_Simple_Insert.sql](RVI_Items_Simple_Insert.sql)** - Simplified RVI items insert
- **[RVI_Sample_Data.sql](RVI_Sample_Data.sql)** - Sample data for RVI testing
- **[RVI_Complete_Test_Data.sql](RVI_Complete_Test_Data.sql)** - Complete test dataset for RVI
- **[RVI_Stock_Summary_Integration.sql](RVI_Stock_Summary_Integration.sql)** - Stock summary view integration
- **[RVI_Stock_Update_Queries.sql](RVI_Stock_Update_Queries.sql)** - Stock update queries and procedures

---

## 🚀 Usage

### **Running SQL Scripts**

#### **Option 1: SQL Server Management Studio (SSMS)**
1. Open SSMS
2. Connect to your SQL Server instance
3. Select the target database (DB_SUPPLY_MOLDED or DB_SUPPLY_RVI)
4. Open the SQL file
5. Execute (F5)

#### **Option 2: Command Line (sqlcmd)**
```bash
sqlcmd -S ServerName -d DatabaseName -i script.sql
```

#### **Option 3: Azure Data Studio**
1. Open Azure Data Studio
2. Connect to server
3. Open SQL file
4. Run script

---

## 📊 Database Structure

### **DB_SUPPLY_HOSE**
- Items table (item_code, qty_per_box, standard_min, standard_max, standard_exp)
- Users table
- Storage logs
- Supply logs

### **DB_SUPPLY_RVI**
- Items table (no expiry tracking)
- Users table
- Stock summary views
- Before Check items

### **DB_SUPPLY_MOLDED**
- Items table (with expiry tracking)
- Users table
- Stock management

---

## ⚠️ Important Notes

### **Auto-Seeding**
The application automatically creates admin users on startup, so manual execution of user insert scripts is **optional**.

### **Test Data**
- Use `*_Sample_Data.sql` files for development/testing
- **DO NOT** run test data scripts on production databases

### **Backup First**
Always backup your database before running update or migration scripts:
```sql
BACKUP DATABASE [DatabaseName] TO DISK = 'C:\Backup\DatabaseName.bak'
```

---

## 🔧 Maintenance Scripts

### **Check Database Connection**
```sql
SELECT @@SERVERNAME AS ServerName, DB_NAME() AS CurrentDatabase;
```

### **Verify Users Table**
```sql
SELECT * FROM users;
```

### **Check Items Count**
```sql
SELECT COUNT(*) AS TotalItems FROM items;
```

---

## 📝 Script Naming Convention

- `[DATABASE]_[Purpose]_[Type].sql`
- Examples:
  - `RVI_Items_Insert.sql` - RVI items insertion
  - `MOLDED_Admin_User_Insert.sql` - MOLDED admin user creation

---

## 🆘 Troubleshooting

### **Permission Denied**
Ensure your SQL user has appropriate permissions:
```sql
GRANT INSERT, UPDATE, DELETE ON [TableName] TO [UserName];
```

### **Foreign Key Constraints**
Disable constraints temporarily if needed:
```sql
ALTER TABLE [TableName] NOCHECK CONSTRAINT ALL;
-- Run your script
ALTER TABLE [TableName] CHECK CONSTRAINT ALL;
```

---

**Last Updated:** January 19, 2026  
**Database Server:** SQL Server 2019+  
**Connection:** See `appsettings.json` for connection strings
