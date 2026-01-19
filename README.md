# � Dashboard WIP House

**Multi-Database Inventory Management System** built with ASP.NET Core MVC for managing stock across HOSE, RVI, and MOLDED production lines.

---

## � Quick Start

### **Run the Application**
```bash
dotnet run
```

### **Access the Dashboard**
Open your browser and navigate to:
- **URL:** http://localhost:5005

### **Login Credentials**

| Database | Username | Password |
|----------|----------|----------|
| **HOSE** | `admin` | `admin123` |
| **RVI** | `adminRVI` | `rvi123` |
| **MOLDED** | `adminMolded` | `molded321` |

---

## 🏗️ Architecture

### **Technology Stack**
- **Framework:** ASP.NET Core MVC 8.0
- **Database:** SQL Server (3 separate databases)
- **ORM:** Entity Framework Core 9.0.8
- **Authentication:** Cookie-based Authentication
- **Excel Processing:** EPPlus 7.0.0
- **Charts:** Chart.js

### **Database Structure**
1. **DB_SUPPLY_HOSE** - Green Hose & After Washing products (with expiry tracking)
2. **DB_SUPPLY_RVI** - RVI products (stock only, no expiry)
3. **DB_SUPPLY_MOLDED** - Molded products (with expiry tracking)

---

## 📁 Project Structure

```
dashboardWIPHouse/
├── Controllers/          # MVC Controllers
│   ├── AccountController.cs    # Unified login
│   ├── HomeController.cs       # HOSE dashboard
│   ├── RVIController.cs        # RVI dashboard
│   └── MoldedController.cs     # MOLDED dashboard
├── Models/              # Data models
├── Views/               # Razor views
├── Data/                # DbContext files
├── docs/                # Documentation files
└── wwwroot/             # Static files (CSS, JS, images)
```

---

## ✨ Features

### **Dashboard Monitoring**
- ✅ Real-time stock monitoring
- ✅ Expiry date tracking (HOSE & MOLDED)
- ✅ Stock level alerts (Shortage, Normal, Over Stock)
- ✅ Interactive charts and visualizations
- ✅ Data export/import via Excel

### **Multi-Database Support**
- ✅ Unified login system
- ✅ Automatic user seeding on startup
- ✅ Database-specific dashboards
- ✅ Role-based access control (Admin/User)

### **Excel Integration**
- ✅ Upload Excel files for bulk data import
- ✅ Download Excel templates
- ✅ Data validation and error reporting

---

## 📚 Documentation

All documentation files are organized in the [`docs/`](docs/) folder:

- **[UNIFIED_LOGIN_README.md](docs/UNIFIED_LOGIN_README.md)** - Login system guide
- **[PENJELASAN_ITEMS_TABLE.md](docs/PENJELASAN_ITEMS_TABLE.md)** - Database structure
- **[ICON_TROUBLESHOOTING.md](docs/ICON_TROUBLESHOOTING.md)** - Icon troubleshooting
- **[FINAL_COLOR_UPDATE.md](docs/FINAL_COLOR_UPDATE.md)** - UI color scheme

---

## 🔧 Configuration

### **Database Connection Strings**
Edit `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=DB_SUPPLY_HOSE;...",
    "AnotherDb": "Server=...;Database=DB_SUPPLY_RVI;...",
    "MoldedDb": "Server=...;Database=DB_SUPPLY_MOLDED;..."
  }
}
```

### **Auto-Seeding Users**
Users are automatically created on application startup if they don't exist:
- `adminMolded` / `molded321`
- `adminMolded321` / `molded321`

---

## �️ Development

### **Build the Project**
```bash
dotnet build
```

### **Run in Development Mode**
```bash
dotnet run
```

### **Database Migrations**
```bash
dotnet ef migrations add MigrationName
dotnet ef database update
```

---

## 📊 Dashboard Features by Module

### **HOSE Dashboard** (`/Home`)
- Expired items tracking
- Near-expired items alerts
- Shortage monitoring
- Stock comparison with min/max standards

### **RVI Dashboard** (`/RVI`)
- Stock level monitoring
- Shortage alerts
- Over stock warnings
- No expiry tracking

### **MOLDED Dashboard** (`/Molded`)
- Full inventory management
- CRUD operations for items
- Expiry tracking
- Stock alerts

---

## 🤝 Contributing

This is a production inventory management system. For changes or improvements, please:
1. Create a feature branch
2. Test thoroughly
3. Submit a pull request

---

## 📝 License

Internal use only - PT. Velasto Indonesia

---

## 👨‍💻 Developer

**Developed by:** Ikhsan  
**Repository:** https://github.com/ikhsan5117/dashboardWIPHouse.git  
**Last Updated:** January 19, 2026

---

## 🆘 Support

For issues or questions:
1. Check the [documentation](docs/)
2. Review troubleshooting guides
3. Contact the development team

---

**Status:** ✅ Production Ready  
**Version:** 1.0.0  
**Framework:** .NET 8.0
