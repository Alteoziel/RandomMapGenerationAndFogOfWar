$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$version = "0.1.6"
$dist = Join-Path $root "dist"
$stage = Join-Path $dist "stage"
$payload = Join-Path $stage "payload"
$bepinexZip = Join-Path $dist "BepInEx_win_x64_5.4.23.5.zip"
$bepinexUrl = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip"
$installerZip = Join-Path $dist "MapGenerator-$version-Installer.zip"
$manualZip = Join-Path $dist "MapGenerator-$version-Manual.zip"
$dll = Join-Path $root "bin\Release\MapGenerator.dll"

if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $dist | Out-Null

dotnet build (Join-Path $root "MapGenerator.csproj") -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "The plugin build failed." }
if (-not (Test-Path -LiteralPath $dll)) { throw "MapGenerator.dll was not built." }

if (-not (Test-Path -LiteralPath $bepinexZip)) {
    Invoke-WebRequest -Uri $bepinexUrl -OutFile $bepinexZip
}
New-Item -ItemType Directory -Force -Path $payload | Out-Null
Expand-Archive -LiteralPath $bepinexZip -DestinationPath $payload -Force

$pluginDest = Join-Path $payload "BepInEx\plugins\MapGenerator"
New-Item -ItemType Directory -Force -Path $pluginDest | Out-Null
Copy-Item -LiteralPath $dll -Destination $pluginDest
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "install.ps1") -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Install Map Generator.bat") -Destination $stage

if (Test-Path -LiteralPath $installerZip) { Remove-Item -LiteralPath $installerZip -Force }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $installerZip

$manualRoot = Join-Path $dist "manual"
if (Test-Path -LiteralPath $manualRoot) { Remove-Item -LiteralPath $manualRoot -Recurse -Force }
$manualPlugin = Join-Path $manualRoot "BepInEx\plugins\MapGenerator"
New-Item -ItemType Directory -Force -Path $manualPlugin | Out-Null
Copy-Item -LiteralPath $dll -Destination $manualPlugin
@'
Map Generator 0.1.6
Manual install for Dungeon Alchemist 1.8.10 on Windows Steam.

1. Close Dungeon Alchemist.
2. If the game folder does not already contain BepInEx\core\BepInEx.dll and winhttp.dll, install BepInEx 5.4.23.5 for Windows x64 first. Extract that zip and copy its files into the folder that contains Dungeon Alchemist.exe. Start the game once, then close it.
   https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip
3. Copy the MapGenerator folder from this zip (BepInEx\plugins\MapGenerator) into BepInEx\plugins in the game folder.
   MapGenerator.dll should end up at BepInEx\plugins\MapGenerator\MapGenerator.dll.
4. Start Dungeon Alchemist, open a map, and press F9.

In Steam, the game folder is under the gear icon, Manage, then Browse local files.

To remove the mod, close the game and delete BepInEx\plugins\MapGenerator. Your maps are not changed.

AI was utilized in the development of this mod.
'@ | Set-Content -LiteralPath (Join-Path $manualRoot "INSTALL.txt") -Encoding utf8

if (Test-Path -LiteralPath $manualZip) { Remove-Item -LiteralPath $manualZip -Force }
Compress-Archive -Path (Join-Path $manualRoot "*") -DestinationPath $manualZip

Write-Output $installerZip
Write-Output $manualZip
