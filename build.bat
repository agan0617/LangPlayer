@echo off
rem Build with the .NET Framework 4.x compiler that ships with Windows (no SDK needed)
setlocal
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set WPF=%FW%\WPF
if not exist dist mkdir dist
"%FW%\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 ^
  /out:dist\LangPlayer.exe /win32icon:src\app.ico /win32manifest:src\app.manifest ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\WindowsBase.dll" /r:System.Xaml.dll ^
  src\*.cs
