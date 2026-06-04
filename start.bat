@echo off
cd /d "%~dp0"

echo ============================================
echo   Permission Management - Starting Services
echo ============================================
echo.

echo [1/3] Starting Docker containers...
docker compose up -d
if %ERRORLEVEL% neq 0 (
    echo [ERROR] Docker failed. Is Docker Desktop running?
    pause
    exit /b 1
)
echo [OK] Docker containers started
echo.

echo [2/3] Starting backend (ASP.NET Core) on port 5000...
start "Backend" cmd /c "cd /d %~dp0backend && dotnet run"
echo [OK] Backend starting...
echo.

echo [3/4] Starting frontend (Vite) on port 5173...
start "Frontend" cmd /c "cd /d %~dp0frontend && npm run dev"
echo [OK] Frontend starting...
echo.

echo [4/4] Starting docs (VitePress) on port 5174...
start "Docs" cmd /c "cd /d %~dp0 && npx vitepress dev docs --port 5174"
echo [OK] Docs starting...
echo.

echo ============================================
echo   All services started!
echo   Backend : http://localhost:5000
echo   Frontend: http://localhost:5173
echo   Docs   : http://localhost:5174
echo   MinIO   : http://localhost:9001
echo   Login   : admin / password
echo ============================================
echo.
echo Close this window to exit (services keep running).
pause >nul
