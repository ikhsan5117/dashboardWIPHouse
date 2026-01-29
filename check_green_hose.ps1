$connString = "Server=10.14.149.34;Database=DB_SUPPLY_HOSE;User Id=usrvelasto;Password=H1s@na2025!!;TrustServerCertificate=True;"
try {
    Add-Type -AssemblyName "System.Data"
    $conn = New-Object System.Data.SqlClient.SqlConnection($connString)
    $conn.Open()
    $cmd = $conn.CreateCommand()
    
    # Get items where status is 'expired' in the view
    $cmd.CommandText = "
        SELECT DISTINCT item_code, last_updated, status_expired
        FROM vw_stock_summary
        WHERE status_expired = 'expired'"
    
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $table = New-Object System.Data.DataTable
    $null = $adapter.Fill($table)
    
    $table | ConvertTo-Json
    $conn.Close()
}
catch {
    Write-Error "Error: $($_.Exception.Message)"
}
