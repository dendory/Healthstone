:: Install Healthstone on the local system
@echo off
set dashboard=%1
set healthstone=%2
echo This will install the Healthstone agent and start the service.
echo.
if "%dashboard%" == "" set /p dashboard="Dashboard URL: "
if "%template%" == "" set /p template="Template name: "
net stop Healthstone
mkdir "%PROGRAMFILES%\healthstone"
copy %~dp0healthstone.exe "%PROGRAMFILES%\healthstone\healthstone.exe"
%SYSTEMROOT%\System32\sc.exe create Healthstone binpath= "%PROGRAMFILES%\healthstone\healthstone.exe" type= own start= auto
%SYSTEMROOT%\System32\sc.exe description Healthstone "Healhstone System Monitor"
%SYSTEMROOT%\System32\sc.exe failure Healthstone reset= 86400 actions= restart/5000/////
reg add HKLM\Software\Healthstone /v dashboard /t REG_SZ /d "%dashboard%" /f
reg add HKLM\Software\Healthstone /v template /t REG_SZ /d "%template%" /f
net start Healthstone
echo.
echo Installation done. The agent will connect to %dashboard% in 30 seconds to fetch its configuration.
echo.