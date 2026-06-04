@echo off
cd /d "%~dp0"

echo ============================================
echo   Permission Management - Stopping Services
echo ============================================
echo.

echo [1/4] Stopping backend...
taskkill /f /im backend.exe 2>nul
echo [OK] Backend stopped
echo.

echo [2/4] Stopping frontend...
taskkill /f /fi "WINDOWTITLE eq Frontend*" 2>nul
echo [OK] Frontend stopped
echo.

echo [3/4] Stopping docs...
taskkill /f /fi "WINDOWTITLE eq Docs*" 2>nul
echo [OK] Docs stopped
echo.

echo [4/4] Stopping Docker containers...
docker compose down
echo [OK] Containers stopped
echo.

echo ============================================
echo   All services stopped
echo ============================================
pause
