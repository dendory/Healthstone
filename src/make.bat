:: Make a CSharp service and install it
@echo off
%SYSTEMROOT%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /out:%~dp0..\clients\Windows\healthstone\healthstone.exe %~dp0healthstone.cs