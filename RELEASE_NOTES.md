# v0.4.0-beta.1 — First public beta

Standalone Hydropneumatic suspension with adjustable ride height and pitch, optional independent front/rear controls, readable settings, its own icon, premium cost/mass balance and editing-session memory for the running gear.

Download **SprocketHydropneumatic-v0.4.0-beta.1.zip**, extract it and run `Install.ps1` with Sprocket closed. Requires **Sprocket 0.2.55.5 on Windows x64** and an existing **BepInEx 6 IL2CPP** installation. The ZIP includes the DLL, part definition, localization, icon and instructions; the loader is not included.

- Up/Down arrows: whole-tank height.
- Page Up/Down: lean forward/backward.
- Home: neutral stance.
- Independent front/rear controls are available in Suspension settings.

Detailed runtime logging and physics/editor diagnostics are now opt-in (`[Diagnostics] VerboseLogging = true`). Normal play avoids their readback checks, logging and editor snapshot allocation. Essential safety checks and errors remain active; the offline regression suite is retained in source.

Validation: 3271 pure-model checks, 13 native Harmony signatures, 32 native layout properties, and part graph/stock asset references. Public beta testing is still needed for different tanks, slopes, suspension switches and save/load behaviour.

Known limits: no fully simulated gas/oil circuit or animated cylinder; physical travel depends on geometry and load. Separate HPS layout history lasts only for the editing session. English name localization only. Only the listed game build is verified.

Report problems through [GitHub Issues](https://github.com/RoanWassink/SprocketHydropneumatic/issues), including versions, reproduction steps and reviewed relevant `[Hydro]` log lines.
