@echo off
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /win32icon:"%~dp0assets\hub-poke.ico" /win32manifest:"%~dp0src\HubButton.manifest" /out:"%~dp0HubButton.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "%~dp0src\HubRenegotiate.cs" "%~dp0src\HubButton.cs"
if errorlevel 1 exit /b 1
echo Built %~dp0HubButton.exe
