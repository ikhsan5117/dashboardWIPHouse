-- =============================================================
-- SYNC DB_SUPPLY_HOSE: Remote (10.14.149.34) → Lokal
-- Jalankan di SQL Server LOKAL (LAPTOP-T952LGJ6\IKHSANSERVER)
-- Prasyarat: Linked Server REMOTE_VELASTO sudah ada
--            (jalankan sync_elwp_prd_from_remote.sql lebih dulu)
-- =============================================================

USE DB_SUPPLY_HOSE;
GO

-- =============================================================
-- LANGKAH 1: Tambah kolom yang ada di remote tapi belum di lokal
-- =============================================================

-- storage_log_aw: tambah transaction_type
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='storage_log_aw' AND COLUMN_NAME='transaction_type'
)
BEGIN
    ALTER TABLE storage_log_aw ADD transaction_type VARCHAR(50) NULL;
    PRINT 'Kolom transaction_type ditambahkan ke storage_log_aw';
END

-- supply_log_aw: tambah to_process
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='supply_log_aw' AND COLUMN_NAME='to_process'
)
BEGIN
    ALTER TABLE supply_log_aw ADD to_process VARCHAR(50) NULL;
    PRINT 'Kolom to_process ditambahkan ke supply_log_aw';
END

-- raks_aw: pastikan kolom location/kode_rak ada
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME='raks_aw' AND COLUMN_NAME='kode_rak'
)
BEGIN
    ALTER TABLE raks_aw ADD kode_rak NVARCHAR(100) NULL;
    PRINT 'Kolom kode_rak ditambahkan ke raks_aw';
END
GO

-- =============================================================
-- LANGKAH 2: SYNC items_aw (master data item)
-- Key: item_code
-- =============================================================
PRINT '>> Sync items_aw ...';

MERGE Items_aw AS tgt
USING (
    SELECT item_code, mesin, qty_per_box, standard_exp, standard_min, standard_max
    FROM REMOTE_VELASTO.DB_SUPPLY_HOSE.dbo.items_aw
) AS src ON tgt.item_code = src.item_code
WHEN MATCHED AND (
    ISNULL(tgt.mesin,'')       <> ISNULL(src.mesin,'')       OR
    ISNULL(tgt.qty_per_box,0)  <> ISNULL(src.qty_per_box,0)  OR
    ISNULL(tgt.standard_exp,0) <> ISNULL(src.standard_exp,0)
) THEN UPDATE SET
    mesin        = src.mesin,
    qty_per_box  = src.qty_per_box,
    standard_exp = src.standard_exp,
    standard_min = src.standard_min,
    standard_max = src.standard_max
WHEN NOT MATCHED BY TARGET THEN INSERT
    (item_code, mesin, qty_per_box, standard_exp, standard_min, standard_max)
VALUES
    (src.item_code, src.mesin, src.qty_per_box,
     src.standard_exp, src.standard_min, src.standard_max);

PRINT CONCAT('   items_aw synced: ', @@ROWCOUNT, ' rows affected');
GO

-- =============================================================
-- LANGKAH 3: SYNC raks_aw (lokasi rak)
-- Key: full_qr
-- Remote: location → Lokal: kode_rak
-- =============================================================
PRINT '>> Sync raks_aw ...';

MERGE raks_aw AS tgt
USING (
    SELECT full_qr, location AS kode_rak, item_code
    FROM REMOTE_VELASTO.DB_SUPPLY_HOSE.dbo.raks_aw
) AS src ON tgt.full_qr = src.full_qr
WHEN MATCHED AND (
    ISNULL(tgt.kode_rak,'')  <> ISNULL(src.kode_rak,'') OR
    ISNULL(tgt.item_code,'') <> ISNULL(src.item_code,'')
) THEN UPDATE SET
    kode_rak  = src.kode_rak,
    item_code = src.item_code
WHEN NOT MATCHED BY TARGET THEN INSERT
    (full_qr, kode_rak, item_code)
VALUES
    (src.full_qr, src.kode_rak, src.item_code);

PRINT CONCAT('   raks_aw synced: ', @@ROWCOUNT, ' rows affected');
GO

-- =============================================================
-- LANGKAH 4: SYNC storage_log_aw (IN transactions)
-- Key: log_id (IDENTITY)
-- Ambil semua data (hanya 14 baris di remote saat ini)
-- =============================================================
PRINT '>> Sync storage_log_aw ...';

SET IDENTITY_INSERT storage_log_aw ON;

MERGE storage_log_aw AS tgt
USING (
    SELECT log_id, item_code, full_qr, production_date,
           box_count, qty_pcs, stored_at, tanggal, transaction_type
    FROM REMOTE_VELASTO.DB_SUPPLY_HOSE.dbo.storage_log_aw
) AS src ON tgt.log_id = src.log_id
WHEN MATCHED AND (
    ISNULL(tgt.qty_pcs,0)   <> ISNULL(src.qty_pcs,0)   OR
    ISNULL(tgt.box_count,0) <> ISNULL(src.box_count,0)
) THEN UPDATE SET
    item_code        = src.item_code,
    full_qr          = src.full_qr,
    production_date  = src.production_date,
    box_count        = src.box_count,
    qty_pcs          = src.qty_pcs,
    stored_at        = src.stored_at,
    tanggal          = src.tanggal,
    transaction_type = src.transaction_type
WHEN NOT MATCHED BY TARGET THEN INSERT
    (log_id, item_code, full_qr, production_date,
     box_count, qty_pcs, stored_at, tanggal, transaction_type)
VALUES
    (src.log_id, src.item_code, src.full_qr, src.production_date,
     src.box_count, src.qty_pcs, src.stored_at, src.tanggal, src.transaction_type);

SET IDENTITY_INSERT storage_log_aw OFF;

PRINT CONCAT('   storage_log_aw synced: ', @@ROWCOUNT, ' rows affected');
GO

-- =============================================================
-- LANGKAH 5: SYNC supply_log_aw (OUT transactions)
-- Key: log_id (IDENTITY)
-- =============================================================
PRINT '>> Sync supply_log_aw ...';

SET IDENTITY_INSERT supply_log_aw ON;

MERGE supply_log_aw AS tgt
USING (
    SELECT log_id, item_code, full_qr, box_count, qty_pcs,
           supplied_at, to_process, tanggal, plant_id
    FROM REMOTE_VELASTO.DB_SUPPLY_HOSE.dbo.supply_log_aw
) AS src ON tgt.log_id = src.log_id
WHEN MATCHED AND (
    ISNULL(tgt.qty_pcs,0)   <> ISNULL(src.qty_pcs,0)   OR
    ISNULL(tgt.box_count,0) <> ISNULL(src.box_count,0)
) THEN UPDATE SET
    item_code   = src.item_code,
    full_qr     = src.full_qr,
    box_count   = src.box_count,
    qty_pcs     = src.qty_pcs,
    supplied_at = src.supplied_at,
    to_process  = src.to_process,
    tanggal     = src.tanggal,
    plant_id    = src.plant_id
WHEN NOT MATCHED BY TARGET THEN INSERT
    (log_id, item_code, full_qr, box_count, qty_pcs,
     supplied_at, to_process, tanggal, plant_id)
VALUES
    (src.log_id, src.item_code, src.full_qr, src.box_count, src.qty_pcs,
     src.supplied_at, src.to_process, src.tanggal, src.plant_id);

SET IDENTITY_INSERT supply_log_aw OFF;

PRINT CONCAT('   supply_log_aw synced: ', @@ROWCOUNT, ' rows affected');
GO

-- =============================================================
-- VERIFIKASI
-- =============================================================
SELECT 'Items_aw'       AS [Table], COUNT(*) AS [Total Rows] FROM Items_aw       UNION ALL
SELECT 'raks_aw',                   COUNT(*)                 FROM raks_aw         UNION ALL
SELECT 'storage_log_aw',            COUNT(*)                 FROM storage_log_aw  UNION ALL
SELECT 'supply_log_aw',             COUNT(*)                 FROM supply_log_aw;
GO
