:: Make a CSharp service and sign it
@%SYSTEMROOT%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /out:%~dp0..\agents\Windows\healthstone\healthstone.exe %~dp0healthstone.cs
@call "C:\Program Files (x86)\Windows Kits\8.0\bin\x64\signtool.exe" sign /n "Patrick Lambert" /t http://timestamp.verisign.com/scripts/timstamp.dll %~dp0..\agents\Windows\healthstone\healthstone.exe
