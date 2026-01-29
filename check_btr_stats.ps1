# Script to check BTR dashboard statistics by replicating controller logic
# Database: DB_SUPPLY_BTR
# Server: 10.14.149.34

$connectionString = "Server=10.14.149.34;Database=DB_SUPPLY_BTR;User Id=usrvelasto;Password=H1s@na2025!!;TrustServerCertificate=True;"

try {
    # Load SQL Server assembly
    Add-Type -AssemblyName "System.Data"
    
    # Create connection
    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = $connectionString
    $connection.Open()
    
    Write-Host "Connected to DB_SUPPLY_BTR successfully!" -ForegroundColor Green
    Write-Host ""
    
    # Query 1: Get all items
    $itemsQuery = "SELECT item_code, standard_min, standard_max FROM items"
    $itemsCmd = New-Object System.Data.SqlClient.SqlCommand($itemsQuery, $connection)
    $itemsAdapter = New-Object System.Data.SqlClient.SqlDataAdapter($itemsCmd)
    $itemsTable = New-Object System.Data.DataTable
    $itemsAdapter.Fill($itemsTable) | Out-Null
    
    Write-Host "Total Items in 'items' table: $($itemsTable.Rows.Count)" -ForegroundColor Cyan
    
    # Query 2: Get all stock summary
    $stockQuery = "SELECT item_code, current_box_stock, status_expired FROM vw_stok_summary"
    $stockCmd = New-Object System.Data.SqlClient.SqlCommand($stockQuery, $connection)
    $stockAdapter = New-Object System.Data.SqlClient.SqlDataAdapter($stockCmd)
    $stockTable = New-Object System.Data.DataTable
    $stockAdapter.Fill($stockTable) | Out-Null
    
    Write-Host "Total Records in 'vw_stok_summary' view: $($stockTable.Rows.Count)" -ForegroundColor Cyan
    Write-Host ""
    
    # Calculate dashboard stats
    $totalItems = $itemsTable.Rows.Count
    $totalStock = ($stockTable | Measure-Object -Property current_box_stock -Sum).Sum
    
    # Count expired and near expired
    $expiredCount = ($stockTable | Where-Object { $_.status_expired -eq "Expired" }).Count
    $nearExpiredCount = ($stockTable | Where-Object { $_.status_expired -eq "Near Exp" }).Count
    
    # Count below min items
    $belowMinCount = 0
    foreach ($item in $itemsTable.Rows) {
        $itemCode = $item.item_code
        $standardMin = $item.standard_min
        
        if ($standardMin -eq [DBNull]::Value) { continue }
        
        # Sum stock for this item
        $currentStock = ($stockTable | Where-Object { $_.item_code -eq $itemCode } | Measure-Object -Property current_box_stock -Sum).Sum
        
        if ($currentStock -lt $standardMin) {
            $belowMinCount++
        }
    }
    
    # Calculate normal items
    $normalCount = $totalItems - $expiredCount - $nearExpiredCount
    
    # Display results
    Write-Host "=== BTR DASHBOARD STATISTICS ===" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Total Items:        $totalItems" -ForegroundColor White
    Write-Host "Total Stock:        $totalStock boxes" -ForegroundColor White
    Write-Host "Expired Items:      $expiredCount" -ForegroundColor Red
    Write-Host "Near Expired Items: $nearExpiredCount" -ForegroundColor DarkYellow
    Write-Host "Below Min Items:    $belowMinCount" -ForegroundColor Magenta
    Write-Host "Normal Items:       $normalCount" -ForegroundColor Green
    Write-Host ""
    
    # Close connection
    $connection.Close()
    
    Write-Host "Query completed successfully!" -ForegroundColor Green
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    if ($connection.State -eq 'Open') {
        $connection.Close()
    }
}
