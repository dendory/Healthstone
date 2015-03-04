:: Make a CSharp service and install it
@echo off
%SYSTEMROOT%\Microsoft.NET\Framework\v3.5\csc.exe /out:..\bin\healthstone.exe healthstone.cs
:end