@echo off
setlocal
where msbuild >nul 2>nul
if errorlevel 1 (
  echo MSBuild wurde nicht gefunden. Bitte in einer Visual-Studio-Entwicklerkonsole starten.
  exit /b 1
)
msbuild MioneAlarmmelder.sln /t:Rebuild /p:Configuration=Release /p:Platform="Any CPU"
if errorlevel 1 exit /b %errorlevel%
echo.
echo Fertig: Alarmmelder\bin\Release
