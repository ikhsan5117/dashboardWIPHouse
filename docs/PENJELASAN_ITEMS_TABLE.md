# Penjelasan Items Table vs Actual Stock

## 1. ITEMS TABLE - MASTER DATA (Katalog Produk)

**Analogi: Seperti "Peraturan/Spesifikasi" untuk setiap produk**

### Contoh Data di Items:

| item_code | mesin | qty_per_box | standard_exp | standard_min | standard_max |
|-----------|-------|-------------|--------------|--------------|--------------|
| TA1240    | M01   | 50          | 30           | 10           | 100          |
| TA1400    | M02   | 100         | 45           | 5            | 80           |
| TA1500    | M03   | 75          | 60           | 15           | 120          |

### Artinya:
- **Item TA1240:**
  - Diproduksi di mesin M01
  - 1 box = 50 pcs
  - Expired setelah 30 hari dari produksi
  - **ATURAN:** Stock minimal harus 10 box
  - **ATURAN:** Stock maksimal 100 box
  - **ATURAN:** Harus dipakai dalam 30 hari

### Sifatnya:
✅ **STATIC** - Jarang berubah  
✅ **MASTER** - Untuk semua batch/transaksi item ini  
✅ **REFERENCE** - Dipakai sebagai acuan/threshold  
❌ **BUKAN stock actual** - Tidak menyimpan berapa box yang ada sekarang  

---

## 2. STORAGE_LOG - TRANSAKSI MASUK (Actual Movement)

**Analogi: Seperti "Nota Barang Masuk" ke gudang**

### Contoh Data di storage_log:

| log_id | item_code | full_qr           | box_count | stored_at  |
|--------|-----------|-------------------|-----------|------------|
| 1      | TA1240    | TA1240-LOT001-A1  | 20        | 2025-01-01 |
| 2      | TA1240    | TA1240-LOT002-A2  | 30        | 2025-01-05 |
| 3      | TA1400    | TA1400-LOT001-B1  | 15        | 2025-01-10 |

### Artinya:
- **Transaksi 1:** Masuk 20 box TA1240 dengan QR code TA1240-LOT001-A1 pada 1 Jan
- **Transaksi 2:** Masuk 30 box TA1240 dengan QR code TA1240-LOT002-A2 pada 5 Jan
- **Transaksi 3:** Masuk 15 box TA1400 dengan QR code TA1400-LOT001-B1 pada 10 Jan

### Sifatnya:
✅ **DYNAMIC** - Setiap hari ada transaksi baru  
✅ **TRANSACTIONAL** - Record setiap movement  
✅ **HISTORICAL** - Tidak boleh diedit/dihapus (audit trail)  

---

## 3. SUPPLY_LOG - TRANSAKSI KELUAR (Actual Movement)

**Analogi: Seperti "Nota Barang Keluar" dari gudang**

### Contoh Data di supply_log:

| log_id | item_code | full_qr           | box_count | supplied_at |
|--------|-----------|-------------------|-----------|-------------|
| 1      | TA1240    | TA1240-LOT001-A1  | 15        | 2025-01-15  |
| 2      | TA1400    | TA1400-LOT001-B1  | 10        | 2025-01-20  |

### Artinya:
- **Transaksi 1:** Keluar 15 box dari lot TA1240-LOT001-A1 pada 15 Jan
- **Transaksi 2:** Keluar 10 box dari lot TA1400-LOT001-B1 pada 20 Jan

---

## 4. VW_STOCK_SUMMARY - ACTUAL CURRENT STOCK

**Analogi: Seperti "Laporan Stock Hari Ini"**

### Kemungkinan Isi View:

| item_code | full_qr           | current_box_stock | last_updated |
|-----------|-------------------|-------------------|--------------|
| TA1240    | TA1240-LOT001-A1  | 5                 | 2025-01-15   |
| TA1240    | TA1240-LOT002-A2  | 30                | 2025-01-05   |
| TA1400    | TA1400-LOT001-B1  | 5                 | 2025-01-20   |

### Perhitungan:
```
TA1240-LOT001-A1:
  IN (storage_log):  20 box (1 Jan)
  OUT (supply_log): -15 box (15 Jan)
  ────────────────────────────────
  CURRENT STOCK:      5 box ✓

TA1240-LOT002-A2:
  IN (storage_log):  30 box (5 Jan)
  OUT (supply_log):   0 box (belum ada keluar)
  ────────────────────────────────
  CURRENT STOCK:     30 box ✓

TA1400-LOT001-B1:
  IN (storage_log):  15 box (10 Jan)
  OUT (supply_log): -10 box (20 Jan)
  ────────────────────────────────
  CURRENT STOCK:      5 box ✓
```

### Sifatnya:
✅ **CALCULATED** - Hasil kalkulasi IN - OUT  
✅ **REAL-TIME** - Update otomatis saat ada transaksi  
✅ **PER QR CODE** - Bisa multiple records per item  

---

## 5. DASHBOARD LOGIC - JOIN MASTER + ACTUAL

### Step by Step:

#### **Step 1: Aggregate Stock per Item Code**
```
TA1240 Total Stock = TA1240-LOT001-A1 (5 box) + TA1240-LOT002-A2 (30 box)
                   = 35 box ← ACTUAL STOCK SEKARANG

TA1400 Total Stock = TA1400-LOT001-B1 (5 box)
                   = 5 box ← ACTUAL STOCK SEKARANG
```

#### **Step 2: Join dengan Items Master**
```
TA1240:
  - Actual Stock: 35 box (dari vw_stock_summary)
  - Standard Min: 10 box (dari Items master)
  - Standard Max: 100 box (dari Items master)
  - Last Updated: 2025-01-15 (dari vw_stock_summary)
  - Standard Exp: 30 hari (dari Items master)

TA1400:
  - Actual Stock: 5 box (dari vw_stock_summary)
  - Standard Min: 5 box (dari Items master)
  - Standard Max: 80 box (dari Items master)
  - Last Updated: 2025-01-20 (dari vw_stock_summary)
  - Standard Exp: 45 hari (dari Items master)
```

#### **Step 3: Determine Status**

**TA1240:**
```
✓ Actual Stock (35) vs Standard Min (10): 35 > 10 → OK
✓ Actual Stock (35) vs Standard Max (100): 35 < 100 → OK
✓ Days until expiry:
  - Last Update: 2025-01-15
  - Today: 2025-10-14
  - Days passed: ~272 hari
  - Standard Exp: 30 hari
  - Days until expiry: 30 - 272 = -242 hari
  → STATUS: EXPIRED ❌
```

**TA1400:**
```
✓ Actual Stock (5) vs Standard Min (5): 5 = 5 → SHORTAGE
✓ Actual Stock (5) vs Standard Max (80): 5 < 80 → OK
✓ Days until expiry:
  - Last Update: 2025-01-20
  - Today: 2025-10-14
  - Days passed: ~267 hari
  - Standard Exp: 45 hari
  - Days until expiry: 45 - 267 = -222 hari
  → STATUS: SHORTAGE (prioritas lebih tinggi daripada expired logic)
```

---

## 6. KESIMPULAN

### Items Table BUKAN Actual Stock!

| Aspek | Items Table | vw_stock_summary |
|-------|-------------|------------------|
| **Isi Data** | Spesifikasi & Threshold | Stock Actual |
| **Contoh** | "Min=10, Max=100, Exp=30 hari" | "Stock sekarang=35 box" |
| **Fungsi** | **ATURAN untuk cek status** | **DATA ACTUAL** |
| **Update** | Jarang (hanya kalau aturan berubah) | Sering (setiap ada transaksi) |
| **Analogi** | Buku manual/SOP | Laporan harian |

### Formula Dashboard:
```
STATUS = Compare(ACTUAL_STOCK, THRESHOLD_MASTER)

ACTUAL_STOCK      → dari vw_stock_summary (real data)
THRESHOLD_MASTER  → dari Items (master/rules)
```

### Contoh Real:
```
Item TA1240:
  MASTER (Items):
    - Min: 10 box     ← ATURAN
    - Max: 100 box    ← ATURAN
    - Exp: 30 hari    ← ATURAN

  ACTUAL (vw_stock_summary):
    - Stock: 35 box   ← DATA REAL
    - Last Update: 15 Jan ← DATA REAL

  COMPARISON:
    35 box vs 10 box (min) → OK, not shortage
    35 box vs 100 box (max) → OK, not overstock
    BUT: Sudah 272 hari sejak last update vs 30 hari (exp)
    → EXPIRED! ❌
```

---

## 🎯 RINGKASAN

**Items = Peraturan/SOP untuk setiap produk**
- Tidak menyimpan stock actual
- Menyimpan threshold/parameter untuk determine status
- Static, jarang berubah

**vw_stock_summary = Laporan Stock Real-time**
- Menyimpan stock actual (hasil kalkulasi IN - OUT)
- Dynamic, update terus
- Ini yang actual stock-nya

**Dashboard = Pembanding**
- Ambil stock actual dari vw_stock_summary
- Ambil aturan dari Items
- Compare → tentukan status (Normal/Shortage/Expired/dll)



