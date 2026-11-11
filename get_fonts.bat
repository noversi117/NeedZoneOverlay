@echo off
REM Downloads Barlow Condensed SemiBold (SIL Open Font License) for the "NEED ZONES" title.
REM If every source fails, the overlay falls back to Windows' built-in Bahnschrift font.
cd /d "%~dp0"
if exist fonts\BarlowCondensed-SemiBold.ttf goto :done
set F=fonts\BarlowCondensed-SemiBold.ttf
curl -fsSL -o %F% "https://github.com/google/fonts/raw/main/ofl/barlowcondensed/BarlowCondensed-SemiBold.ttf" && goto :check
curl -fsSL -o %F% "https://cdn.jsdelivr.net/npm/@expo-google-fonts/barlow-condensed/600SemiBold/BarlowCondensed_600SemiBold.ttf" && goto :check
curl -fsSL -o %F% "https://cdn.jsdelivr.net/fontsource/fonts/barlow-condensed@latest/latin-600-normal.ttf" && goto :check
goto :fallback
:check
for %%A in (%F%) do if %%~zA LSS 20000 (del %F% & goto :fallback)
echo Barlow Condensed downloaded.
goto :done
:fallback
if exist %F% del %F%
echo Could not download Barlow Condensed - the title will use Bahnschrift instead.
:done
exit /b 0
