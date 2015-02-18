:: Make a CSharp service and install it
@echo off
net stop Healthstone
timeout 1 > null
%SYSTEMROOT%\Microsoft.NET\Framework\v3.5\csc.exe /out:c:\healthstone\healthstone.exe healthstone.cs
IF ERRORLEVEL 1 GOTO end
%SYSTEMROOT%\System32\sc.exe create Healthstone binpath=c:\healthstone\healthstone.exe type=own start=auto
%SYSTEMROOT%\System32\sc.exe description Healthstone "Healhstone System Monitor"
net start Healthstone
:end