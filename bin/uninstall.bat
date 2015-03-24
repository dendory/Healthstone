:: Install Healthstone on the local system
@echo off
echo.
echo This will uninstall Healthstone System Monitor from %PROGRAMFILES%\healthstone
echo.
pause
net stop Healthstone
%SYSTEMROOT%\System32\sc.exe delete Healthstone
del "%PROGRAMFILES%\healthstone\healthstone.cfg"
del "%PROGRAMFILES%\healthstone\healthstone.exe"
rd "%PROGRAMFILES%\healthstone"
echo Done.
pause