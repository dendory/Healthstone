:: Install Healthstone on the local system
@echo off
echo This will install Healthstone and start the service.
echo.
net stop Healthstone
mkdir "%PROGRAMFILES%\healthstone"
copy %~dp0healthstone.cfg "%PROGRAMFILES%\healthstone\healthstone.cfg"
copy %~dp0healthstone.exe "%PROGRAMFILES%\healthstone\healthstone.exe"
%SYSTEMROOT%\System32\sc.exe create Healthstone binpath= "%PROGRAMFILES%\healthstone\healthstone.exe" type= own start= auto
%SYSTEMROOT%\System32\sc.exe description Healthstone "Healhstone System Monitor"
%SYSTEMROOT%\System32\sc.exe failure Healthstone reset= 86400 actions= restart/5000/////
reg add HKLM\Software\Healthstone /v Config /t REG_SZ /d "%PROGRAMFILES%\healthstone\healthstone.cfg" /f
net start Healthstone
echo.
echo Installation done. To customize your installation edit %PROGRAMFILES%\healthstone\healthstone.cfg and restart the service.
echo.