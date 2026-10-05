using BepInEx.Configuration;
using SprocketKeybinds;
using UnityEngine.InputSystem;

namespace SprocketHydropneumatic;

internal static class HydroControls
{
    private sealed record Binding(ModKeybind Handle, ConfigEntry<Key> Legacy);
    private static readonly List<Binding> bindings = new();
    private static readonly HashSet<string> migrated = new();
    internal static ModKeybind Raise = null!, Lower = null!, Forward = null!, Backward = null!, Neutral = null!;

    internal static void Configure(ConfigFile config)
    {
        if (bindings.Count != 0) return; // Plugin lifetime, not scene lifetime.
        ModKeybind Register(string id, string label, string legacyName, Key factoryKey, string factoryPath)
        {
            var legacy = config.Bind("Keys", legacyName, factoryKey,
                "Legacy binding imported once. Change HPS controls in the game's Settings keybind menu.");
            var handle = Keybinds.RegisterButton(Plugin.Guid, "Hydropneumatic", id, label, factoryPath);
            bindings.Add(new(handle, legacy));
            return handle;
        }
        Raise = Register("raise", "HPS / Raise", "FrontUp", Key.UpArrow, "<Keyboard>/upArrow");
        Lower = Register("lower", "HPS / Lower", "FrontDown", Key.DownArrow, "<Keyboard>/downArrow");
        Forward = Register("tilt-forward", "HPS / Tilt forward", "RearUp", Key.PageUp, "<Keyboard>/pageUp");
        Backward = Register("tilt-backward", "HPS / Tilt back", "RearDown", Key.PageDown, "<Keyboard>/pageDown");
        Neutral = Register("neutral", "HPS / Neutral", "Neutral", Key.Home, "<Keyboard>/home");
        TryMigrate();
    }

    internal static void TryMigrate()
    {
        if (migrated.Count == bindings.Count || Keybinds.IsConfiguring) return;
        var keyboard = Keyboard.current;
        foreach (var binding in bindings)
        {
            if (migrated.Contains(binding.Handle.Id)) continue;
            var key = binding.Legacy.Value;
            if (key != Key.None && keyboard == null) continue;
            // Resolve the actual InputSystem control name; enum names aren't paths.
            string path = key == Key.None ? "" : "<Keyboard>/" + keyboard![key].name;
            Keybinds.TryImportLegacyBinding(binding.Handle, path);
            migrated.Add(binding.Handle.Id); // Existing API records, including unbound, win.
        }
    }

    internal static string Display(ModKeybind binding)
    {
        string label = binding.GetBindingDisplayString();
        return string.IsNullOrWhiteSpace(label) ? "Unassigned" : label;
    }
}
