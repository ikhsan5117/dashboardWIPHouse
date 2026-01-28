@echo off
:: Pindah ke folder tempat file .bat ini berada (WAJIB saat Run as Administrator)
cd /d "%~dp0"

:: Cek apakah dijalankan sebagai Administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Harap jalankan file ini sebagai ADMINISTRATOR!
    echo Klik kanan file ini lalu pilih 'Run as Administrator'.
    pause
    exit /b
)

echo --------------------------------------------------
echo [1/4] MENGHENTIKAN IIS (IISRESET STOP) ...
echo --------------------------------------------------
iisreset /stop

echo.
echo [2/4] MEMBERSIHKAN FOLDER LAMA ...
echo --------------------------------------------------
if exist C:\Published\WIPHouse (
    rd /s /q C:\Published\WIPHouse
)

echo.
echo [3/4] MELAKUKAN PUBLISH ULANG ...
echo --------------------------------------------------
:: Perintah publish sekarang dijalankan di folder yang benar
dotnet publish -c Release -o C:\Published\WIPHouse

echo.
echo [4/4] MENJALANKAN KEMBALI IIS (IISRESET START) ...
echo --------------------------------------------------
iisreset /start

echo.
echo ==================================================
echo UPDATE SELESAI DENGAN BERSIH!
echo Silakan cek browser Anda (Tekan Ctrl+F5).
echo ==================================================
pause
