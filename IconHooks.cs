using HarmonyLib;
using Sprocket.PartImporting;
using UnityEngine;

namespace SprocketHydropneumatic;

[HarmonyPatch]
internal static class IconHooks
{
    private static Texture2D? texture;
    private static Sprite? icon;
    private static bool attempted;

    [HarmonyPostfix, HarmonyPatch(typeof(PartDefinitionCardFactory), nameof(PartDefinitionCardFactory.CreateCard))]
    private static void Card(PartDefinition __0, PartDisplayCard __result)
    {
        if (__0?.guid != HydroPart.Guid || __result == null) return;
        try
        {
            if (!attempted)
            {
                attempted = true;
                var path = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!, "assets", "hydropneumatic-icon.png");
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false))
                    throw new InvalidOperationException("Could not decode hydropneumatic icon PNG.");
                icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100);
                texture.hideFlags = HideFlags.HideAndDontSave;
                icon.hideFlags = HideFlags.HideAndDontSave;
                Plugin.ModLog.LogInfo("[Hydro] Custom hydropneumatic part icon loaded.");
            }
            if (icon != null) __result.Icon = icon;
        }
        catch (Exception ex) { RuntimeHydro.Warn("Part icon", ex); }
    }
}
