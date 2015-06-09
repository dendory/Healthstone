@echo off
echo *** NodePoint initial setup ***
echo.
echo Detected installation folder: "%CD%"
echo.
echo Enter the credentials for a local user under which NodePoint should run. It should have Registry access and write access to the NodePoint folder. It will be created if the user does not exist.
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
%windir%\system32\inetsrv\appcmd add vdir /app.name:"Default Web Site/" /path:/nodepoint /physicalPath:"%CD%\www"
%windir%\system32\inetsrv\appcmd set config -section:isapiCgiRestriction /+[path='%CD%\www\nodepoint.exe',allowed='true',description='NodePoint']
%windir%\system32\inetsrv\appcmd set config /section:handlers /accessPolicy:Execute,Read,Script
echo.
echo * Restarting IIS
iisreset
echo.
echo You can access the initial configuration at: http://localhost/nodepoint
echo You can view the README at: http://localhost/nodepoint/README.html
echo.
pause
