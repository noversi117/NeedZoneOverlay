@echo off
REM Builds and runs the overlay in debug mode (handy while testing).
cd /d "%~dp0"
call get_fonts.bat
dotnet run --project NeedZones.csproj
