#!/bin/bash
# Rebuilds the scene from code and then the Mac app, using Unity in batch mode.
# Usage: Tools/build.sh            (scene + Mac app)
#        Tools/build.sh scene      (scene only)
cd "$(dirname "$0")/.."
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.6.2f1-arm64/Unity.app/Contents/MacOS/Unity}"
mkdir -p Logs
"$UNITY" -batchmode -projectPath "$PWD" -executeMethod BakeryBuilder.BuildFromCommandLine -logFile Logs/build.log || { grep -E "error CS|Exception" Logs/build.log | sort -u | head -20; exit 1; }
grep -E "warning CS|Missing (sprite|sound)" Logs/build.log | sort -u | head -20
[ "$1" == "scene" ] && exit 0
"$UNITY" -batchmode -projectPath "$PWD" -executeMethod BakeryBuilder.BuildMacFromCommandLine -logFile Logs/buildmac.log || { grep -E "error CS|Exception|Error" Logs/buildmac.log | sort -u | head -20; exit 1; }
grep "Enzo's Bakery build" Logs/buildmac.log
