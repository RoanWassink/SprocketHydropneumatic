param([string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Sprocket', [string]$PrototypeDll = '')
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')
$checks = @(
    @('Sprocket.Vehicles.ContinuousTracks', 'Sprocket.Vehicles.Tracks.TrackAssembly', 'Build', ''),
    @('Sprocket.Vehicles.ContinuousTracks.Editor', 'Sprocket.Vehicles.Tracks.Editor.TrackEditor', 'OnComponentRebuilt', ''),
    @('Sprocket.Vehicles.ContinuousTracks', 'Sprocket.Vehicles.Tracks.VoluteSuspensionSpring', 'Build', 'Sprocket.Vehicles.Tracks.TrackBuildFlags,System.Single,Sprocket.Vehicles.Tracks.SpringBuildContext'),
    @('Sprocket.VehiclePartImporting', 'Sprocket.PartImporting.PartDefinitionCardFactory', 'CreateCard', 'Sprocket.PartImporting.PartDefinition'),
    @('Sprocket.Vehicles.ContinuousTracks', 'Sprocket.Vehicles.Tracks.TrackBlueprint', 'Save', 'Sprocket.Blueprints.IBlueprintSerializer,Sprocket.Blueprints.BlueprintSaveContext'),
    @('Sprocket.Vehicles.ContinuousTracks', 'Sprocket.Vehicles.Tracks.TrackBlueprint', 'Load', 'Sprocket.Blueprints.IBlueprintDeserializer,Sprocket.FileVersion,Sprocket.Blueprints.BlueprintLoadContext'),
    @('Sprocket.Blueprints', 'Sprocket.Blueprints.Blueprint', 'Copy', 'Sprocket.Blueprints.Blueprint,Sprocket.Blueprints.Blueprint'),
    @('Sprocket.Vehicles.ContinuousTracks', 'Sprocket.Vehicles.Tracks.TrackAssembly', 'CreateTrackController', 'UnityEngine.Rigidbody,Sprocket.ContinuousTracks.ITrackBehaviourFactory,Sprocket.ContinuousTracks.ITrackBeltBehaviour,Sprocket.ContinuousTracks.TrackGroundSampler,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1<Sprocket.ContinuousTracks.TrackWheel>,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1<Sprocket.ContinuousTracks.SuspensionBehaviour>,System.Single'),
    @('Sprocket.VehicleController', 'Sprocket.Gameplay.VehicleControl.VehicleController', 'UpdateControl', 'Sprocket.GameTime,UnityEngine.Ray,Sprocket.Gameplay.VehicleControl.IVehicleControlInputs,UnityEngine.Camera'),
    @('Sprocket.Vehicles.Powertrains', 'Sprocket.Vehicles.Powertrains.VehicleMovementRegister', 'FixedUpdate', 'System.Single'),
    @('Sprocket.Vehicles.Powertrains', 'Sprocket.Vehicles.Powertrains.VehicleMovementRegister', 'Dispose', ''),
    @('Sprocket.ContinuousTracks', 'Sprocket.ContinuousTracks.TrackBehaviour', 'Release', ''),
    @('Sprocket.Vehicles.ContinuousTracks.Editor', 'Sprocket.Vehicles.Tracks.Editor.TrackEditor', 'OnGUI', 'Sprocket.UI.IGUILayout')
)
foreach ($check in $checks) {
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir "BepInEx\interop\$($check[0]).dll"))
    try {
        $type = $assembly.MainModule.Types | Where-Object FullName -EQ $check[1]
        $methods = @($type.Methods | Where-Object Name -EQ $check[2])
        if ($methods.Count -ne 1) { throw "Ambiguous/missing Harmony target: $($check[1])::$($check[2])" }
        $signature = ($methods[0].Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ','
        if ($signature -cne $check[3]) { throw "Changed signature: $($methods[0].FullName)" }
        "PASS $($check[1])::$($check[2])"
    } finally { $assembly.Dispose() }
}
$gameHash = (Get-FileHash -LiteralPath (Join-Path $GameDir 'GameAssembly.dll') -Algorithm SHA256).Hash
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'LayoutMemoryHooks.cs')) {
    $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'LayoutMemoryHooks.cs') -Raw
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'BepInEx\interop\Sprocket.Vehicles.ContinuousTracks.dll'))
    try {
        $groups = [regex]::Matches($source, 'typeof\((\w+)\), new\[\] \{ ([^}]+)')
        if ($groups.Count -ne 4) { throw 'Layout property schema could not be checked.' }
        $count = 0
        foreach ($group in $groups) {
            $type = $assembly.MainModule.Types | Where-Object FullName -EQ "Sprocket.Vehicles.Tracks.$($group.Groups[1].Value)"
            foreach ($name in [regex]::Matches($group.Groups[2].Value, '"(\w+)"')) {
                $property = $type.Properties | Where-Object Name -EQ $name.Groups[1].Value
                if (!$property.GetMethod.IsPublic -or !$property.SetMethod.IsPublic) { throw "Layout field cannot be read/written: $($type.Name).$($name.Groups[1].Value)" }
                $count++
            }
        }
        "PASS: $count native layout properties have public getters/setters."
    } finally { $assembly.Dispose() }
}
if ($gameHash -ne '18A9A15B5E5F11898ED4DC34FC3E2D4C12950C3B37AC1FA499E8B00592DEDD56') { throw "Different game binary: recheck native arm/solver behaviour before installing this prototype." }
"PASS: $($checks.Count) unambiguous Harmony signatures and researched game binary. The ref-struct arm hook from v0.1 is deliberately absent."
if ($PrototypeDll) {
    $prototype = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $PrototypeDll).Path)
    try {
        foreach ($type in $prototype.MainModule.Types) {
            foreach ($method in $type.Methods) {
                $patches = @($method.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch' })
                if (!$patches.Count) { continue }
                if (@($method.Parameters | Where-Object { $_.ParameterType.IsByReference }).Count) { throw "Prototype has unsafe by-ref hook: $($method.FullName)" }
                foreach ($patch in $patches) {
                    if (@($patch.ConstructorArguments | Where-Object { $_.Value -eq 'EnableSuspensionPhysics' }).Count) { throw 'The crashing arm hook is still present.' }
                }
            }
        }
        'PASS: Built plugin has no by-ref Harmony callback and no EnableSuspensionPhysics patch.'
    } finally { $prototype.Dispose() }
}
