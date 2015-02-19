:: Install Healthstone on the local system
@echo off
echo.
echo This will uninstall Healthstone System Monitor from %SYSTEMROOT%\healthstone
echo.
pause
net stop Healthstone
%SYSTEMROOT%\System32\sc.exe delete Healthstone
del %SYSTEMROOT%\healthstone\healthstone.cfg
del %SYSTEMROOT%\healthstone\healthstone.exe
echo Done.
pause