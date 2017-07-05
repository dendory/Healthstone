@echo off
echo *** Healthstone dashboard IIS setup ***
echo.
echo Detected installation folder: "%CD%"
echo.
echo Enter the credentials for a local user under which Healthstone dashboard should run. It should have write access to the db folder. It will be created if the user does not exist.
echo.
set /p id="Username: "
set /p pass="Password: "
echo.
echo * Creating user
net user %id% %pass% /add /y
net localgroup "Administrators" %id% /add /y
echo.
echo * Configuring IIS
%windir%\system32\inetsrv\appcmd set site "Default Web Site" -virtualDirectoryDefaults.userName:%id% -virtualDirectoryDefaults.password:%pass%
%windir%\system32\inetsrv\appcmd add vdir /app.name:"Default Web Site/" /path:/healthstone /physicalPath:"%CD%\www"
%windir%\system32\inetsrv\appcmd set config -section:isapiCgiRestriction /+[path='%CD%\www\dashboard.py',allowed='true',description='Healthstone']
%windir%\system32\inetsrv\appcmd set config /section:handlers /accessPolicy:Execute,Read,Script
echo.
echo * Restarting IIS
iisreset
echo * Adding scheduled task
schtasks /create /tn "Healthstone Probe Automation" /sc minute /mo 5 /tr "(cd %CD%\www && python.exe dashboard.py)" /ru "SYSTEM" /f
echo.
echo You can view the dashboard at: http://localhost/healthstone
echo.
pause
