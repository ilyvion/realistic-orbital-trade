$ErrorActionPreference = 'Stop'

$Target = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\RealisticOrbitalTrade"

# build dlls
$env:RimWorldVersion = "1.5"
dotnet build .vscode
#dotnet build --configuration Release .vscode
if ($LASTEXITCODE -gt 0) {
    throw "Build failed"
}
$env:RimWorldVersion = "1.4"
dotnet build .vscode
#dotnet build --configuration Release .vscode
if ($LASTEXITCODE -gt 0) {
    throw "Build failed"
}

# remove pdbs (for release)
# Remove-Item -Path .\1.5\Assemblies\RealisticOrbitalTrade.pdb -ErrorAction SilentlyContinue
# Remove-Item -Path .\1.4\Assemblies\RealisticOrbitalTrade.pdb -ErrorAction SilentlyContinue

# remove mod folder
Remove-Item -Path $Target -Recurse -ErrorAction SilentlyContinue

# copy mod files
Copy-Item -Path 1.5 $Target\1.5 -Recurse
Copy-Item -Path 1.4 $Target\1.4 -Recurse

Copy-Item -Path Common $Target\Common -Recurse

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
