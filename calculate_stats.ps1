$connString = "Server=10.14.149.34;Database=DB_SUPPLY_HOSE;User Id=usrvelasto;Password=H1s@na2025!!;TrustServerCertificate=True;"
try {
    Add-Type -AssemblyName "System.Data"
    $conn = New-Object System.Data.SqlClient.SqlConnection($connString)
    $conn.Open()
    
    # 1. Load All Items
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT item_code, standard_exp, standard_min, standard_max FROM Items"
    $itemsTable = New-Object System.Data.DataTable
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $null = $adapter.Fill($itemsTable)
    
    # 2. Load Stock Summary
    $cmd.CommandText = "SELECT item_code, current_box_stock, last_updated FROM vw_stock_summary"
    $stockTable = New-Object System.Data.DataTable
    $adapter.SelectCommand = $cmd
    $null = $adapter.Fill($stockTable)
    
    # 3. Process Logic
    $stockAggregated = @{}
    foreach ($row in $stockTable.Rows) {
        $ic = $row.item_code.Trim()
        if (-not $stockAggregated.ContainsKey($ic)) {
            $stockAggregated[$ic] = @{
                TotalStock  = 0;
                LastUpdated = [DateTime]::MinValue;
            }
        }
        $stockAggregated[$ic].TotalStock += [int]($row.current_box_stock)
        
        $luStr = $row.last_updated
        if (-not [string]::IsNullOrEmpty($luStr)) {
            $formats = @("M/d/yy H:mm", "M/d/yyyy H:mm", "yyyy-MM-dd HH:mm:ss", "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "MM/dd/yyyy HH:mm:ss", "MM/dd/yyyy HH:mm", "d/M/yyyy H:mm", "d/M/yy H:mm")
            $parsedDate = [DateTime]::MinValue
            foreach ($f in $formats) {
                if ([DateTime]::TryParseExact($luStr, $f, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::None, [ref]$parsedDate)) {
                    if ($parsedDate -gt $stockAggregated[$ic].LastUpdated) {
                        $stockAggregated[$ic].LastUpdated = $parsedDate
                    }
                    break
                }
            }
        }
    }
    
    $stats = @{
        Expired     = 0;
        NearExpired = 0;
        Shortage    = 0;
        OverStock   = 0;
        Normal      = 0;
        TotalItems  = $itemsTable.Rows.Count;
    }
    
    $now = Get-Date

    foreach ($item in $itemsTable.Rows) {
        $ic = $item.item_code.Trim()
        $stdExp = if ($item.standard_exp -is [System.DBNull]) { $null } else { [int]$item.standard_exp }
        $stdMin = if ($item.standard_min -is [System.DBNull]) { $null } else { [int]$item.standard_min }
        $stdMax = if ($item.standard_max -is [System.DBNull]) { $null } else { [int]$item.standard_max }
        
        $totalStock = 0
        $lastUpdated = [DateTime]::MinValue
        
        if ($stockAggregated.ContainsKey($ic)) {
            $totalStock = $stockAggregated[$ic].TotalStock
            $lastUpdated = $stockAggregated[$ic].LastUpdated
        }
        
        $isExp = $false
        $isNearExp = $false
        $isShort = $false
        $isOver = $false
        
        # 1. Check Expiry
        if ($stdExp -ne $null -and $lastUpdated -ne [DateTime]::MinValue) {
            $daysSinceUpdate = ($now - $lastUpdated).TotalDays
            $daysUntilExpiry = $stdExp - $daysSinceUpdate
            
            if ($daysUntilExpiry -lt 0) {
                $isExp = $true
            }
            else {
                $threshold = if ($stdExp -le 3) { 1 } else { 3 }
                if ($daysUntilExpiry -le $threshold) {
                    $isNearExp = $true
                }
            }
        }
        
        # 2. Check Stock Levels (Priority order same as C# code's logic usually)
        # Note: In HomeController, status prioritization determines the single string display status
        
        if ($isExp) {
            $stats.Expired++
        }
        elseif ($isNearExp) {
            $stats.NearExpired++
        }
        else {
            # Check for stock levels only if not expired/near expired
            if ($stdMin -ne $null -and $totalStock -le $stdMin) {
                $stats.Shortage++
            }
            elseif ($stdMax -ne $null -and $totalStock -gt $stdMax) {
                $stats.OverStock++
            }
            else {
                $stats.Normal++
            }
        }
    }
    
    $stats | ConvertTo-Json
    $conn.Close()
}
catch {
    Write-Error "Error: $($_.Exception.Message)"
}
