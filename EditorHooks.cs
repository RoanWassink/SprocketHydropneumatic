using HarmonyLib;
using Il2CppInterop.Runtime;
using Sprocket.UI;
using Sprocket.Vehicles;
using Sprocket.Vehicles.Tracks;
using Sprocket.Vehicles.Tracks.Editor;

namespace SprocketHydropneumatic;

[HarmonyPatch]
internal static class EditorHooks
{
    [HarmonyPostfix, HarmonyPatch(typeof(TrackEditor), nameof(TrackEditor.OnGUI))]
    private static void Draw(TrackEditor __instance, IGUILayout __0)
    {
        if (!Plugin.Enabled.Value || !TrackEditor.suspensionShown) return;
        try
        {
            var track = __instance.Component;
            if (track == null || !HydroPart.Selected(track)) return;
            var ui = __0.TryCast<IGUIElementDrawer>();
            var blueprint = __instance.blueprint;
            if (ui == null || blueprint == null) return;
            var vehicle = track.Vehicle;
            var settings = Profiles.Get(blueprint);
            settings.Enabled = true;
            ui.Header("Hydropneumatic suspension");
            ui.EndRow();
            var defaultTip = new UITooltip("HPS defaults", "Reset only hydraulic limits and ride settings. Keep the wheels and normal suspension geometry.");
            ui.Button("Reset HPS settings", DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction>((Action)(() =>
                Apply(__instance, vehicle, new HydroSettings { Enabled = true }))), ref defaultTip);
            ui.EndRow();
            ui.Header("Controls");
            ui.EndRow();
            ui.ToggleField("Independent front and rear controls", settings.IndependentControls,
                DelegateSupport.ConvertDelegate<Il2CppSystem.Action<bool>>((Action<bool>)(enabled =>
                {
                    var next = Profiles.Get(__instance.blueprint).Copy();
                    next.IndependentControls = enabled;
                    Apply(__instance, vehicle, next);
                })), "Off: HPS Raise/Lower adjusts tank height; Tilt forward/back adjusts pitch. On: Raise/Lower controls the front; Tilt forward/back controls the rear. Bindings: Settings keybind menu, HPS labels.");
            ui.EndRow();
            foreach (var line in RuntimeHydro.ControlLabels(settings.IndependentControls))
            {
                ui.EndRow(); ui.InfoField(line, Math.Max(1, ui.ColumnCount)); ui.EndRow();
            }
            ui.Header("Front height limits");
            Slider(ui, __instance, vehicle, settings, "Maximum lowering (mm)", -settings.FrontMin * 1000, 0, 500, (s,v) => s.FrontMin = -v * .001f);
            Slider(ui, __instance, vehicle, settings, "Maximum raising (mm)", settings.FrontMax * 1000, 0, 500, (s,v) => s.FrontMax = v * .001f);
            ui.Header("Rear height limits");
            Slider(ui, __instance, vehicle, settings, "Maximum lowering (mm)", -settings.RearMin * 1000, 0, 500, (s,v) => s.RearMin = -v * .001f);
            Slider(ui, __instance, vehicle, settings, "Maximum raising (mm)", settings.RearMax * 1000, 0, 500, (s,v) => s.RearMax = v * .001f);
            ui.Header("Height adjustment");
            Slider(ui, __instance, vehicle, settings, "Total wheel travel (mm)", settings.Stroke * 1000, 20, 1000, (s,v) => s.Stroke = v * .001f);
            Slider(ui, __instance, vehicle, settings, "Travel kept in reserve (mm)", settings.Reserve * 1000, 0, 150, (s,v) => s.Reserve = v * .001f);
            Slider(ui, __instance, vehicle, settings, "Height change speed (mm/s)", settings.Speed * 1000, 5, 200, (s,v) => s.Speed = v * .001f);
            ui.Header("Ride feel");
            Slider(ui, __instance, vehicle, settings, "Spring firmness", settings.Stiffness, .25f, 4, (s,v) => s.Stiffness = v);
            Slider(ui, __instance, vehicle, settings, "Firmness when compressed", settings.Progression, 0, 3, (s,v) => s.Progression = v);
            Slider(ui, __instance, vehicle, settings, "Bounce damping", settings.Damping, .25f, 4, (s,v) => s.Damping = v);
        }
        catch (Exception ex) { RuntimeHydro.Warn("Suspension editor", ex); }
    }

    private static void Slider(IGUIElementDrawer ui, TrackEditor editor, IVehicleGateway vehicle,
        HydroSettings settings, string label, float value, float min, float max, Action<HydroSettings,float> setter)
    {
        ui.EndRow();
        ui.InfoField(label, Math.Max(1, ui.ColumnCount));
        ui.EndRow();
        ui.Slider("", value, min, max, DelegateSupport.ConvertDelegate<Il2CppSystem.Action<float>>((Action<float>)(v =>
        {
            // Read fresh state: several UI callbacks can run before redraw.
            var next = Profiles.Get(editor.blueprint).Copy();
            setter(next, Math.Clamp(v, min, max));
            Apply(editor, vehicle, next);
        })));
        ui.EndRow();
    }

    private static void Apply(TrackEditor editor, IVehicleGateway vehicle, HydroSettings settings)
    {
        if (!settings.IsValid()) return;
        var objects = vehicle.ObjectReader.Items;
        int objectCount = objects.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<VehicleObject>>().Count;
        for (int i = 0; i < objectCount; i++)
        {
            var components = objects[i].Components;
            int componentCount = components.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<VehicleComponent>>().Count;
            for (int j = 0; j < componentCount; j++)
            {
                var track = components[j].TryCast<TrackAssembly>();
                if (track?.BlueprintSlot?.Blueprint is not {} blueprint || !HydroPart.Selected(track)) continue;
                Profiles.Set(blueprint, settings.Copy());
                track.BlueprintSlot.MarkModified();
            }
        }
        Profiles.Set(editor.blueprint, settings.Copy());
        editor.blueprint.MarkModified();
        // The native slider owns a Property edit until pointer-up. Rebuilding the
        // panel here replaces that Property mid-drag and makes EndEdit fail.
    }
}
