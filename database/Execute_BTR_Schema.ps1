# =============================================
# PowerShell Script to Execute SQL Schema
# Database: DB_SUPPLY_BTR
# =============================================

param(
    [string]$Server = "10.14.149.34",
    [string]$Username = "usrvelasto",
    [string]$Password = "H1s@na2025!!",
    [string]$SqlFile = "e:\dashboardWIPHouse\Database\DB_SUPPLY_BTR_Schema.sql"
)

Write-Host "==============================================`n" -ForegroundColor Cyan
Write-Host "  Database BTR Setup Script" -ForegroundColor Cyan
Write-Host "==============================================`n" -ForegroundColor Cyan

# Connection String
$connectionString = "Server=$Server;User Id=$Username;Password=$Password;TrustServerCertificate=True;Connection Timeout=30;"

Write-Host "[INFO] Connecting to SQL Server: $Server" -ForegroundColor Yellow
Write-Host "[INFO] Reading SQL file: $SqlFile`n" -ForegroundColor Yellow

try {
    # Read SQL file
    if (-not (Test-Path $SqlFile)) {
        throw "SQL file not found: $SqlFile"
    }
    
    $sqlContent = Get-Content -Path $SqlFile -Raw
    
    # Split by GO statements
    $sqlBatches = $sqlContent -split '\r?\nGO\r?\n'
    
    # Create SQL Connection
    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = $connectionString
    $connection.Open()
    
    Write-Host "[SUCCESS] Connected to SQL Server!`n" -ForegroundColor Green
    
    $batchNumber = 0
    $successCount = 0
    $errorCount = 0
    
    foreach ($batch in $sqlBatches) {
        $batch = $batch.Trim()
        
        if ([string]::IsNullOrWhiteSpace($batch)) {
            continue
        }
        
        $batchNumber++
        
        try {
            $command = $connection.CreateCommand()
            $command.CommandText = $batch
            $command.CommandTimeout = 60
            
            # Execute and capture messages
            $reader = $command.ExecuteReader()
            
            # Read all result sets
            do {
                while ($reader.Read()) {
                    # Process results if any
                }
            } while ($reader.NextResult())
            
            $reader.Close()
            
            Write-Host "[BATCH $batchNumber] Executed successfully" -ForegroundColor Green
            $successCount++
            
        } catch {
            Write-Host "[BATCH $batchNumber] ERROR: $($_.Exception.Message)" -ForegroundColor Red
            $errorCount++
        }
    }
    
    $connection.Close()
    
    Write-Host "`n==============================================`n" -ForegroundColor Cyan
    Write-Host "  Execution Summary" -ForegroundColor Cyan
    Write-Host "==============================================`n" -ForegroundColor Cyan
    Write-Host "Total Batches: $batchNumber" -ForegroundColor White
    Write-Host "Successful: $successCount" -ForegroundColor Green
    Write-Host "Errors: $errorCount" -ForegroundColor $(if ($errorCount -gt 0) { "Red" } else { "Green" })
    Write-Host "`n==============================================`n" -ForegroundColor Cyan
    
    if ($errorCount -eq 0) {
        Write-Host "[SUCCESS] Database DB_SUPPLY_BTR created successfully!" -ForegroundColor Green
        Write-Host "[INFO] You can now add the connection string to appsettings.json" -ForegroundColor Yellow
        Write-Host "`nSuggested connection string:" -ForegroundColor Cyan
        Write-Host '"BTRDb": "Server=10.14.149.34;Database=DB_SUPPLY_BTR;User Id=usrvelasto;Password=H1s@na2025!!;TrustServerCertificate=True;"' -ForegroundColor White
    } else {
        Write-Host "[WARNING] Some errors occurred during execution. Please review the output above." -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "`n[ERROR] Failed to execute SQL script:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host "`nStack Trace:" -ForegroundColor Yellow
    Write-Host $_.Exception.StackTrace -ForegroundColor Gray
    exit 1
}

Write-Host "`nPress any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
