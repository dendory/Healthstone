:: Make a CSharp service and install it
@echo off
%SYSTEMROOT%\Microsoft.NET\Framework\v3.5\csc.exe /out:%~dp0..\clients\Windows\healthstone\healthstone.exe %~dp0healthstone.cs