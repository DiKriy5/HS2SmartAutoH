@echo off
setlocal

rem ================== SET YOUR PATHS ==================
rem Game folder (where HoneySelect2.exe is)
set "GAME=C:\Games\HoneySelect2"
rem BepInEx\core folder (with Mod Organizer 2 it is inside the BepInEx mod instead)
set "BEPCORE=%GAME%\BepInEx\core"
rem =====================================================

set "MANAGED=%GAME%\HoneySelect2_Data\Managed"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (echo [ERROR] csc.exe ^(.NET Framework 4^) not found & goto :end)
if not exist "%MANAGED%\Assembly-CSharp.dll" (echo [ERROR] Game not found: check GAME in build.bat & goto :end)
if not exist "%BEPCORE%\BepInEx.dll" (echo [ERROR] BepInEx.dll not found: check BEPCORE in build.bat & goto :end)

set "HARMONY=%BEPCORE%\0Harmony.dll"
if not exist "%HARMONY%" set "HARMONY=%BEPCORE%\0Harmony20.dll"

set "NETSTD="
if exist "%MANAGED%\netstandard.dll" set NETSTD=/r:"%MANAGED%\netstandard.dll"

cd /d "%~dp0HS2SmartAutoH"
if not exist "..\out" mkdir "..\out"

"%CSC%" /nologo /target:library /optimize+ /nowarn:1701,1702,618 ^
 /out:"..\out\HS2SmartAutoH.dll" ^
 /r:"%BEPCORE%\BepInEx.dll" ^
 /r:"%HARMONY%" ^
 /r:"%MANAGED%\Assembly-CSharp.dll" ^
 /r:"%MANAGED%\Assembly-CSharp-firstpass.dll" ^
 /r:"%MANAGED%\IL.dll" ^
 /r:"%MANAGED%\DOTween.dll" ^
 /r:"%MANAGED%\UnityEngine.dll" ^
 /r:"%MANAGED%\UnityEngine.CoreModule.dll" ^
 /r:"%MANAGED%\UnityEngine.AnimationModule.dll" ^
 /r:"%MANAGED%\UnityEngine.IMGUIModule.dll" ^
 /r:"%MANAGED%\UnityEngine.UI.dll" ^
 /r:System.Xml.dll /r:System.Core.dll /r:System.Data.dll ^
 %NETSTD% ^
 *.cs Hooks\*.cs Properties\*.cs

if errorlevel 1 (
  echo.
  echo [ERROR] Build failed. Copy all text above and send it to Claude.
) else (
  echo.
  echo [OK] Done: out\HS2SmartAutoH.dll
)

:end
echo.
pause
