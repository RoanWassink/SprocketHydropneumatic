using Sprocket.Vehicles;
using Sprocket.Vehicles.Tracks;
using Sprocket.Vehicles.Tracks.Editor;

namespace SprocketHydropneumatic;

internal static class HydroPart
{
    internal const string Guid = "75f3d8e2-6c19-4b27-9f50-481c6a8d02b4";

    internal static bool Selected(TrackAssembly track) =>
        track.Suspensions?.SharedMountBlueprint?.Blueprint?.partGuid == Guid;

    internal static TrackAssembly? SelectedTrack(TrackEditor editor, IVehicleGateway vehicle)
    {
        var objects = vehicle.ObjectReader.Items;
        int count = objects.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<VehicleObject>>().Count;
        for (int i = 0; i < count; i++)
        {
            var components = objects[i].Components;
            int n = components.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<VehicleComponent>>().Count;
            for (int j = 0; j < n; j++)
            {
                var track = components[j].TryCast<TrackAssembly>();
                if (track != null && track.BlueprintSlot?.Blueprint?.Pointer == editor.blueprint?.Pointer && Selected(track)) return track;
            }
        }
        return null;
    }
}
