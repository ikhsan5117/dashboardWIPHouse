# ============================================
# RUN Dashboard WIP House
# ============================================
# Script untuk menjalankan aplikasi tanpa
# perlu setup PATH
# ============================================

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Dashboard WIP House - Starter" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# dotnet path
$dotnetExe = "C:\Program Files\dotnet\dotnet.exe"

# Check if dotnet exists
if (-not (Test-Path $dotnetExe)) {
    Write-Host "❌ dotnet tidak ditemukan!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Silakan install .NET SDK 8.0 dari:" -ForegroundColor Yellow
    Write-Host "https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Cyan
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

# Get dotnet version
$version = & $dotnetExe --version
Write-Host "✅ .NET SDK Version: $version" -ForegroundColor Green
Write-Host ""

# Get IP Address
Write-Host "📡 Getting IP Address..." -ForegroundColor Yellow
$ipAddress = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object {
    $_.InterfaceAlias -like "*Wi-Fi*" -or $_.InterfaceAlias -like "*Ethernet*"
} | Where-Object {
    $_.IPAddress -notlike "169.254.*" -and $_.IPAddress -ne "127.0.0.1"
} | Select-Object -First 1).IPAddress

if ($ipAddress) {
    Write-Host "✅ IP Address: $ipAddress" -ForegroundColor Green
} else {
    Write-Host "⚠️  IP Address tidak ditemukan" -ForegroundColor Yellow
    $ipAddress = "localhost"
}
Write-Host ""

# Ask which profile to use
Write-Host "Pilih profile untuk menjalankan aplikasi:" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. HTTP  - http://localhost:5005" -ForegroundColor White
Write-Host "2. HTTPS - https://localhost:7160 (untuk tablet/camera)" -ForegroundColor White
Write-Host ""

$choice = Read-Host "Pilih (1 atau 2) [default: 2]"

if ([string]::IsNullOrWhiteSpace($choice)) {
    $choice = "2"
}

$profile = "http"
$url = "http://localhost:5005"

if ($choice -eq "2") {
    $profile = "https"
    $url = "https://localhost:7160"
    $tabletUrl = "https://$ipAddress:7160"
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  Starting Application..." -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Profile: $profile" -ForegroundColor Cyan
Write-Host "Local URL: $url" -ForegroundColor Yellow

if ($choice -eq "2") {
    Write-Host "Tablet URL: $tabletUrl" -ForegroundColor Yellow
    Write-Host "Camera Test: $tabletUrl/camera-test.html" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Press Ctrl+C to stop the application" -ForegroundColor Gray
Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host ""

# Run the application
try {
    & $dotnetExe run --launch-profile $profile
} catch {
    Write-Host ""
    Write-Host "❌ Error: $_" -ForegroundColor Red
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}
