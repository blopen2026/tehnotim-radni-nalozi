@echo off
rem Pravi paket aplikacije za server: publish\RadniNalozi.zip
cd /d "%~dp0"
if exist publish rmdir /s /q publish
dotnet publish src\Tehnotim.RadniNalozi -c Release -o publish\RadniNalozi
if errorlevel 1 goto greska
powershell -NoProfile -Command "Compress-Archive -Path 'publish\RadniNalozi\*' -DestinationPath 'publish\RadniNalozi.zip' -Force"
if errorlevel 1 goto greska
echo.
echo Gotovo: %~dp0publish\RadniNalozi.zip
pause
exit /b 0
:greska
echo.
echo GRESKA - paket nije napravljen. Posaljite poruku iznad.
pause
exit /b 1
