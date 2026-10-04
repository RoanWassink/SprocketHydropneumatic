using HarmonyLib;
using Sprocket.Vehicles;
using Sprocket.Vehicles.Tracks;

namespace SprocketHydropneumatic;

[HarmonyPatch]
internal static class BalanceHooks
{
    private static int logged;
    [HarmonyPostfix, HarmonyPatch(typeof(VoluteSuspensionSpring), nameof(VoluteSuspensionSpring.Build))]
    private static void Built(VoluteSuspensionSpring __instance)
    {
        // ComponentID is the unique fileID from our own part. VehicleObject.GUID
        // identifies a runtime object and must not be compared to an asset GUID.
        if (__instance.ComponentID != "hydroAccumulator") return;
        try
        {
            // HVSS adapter has 15 degrees of one-sided travel. A hydraulic
            // cylinder needs compression AND extension around its loaded stand.
            var spring = __instance.results;
            float travel = 35f * MathF.PI / 180f;
            __instance.results = new SpringResults(spring.SpringConstant, travel, -travel);
            float mass = __instance.GetMass(MassType.RunningGear);
            if (!float.IsFinite(mass) || mass < 0) return;
            // Native HVSS: material 0.243/kg + assembly 16.20 per spring.
            // Precision cylinder, seals, accumulator and active hydraulic controls.
            __instance.SetCost(mass * 1.2f, MassType.RunningGear, CostType.Material);
            __instance.SetCost(160f, MassType.RunningGear, CostType.Assembly);
            if (Plugin.Diagnostics.Value && logged++ < 4) Plugin.ModLog.LogInfo($"[Hydro] BALANCE hydraulic unit mass={mass:F1}kg material={mass * 1.2f:F2} assembly=160.00");
        }
        catch (Exception ex) { RuntimeHydro.Warn("Hydraulic unit cost", ex); }
    }
}
