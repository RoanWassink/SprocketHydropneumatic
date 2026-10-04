using System.Reflection;
using HarmonyLib;
using Sprocket.Vehicles.Tracks;

namespace SprocketHydropneumatic;

[HarmonyPatch]
internal static class LayoutMemoryHooks
{
    // Capture primitive user settings, not native objects that get destroyed when
    // replacing a part. Direct setters avoid MarkModified recursion during Build.
    private sealed class Values
    {
        private readonly PropertyInfo[] properties;
        private readonly object?[] values;
        internal Values(object source, Type type, string[] names)
        {
            properties = names.Select(n => type.GetProperty(n) ?? throw new MissingMemberException(type.Name, n)).ToArray();
            values = properties.Select(p => p.GetValue(source)).ToArray();
        }
        internal void Apply(object target)
        { for (int i = 0; i < properties.Length; i++) properties[i].SetValue(target, values[i]); }
    }
    private sealed record Layout(Values Wheel, Values Mount, Values Array, Values Suspension)
    {
        internal static Layout Capture(TrackPackedBlueprint b) => new(
            new(b.Roadwheel, typeof(WheelBlueprint), new[] { "ModelPartGuid", "NumberPerAxle", "AxleOffset", "SpacingOnAxle", "Diameter", "Width", "GenerateColliders" }),
            new(b.RoadwheelMount, typeof(WheelMountBlueprint), new[] { "HeightOffset", "ForwardOffset", "SideOffset", "RearFacing", "Scale" }),
            new(b.Roadwheels, typeof(WheelMountArrayBlueprint), new[] { "Spacing", "Length", "Count", "InterleaveOverlapFraction", "GroupSpacing", "GroupingCount", "GroupingOffset", "ForwardOrigin", "HeightOrigin", "SideOrigin", "ForwardOffset", "HeightOffset", "SideOffset", "Angle", "SyncLength", "SpacingMode" }),
            new(b.Suspension, typeof(SuspensionBlueprint), new[] { "TargetAngle", "MaxUpTravelLimiter", "Damper", "ArmLength" }));
        internal void Apply(TrackPackedBlueprint b)
        { Wheel.Apply(b.Roadwheel); Mount.Apply(b.RoadwheelMount); Array.Apply(b.Roadwheels); Suspension.Apply(b.Suspension); }
    }
    private sealed class Entry
    {
        internal readonly WheelMountBlueprint Owner; // retain wrapper to prevent pointer reuse
        internal readonly PartLayoutHistory<Layout> History = new();
        internal Entry(WheelMountBlueprint owner) { Owner = owner; }
    }
    private static readonly Dictionary<IntPtr, Entry> Entries = new();

    [HarmonyPrefix, HarmonyPatch(typeof(TrackAssembly), nameof(TrackAssembly.Build))]
    private static void Before(TrackAssembly __instance)
    {
        try
        {
            var mount = __instance.Suspensions?.SharedMountBlueprint?.Blueprint;
            if (mount == null) return;
            if (!Entries.TryGetValue(mount.Pointer, out var entry))
            {
                if (Entries.Count > 256) Entries.Clear();
                Entries[mount.Pointer] = entry = new(mount);
            }
            string? part = mount.partGuid;
            var restore = entry.History.Begin(part == HydroPart.Guid);
            if (restore == null) return;
            var b = __instance.GetBlueprints();
            if (b.Roadwheel == null || b.RoadwheelMount == null || b.Roadwheels == null || b.Suspension == null) return;
            restore.Apply(b);
            if (Plugin.Diagnostics.Value) Plugin.ModLog.LogInfo($"[Hydro] LAYOUT RESTORED diameter={b.Roadwheel.Diameter:F3}m count={b.Roadwheels.Count} forward={b.Roadwheels.ForwardOffset}mm arm={b.Suspension.ArmLength}mm");
        }
        catch (Exception ex) { RuntimeHydro.Warn("Restore HPS wheel layout", ex); }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(TrackAssembly), nameof(TrackAssembly.Build))]
    private static void After(TrackAssembly __instance)
    {
        try
        {
            var mount = __instance.Suspensions?.SharedMountBlueprint?.Blueprint;
            if (mount == null || !Entries.TryGetValue(mount.Pointer, out var entry) || mount.partGuid != HydroPart.Guid) return;
            var b = __instance.GetBlueprints();
            if (b.Roadwheel == null || b.RoadwheelMount == null || b.Roadwheels == null || b.Suspension == null) return;
            entry.History.Remember(Layout.Capture(b));
        }
        catch (Exception ex) { RuntimeHydro.Warn("Remember HPS wheel layout", ex); }
    }
}
