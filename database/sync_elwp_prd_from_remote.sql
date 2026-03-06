-- =============================================================
-- SYNC ELWP_PRD: Remote (10.14.149.34) → Lokal
-- Jalankan di SQL Server LOKAL (LAPTOP-T952LGJ6\IKHSANSERVER)
-- =============================================================
-- LANGKAH 1: Buat Linked Server ke remote (jalankan sekali saja)
-- Kalau sudah ada, skip bagian ini.
-- =============================================================

IF NOT EXISTS (SELECT 1 FROM sys.servers WHERE name = N'REMOTE_VELASTO')
BEGIN
    EXEC sp_addlinkedserver
        @server     = N'REMOTE_VELASTO',
        @srvproduct = N'',
        @provider   = N'SQLNCLI',
        @datasrc    = N'10.14.149.34';

    EXEC sp_addlinkedsrvlogin
        @rmtsrvname  = N'REMOTE_VELASTO',
        @useself     = N'FALSE',
        @locallogin  = NULL,
        @rmtuser     = N'usrvelasto',
        @rmtpassword = N'H1s@na2025!!';

    PRINT 'Linked Server REMOTE_VELASTO berhasil dibuat.';
END
ELSE
    PRINT 'Linked Server REMOTE_VELASTO sudah ada, lanjut sync.';
GO

-- =============================================================
-- LANGKAH 2: Tambah kolom OriginalMesinId jika belum ada di lokal
-- =============================================================
USE ELWP_PRD;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'produksi'
      AND TABLE_NAME   = 'tb_elwp_produksi_plannings'
      AND COLUMN_NAME  = 'OriginalMesinId'
)
BEGIN
    ALTER TABLE produksi.tb_elwp_produksi_plannings
        ADD OriginalMesinId INT NULL;
    PRINT 'Kolom OriginalMesinId ditambahkan.';
END
GO

-- =============================================================
-- LANGKAH 3: SYNC tb_elwp_produksi_mesins
-- Tambah baris baru, update baris yang berubah
-- =============================================================
PRINT '>> Sync tb_elwp_produksi_mesins ...';

SET IDENTITY_INSERT produksi.tb_elwp_produksi_mesins ON;

MERGE produksi.tb_elwp_produksi_mesins AS tgt
USING (
    SELECT Id, KodeMesin, NamaMesin, PlantId, AreaId, Keterangan, IsActive, CreatedAt, UpdatedAt
    FROM REMOTE_VELASTO.ELWP_PRD.produksi.tb_elwp_produksi_mesins
) AS src ON tgt.Id = src.Id
WHEN MATCHED AND (
    tgt.KodeMesin  <> src.KodeMesin  OR
    tgt.NamaMesin  <> src.NamaMesin  OR
    tgt.PlantId    <> src.PlantId    OR
    tgt.AreaId     <> src.AreaId     OR
    ISNULL(tgt.Keterangan,'') <> ISNULL(src.Keterangan,'') OR
    tgt.IsActive   <> src.IsActive
) THEN UPDATE SET
    KodeMesin  = src.KodeMesin,
    NamaMesin  = src.NamaMesin,
    PlantId    = src.PlantId,
    AreaId     = src.AreaId,
    Keterangan = src.Keterangan,
    IsActive   = src.IsActive,
    UpdatedAt  = src.UpdatedAt
WHEN NOT MATCHED BY TARGET THEN INSERT
    (Id, KodeMesin, NamaMesin, PlantId, AreaId, Keterangan, IsActive, CreatedAt, UpdatedAt)
VALUES
    (src.Id, src.KodeMesin, src.NamaMesin, src.PlantId, src.AreaId,
     src.Keterangan, src.IsActive, src.CreatedAt, src.UpdatedAt);

PRINT CONCAT('   Mesins synced: ', @@ROWCOUNT, ' rows affected');

SET IDENTITY_INSERT produksi.tb_elwp_produksi_mesins OFF;
GO

-- =============================================================
-- LANGKAH 4: SYNC tb_elwp_produksi_plannings
-- Ambil data 7 hari ke belakang + ke depan (1 minggu window)
-- =============================================================
PRINT '>> Sync tb_elwp_produksi_plannings ...';

SET IDENTITY_INSERT produksi.tb_elwp_produksi_plannings ON;

MERGE produksi.tb_elwp_produksi_plannings AS tgt
USING (
    SELECT Id, PlantId, AreaId, MesinId, TanggalPlanning, PnSap, KodeItem,
           PartName, QtyPlanning, Shift, LoadingTimeHours,
           CreatedAt, UpdatedAt, CreatedBy, PlanningDate, OriginalMesinId
    FROM REMOTE_VELASTO.ELWP_PRD.produksi.tb_elwp_produksi_plannings
    WHERE PlanningDate >= CAST(GETDATE()-7 AS DATE)
      AND PlanningDate <= CAST(GETDATE()+7 AS DATE)
) AS src ON tgt.Id = src.Id
WHEN MATCHED AND (
    tgt.QtyPlanning     <> src.QtyPlanning     OR
    tgt.Shift           <> src.Shift           OR
    ISNULL(tgt.PartName,'') <> ISNULL(src.PartName,'') OR
    tgt.KodeItem        <> src.KodeItem        OR
    tgt.MesinId         <> src.MesinId         OR
    tgt.LoadingTimeHours <> src.LoadingTimeHours
) THEN UPDATE SET
    PlantId          = src.PlantId,
    AreaId           = src.AreaId,
    MesinId          = src.MesinId,
    TanggalPlanning  = src.TanggalPlanning,
    PnSap            = src.PnSap,
    KodeItem         = src.KodeItem,
    PartName         = src.PartName,
    QtyPlanning      = src.QtyPlanning,
    Shift            = src.Shift,
    LoadingTimeHours = src.LoadingTimeHours,
    UpdatedAt        = src.UpdatedAt,
    PlanningDate     = src.PlanningDate,
    OriginalMesinId  = src.OriginalMesinId
WHEN NOT MATCHED BY TARGET THEN INSERT
    (Id, PlantId, AreaId, MesinId, TanggalPlanning, PnSap, KodeItem,
     PartName, QtyPlanning, Shift, LoadingTimeHours,
     CreatedAt, UpdatedAt, CreatedBy, PlanningDate, OriginalMesinId)
VALUES
    (src.Id, src.PlantId, src.AreaId, src.MesinId, src.TanggalPlanning,
     src.PnSap, src.KodeItem, src.PartName, src.QtyPlanning, src.Shift,
     src.LoadingTimeHours, src.CreatedAt, src.UpdatedAt, src.CreatedBy,
     src.PlanningDate, src.OriginalMesinId);

PRINT CONCAT('   Plannings synced: ', @@ROWCOUNT, ' rows affected');

SET IDENTITY_INSERT produksi.tb_elwp_produksi_plannings OFF;
GO

-- =============================================================
-- VERIFIKASI
-- =============================================================
SELECT 'tb_elwp_produksi_mesins' AS [Table], COUNT(*) AS [Rows] FROM produksi.tb_elwp_produksi_mesins
UNION ALL
SELECT 'tb_elwp_produksi_plannings (hari ini)', COUNT(*) FROM produksi.tb_elwp_produksi_plannings
WHERE PlanningDate = CAST(GETDATE() AS DATE);
GO
