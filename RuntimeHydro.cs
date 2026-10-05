using BepInEx.Configuration;
using HarmonyLib;
using Sprocket;
using Sprocket.ContinuousTracks;
using Sprocket.Gameplay.VehicleControl;
using Sprocket.Vehicles;
using Sprocket.Vehicles.Powertrains;
using Sprocket.Vehicles.Tracks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using NativeSpring = Sprocket.ContinuousTracks.SuspensionSpring;

namespace SprocketHydropneumatic;

internal static class RuntimeHydro
{
    private sealed class TrackState
    {
        internal readonly TrackBehaviour Track;
        internal readonly TrackBlueprint Blueprint;
        internal int BodyId;
        internal bool Connected;
        internal readonly NativeSpring[] Original;
        internal readonly SpringGeometry[] Geometry;
        internal readonly float[] Fractions;
        internal float[] RestHeights = Array.Empty<float>(), SampleFractions = Array.Empty<float>();
        internal bool WaitingForSupport;
        internal bool Modified, Failed;
        internal float LastLog;
        internal TrackState(TrackBehaviour track, TrackBlueprint blueprint, int bodyId,
            NativeSpring[] original, SpringGeometry[] geometry, float[] fractions)
        { Track = track; Blueprint = blueprint; BodyId = bodyId; Original = original; Geometry = geometry; Fractions = fractions; }
    }

    private static readonly Dictionary<IntPtr, TrackState> Tracks = new();
    private static readonly Dictionary<int, HeightCommands> Commands = new();
    private static readonly HashSet<int> ActiveBodies = new();
    private static readonly HashSet<string> Warnings = new();
    private static int controlledBody;
    private static int inputFrame = -100, lastCaptureFrame = -1, frontInput, rearInput;
    private static bool resetInput;

    internal static void Configure(ConfigFile config) => HydroControls.Configure(config);
    internal static string[] ControlLabels(bool independent) => new[]
    {
        "Change bindings in Settings / keybinds (HPS labels)",
        (independent ? "Raise front: " : "Raise tank: ") + HydroControls.Display(HydroControls.Raise),
        (independent ? "Lower front: " : "Lower tank: ") + HydroControls.Display(HydroControls.Lower),
        (independent ? "Raise rear (Tilt forward): " : "Lean forward: ") + HydroControls.Display(HydroControls.Forward),
        (independent ? "Lower rear (Tilt back): " : "Lean backward: ") + HydroControls.Display(HydroControls.Backward),
        "Return to normal height: " + HydroControls.Display(HydroControls.Neutral)
    };

    internal static void Warn(string area, Exception ex)
    { if (Warnings.Add(area)) Plugin.ModLog.LogWarning($"[Hydro] {area}: {ex.Message}"); }

    internal static void Bind(TrackAssembly assembly, Rigidbody body, ITrackBehaviour result)
    {
        // Ordinary parts are never registered, even if an old v0.1 blueprint has Enabled=true.
        if (!HydroPart.Selected(assembly)) return;
        if (Plugin.Diagnostics.Value) Plugin.ModLog.LogInfo("[Hydro] Binding independent hydropneumatic part.");
        var track = result?.TryCast<TrackBehaviour>();
        var blueprint = assembly.BlueprintSlot?.Blueprint;
        if (track == null || blueprint == null || body == null || track.suspensionsManaged == null) return;
        var springs = track.suspensionsManaged;
        if (springs.Length == 0) return;
        var original = new NativeSpring[springs.Length];
        var geometry = new SpringGeometry[springs.Length];
        var positions = new float[springs.Length];
        float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
        var transforms = track.suspensionArmTransforms;
        // Resolve ordinary object references AFTER the native controller has been built.
        // Do not patch EnableSuspensionPhysics: its ref struct is a boxed interop wrapper
        // and crashed v0.1 in the ReversePInvoke/native exception bridge.
        var referenceAngles = new Dictionary<IntPtr, float>();
        var mounts = assembly.Suspensions.Assemblies;
        int mountCount = mounts.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<WheelMount>>().Count;
        for (int m = 0; m < mountCount; m++)
        {
            var arms = mounts[m].Suspension?.arms;
            if (arms == null) continue;
            for (int a = 0; a < arms.Length; a++)
            {
                var arm = arms[a];
                if (arm?.transform == null) continue;
                float targetAngle = arm.lockArmAngle ? -arm.armPreBendAngle : arm.BlueprintSlot.Blueprint.TargetAngle;
                float reference = (MathF.PI * .5f + targetAngle * MathF.PI / 180f) * (arm.ForwardFacing ? 1 : -1);
                referenceAngles[arm.transform.Pointer] = reference;
            }
        }
        for (int i = 0; i < springs.Length; i++)
        {
            original[i] = springs[i];
            var spring = original[i];
            var transform = transforms != null && i < transforms.Length ? transforms[i] : null;
            float reference = Math.Clamp(spring.restAngle, spring.minAngle, spring.maxAngle);
            if (transform != null && referenceAngles.TryGetValue(transform.Pointer, out float designAngle)) reference = designAngle;
            reference = Math.Clamp(reference, spring.minAngle, spring.maxAngle);
            geometry[i] = new SpringGeometry(spring.armLength, reference, spring.minAngle, spring.maxAngle).ConstrainToReferenceBranch();
            // Front is +Z in the VEHICLE frame; belt-local X may be reversed on one side.
            positions[i] = transform != null ? body.transform.InverseTransformPoint(transform.position).z : float.NaN;
            minZ = Math.Min(minZ, positions[i]); maxZ = Math.Max(maxZ, positions[i]);
        }
        if (!float.IsFinite(minZ) || !float.IsFinite(maxZ) || maxZ - minZ < .01f || geometry.Any(g => !g.Valid))
        {
            int invalid = Array.FindIndex(geometry, g => !g.Valid);
            string detail = invalid >= 0 ? $" spring={invalid} length={geometry[invalid].Length:F3} reference={geometry[invalid].ReferenceAngle:F3} commandStops={geometry[invalid].MinAngle:F3}..{geometry[invalid].MaxAngle:F3}" : "";
            Plugin.ModLog.LogWarning($"[Hydro] Unsupported geometry: springs={springs.Length} transforms={transforms?.Length ?? 0} vehicleZ={minZ:F3}..{maxZ:F3}{detail}; this track remains vanilla.");
            return;
        }
        var fractions = positions.Select(z => (z - minZ) / (maxZ - minZ)).ToArray();
        int limited = Enumerable.Range(0, geometry.Length).Count(i => geometry[i].MinAngle != original[i].minAngle || geometry[i].MaxAngle != original[i].maxAngle);
        if (limited > 0)
            Plugin.ModLog.LogInfo($"[Hydro] Hydraulic travel limited to neutral arm branch for {limited}/{geometry.Length} springs; native geometry and stops preserved.");
        Tracks[track.Pointer] = new(track, blueprint, body.GetInstanceID(), original, geometry, fractions);
        var settings = Profiles.Get(blueprint);
        settings.Enabled = true;
        if (settings.Enabled)
        {
            var ranges = geometry.Select(g => g.HeightRange(settings.Reserve)).ToArray();
            if (Plugin.Diagnostics.Value) Plugin.ModLog.LogInfo($"[Hydro] BOUND track={track.Pointer} body={body.Pointer} springs={springs.Length} commonPhysicalRange={ranges.Max(r=>r.Min)*1000:0}..{ranges.Min(r=>r.Max)*1000:0}mm");
        }
    }

    private static bool EnsureSupport(TrackState state)
    {
        if (state.RestHeights.Length > 0) return true;
        // The sampler's slice is assigned by native TrackBehaviour.FixedUpdate,
        // after CreateTrackController and movement-register registration.
        var info = state.Track.UpdateInfo;
        var restHeights = info.patchBeltRestHeights;
        var samples = info.beltSamplePoints;
        if (restHeights.Length == 0 || samples.Length == 0)
        {
            if (!state.WaitingForSupport)
                if (Plugin.Diagnostics.Value) Plugin.ModLog.LogInfo($"[Hydro] Waiting for native support buffers track={state.Track.Pointer} heights={restHeights.Length} samples={samples.Length}");
            state.WaitingForSupport = true;
            return false;
        }
        if (restHeights.Length != samples.Length)
            throw new InvalidOperationException($"Mismatched initialized support buffers: heights={restHeights.Length}, samples={samples.Length}");
        var original = state.Original;
        int minIndex = Enumerable.Range(0, original.Length).MinBy(i => original[i].origin.x);
        int maxIndex = Enumerable.Range(0, original.Length).MaxBy(i => original[i].origin.x);
        float firstX = original[minIndex].origin.x, lastX = original[maxIndex].origin.x;
        if (!float.IsFinite(firstX) || !float.IsFinite(lastX) || lastX - firstX < .01f)
            throw new InvalidOperationException("Missing longitudinal spring geometry.");
        var baseline = new float[restHeights.Length];
        var fractions = new float[restHeights.Length];
        for (int i = 0; i < baseline.Length; i++)
        {
            baseline[i] = restHeights[i];
            fractions[i] = BeltSupport.FrontFraction(samples[i].origin.x, firstX, lastX, state.Fractions[minIndex], state.Fractions[maxIndex]);
            if (!float.IsFinite(baseline[i]) || !float.IsFinite(fractions[i]))
                throw new InvalidOperationException("Invalid belt support profile.");
        }
        state.RestHeights = baseline; state.SampleFractions = fractions;
        if (Plugin.Diagnostics.Value) Plugin.ModLog.LogInfo($"[Hydro] SUPPORT READY track={state.Track.Pointer} points={baseline.Length}");
        return true;
    }

    internal static void CaptureInput(VehicleController controller, GameTime time)
    {
        HydroControls.TryMigrate();
        if (lastCaptureFrame == Time.frameCount) return;
        controlledBody = 0; frontInput = rearInput = 0; resetInput = false;
        if (!Plugin.Enabled.Value || !Application.isFocused || time.PauseState != PauseState.Unpaused || time.TimeScale <= 0 || time.DeltaTime <= 0) return;
        var selected = EventSystem.current?.currentSelectedGameObject;
        if (selected != null && ((selected.GetComponent<TMP_InputField>()?.isFocused ?? false) ||
            (selected.GetComponent<UnityEngine.UI.InputField>()?.isFocused ?? false))) return;
        var vehicle = controller.ControlledVehicle?.TryCast<VehicleBehaviour>();
        if (vehicle?.Rigidbody == null) return;
        int bodyId = vehicle.Rigidbody.GetInstanceID();
        if (!ActiveBodies.Contains(bodyId)) return;
        lastCaptureFrame = Time.frameCount;
        controlledBody = bodyId;
        inputFrame = Time.frameCount;
        frontInput = (HydroControls.Raise.IsHeld ? 1 : 0) - (HydroControls.Lower.IsHeld ? 1 : 0);
        rearInput = (HydroControls.Forward.IsHeld ? 1 : 0) - (HydroControls.Backward.IsHeld ? 1 : 0);
        resetInput = HydroControls.Neutral.WasPressedThisFrame;
    }

    // Called BEFORE the central movement register schedules any new physics work.
    // Complete its previous combined handle before touching shared NativeArrays.
    internal static void Physics(VehicleMovementRegister register, float dt)
    {
        if (!float.IsFinite(dt) || dt <= 0 || Tracks.Count == 0 ||
            !Tracks.Values.Any(s => s.Modified || (!s.Failed && Plugin.Enabled.Value && Profiles.Get(s.Blueprint).Enabled))) return;
        register.currentUpdateHandle.Complete();
        ActiveBodies.Clear();
        var active = new List<TrackState>();
        // The designer and measurement tools also build TrackBehaviours. Resolve
        // only tracks actually attached to a registered vehicle physics module.
        for (int i = 0; i < register.register.Count; i++)
        {
            var module = register.register[i];
            if (module?.body == null) continue;
            int bodyId = module.body.GetInstanceID();
            void Connect(Sprocket.Powertrains.Propulsion.IPropulsion propulsion)
            {
                var track = propulsion?.TryCast<TrackBehaviour>();
                if (track == null || !Tracks.TryGetValue(track.Pointer, out var state)) return;
                state.BodyId = bodyId;
                ActiveBodies.Add(bodyId);
                if (!active.Contains(state)) active.Add(state);
                if (!state.Connected)
                {
                    state.Connected = true;
                    var s = state.Original[0];
                    if (Plugin.Diagnostics.Value) Plugin.ModLog.LogInfo($"[Hydro] ACTIVE track={track.Pointer} bodyId={bodyId} arm={s.armLength:0.000}m rest={s.restAngle:0.000} reference={state.Geometry[0].ReferenceAngle:0.000} stops={s.minAngle:0.000}..{s.maxAngle:0.000} K={s.springConstant:0.0}");
                }
            }
            Connect(module.leftPropulsion); Connect(module.rightPropulsion);
        }
        var stepped = new HashSet<int>();
        foreach (var state in active)
        {
            if (state.Failed) continue;
            try
            {
                if (!EnsureSupport(state)) continue;
                var settings = Profiles.Get(state.Blueprint);
                if (!Plugin.Enabled.Value || !settings.Enabled || !settings.IsValid())
                { Restore(state); Commands.Remove(state.BodyId); continue; }
                if (!Commands.TryGetValue(state.BodyId, out var command)) Commands[state.BodyId] = command = new();
                if (stepped.Add(state.BodyId))
                {
                    bool fresh = Application.isFocused && Time.frameCount - inputFrame <= 1 && state.BodyId == controlledBody;
                    if (fresh && resetInput) { command.Reset(); resetInput = false; }
                    var bounded = settings.Copy();
                    var allGeometry = active.Where(t => t.BodyId == state.BodyId).SelectMany(t => t.Geometry);
                    float physicalMin = allGeometry.Max(g => g.HeightRange(settings.Reserve).Min);
                    float physicalMax = allGeometry.Min(g => g.HeightRange(settings.Reserve).Max);
                    bounded.FrontMin = Math.Max(bounded.FrontMin, physicalMin);
                    bounded.RearMin = Math.Max(bounded.RearMin, physicalMin);
                    bounded.FrontMax = Math.Min(bounded.FrontMax, physicalMax);
                    bounded.RearMax = Math.Min(bounded.RearMax, physicalMax);
                    command.Step(bounded, fresh ? frontInput : 0, fresh ? rearInput : 0, dt);
                }
                Update(state, settings, command, dt);
            }
            catch (Exception ex)
            {
                try { Restore(state); } catch (Exception restore) { Warn("Restore after physics failure", restore); }
                state.Failed = true;
                Warn("Physics disabled for track " + state.Track.Pointer, ex);
            }
        }
    }

    private static void Update(TrackState state, HydroSettings settings, HeightCommands command, float dt)
    {
        var native = state.Track.springs;
        var current = state.Track.springStates;
        if (native.Length != state.Original.Length || current.Length != state.Original.Length)
            throw new InvalidOperationException("Track buffers changed; re-enter simulation to bind again.");
        var changed = new NativeSpring[state.Original.Length];
        float minHeight = float.PositiveInfinity, maxHeight = float.NegativeInfinity;
        for (int i = 0; i < changed.Length; i++)
        {
            var spring = state.Original[i];
            var geometry = state.Geometry[i];
            var range = geometry.HeightRange(settings.Reserve);
            float requested = command.At(state.Fractions[i]);
            float height = Math.Clamp(requested, range.Min, range.Max);
            var fit = HydroSpring.Fit(spring.restAngle, geometry, height, current[i].angle, settings);
            float targetK = spring.springConstant * fit.StiffnessRatio;
            // Smooth the coefficient as terrain changes to avoid per-step stiffness jumps.
            float previousK = native[i].springConstant;
            float blend = Math.Clamp(dt * 5f, 0, 1);
            float appliedK = previousK + (targetK - previousK) * blend;
            float appliedRatio = spring.springConstant > 0 ? appliedK / spring.springConstant : 1;
            float target = geometry.AngleForHeight(height, settings.Reserve);
            spring.restAngle = target + (spring.restAngle - geometry.ReferenceAngle) / Math.Max(.25f, appliedRatio);
            spring.springConstant = appliedK;
            spring.damping *= settings.Damping;
            if (!float.IsFinite(spring.restAngle) || !float.IsFinite(spring.springConstant) || spring.springConstant <= 0 ||
                !float.IsFinite(spring.damping)) throw new InvalidOperationException("Invalid spring parameters.");
            changed[i] = spring;
            minHeight = Math.Min(minHeight, height); maxHeight = Math.Max(maxHeight, height);
        }
        state.Modified = true;
        // The hull solver uses belt-rest heights plus suspensionPreload, rather
        // than individual spring restAngle, to compute contact compression.
        // Lower local wheel/belt rest Y to raise the hull through native physics.
        var support = state.Track.UpdateInfo.patchBeltRestHeights;
        if (support.Length != state.RestHeights.Length)
            throw new InvalidOperationException("Belt support buffer changed.");
        float commonMin = state.Geometry.Max(g => g.HeightRange(settings.Reserve).Min);
        float commonMax = state.Geometry.Min(g => g.HeightRange(settings.Reserve).Max);
        if (commonMin > commonMax) throw new InvalidOperationException("No shared mechanical travel range.");
        for (int i = 0; i < support.Length; i++)
            support[i] = BeltSupport.RestHeight(state.RestHeights[i], command.At(state.SampleFractions[i]), commonMin, commonMax);
        for (int i = 0; i < changed.Length; i++)
        {
            native[i] = changed[i]; // NativeArray value type: explicitly write back.
            state.Track.suspensionsManaged[i] = changed[i];
        }
        if (Plugin.Diagnostics.Value && Time.unscaledTime - state.LastLog > 2 && state.BodyId == controlledBody &&
            (frontInput != 0 || rearInput != 0 || command.Resetting))
        {
            state.LastLog = Time.unscaledTime;
            var jobSprings = state.Track.UpdateInfo.springs;
            if (jobSprings.Length != changed.Length || Math.Abs(jobSprings[0].restAngle - changed[0].restAngle) > .0001f)
                throw new InvalidOperationException("Spring write does not reach the track's physics job.");
            Plugin.ModLog.LogInfo($"[Hydro] COMMAND front={command.Front*1000:0}mm rear={command.Rear*1000:0}mm applied={minHeight*1000:0}..{maxHeight*1000:0}mm track={state.Track.Pointer} bodyId={state.BodyId} rest={jobSprings[0].restAngle:0.000} actual={current[0].angle:0.000} K={jobSprings[0].springConstant:0.0} support0={support[0]:0.000} base0={state.RestHeights[0]:0.000}");
        }
    }

    private static void Restore(TrackState state)
    {
        if (!state.Modified) return;
        var native = state.Track.springs;
        if (native.Length != state.Original.Length) return;
        for (int i = 0; i < state.Original.Length; i++)
        {
            native[i] = state.Original[i];
            state.Track.suspensionsManaged[i] = state.Original[i];
        }
        var support = state.Track.UpdateInfo.patchBeltRestHeights;
        if (support.Length == state.RestHeights.Length)
            for (int i = 0; i < support.Length; i++) support[i] = state.RestHeights[i];
        state.Modified = false;
    }

    internal static void ReleaseTrack(IntPtr track)
    {
        if (Tracks.Remove(track, out var state) && !Tracks.Values.Any(s => s.BodyId == state.BodyId)) Commands.Remove(state.BodyId);
    }
    internal static void Clear()
    { Tracks.Clear(); Commands.Clear(); ActiveBodies.Clear(); controlledBody = 0; inputFrame = -100; }
}

[HarmonyPatch]
internal static class RuntimeHooks
{
    [HarmonyPostfix, HarmonyPatch(typeof(TrackAssembly), nameof(TrackAssembly.CreateTrackController))]
    private static void Track(TrackAssembly __instance, Rigidbody __0, ITrackBehaviour __result)
    { try { RuntimeHydro.Bind(__instance, __0, __result); } catch (Exception ex) { RuntimeHydro.Warn("Track binding", ex); } }

    [HarmonyPostfix, HarmonyPatch(typeof(VehicleController), nameof(VehicleController.UpdateControl))]
    private static void Input(VehicleController __instance, GameTime __0)
    { try { RuntimeHydro.CaptureInput(__instance, __0); } catch (Exception ex) { RuntimeHydro.Warn("Input", ex); } }

    [HarmonyPrefix, HarmonyPatch(typeof(VehicleMovementRegister), nameof(VehicleMovementRegister.FixedUpdate))]
    private static void Step(VehicleMovementRegister __instance, float __0)
    { try { RuntimeHydro.Physics(__instance, __0); } catch (Exception ex) { RuntimeHydro.Warn("Physics scheduling", ex); } }

    [HarmonyPostfix, HarmonyPatch(typeof(TrackBehaviour), nameof(TrackBehaviour.Release))]
    private static void Released(TrackBehaviour __instance) => RuntimeHydro.ReleaseTrack(__instance.Pointer);

    [HarmonyPostfix, HarmonyPatch(typeof(VehicleMovementRegister), nameof(VehicleMovementRegister.Dispose))]
    private static void Dispose() => RuntimeHydro.Clear();
}
