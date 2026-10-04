using HarmonyLib;
using Sprocket.Blueprints;
using Sprocket.Vehicles.Tracks;

namespace SprocketHydropneumatic;

internal static class Profiles
{
    private sealed record Entry(TrackBlueprint Blueprint, HydroSettings Settings);
    private static readonly Dictionary<IntPtr, Entry> Entries = new();
    internal const string SaveKey = "roanHydropneumaticV1";

    internal static HydroSettings Get(TrackBlueprint blueprint)
    {
        if (!Entries.TryGetValue(blueprint.Pointer, out var entry))
            Entries[blueprint.Pointer] = entry = new(blueprint, new());
        return entry.Settings;
    }

    internal static void Set(TrackBlueprint blueprint, HydroSettings settings) =>
        Entries[blueprint.Pointer] = new(blueprint, settings);
}

[HarmonyPatch]
internal static class BlueprintHooks
{
    [HarmonyPostfix, HarmonyPatch(typeof(TrackBlueprint), nameof(TrackBlueprint.Save))]
    private static void Save(TrackBlueprint __instance, IBlueprintSerializer __0)
    {
        try
        {
            var settings = Profiles.Get(__instance);
            if (settings.IsValid()) __0.AddValue(Profiles.SaveKey, settings.Serialize());
        }
        catch (Exception ex) { RuntimeHydro.Warn("Blueprint save", ex); }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(TrackBlueprint), nameof(TrackBlueprint.Load))]
    private static void Load(TrackBlueprint __instance, IBlueprintDeserializer __0)
    {
        string? json;
        // Old/vanilla blueprints do not have the optional key.
        try { json = __0.GetString(Profiles.SaveKey); }
        catch { Profiles.Set(__instance, new()); return; }
        if (string.IsNullOrWhiteSpace(json)) { Profiles.Set(__instance, new()); return; }
        try { Profiles.Set(__instance, HydroSettings.Parse(json)); }
        catch (Exception ex) { Profiles.Set(__instance, new()); RuntimeHydro.Warn("Invalid saved hydro settings; vanilla selected", ex); }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Blueprint), nameof(Blueprint.Copy))]
    private static void Copy(Blueprint __0, Blueprint __1)
    {
        try
        {
            var source = __0.TryCast<TrackBlueprint>();
            var destination = __1.TryCast<TrackBlueprint>();
            if (source != null && destination != null) Profiles.Set(destination, Profiles.Get(source).Copy());
        }
        catch (Exception ex) { RuntimeHydro.Warn("Blueprint copy", ex); }
    }
}
