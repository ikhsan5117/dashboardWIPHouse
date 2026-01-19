# 🔧 Troubleshooting Icons Dashboard

## ✅ Masalah yang Sudah Diperbaiki:

### 1. **Icon Lama Hilang**
- **Masalah**: FontAwesome icons tidak muncul karena CSS fallback salah
- **Solusi**: Mengubah CSS agar FontAwesome icons muncul sebagai default, dan disembunyikan hanya jika PNG berhasil dimuat

### 2. **Shortage Tidak Ada Warna**
- **Masalah**: Background gradient hilang karena konflik CSS
- **Solusi**: Menambahkan `!important` dan CSS spesifik untuk memastikan background critical muncul

## 🎯 Status Saat Ini:

### ✅ **Yang Sudah Bekerja:**
- FontAwesome icons muncul sebagai default
- Background gradient untuk semua stats cards (termasuk shortage)
- CSS responsive untuk berbagai ukuran layar
- Fallback system yang benar

### 🔄 **Yang Perlu Testing:**
- Upload file PNG ke folder `wwwroot/img/stats/`
- Test apakah PNG icons menggantikan FontAwesome icons
- Test responsive design

## 📁 **File PNG yang Diperlukan:**

```
wwwroot/img/stats/
├── expired.png          ✅ (sudah ada CSS)
├── near_expired.png     ✅ (sudah ada CSS) 
├── shortage.png         ✅ (sudah ada CSS)
├── over_stock.png       ✅ (sudah ada CSS)
└── normal.png           ✅ (sudah ada CSS)
```

## 🧪 **Testing Steps:**

### 1. **Test Tanpa PNG Files:**
- Buka dashboard
- Pastikan FontAwesome icons muncul di semua stats cards
- Pastikan background gradient muncul (termasuk shortage yang berwarna pink/merah)

### 2. **Test Dengan PNG Files:**
- Upload file PNG ke folder `wwwroot/img/stats/`
- Refresh dashboard
- Pastikan PNG icons muncul menggantikan FontAwesome icons
- Pastikan background gradient tetap muncul

### 3. **Test Responsive:**
- Test di desktop, tablet, dan mobile
- Pastikan icons ter-scale dengan baik

## 🐛 **Jika Masih Ada Masalah:**

### **Shortage Masih Tidak Ada Warna:**
```css
/* Tambahkan CSS ini jika masih ada masalah */
.widget-icon.themed-background-critical {
    background: linear-gradient(135deg, #e91e63 0%, #ad1457 100%) !important;
}
```

### **Icons Tidak Muncul:**
1. Check browser console untuk error 404
2. Pastikan file PNG ada di folder yang benar
3. Pastikan nama file sesuai (case-sensitive)

### **PNG Icons Tidak Menggantikan FontAwesome:**
1. Check apakah file PNG bisa diakses langsung: `http://localhost:port/img/stats/expired.png`
2. Pastikan CSS class sudah benar: `expired-icon`, `near-expired-icon`, dll.

## 🎨 **CSS Structure yang Benar:**

```css
/* Background gradient (warna) */
.themed-background-critical {
    background: linear-gradient(135deg, #e91e63 0%, #ad1457 100%) !important;
}

/* PNG icon (gambar) */
.widget-icon.shortage-icon {
    background-image: url('/img/stats/shortage.png');
    background-size: 60px 60px;
    background-repeat: no-repeat;
    background-position: center;
}

/* FontAwesome fallback */
.widget-icon i {
    display: block; /* Default: tampilkan FontAwesome */
}

.widget-icon.shortage-icon i {
    display: none; /* Sembunyikan FontAwesome jika PNG ada */
}
```

## 📱 **Responsive Sizes:**

- **Desktop**: 60x60px
- **Tablet**: 50x50px  
- **Mobile**: 45x45px
- **Small Mobile**: 40x40px

## ✅ **Expected Result:**

1. **Tanpa PNG**: FontAwesome icons + background gradient
2. **Dengan PNG**: PNG icons + background gradient
3. **Shortage**: Background pink/merah + icon (FontAwesome atau PNG)
4. **Responsive**: Icons ter-scale sesuai ukuran layar



