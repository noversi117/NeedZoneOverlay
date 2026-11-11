@echo off
REM Builds CotwNeedOverlay.exe into the "publish" folder. Needs the .NET 8 SDK.
cd /d "%~dp0"
where dotnet >nul 2>nul || (echo The .NET 8 SDK is not installed. Get it from https://dotnet.microsoft.com/download/dotnet/8.0 & pause & exit /b 1)
call get_fonts.bat
dotnet publish NeedZones.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish || goto :fail
echo.
echo Done: publish\CotwNeedOverlay.exe  (keep the publish\data folder and icon.ico next to it)
pause
exit /b 0
:fail
echo Build failed - copy the red error lines above and send them to Claude.
pause
exit /b 1
