:: Make a CSharp service and sign it
@echo off
cd %~dp0
%SYSTEMROOT%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /platform:x64 /out:..\64\healthstone\healthstone.exe healthstone.cs
%SYSTEMROOT%\Microsoft.NET\Framework\v4.0.30319\csc.exe /out:..\32\healthstone\healthstone.exe healthstone.cs
call "C:\Program Files\Microsoft SDKs\Windows\v7.1\Bin\signtool.exe" sign /n "Patrick Lambert" /t http://timestamp.verisign.com/scripts/timstamp.dll ..\64\healthstone\healthstone.exe
call "C:\Program Files\Microsoft SDKs\Windows\v7.1\Bin\signtool.exe" sign /n "Patrick Lambert" /t http://timestamp.verisign.com/scripts/timstamp.dll ..\32\healthstone\healthstone.exe
del ..\..\..\server\healthstone\www\healthstone-agent-win64.zip
del ..\..\..\server\healthstone\www\healthstone-agent-win32.zip
cd ..\64
call "C:\Program Files\7-Zip\7z" a ..\..\..\server\healthstone\www\healthstone-agent-win64.zip healthstone
cd ..\32
call "C:\Program Files\7-Zip\7z" a ..\..\..\server\healthstone\www\healthstone-agent-win32.zip healthstone
