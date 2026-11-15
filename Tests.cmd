@echo off
setlocal
cd /d "%~dp0"
dotnet run --project tests\LibraryDesk.Tests
set result=%errorlevel%
pause
exit /b %result%
