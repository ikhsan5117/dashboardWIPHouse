# ============================================
# Fix dotnet PATH - Dashboard WIP House
# ============================================
# Script ini akan menambahkan dotnet ke PATH
# agar bisa diakses dari mana saja
# ============================================

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Fix dotnet PATH" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "⚠️  Untuk permanent fix, script ini perlu Administrator" -ForegroundColor Yellow
    Write-Host "   Tapi kita bisa fix untuk session ini saja..." -ForegroundColor Yellow
    Write-Host ""
}

# dotnet path
$dotnetPath = "C:\Program Files\dotnet"

# Check if dotnet exists
if (Test-Path "$dotnetPath\dotnet.exe") {
    Write-Host "✅ dotnet found: $dotnetPath\dotnet.exe" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "❌ dotnet tidak ditemukan di: $dotnetPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Silakan install .NET SDK dari:" -ForegroundColor Yellow
    Write-Host "https://dotnet.microsoft.com/download" -ForegroundColor Cyan
    Read-Host "Press Enter to exit"
    exit 1
}

# Check if already in PATH
$currentPath = [Environment]::GetEnvironmentVariable("Path", "Machine")
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")

$alreadyInPath = ($currentPath -like "*$dotnetPath*") -or ($userPath -like "*$dotnetPath*")

if ($alreadyInPath) {
    Write-Host "✅ dotnet sudah ada di PATH" -ForegroundColor Green
    Write-Host ""
    Write-Host "Tapi PowerShell session Anda mungkin perlu di-restart." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Solusi:" -ForegroundColor Cyan
    Write-Host "1. Tutup PowerShell" -ForegroundColor White
    Write-Host "2. Buka PowerShell baru" -ForegroundColor White
    Write-Host "3. Test: dotnet --version" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host "⚠️  dotnet TIDAK ada di PATH" -ForegroundColor Yellow
    Write-Host ""
    
    if ($isAdmin) {
        Write-Host "Menambahkan dotnet ke System PATH..." -ForegroundColor Yellow
        
        try {
            # Add to System PATH
            $newPath = $currentPath + ";" + $dotnetPath
            [Environment]::SetEnvironmentVariable("Path", $newPath, "Machine")
            
            Write-Host "✅ dotnet berhasil ditambahkan ke PATH!" -ForegroundColor Green
            Write-Host ""
            Write-Host "PENTING: Restart PowerShell untuk apply changes" -ForegroundColor Yellow
            Write-Host ""
        } catch {
            Write-Host "❌ Gagal menambahkan ke PATH: $_" -ForegroundColor Red
        }
    } else {
        Write-Host "Menambahkan dotnet ke User PATH..." -ForegroundColor Yellow
        
        try {
            # Add to User PATH
            $newUserPath = $userPath + ";" + $dotnetPath
            [Environment]::SetEnvironmentVariable("Path", $newUserPath, "User")
            
            Write-Host "✅ dotnet berhasil ditambahkan ke User PATH!" -ForegroundColor Green
            Write-Host ""
            Write-Host "PENTING: Restart PowerShell untuk apply changes" -ForegroundColor Yellow
            Write-Host ""
        } catch {
            Write-Host "❌ Gagal menambahkan ke PATH: $_" -ForegroundColor Red
        }
    }
}

# Add to current session
Write-Host "Menambahkan dotnet ke session ini..." -ForegroundColor Yellow
$env:Path += ";$dotnetPath"
Write-Host "✅ dotnet tersedia untuk session ini" -ForegroundColor Green
Write-Host ""

# Test dotnet
Write-Host "Testing dotnet..." -ForegroundColor Yellow
try {
    $version = & "$dotnetPath\dotnet.exe" --version 2>&1
    Write-Host "✅ dotnet version: $version" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "❌ Error testing dotnet: $_" -ForegroundColor Red
    Write-Host ""
}

Write-Host "============================================" -ForegroundColor Green
Write-Host "  ✅ SELESAI!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""

Write-Host "📝 Next Steps:" -ForegroundColor Cyan
Write-Host ""
Write-Host "Untuk session ini (temporary):" -ForegroundColor Yellow
Write-Host "  dotnet --version" -ForegroundColor White
Write-Host "  dotnet run --launch-profile https" -ForegroundColor White
Write-Host ""

Write-Host "Untuk permanent fix:" -ForegroundColor Yellow
Write-Host "  1. Tutup PowerShell ini" -ForegroundColor White
Write-Host "  2. Buka PowerShell BARU" -ForegroundColor White
Write-Host "  3. Test: dotnet --version" -ForegroundColor White
Write-Host ""

Read-Host "Press Enter to exit"
