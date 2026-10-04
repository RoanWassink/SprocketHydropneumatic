param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Sprocket')
$ErrorActionPreference = 'Stop'
if (Get-Process -Name Sprocket -ErrorAction SilentlyContinue) { throw 'Sluit Sprocket voordat je deze DLL installeert.' }
$gamePath = (Resolve-Path -LiteralPath $GameDir).Path
$pluginsPath = Join-Path $gamePath 'BepInEx\plugins'
if (!(Test-Path -LiteralPath (Join-Path $gamePath 'BepInEx\interop\Sprocket.ContinuousTracks.dll'))) { throw 'Geen passende BepInEx IL2CPP-installatie gevonden.' }
$gameHash = (Get-FileHash -LiteralPath (Join-Path $gamePath 'GameAssembly.dll') -Algorithm SHA256).Hash
if ($gameHash -ne '18A9A15B5E5F11898ED4DC34FC3E2D4C12950C3B37AC1FA499E8B00592DEDD56') { throw 'Deze gamebinary wijkt af van de onderzochte versie; hercontrole nodig.' }
$dllSource = Join-Path $PSScriptRoot 'SprocketHydropneumatic.dll'
if (!(Test-Path -LiteralPath $dllSource)) { $dllSource = Join-Path $PSScriptRoot 'release\SprocketHydropneumatic.dll' }
if (!(Test-Path -LiteralPath $dllSource)) { throw 'Prototype-DLL ontbreekt.' }
$targetDirectory = Join-Path $pluginsPath 'SprocketHydropneumatic'
$targetDll = Join-Path $targetDirectory 'SprocketHydropneumatic.dll'
$assetSource = Join-Path $PSScriptRoot 'assets'
$partSource = Join-Path $assetSource 'hydropneumaticSuspensionPart.json'
$nameSource = Join-Path $assetSource 'hydropneumaticSuspension.xml'
$iconSource = Join-Path $assetSource 'hydropneumatic-icon.png'
if (!(Test-Path -LiteralPath $iconSource)) { throw 'Het hydropneumatische icoon ontbreekt.' }
$iconDirectory = Join-Path $targetDirectory 'assets'
$iconTarget = Join-Path $iconDirectory 'hydropneumatic-icon.png'
if (!(Test-Path -LiteralPath $partSource) -or !(Test-Path -LiteralPath $nameSource)) { throw 'Het zelfstandige part of de naamdefinitie ontbreekt.' }
$partTarget = Join-Path $gamePath 'Sprocket_Data\StreamingAssets\Parts\hydropneumaticSuspensionPart.json'
$nameTarget = Join-Path $gamePath 'Sprocket_Data\StreamingAssets\Localization\en-UK\Parts\hydropneumaticSuspension.xml'
$configPath = Join-Path $gamePath 'BepInEx\config\nl.roan.sprocket.hydropneumatic.cfg'
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupDirectory = Join-Path $PSScriptRoot "backups\$timestamp"
New-Item -ItemType Directory -Force -Path $backupDirectory | Out-Null
if (Test-Path -LiteralPath $targetDll) { Copy-Item -LiteralPath $targetDll -Destination (Join-Path $backupDirectory 'SprocketHydropneumatic.dll') }
if (Test-Path -LiteralPath $configPath) { Copy-Item -LiteralPath $configPath -Destination (Join-Path $backupDirectory 'nl.roan.sprocket.hydropneumatic.cfg') }
if (Test-Path -LiteralPath $partTarget) { Copy-Item -LiteralPath $partTarget -Destination (Join-Path $backupDirectory 'hydropneumaticSuspensionPart.json') }
if (Test-Path -LiteralPath $nameTarget) { Copy-Item -LiteralPath $nameTarget -Destination (Join-Path $backupDirectory 'hydropneumaticSuspension.xml') }
if (Test-Path -LiteralPath $iconTarget) { Copy-Item -LiteralPath $iconTarget -Destination (Join-Path $backupDirectory 'hydropneumatic-icon.png') }
$logPath = Join-Path $gamePath 'BepInEx\LogOutput.log'
if (Test-Path -LiteralPath $logPath) { Copy-Item -LiteralPath $logPath -Destination (Join-Path $backupDirectory 'LogOutput-before-install.log') }
New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
# Recheck immediately before replacement; leave configuration and other mods alone.
if (Get-Process -Name Sprocket -ErrorAction SilentlyContinue) { throw 'Sprocket is inmiddels gestart; installatie afgebroken.' }
Copy-Item -LiteralPath $dllSource -Destination $targetDll -Force
Copy-Item -LiteralPath $partSource -Destination $partTarget -Force
Copy-Item -LiteralPath $nameSource -Destination $nameTarget -Force
New-Item -ItemType Directory -Force -Path $iconDirectory | Out-Null
Copy-Item -LiteralPath $iconSource -Destination $iconTarget -Force
if ((Get-FileHash -LiteralPath $dllSource).Hash -ne (Get-FileHash -LiteralPath $targetDll).Hash) { throw 'DLL-hashcontrole mislukt.' }
if ((Get-FileHash -LiteralPath $partSource).Hash -ne (Get-FileHash -LiteralPath $partTarget).Hash) { throw 'Part-hashcontrole mislukt.' }
if ((Get-FileHash -LiteralPath $nameSource).Hash -ne (Get-FileHash -LiteralPath $nameTarget).Hash) { throw 'Naamdefinitie-hashcontrole mislukt.' }
if ((Get-FileHash -LiteralPath $iconSource).Hash -ne (Get-FileHash -LiteralPath $iconTarget).Hash) { throw 'Icoon-hashcontrole mislukt.' }
"Geïnstalleerd: $targetDll"
"Onderdeel: $partTarget"
"Back-up: $backupDirectory"
