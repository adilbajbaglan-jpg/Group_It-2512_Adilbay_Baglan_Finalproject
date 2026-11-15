@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>&1
if errorlevel 1 (
  echo Install the .NET 8 x64 SDK, reopen this folder, and try again.
  pause
  exit /b 1
)
if not exist data\library.json (
  copy /y data\sample.json data\library.json >nul
  if errorlevel 1 goto failed
)
dotnet run --project src\LibraryDesk
if errorlevel 1 goto failed
exit /b 0
:failed
pause
exit /b 1
