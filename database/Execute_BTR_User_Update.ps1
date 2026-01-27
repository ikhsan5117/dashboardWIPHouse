# =============================================
# PowerShell Script to Update BTR User
# =============================================

$Server = "10.14.149.34"
$Username = "usrvelasto"
$Password = "H1s@na2025!!"
$SqlFile = "e:\dashboardWIPHouse\Database\Update_BTR_User.sql"

Write-Host "`n[INFO] Updating BTR User..." -ForegroundColor Yellow

$connectionString = "Server=$Server;User Id=$Username;Password=$Password;TrustServerCertificate=True;"

try {
    $sqlContent = Get-Content -Path $SqlFile -Raw
    $sqlBatches = $sqlContent -split '\r?\nGO\r?\n'
    
    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = $connectionString
    $connection.Open()
    
    foreach ($batch in $sqlBatches) {
        $batch = $batch.Trim()
        if ([string]::IsNullOrWhiteSpace($batch)) { continue }
        
        $command = $connection.CreateCommand()
        $command.CommandText = $batch
        $reader = $command.ExecuteReader()
        
        do {
            while ($reader.Read()) {
                for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                    Write-Host "$($reader.GetName($i)): $($reader.GetValue($i))" -ForegroundColor Cyan
                }
            }
        } while ($reader.NextResult())
        
        $reader.Close()
    }
    
    $connection.Close()
    
    Write-Host "`n[SUCCESS] BTR User updated successfully!" -ForegroundColor Green
    Write-Host "Username: adminBTR" -ForegroundColor White
    Write-Host "Password: BTR123" -ForegroundColor White
    
}
catch {
    Write-Host "`n[ERROR] $($_.Exception.Message)" -ForegroundColor Red
}
