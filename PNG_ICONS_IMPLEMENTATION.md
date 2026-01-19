# 🎨 Implementasi PNG Icons untuk Dashboard Stats

## 📁 Lokasi File PNG

Simpan file PNG di folder: `wwwroot/img/stats/`

```
wwwroot/
└── img/
    └── stats/
        ├── expired.png          # Icon untuk "Already Expired"
        ├── near-expired.png     # Icon untuk "Near Expired" 
        ├── shortage.png         # Icon untuk "Shortage"
        ├── over-stock.png       # Icon untuk "Over Stock"
        └── normal.png           # Icon untuk "Normal"
```

## 🎯 Spesifikasi File PNG

### Ukuran & Format:
- **Format**: PNG dengan background transparan
- **Ukuran Optimal**: 60x60px atau 120x120px (akan di-scale otomatis)
- **Style**: Flat design atau outline style
- **Warna**: Putih atau warna terang (akan ditampilkan di background berwarna)

### Rekomendasi Icon:
1. **expired.png** - Tanda expired (jam, kalender, tanda X)
2. **near-expired.png** - Tanda peringatan (segitiga warning, jam)
3. **shortage.png** - Tanda kekurangan (kotak kosong, baterai rendah)
4. **over-stock.png** - Tanda kelebihan (panah naik, kotak penuh)
5. **normal.png** - Tanda normal (centang, lingkaran hijau)

## 🔧 Implementasi yang Sudah Dilakukan

### 1. CSS Classes
```css
.widget-icon.expired-icon {
    background-image: url('/img/stats/expired.png');
}
.widget-icon.near-expired-icon {
    background-image: url('/img/stats/near-expired.png');
}
.widget-icon.shortage-icon {
    background-image: url('/img/stats/shortage.png');
}
.widget-icon.over-stock-icon {
    background-image: url('/img/stats/over-stock.png');
}
.widget-icon.normal-icon {
    background-image: url('/img/stats/normal.png');
}
```

### 2. HTML Structure
```html
<div class="widget-icon themed-background-danger animation-fadeIn expired-icon">
    <i class="fa fa-times-circle"></i> <!-- Fallback FontAwesome icon -->
</div>
```

### 3. Responsive Sizing
- **Desktop**: 60x60px
- **Tablet**: 50x50px  
- **Mobile**: 45x45px
- **Small Mobile**: 40x40px

## 🎨 Sumber Download Icons

### Rekomendasi:
1. **Flaticon**: https://www.flaticon.com/
   - Pilih style "Flat" atau "Outline"
   - Filter by "PNG" format
   - Download dengan background transparan

2. **Icons8**: https://icons8.com/
   - Pilih style "Flat" atau "Outline"
   - Download PNG format

3. **Feather Icons**: https://feathericons.com/
   - Style outline yang konsisten
   - Download SVG lalu convert ke PNG

4. **Heroicons**: https://heroicons.com/
   - Style outline modern
   - Download SVG lalu convert ke PNG

## 🔄 Fallback System

Jika file PNG tidak ditemukan, sistem akan otomatis menampilkan FontAwesome icons sebagai fallback:

```css
.widget-icon i {
    display: none; /* Sembunyikan FontAwesome secara default */
}

.widget-icon.expired-icon i,
.widget-icon.near-expired-icon i,
.widget-icon.shortage-icon i,
.widget-icon.over-stock-icon i,
.widget-icon.normal-icon i {
    display: block; /* Tampilkan FontAwesome jika PNG tidak ada */
}
```

## 📱 Testing

### 1. Test dengan PNG Icons:
- Upload file PNG ke folder `wwwroot/img/stats/`
- Refresh halaman dashboard
- Pastikan PNG icons muncul di stats cards

### 2. Test Fallback:
- Hapus salah satu file PNG
- Refresh halaman
- Pastikan FontAwesome icon muncul sebagai fallback

### 3. Test Responsive:
- Test di berbagai ukuran layar
- Pastikan icons ter-scale dengan baik

## 🚀 Langkah Implementasi

1. **Download Icons** dari Flaticon atau sumber lain
2. **Rename files** sesuai dengan nama yang diperlukan:
   - `expired.png`
   - `near-expired.png` 
   - `shortage.png`
   - `over-stock.png`
   - `normal.png`
3. **Upload files** ke folder `wwwroot/img/stats/`
4. **Test** di browser untuk memastikan icons muncul
5. **Adjust size** jika diperlukan (edit CSS `background-size`)

## 🎨 Customization

### Mengubah Ukuran Icons:
```css
.widget-simple-custom .widget-icon {
    background-size: 70px 70px; /* Ubah ukuran di sini */
}
```

### Mengubah Posisi Icons:
```css
.widget-simple-custom .widget-icon {
    background-position: center top; /* Ubah posisi di sini */
}
```

## ✅ Checklist Implementasi

- [ ] Folder `wwwroot/img/stats/` sudah dibuat
- [ ] File PNG sudah didownload dari Flaticon
- [ ] File PNG sudah di-upload ke folder yang benar
- [ ] Icons muncul di dashboard stats
- [ ] Responsive design bekerja dengan baik
- [ ] Fallback FontAwesome icons bekerja jika PNG tidak ada
- [ ] Semua 5 stats cards menampilkan icons yang sesuai

## 🐛 Troubleshooting

### Icons tidak muncul:
1. Pastikan file PNG ada di folder `wwwroot/img/stats/`
2. Pastikan nama file sesuai (case-sensitive)
3. Check browser console untuk error 404
4. Pastikan file PNG tidak corrupt

### Icons terlalu besar/kecil:
1. Edit CSS `background-size` di `.widget-simple-custom .widget-icon`
2. Atau resize file PNG asli

### Icons tidak responsive:
1. Pastikan CSS responsive sudah di-update
2. Check media queries untuk berbagai ukuran layar



