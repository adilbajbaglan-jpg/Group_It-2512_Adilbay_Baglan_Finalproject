@echo off
setlocal
cd /d "%~dp0"
where cl >nul 2>&1
if errorlevel 1 (
  echo Open an x64 Native Tools Command Prompt for Visual Studio with C++ build tools.
  exit /b 1
)
if not exist runtimes\win-x64 mkdir runtimes\win-x64
cl /nologo /W4 /O2 /LD /TC libraryfees.c /link /OUT:runtimes\win-x64\libraryfees.dll /IMPLIB:"%TEMP%\LibraryDeskFees.lib"
exit /b %errorlevel%
