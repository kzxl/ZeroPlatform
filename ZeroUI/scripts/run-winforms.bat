@echo off
cd /d "%~dp0\.."
echo Starting ZeroUI WinForms Demo...
dotnet run --project demo\WinformDemo\WinformDemo.csproj
