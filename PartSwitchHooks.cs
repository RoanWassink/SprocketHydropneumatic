using HarmonyLib;
using Sprocket.Vehicles.Tracks;
using Sprocket.Vehicles.Tracks.Editor;

namespace SprocketHydropneumatic;

[HarmonyPatch]
internal static class PartSwitchHooks
{
    // Native part changes destroy and recreate the mount/arm objects. The editor
    // caches those objects and uses them to recalculate bounds on subsequent builds.
    private sealed record EditorState(IntPtr Track, string? Part);
    private static readonly Dictionary<IntPtr, EditorState> Editors = new();
    private sealed record WheelState(WheelBlueprint Wheel, float Diameter, float Width);
    private static readonly Dictionary<IntPtr, WheelState> BeforeRebuild = new();

    [HarmonyPrefix, HarmonyPatch(typeof(TrackEditor), nameof(TrackEditor.OnComponentRebuilt))]
    private static void Refresh(TrackEditor __instance)
    {
        try
        {
            var track = __instance.Component;
            var array = track?.Suspensions;
            if (track == null || array == null) return;
            var part = array.SharedMountBlueprint?.Blueprint?.partGuid;
            Editors.TryGetValue(__instance.Pointer, out var old);
            bool wasHydro = old?.Track == track.Pointer && old.Part == HydroPart.Guid;
            if (Editors.Count > 128) Editors.Clear();
            Editors[__instance.Pointer] = new(track.Pointer, part);
            if (part != HydroPart.Guid && !wasHydro) return;
            var sharedWheel = array.SharedWheelBlueprint?.Blueprint;
            if (Plugin.Diagnostics.Value && part == HydroPart.Guid && sharedWheel != null)
                BeforeRebuild[__instance.Pointer] = new(sharedWheel, sharedWheel.Diameter, sharedWheel.Width);
            var mounts = array.Assemblies;
            int count = mounts.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<WheelMount>>().Count;
            if (count == 0) return;
            var selected = __instance.selectedWheelMount;
            var replacement = mounts[0];
            bool current = false;
            for (int i = 0; i < count; i++)
                if (selected != null && mounts[i].Pointer == selected.Pointer)
                { replacement = mounts[i]; current = true; break; }
            // Preserve a selected idler, sprocket or roller: only refresh the
            // suspension selection, whose arm belongs to the rebuilt mounts.
            if (selected != null && selected.ParentArray?.TryCast<WheelMountArray>()?.Pointer != array.Pointer &&
                selected.VehicleObject?.Released == false) return;
            var arms = replacement.Suspension?.arms;
            if (arms == null || arms.Length == 0) return;
            var arm = __instance.selectedSuspensionArm;
            bool armCurrent = false;
            for (int i = 0; i < arms.Length; i++)
                if (arm != null && arms[i].Pointer == arm.Pointer) { armCurrent = true; break; }
            if (current && armCurrent) return;
            __instance.selectedWheelMount = replacement;
            __instance.selectedSuspensionArm = arms[0];
            var wheel = array.SharedWheelBlueprint?.Blueprint;
            if (Plugin.Diagnostics.Value) Plugin.ModLog.LogInfo($"[Hydro] EDITOR REBOUND part={part} mounts={count} diameter={wheel?.Diameter ?? -1:F3}m scale={array.SharedMountBlueprint?.Blueprint?.Scale ?? -1:F3}");
        }
        catch (Exception ex) { RuntimeHydro.Warn("Part switch editor references", ex); }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(TrackEditor), nameof(TrackEditor.OnComponentRebuilt))]
    private static void Check(TrackEditor __instance)
    {
        if (!BeforeRebuild.Remove(__instance.Pointer, out var before)) return;
        try
        {
            if (before.Wheel.Diameter != before.Diameter || before.Wheel.Width != before.Width)
                Plugin.ModLog.LogWarning($"[Hydro] EDITOR WHEEL CHANGE diameter={before.Diameter:F3}->{before.Wheel.Diameter:F3} width={before.Width:F3}->{before.Wheel.Width:F3}");
            var mount = __instance.selectedWheelMount;
            var shared = __instance.Component?.Suspensions?.SharedWheelBlueprint?.Blueprint;
            if (mount?.WheelBlueprint?.Blueprint is {} wheel && shared != null && wheel.Pointer != shared.Pointer)
                Plugin.ModLog.LogWarning($"[Hydro] EDITOR WHEEL SLOT differs: instanceDiameter={wheel.Diameter:F3} sharedDiameter={shared.Diameter:F3}");
        }
        catch (Exception ex) { RuntimeHydro.Warn("Part switch wheel diagnostics", ex); }
    }
}
