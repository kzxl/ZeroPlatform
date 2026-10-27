@echo off
setlocal
cd /d "%~dp0"

echo ========================================================
echo  ZeroUI High-Performance Desktop Showcase Launcher
echo ========================================================
echo.
echo Select demo to launch:
echo   [1] WinForms Demo (Industrial, Trend Studio, New Analytics)
echo   [2] WPF Demo      (Vector Canvas, GPU Acceleration, New Analytics)
echo   [3] Both Demos    (Side-by-side)
echo   [4] Run All Unit Tests
echo   [Q] Quit
echo.

set /p choice="Enter choice [1-4, Q]: "

if /i "%choice%"=="1" goto run_winforms
if /i "%choice%"=="2" goto run_wpf
if /i "%choice%"=="3" goto run_both
if /i "%choice%"=="4" goto run_tests
if /i "%choice%"=="q" goto end
goto run_winforms

:run_winforms
echo.
echo Launching WinForms Demo...
start "ZeroUI WinForms Demo" dotnet run --project demo\WinformDemo\WinformDemo.csproj
goto end

:run_wpf
echo.
echo Launching WPF Demo...
start "ZeroUI WPF Demo" dotnet run --project demo\WpfDemo\WpfDemo.csproj
goto end

:run_both
echo.
echo Launching Both Demos...
start "ZeroUI WinForms Demo" dotnet run --project demo\WinformDemo\WinformDemo.csproj
timeout /t 2 >nul
start "ZeroUI WPF Demo" dotnet run --project demo\WpfDemo\WpfDemo.csproj
goto end

:run_tests
echo.
echo Running All Test Suites...
dotnet test ZeroUI.slnx
pause
goto end

:end
endlocal
