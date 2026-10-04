using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace SprocketHydropneumatic;

[BepInPlugin("nl.roan.sprocket.hydropneumatic", "Sprocket Hydropneumatic", "0.4.0")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource ModLog = null!;
    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<bool> Diagnostics = null!;
    private Harmony? core, editor, icons;
    public override void Load()
    {
        ModLog = Log;
        Enabled = Config.Bind("General", "Enabled", true, "Enable the experimental hydropneumatic system on the independent Hydropneumatic suspension part.");
        Diagnostics = Config.Bind("Diagnostics", "VerboseLogging", false, "Enable detailed physics readback and editor diagnostics when reporting a bug. Leave disabled for normal play.");
        RuntimeHydro.Configure(Config);
        try
        {
            core = new Harmony("nl.roan.sprocket.hydropneumatic.core");
            core.CreateClassProcessor(typeof(BlueprintHooks)).Patch();
            core.CreateClassProcessor(typeof(BalanceHooks)).Patch();
            core.CreateClassProcessor(typeof(LayoutMemoryHooks)).Patch();
            core.CreateClassProcessor(typeof(RuntimeHooks)).Patch();
            Log.LogInfo("[Hydro] Core ready. v0.4.0 beta 1; remembered HPS layout and two-sided hydraulic travel.");
        }
        catch (Exception ex)
        {
            core?.UnpatchSelf();
            Log.LogError("[Hydro] Core disabled: " + ex);
            return;
        }
        try
        {
            icons = new Harmony("nl.roan.sprocket.hydropneumatic.icons");
            icons.CreateClassProcessor(typeof(IconHooks)).Patch();
        }
        catch (Exception ex)
        {
            icons?.UnpatchSelf();
            Log.LogWarning("[Hydro] Custom icon disabled: " + ex.Message);
        }
        try
        {
            editor = new Harmony("nl.roan.sprocket.hydropneumatic.editor");
            editor.CreateClassProcessor(typeof(PartSwitchHooks)).Patch();
            editor.CreateClassProcessor(typeof(EditorHooks)).Patch();
            Log.LogInfo("[Hydro] Editor ready. Choose the Hydropneumatic part alongside Christie, torsion bar and HVSS.");
        }
        catch (Exception ex)
        {
            editor?.UnpatchSelf();
            Log.LogError("[Hydro] Editor disabled; saved hydro settings remain supported: " + ex);
        }
    }
}

