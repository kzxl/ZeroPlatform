@echo off
cd /d "%~dp0\.."
echo Starting ZeroUI WPF Demo...
dotnet run --project demo\WpfDemo\WpfDemo.csproj
