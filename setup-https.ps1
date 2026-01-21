# ============================================
# Setup HTTPS untuk Dashboard WIP House
# ============================================
# Script ini akan:
# 1. Generate dan trust development certificate
# 2. Export certificate untuk tablet
# 3. Konfigurasi Windows Firewall
# 4. Menampilkan informasi akses
# ============================================

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Setup HTTPS - Dashboard WIP House" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "❌ Script ini harus dijalankan sebagai Administrator!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Cara menjalankan:" -ForegroundColor Yellow
    Write-Host "1. Klik kanan PowerShell" -ForegroundColor Yellow
    Write-Host "2. Pilih 'Run as Administrator'" -ForegroundColor Yellow
    Write-Host "3. Jalankan script ini lagi" -ForegroundColor Yellow
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "✅ Running as Administrator" -ForegroundColor Green
Write-Host ""

# ============================================
# Step 1: Get IP Address
# ============================================
Write-Host "📡 Mendapatkan IP Address..." -ForegroundColor Yellow

$ipAddress = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object {
    $_.InterfaceAlias -like "*Wi-Fi*" -or $_.InterfaceAlias -like "*Ethernet*"
} | Where-Object {
    $_.IPAddress -notlike "169.254.*" -and $_.IPAddress -ne "127.0.0.1"
} | Select-Object -First 1).IPAddress

if (-not $ipAddress) {
    Write-Host "❌ Tidak dapat menemukan IP Address!" -ForegroundColor Red
    Write-Host "Pastikan komputer terhubung ke WiFi atau Ethernet" -ForegroundColor Yellow
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "✅ IP Address: $ipAddress" -ForegroundColor Green
Write-Host ""

# ============================================
# Step 2: Find dotnet executable
# ============================================
Write-Host "🔍 Mencari dotnet executable..." -ForegroundColor Yellow

$dotnetPath = $null

# Try to find dotnet in PATH
$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetCmd) {
    $dotnetPath = $dotnetCmd.Source
} else {
    # Try common locations
    $commonPaths = @(
        "C:\Program Files\dotnet\dotnet.exe",
        "C:\Program Files (x86)\dotnet\dotnet.exe",
        "$env:ProgramFiles\dotnet\dotnet.exe",
        "$env:ProgramFiles(x86)\dotnet\dotnet.exe"
    )
    
    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            $dotnetPath = $path
            break
        }
    }
}

if (-not $dotnetPath) {
    Write-Host "❌ dotnet tidak ditemukan!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Silakan install .NET SDK dari:" -ForegroundColor Yellow
    Write-Host "https://dotnet.microsoft.com/download" -ForegroundColor Cyan
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "✅ dotnet found: $dotnetPath" -ForegroundColor Green
Write-Host ""

# ============================================
# Step 3: Clean and Generate Certificate
# ============================================
Write-Host "🔐 Generating HTTPS Certificate..." -ForegroundColor Yellow

try {
    # Clean existing certificates
    & $dotnetPath dev-certs https --clean 2>&1 | Out-Null
    
    # Generate and trust new certificate
    $trustResult = & $dotnetPath dev-certs https --trust 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Certificate generated and trusted" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Certificate generated, but trust failed" -ForegroundColor Yellow
        Write-Host "Anda mungkin perlu menerima dialog trust certificate" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ Error generating certificate: $_" -ForegroundColor Red
}

Write-Host ""

# ============================================
# Step 4: Export Certificate
# ============================================
Write-Host "📤 Exporting certificate untuk tablet..." -ForegroundColor Yellow

$desktopPath = [Environment]::GetFolderPath("Desktop")
$certPath = Join-Path $desktopPath "aspnetcore-dev-cert.cer"
$pfxPath = Join-Path $desktopPath "aspnetcore-dev-cert.pfx"
$certPassword = "WIPHouse2026!"

try {
    # Export as .cer (for Android/iOS)
    & $dotnetPath dev-certs https --export-path $certPath --format Pem 2>&1 | Out-Null
    
    if (Test-Path $certPath) {
        Write-Host "✅ Certificate exported: $certPath" -ForegroundColor Green
    }
    
    # Export as .pfx (with password)
    & $dotnetPath dev-certs https -ep $pfxPath -p $certPassword 2>&1 | Out-Null
    
    if (Test-Path $pfxPath) {
        Write-Host "✅ PFX exported: $pfxPath" -ForegroundColor Green
        Write-Host "   Password: $certPassword" -ForegroundColor Cyan
    }
} catch {
    Write-Host "⚠️  Export certificate gagal: $_" -ForegroundColor Yellow
}

Write-Host ""

# ============================================
# Step 5: Configure Firewall
# ============================================
Write-Host "🔥 Configuring Windows Firewall..." -ForegroundColor Yellow

try {
    # Remove existing rules if any
    Remove-NetFirewallRule -DisplayName "ASP.NET Core HTTPS" -ErrorAction SilentlyContinue
    Remove-NetFirewallRule -DisplayName "ASP.NET Core HTTP" -ErrorAction SilentlyContinue
    
    # Add new rules
    New-NetFirewallRule -DisplayName "ASP.NET Core HTTPS" -Direction Inbound -LocalPort 7160 -Protocol TCP -Action Allow | Out-Null
    New-NetFirewallRule -DisplayName "ASP.NET Core HTTP" -Direction Inbound -LocalPort 5005 -Protocol TCP -Action Allow | Out-Null
    
    Write-Host "✅ Firewall rules configured" -ForegroundColor Green
    Write-Host "   - Port 7160 (HTTPS) opened" -ForegroundColor Cyan
    Write-Host "   - Port 5005 (HTTP) opened" -ForegroundColor Cyan
} catch {
    Write-Host "⚠️  Firewall configuration gagal: $_" -ForegroundColor Yellow
}

Write-Host ""

# ============================================
# Step 6: Display Access Information
# ============================================
Write-Host "============================================" -ForegroundColor Green
Write-Host "  ✅ SETUP SELESAI!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""

Write-Host "📱 AKSES DARI TABLET:" -ForegroundColor Cyan
Write-Host "   URL: https://$ipAddress:7160" -ForegroundColor Yellow
Write-Host ""

Write-Host "🔐 CERTIFICATE UNTUK TABLET:" -ForegroundColor Cyan
Write-Host "   File: $certPath" -ForegroundColor Yellow
Write-Host "   Transfer file ini ke tablet dan install" -ForegroundColor Gray
Write-Host ""

Write-Host "👤 LOGIN CREDENTIALS:" -ForegroundColor Cyan
Write-Host "   HOSE   : admin / admin123" -ForegroundColor Yellow
Write-Host "   RVI    : adminRVI / rvi123" -ForegroundColor Yellow
Write-Host "   MOLDED : adminMolded / molded321" -ForegroundColor Yellow
Write-Host ""

Write-Host "🚀 CARA MENJALANKAN APLIKASI:" -ForegroundColor Cyan
Write-Host "   1. Buka Visual Studio" -ForegroundColor White
Write-Host "   2. Pilih profile 'https' (bukan 'http')" -ForegroundColor White
Write-Host "   3. Tekan F5 atau klik Run" -ForegroundColor White
Write-Host ""
Write-Host "   Atau via command line:" -ForegroundColor White
Write-Host "   cd e:\dashboardWIPHouse" -ForegroundColor Gray
Write-Host "   dotnet run --launch-profile https" -ForegroundColor Gray
Write-Host ""

Write-Host "📚 DOKUMENTASI LENGKAP:" -ForegroundColor Cyan
Write-Host "   Baca: SETUP_HTTPS_TABLET.md" -ForegroundColor Yellow
Write-Host ""

Write-Host "============================================" -ForegroundColor Green
Write-Host ""

# Create QR Code URL file
$qrUrl = "https://$ipAddress:7160"
$qrFilePath = Join-Path $desktopPath "tablet-access-url.txt"
Set-Content -Path $qrFilePath -Value $qrUrl
Write-Host "💾 URL saved to: $qrFilePath" -ForegroundColor Green
Write-Host ""

Read-Host "Press Enter to exit"
