#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

rm -rf Alarmmelder/bin/Release Alarmmelder/obj/Release

if command -v msbuild >/dev/null 2>&1; then
  msbuild MioneAlarmmelder.sln \
    /t:Rebuild \
    /p:Configuration=Release \
    '/p:Platform=Any CPU'
elif command -v xbuild >/dev/null 2>&1; then
  xbuild MioneAlarmmelder.sln \
    /target:Rebuild \
    /property:Configuration=Release \
    '/property:Platform=Any CPU'
else
  echo "Mono/MSBuild wurde nicht gefunden. Installieren Sie es mit: brew install mono" >&2
  exit 1
fi

echo
echo "Fertig: Alarmmelder/bin/Release"
