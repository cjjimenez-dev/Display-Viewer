@echo off
setlocal
echo =========================================================
echo  Display 2 Live Viewer - Native Windows Compiler
echo =========================================================
echo.

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
)

if not exist "%CSC%" (
    echo [ERROR] .NET Framework C# Compiler not found!
    echo Please ensure .NET Framework 4.5+ is installed on this PC.
    pause
    exit /b 1
)

echo Compiling Display2Viewer.cs using native csc.exe with app.ico...
"%CSC%" /target:winexe /optimize+ /win32icon:"%~dp0app.ico" /out:"%~dp0Display2Viewer.exe" "%~dp0Display2Viewer.cs"

if %ERRORLEVEL% equ 0 (
    echo.
    echo [SUCCESS] Display2Viewer.exe compiled successfully!
    echo Location: %~dp0Display2Viewer.exe
) else (
    echo.
    echo [FAILED] Compilation encountered an error.
)

echo.
pause
