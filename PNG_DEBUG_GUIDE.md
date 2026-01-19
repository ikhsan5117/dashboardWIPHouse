# 🔧 PNG Icons Debug Guide

## 🚨 **Masalah: PNG Icons Tidak Terbaca**

## ✅ **Yang Sudah Diperbaiki:**

### 1. **CSS dengan !important**
```css
.widget-icon.expired-icon {
    background-image: url('/img/stats/expired.png') !important;
    background-size: 60px 60px !important;
    background-repeat: no-repeat !important;
    background-position: center !important;
}
```

### 2. **Debug Border**
- Menambahkan border putih transparan untuk melihat apakah PNG area ter-render
- Jika border muncul = CSS class bekerja
- Jika border tidak muncul = ada masalah dengan CSS class

### 3. **FontAwesome Fallback**
- FontAwesome icons muncul sebagai default
- Disembunyikan hanya jika PNG berhasil dimuat

## 🧪 **Testing Steps:**

### **Step 1: Test PNG Access**
1. Buka: `http://localhost:port/test-simple.html`
2. Check apakah semua 5 PNG icons muncul
3. Check console untuk error messages

### **Step 2: Test Dashboard**
1. Buka dashboard utama
2. Check apakah ada border putih di stats cards
3. Check apakah FontAwesome icons muncul
4. Check browser console untuk 404 errors

### **Step 3: Test Direct PNG Access**
1. Buka langsung: `http://localhost:port/img/stats/expired.png`
2. Pastikan PNG file dapat diakses
3. Test semua 5 PNG files

## 🐛 **Troubleshooting:**

### **Jika PNG Icons Tidak Muncul:**

#### **A. Check File Path:**
```
wwwroot/img/stats/
├── expired.png
├── near_expired.png
├── shortage.png
├── over_stock.png
└── normal.png
```

#### **B. Check Browser Console:**
- Buka F12 → Console
- Look for 404 errors
- Look for CORS errors
- Look for network errors

#### **C. Check CSS Class:**
- Pastikan HTML memiliki class yang benar:
  - `expired-icon`
  - `near-expired-icon`
  - `shortage-icon`
  - `over-stock-icon`
  - `normal-icon`

#### **D. Check CSS Specificity:**
- Pastikan CSS dengan `!important` tidak di-override
- Check apakah ada CSS lain yang menimpa

### **Jika FontAwesome Icons Tidak Muncul:**
1. Check apakah FontAwesome CSS ter-load
2. Check browser console untuk error
3. Pastikan HTML structure benar

### **Jika Background Gradient Hilang:**
1. Check CSS specificity
2. Pastikan class `themed-background-*` ada
3. Hard refresh browser (Ctrl+F5)

## 🎯 **Expected Result:**

### **Skenario 1: PNG Icons Berhasil**
- ✅ PNG icons muncul di stats cards
- ✅ FontAwesome icons tidak muncul
- ✅ Background gradient sesuai warna
- ✅ Border putih terlihat (debug)

### **Skenario 2: PNG Icons Gagal**
- ✅ FontAwesome icons muncul
- ✅ Background gradient sesuai warna
- ✅ Border putih terlihat (debug)
- ❌ PNG icons tidak muncul

## 🔍 **Debug Information:**

### **CSS Classes yang Digunakan:**
```html
<div class="widget-icon themed-background-danger animation-fadeIn expired-icon">
    <i class="fa fa-times-circle"></i>
</div>
```

### **CSS yang Diterapkan:**
```css
.widget-icon.expired-icon {
    background-image: url('/img/stats/expired.png') !important;
    background-size: 60px 60px !important;
    background-repeat: no-repeat !important;
    background-position: center !important;
    border: 2px solid rgba(255, 255, 255, 0.3) !important;
}
```

### **FontAwesome Fallback:**
```css
.widget-icon.expired-icon i {
    display: none !important;
}
```

## 📋 **Checklist:**

- [ ] File PNG ada di `wwwroot/img/stats/`
- [ ] File PNG dapat diakses langsung via browser
- [ ] CSS class benar di HTML
- [ ] CSS dengan `!important` tidak di-override
- [ ] Browser console tidak ada error 404
- [ ] FontAwesome icons muncul sebagai fallback
- [ ] Background gradient muncul
- [ ] Border putih terlihat (debug)

## 🚀 **Next Steps:**

1. **Test dengan `test-simple.html`** - Pastikan PNG dapat diakses
2. **Check browser console** - Look for errors
3. **Check CSS class** - Pastikan HTML memiliki class yang benar
4. **Hard refresh** - Ctrl+F5 untuk clear cache
5. **Test di browser lain** - Chrome, Firefox, Edge

## 💡 **Tips:**

- Gunakan browser developer tools (F12)
- Check Network tab untuk melihat apakah PNG files di-load
- Check Elements tab untuk melihat CSS yang diterapkan
- Test dengan file PNG yang berbeda untuk memastikan path benar



