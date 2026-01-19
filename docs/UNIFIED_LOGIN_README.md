# Unified Login System Documentation

## Overview
Sistem login telah diunifikasi untuk mendukung kedua database (HOSE dan RVI) dalam satu halaman login yang sama. Pengguna dapat memilih database yang ingin diakses dan login dengan credentials yang sesuai.

## Features

### 1. **Unified Login Page**
- Satu halaman login untuk kedua database
- Dropdown selection untuk memilih database
- Dynamic placeholder berdasarkan database yang dipilih
- Informasi credentials default ditampilkan di halaman login

### 2. **Database Selection**
- **HOSE Database**: Untuk akses Green Hose dan After Washing dashboards
- **RVI Database**: Untuk akses RVI dashboard
- Auto-redirect berdasarkan database yang dipilih

### 3. **Authentication Flow**
- Login dengan database HOSE → Redirect ke Green Hose Dashboard
- Login dengan database RVI → Redirect ke RVI Dashboard
- Session management terpisah untuk setiap database
- Claims-based authentication dengan database identifier

## Login Credentials

### HOSE Database
- **Admin**: `admin` / `admin123`
- **User**: `user` / `user123`

### RVI Database
- **Admin**: `adminRVI` / `rvi123`
- **User**: `userRVI` / `rvi123`

## Technical Implementation

### AccountController Updates
```csharp
[HttpPost]
public async Task<IActionResult> Login(string username, string password, string database)
{
    if (database == "HOSE")
    {
        // Authenticate against HOSE database
        // Set claims with Database = "HOSE"
        // Redirect to Home/Index
    }
    else if (database == "RVI")
    {
        // Authenticate against RVI database
        // Set claims with Database = "RVI"
        // Set RVI session variables
        // Redirect to RVI/Index
    }
}
```

### Authentication Claims
- `ClaimTypes.Name`: Username
- `ClaimTypes.Role`: Admin/User
- `Database`: HOSE/RVI (custom claim)

### Session Management
- HOSE: Standard ASP.NET Core authentication
- RVI: Additional session variables for RVI-specific data

## UI/UX Features

### 1. **Database Selection Dropdown**
- Styled select dropdown dengan custom arrow
- Required field validation
- Clear labeling untuk setiap database

### 2. **Dynamic Placeholders**
- Username dan password placeholders berubah berdasarkan database yang dipilih
- Hints untuk credentials yang valid

### 3. **Credentials Information**
- Box informasi dengan default credentials
- Styling yang konsisten dengan design login

### 4. **Error Handling**
- Specific error messages untuk setiap database
- Validation untuk database selection
- User-friendly error display

## Navigation Updates

### Menu Changes
- "RVI Dashboard" menu sekarang mengarah ke `/Account/Login`
- User dapat memilih database dari halaman login
- Consistent navigation experience

### Access Control
- RVI Dashboard: Requires `Database = "RVI"` claim
- HOSE Dashboards: Requires `Database = "HOSE"` claim
- Automatic redirect ke login jika tidak authenticated

## Security Features

### 1. **Database Isolation**
- Separate authentication untuk setiap database
- Claims-based access control
- Session isolation

### 2. **Input Validation**
- Required field validation
- Database selection validation
- SQL injection protection melalui Entity Framework

### 3. **Error Handling**
- Generic error messages untuk security
- Detailed logging untuk troubleshooting
- Graceful fallback untuk connection issues

## Usage Instructions

### 1. **Access Login Page**
- URL: `/Account/Login`
- Atau klik menu "RVI Dashboard" di sidebar

### 2. **Login Process**
1. Pilih database (HOSE atau RVI)
2. Masukkan username dan password
3. Klik "LOGIN"
4. Sistem akan redirect ke dashboard yang sesuai

### 3. **Dashboard Access**
- **HOSE**: Redirect ke Green Hose Dashboard
- **RVI**: Redirect ke RVI Dashboard
- **After Washing**: Accessible dari HOSE login

## Migration Notes

### Removed Components
- `Views/RVI/Login.cshtml` - Deleted (unified login)
- RVI Controller login methods - Removed
- Separate RVI routing - Simplified

### Updated Components
- `AccountController` - Enhanced dengan dual database support
- `RVIController` - Updated authentication check
- `Views/Account/Login.cshtml` - Enhanced dengan database selection
- Navigation menu - Updated links

## Benefits

### 1. **User Experience**
- Single entry point untuk semua dashboards
- Consistent login experience
- Clear database selection

### 2. **Maintenance**
- Single login page to maintain
- Centralized authentication logic
- Easier credential management

### 3. **Security**
- Centralized authentication handling
- Consistent security policies
- Better audit trail

## Testing

### Test Cases
1. **HOSE Login**: admin/admin123 → Green Hose Dashboard
2. **RVI Login**: adminRVI/rvi123 → RVI Dashboard
3. **Invalid Credentials**: Error message display
4. **No Database Selection**: Validation error
5. **Cross-database Access**: Proper access control

### Build Status
- ✅ Build successful
- ✅ No critical errors
- ✅ All functionality working
- ✅ Navigation updated
- ✅ Authentication flow complete

## Future Enhancements

### Potential Improvements
1. **Remember Database Selection**: Cookie untuk database preference
2. **Auto-fill Credentials**: Browser autofill support
3. **Password Reset**: Forgot password functionality
4. **Multi-factor Authentication**: Enhanced security
5. **User Management**: Admin panel untuk user management

## Troubleshooting

### Common Issues
1. **Database Connection Error**: Check connection strings
2. **Authentication Failed**: Verify credentials
3. **Redirect Issues**: Check claims and routing
4. **Session Problems**: Clear browser cache

### Logs
- Authentication attempts logged
- Database connection status logged
- Error details logged for debugging




