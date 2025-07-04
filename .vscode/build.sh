#!/usr/bin/env bash
set -e

export RimWorldVersion="$1"
CONFIGURATION="Debug"
TARGET="$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/RimWorld/Mods/RealisticOrbitalTrade"

mkdir -p .savedatafolder/1.6
mkdir -p .savedatafolder/1.5
mkdir -p .savedatafolder/1.4

# build dlls
dotnet build --configuration "$CONFIGURATION" Source/RealisticOrbitalTrade/RealisticOrbitalTrade.csproj
dotnet build --configuration "$CONFIGURATION" Source/RealisticOrbitalTrade.DynamicTradeInterface/RealisticOrbitalTrade.DynamicTradeInterface.csproj

if [ "$RimWorldVersion" = "1.4" ]; then
dotnet build --configuration "$CONFIGURATION" Source/RealisticOrbitalTrade.WeHadATrader/RealisticOrbitalTrade.WeHadATrader.csproj
fi

if [ "$RimWorldVersion" = "1.5" ]; then
    dotnet build --configuration "$CONFIGURATION" Source/RealisticOrbitalTrade.TweaksGalore/RealisticOrbitalTrade.TweaksGalore.csproj
fi

if [ "$RimWorldVersion" = "1.4" ] || [ "$RimWorldVersion" = "1.5" ]; then
    dotnet build --configuration "$CONFIGURATION" Source/RealisticOrbitalTrade.AutoSeller/RealisticOrbitalTrade.AutoSeller.csproj
fi

# remove mod folder
rm -rf "$TARGET"

# copy mod files
mkdir -p "$TARGET"
cp -r "$RimWorldVersion" "$TARGET/$RimWorldVersion"

# copy interop mod files
cp -r "${RimWorldVersion}_DynamicTradeInterface" "$TARGET/${RimWorldVersion}_DynamicTradeInterface"

if [ "$RimWorldVersion" = "1.4" ]; then
    cp -r "${RimWorldVersion}_WeHadATrader" "$TARGET/${RimWorldVersion}_WeHadATrader"
fi

if [ "$RimWorldVersion" = "1.5" ]; then
    cp -r "${RimWorldVersion}_TweaksGalore" "$TARGET/${RimWorldVersion}_TweaksGalore"
fi

if [ "$RimWorldVersion" = "1.4" ] || [ "$RimWorldVersion" = "1.5" ]; then
    cp -r "${RimWorldVersion}_AutoSeller" "$TARGET/${RimWorldVersion}_AutoSeller"
fi

cp -r Common "$TARGET/Common"

mkdir -p "$TARGET/About"
cp About/About.xml "$TARGET/About"
cp About/Preview.png "$TARGET/About"
cp About/ModIcon.png "$TARGET/About"
cp About/PublishedFileId.txt "$TARGET/About"

cp CHANGELOG.md "$TARGET"
cp LICENSE "$TARGET"
cp LICENSE.Apache-2.0 "$TARGET"
cp LICENSE.MIT "$TARGET"
cp README.md "$TARGET"
cp LoadFolders.xml "$TARGET"

# Trigger auto-hotswap
touch "$TARGET/$RimWorldVersion/Assemblies/RealisticOrbitalTrade.dll.hotswap"
