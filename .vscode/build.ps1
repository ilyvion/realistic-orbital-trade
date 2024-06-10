$ErrorActionPreference = 'Stop'

$env:RimWorldVersion = $args[0]
$Configuration = 'Debug'

$VersionTargetPrefix = "D:\RimWorld"
$VersionTargetSuffix = "Mods\RealisticOrbitalTrade"
$Target = "$VersionTargetPrefix\$env:RimWorldVersion\$VersionTargetSuffix"

$env:RimWorldSteamWorkshopFolderPath = "..\..\.deps\refs"
# $env:RimWorldSteamWorkshopFolderPath = "C:\Program Files (x86)\Steam\steamapps\workshop\content\294100"

# build dlls
dotnet build --configuration $Configuration Source/RealisticOrbitalTrade/RealisticOrbitalTrade.csproj
if ($LASTEXITCODE -gt 0) {
    throw "Build failed"
}
dotnet build --configuration $Configuration Source/RealisticOrbitalTrade.WeHadATrader/RealisticOrbitalTrade.WeHadATrader.csproj
if ($LASTEXITCODE -gt 0) {
    throw "Build failed"
}
dotnet build --configuration $Configuration Source/RealisticOrbitalTrade.DynamicTradeInterface/RealisticOrbitalTrade.DynamicTradeInterface.csproj
if ($LASTEXITCODE -gt 0) {
    throw "Build failed"
}
dotnet build --configuration $Configuration Source/RealisticOrbitalTrade.AutoSeller/RealisticOrbitalTrade.AutoSeller.csproj
if ($LASTEXITCODE -gt 0) {
    throw "Build failed"
}

if ($env:RimWorldVersion -eq "1.5") {
    dotnet build --configuration $Configuration Source/RealisticOrbitalTrade.TweaksGalore/RealisticOrbitalTrade.TweaksGalore.csproj
    if ($LASTEXITCODE -gt 0) {
        throw "Build failed"
    }
}

# remove mod folder
Remove-Item -Path $Target -Recurse -ErrorAction SilentlyContinue

# copy mod files
Copy-Item -Path $env:RimWorldVersion $Target\$env:RimWorldVersion -Recurse

# copy interop mod files
Copy-Item -Path ${env:RimWorldVersion}_WeHadATrader $Target\${env:RimWorldVersion}_WeHadATrader -Recurse
Copy-Item -Path ${env:RimWorldVersion}_DynamicTradeInterface $Target\${env:RimWorldVersion}_DynamicTradeInterface -Recurse
Copy-Item -Path ${env:RimWorldVersion}_AutoSeller $Target\${env:RimWorldVersion}_AutoSeller -Recurse

if ($env:RimWorldVersion -eq "1.5") {
    Copy-Item -Path ${env:RimWorldVersion}_TweaksGalore $Target\${env:RimWorldVersion}_TweaksGalore -Recurse
}

Copy-Item -Path Common $Target\Common -Recurse
Copy-Item -Path UpdateLog $Target\UpdateLog -Recurse

New-Item -Path $Target -ItemType Directory -Name About
Copy-Item -Path About\About.xml $Target\About
Copy-Item -Path About\Manifest.xml $Target\About
Copy-Item -Path About\Preview.png $Target\About
Copy-Item -Path About\ModIcon.png $Target\About
Copy-Item -Path About\PublishedFileId.txt $Target\About

Copy-Item -Path CHANGELOG.md $Target
Copy-Item -Path LICENSE $Target
Copy-Item -Path LICENSE.Apache-2.0 $Target
Copy-Item -Path LICENSE.MIT $Target
Copy-Item -Path README.md $Target
Copy-Item -Path LoadFolders.xml $Target

# Trigger auto-hotswap
New-Item -Path $Target\$env:RimWorldVersion\Assemblies\RealisticOrbitalTrade.dll.hotswap -Type file
