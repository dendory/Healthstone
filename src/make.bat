:: Make a CSharp service and install it
@echo off
%SYSTEMROOT%\Microsoft.NET\Framework\v3.5\csc.exe /out:%~dp0..\client\healthstone.exe %~dp0healthstone.cs
%SYSTEMROOT%\Microsoft.NET\Framework\v3.5\csc.exe /out:%~dp0..\server\healthstone\www\dashboard.exe %~dp0dashboard.cs