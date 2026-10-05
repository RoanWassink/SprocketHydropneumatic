# Sprocket Hydropneumatic 0.4.2 — shared Keybinds integration

A standalone **Hydropneumatic** suspension part for Sprocket, alongside Christie, torsion bar and HVSS. Adjust ride height and hull pitch while driving, through the game's suspension physics.

**Compatibility:** Windows x64, Sprocket **0.2.55.5**, BepInEx **6 IL2CPP**. Other game builds have not been verified. This local development build is version `0.4.2`; it is provided on GitHub. It also requires the shared **Sprocket Keybinds >=0.1.3 <0.2.0** plugin, installed separately.

## Install — copy and paste (recommended)

**No PowerShell script is required.** Install the shared **Sprocket Keybinds >=0.1.3 <0.2.0** plugin first. Requires Sprocket **0.2.55.5** on Windows x64 and a working **BepInEx 6 IL2CPP** installation. Start the game once after installing BepInEx, then close it. The loader is not included in this mod.

1. Use the local **SprocketHydropneumatic-v0.4.2.zip** package. This release requires the shared Keybinds API.
2. Close Sprocket. Extract the ZIP anywhere convenient.
3. In Steam, right-click **Sprocket → Manage → Browse local files**. This opens the game folder containing `Sprocket.exe`.
4. Copy **both `BepInEx` and `Sprocket_Data` folders** from the extracted ZIP into that game folder in one paste. Let Windows merge the folders; when updating this mod, replace its matching files. Accept a Windows administrator prompt if required. Do not delete the existing folders or put the extracted ZIP's outer folder inside the game folder.
5. Start Sprocket and choose **Hydropneumatic** in the suspension part list.

Alternatively, extract the ZIP directly into the folder containing `Sprocket.exe`. The ZIP already has the correct directory structure:

```text
Sprocket/                         <- your existing game folder
├── Sprocket.exe                  <- already installed
├── BepInEx/
│   └── plugins/SprocketHydropneumatic/
│       ├── SprocketHydropneumatic.dll
│       └── assets/hydropneumatic-icon.png
└── Sprocket_Data/StreamingAssets/
    ├── Parts/hydropneumaticSuspensionPart.json
    └── Localization/en-UK/Parts/hydropneumaticSuspension.xml
```

The manual ZIP contains only four mod files in these folders, plus instructions/checksums at the archive root. It does not include your configuration or other plugins. If updating, you can back up those four existing files first. Avoid duplicate copies of the DLL. Manual installation does not perform the optional installer's game-version check; confirm your game version matches before installing.

### Optional PowerShell installer

The local **SprocketHydropneumatic-v0.4.2-local.zip** is available for users who prefer an installer with game-binary checks and automatic backups. Extract that ZIP to a writable folder and, with Sprocket closed, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1
```

For another Steam library, append `-GameDir "D:\SteamLibrary\steamapps\common\Sprocket"`. If Windows denies writing to the game folder, run that PowerShell window as administrator. Existing configuration is preserved. Do not bypass a game-version mismatch. The two packages contain the **same plugin and game assets**; choose either installation method.

### Uninstall

First change tanks using HPS back to a stock suspension and save them; a missing custom part can prevent a tank from loading. Then remove the four mod files listed above. Keep the existing `BepInEx` and `Sprocket_Data` folders.

## Controls

Change bindings in the normal game **Settings keybind menu**, using the **HPS /** labels. The HPS panel displays the current bindings, including Unassigned. Defaults below are factory defaults, not fixed keys.

| Menu label | Factory default | Normal mode | Independent mode |
|---|---|---|---|
| HPS / Raise | Up arrow | Raise whole tank | Raise front |
| HPS / Lower | Down arrow | Lower whole tank | Lower front |
| HPS / Tilt forward | Page Up | Lean forward | Raise rear |
| HPS / Tilt back | Page Down | Lean backward | Lower rear |
| HPS / Neutral | Home | Gradually return to neutral | Gradually return to neutral |

Hold adjustment actions for gradual movement. Enable **Independent front and rear controls** in Suspension settings for separate adjustment. The same five actions serve both modes; there is no additional HPS tab or category.

Existing `[Keys]` values in `BepInEx/config/nl.roan.sprocket.hydropneumatic.cfg` are imported once. Saved shared-API choices take priority, including Reset and Unassigned. Those legacy entries remain available for compatibility, but new changes belong in the game's keybind menu. Input is suppressed while paused, unfocused, editing text or configuring bindings.

## Setup and balance

Use the standard editor suspension height for the neutral stance. HPS adds front/rear travel limits, equivalent wheel stroke, suspension reserve, adjustment speed, stiffness, progression and damping. New profiles default to **±100 mm**, **300 mm stroke**, **30 mm reserve**, and **40 mm/s**. **Reset HPS settings** restores the HPS defaults without changing the ordinary wheel geometry.

The first HPS selection inherits the existing running gear. Returning to HPS during the same editing session restores its previous wheel diameter, width, count, spacing, offsets and suspension geometry. Stock suspensions keep their own mechanical limits.

HPS is priced as a premium suspension: the hydraulic unit costs **1.20 per kg + 160 assembly**. Its reference suspension mass is **185 kg per roadwheel**, before scaling and excluding the wheel/track, roughly 43% above the HVSS reference. These are gameplay balance choices, not measurements of a specific real tank; see [BALANCE.md](BALANCE.md) for the detailed development comparison (Dutch).

## Known limitations

- Actual movement depends on tank mass, arm geometry, mechanical stops and terrain. Slider limits are commands, not a guaranteed measured hull displacement.
- This approximates hydropneumatic behaviour through native springs and belt support heights. It has no complete gas/oil/pump simulation or animated hydraulic cylinder. The model references existing game meshes; the custom icon represents a hydraulic unit and accumulator.
- The extra history of a previous HPS layout exists only during the editing session. Saving with HPS selected saves its active geometry normally. Saving another suspension and later reloading does not preserve a separate historical HPS layout.
- Old tanks already saved with shrunken wheels cannot automatically recover their original dimensions.
- The supplied name localization is English (`en-UK`). Updates to Sprocket may require a new compatible mod build.

## Testing and bug reports

The earlier builds were tried in-game by the author/user. The model regression suite, native hook/property checks and part-reference checks cover the existing physics. The Keybinds integration adds a compiled dependency/input check; it still needs in-game testing of rebinding, migration, resets and both control modes. These do not replace playtesting on other tanks and machines.

Please [open an issue](https://github.com/RoanWassink/SprocketHydropneumatic/issues) with the game/mod versions, steps to reproduce, tank mass, control mode and the suspension types involved. Useful tests include HPS → VVSS → HPS, Christie/HVSS/torsion bar switches, wheel diameter/spacing/forward offsets, slopes, heavy tanks, saving/loading, Home and the reset button.

Detailed diagnostics are **off by default**. To investigate a bug, set this in the mod configuration and restart the game:

```ini
[Diagnostics]
VerboseLogging = true
```

Include the relevant `[Hydro]` lines from `BepInEx/LogOutput.log`, after reviewing the log for personal paths or unrelated information. Turn diagnostics off again for normal play. Error reporting and runtime safety checks remain enabled regardless of this option.

## Build from source

Requires .NET SDK 8, a local compatible game/BepInEx installation, and the separately installed SprocketKeybinds API DLL. The API reference uses `Private=false`; no API copy is distributed. Game assemblies are referenced locally and never redistributed.

```powershell
dotnet build SprocketHydropneumatic.csproj -c Release -p:UseSharedCompilation=false
dotnet run --project tests/Hydro.Tests.csproj -c Release -p:UseSharedCompilation=false
.\verify-interop.ps1 -PrototypeDll .\work\bin\SprocketHydropneumatic\Release\net6.0\SprocketHydropneumatic.dll
python verify-part.py
```

For a custom install, pass `-p:GameDir="D:\SteamLibrary\steamapps\common\Sprocket"` when building, `-GameDir` to `verify-interop.ps1`, and `--game-dir` to `verify-part.py`. The pure tests run only during development, not in the game.

## Local 0.4.2 geometry repair

Native hydraulic stops that cross a cosine turning point now restrict HPS commands to the safe branch containing the neutral angle. This supports the reproduced -60 degree editor arm without changing its neutral stance, native mechanical stops, stiffness, mass or cost. Exactly ambiguous neutral angles and missing transforms remain unsupported, with detailed log output. 3583 pure model checks pass; live playtesting is still required.
