@echo off
rem Builds AlyssaVR_Hands.dll and AlyssaVR_ScreenFX.dll into dist\BepInEx\plugins\
rem Usage:  build.cmd ["path\to\ALYSSA game folder"]
rem Needs: the game with BepInEx 5 installed (for reference assemblies) and the .NET Framework 4 compiler (part of Windows).
setlocal

set "GAME=%~1"
if "%GAME%"=="" set "GAME=C:\Program Files (x86)\Steam\steamapps\common\ALYSSA"
set "M=%GAME%\ALYSSA_Data\Managed"
set "B=%GAME%\BepInEx\core"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "OUT=%~dp0dist\BepInEx\plugins"

if not exist "%M%\Assembly-CSharp.dll" (
  echo Game not found: "%GAME%"
  echo Usage: build.cmd "D:\SteamLibrary\steamapps\common\ALYSSA"
  exit /b 1
)
if not exist "%B%\BepInEx.dll" (
  echo BepInEx 5 is not installed in "%GAME%"
  exit /b 1
)
if not exist "%CSC%" (
  echo C# compiler not found: "%CSC%"
  exit /b 1
)

if not exist "%OUT%\AlyssaVR_Hands" mkdir "%OUT%\AlyssaVR_Hands"
if not exist "%OUT%\AlyssaVR_ScreenFX" mkdir "%OUT%\AlyssaVR_ScreenFX"

set "REFS=-r:"%M%\mscorlib.dll" -r:"%M%\System.dll" -r:"%M%\System.Core.dll" -r:"%M%\netstandard.dll" -r:"%M%\UnityEngine.dll" -r:"%M%\UnityEngine.CoreModule.dll" -r:"%B%\BepInEx.dll""

echo Building AlyssaVR_Hands...
"%CSC%" -nologo -target:library -optimize -nostdlib -noconfig -warn:1 ^
  -out:"%OUT%\AlyssaVR_Hands\AlyssaVR_Hands.dll" ^
  %REFS% ^
  -r:"%M%\UnityEngine.PhysicsModule.dll" -r:"%M%\UnityEngine.InputLegacyModule.dll" ^
  -r:"%M%\UnityEngine.ParticleSystemModule.dll" -r:"%M%\Unity.InputSystem.dll" ^
  -r:"%M%\UnityEngine.UI.dll" -r:"%M%\UnityEngine.UIModule.dll" ^
  -r:"%M%\Assembly-CSharp.dll" -r:"%M%\GameCreator.Runtime.Core.dll" ^
  -r:"%B%\0Harmony.dll" ^
  "%~dp0src\AlyssaVR_Hands\*.cs"
if errorlevel 1 exit /b 1

echo Building AlyssaVR_ScreenFX...
"%CSC%" -nologo -target:library -optimize -nostdlib -noconfig -warn:1 ^
  -out:"%OUT%\AlyssaVR_ScreenFX\AlyssaVR_ScreenFX.dll" ^
  %REFS% ^
  "%~dp0src\AlyssaVR_ScreenFX\*.cs"
if errorlevel 1 exit /b 1

echo Done: %OUT%
endlocal
