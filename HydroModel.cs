using System.Text.Json;

namespace SprocketHydropneumatic;

// Game-independent control/geometry model. Distances are metres, angles radians.
public sealed class HydroSettings
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; }
    public bool IndependentControls { get; set; }
    public float FrontMin { get; set; } = -.10f;
    public float FrontMax { get; set; } = .10f;
    public float RearMin { get; set; } = -.10f;
    public float RearMax { get; set; } = .10f;
    // Equivalent wheel travel, not literal actuator travel: there is no cylinder mesh yet.
    public float Stroke { get; set; } = .30f;
    public float Reserve { get; set; } = .03f;
    public float Speed { get; set; } = .04f;
    public float Stiffness { get; set; } = 1f;
    public float Progression { get; set; } = 1f;
    public float Damping { get; set; } = 1.25f;

    public bool IsValid() => Version == 1 &&
        float.IsFinite(FrontMin) && float.IsFinite(FrontMax) &&
        float.IsFinite(RearMin) && float.IsFinite(RearMax) &&
        FrontMin >= -.5f && FrontMin <= 0 && FrontMax >= 0 && FrontMax <= .5f &&
        RearMin >= -.5f && RearMin <= 0 && RearMax >= 0 && RearMax <= .5f &&
        float.IsFinite(Stroke) && Stroke >= .02f && Stroke <= 1f &&
        float.IsFinite(Reserve) && Reserve >= 0 && Reserve <= .15f &&
        float.IsFinite(Speed) && Speed >= .005f && Speed <= .2f &&
        float.IsFinite(Stiffness) && Stiffness >= .25f && Stiffness <= 4f &&
        float.IsFinite(Progression) && Progression >= 0 && Progression <= 3f &&
        float.IsFinite(Damping) && Damping >= .25f && Damping <= 4f;

    public string Serialize() => JsonSerializer.Serialize(this);
    public static HydroSettings Parse(string json)
    {
        var settings = JsonSerializer.Deserialize<HydroSettings>(json);
        return settings?.IsValid() == true ? settings : throw new ArgumentException("Invalid or unsupported hydro settings");
    }
    public HydroSettings Copy() => Parse(Serialize());
}

// Remember HPS's own layout across temporary selections whose mechanical limits
// may clamp shared wheel dimensions. A first selection keeps the current layout.
public sealed class PartLayoutHistory<T> where T : class
{
    private bool wasHydro;
    private T? snapshot;
    public T? Begin(bool hydro)
    {
        T? restore = hydro && !wasHydro ? snapshot : null;
        wasHydro = hydro;
        return restore;
    }
    public void Remember(T value) { if (wasHydro) snapshot = value; }
}

public sealed class HeightCommands
{
    public float Front { get; private set; }
    public float Rear { get; private set; }
    public bool Resetting { get; private set; }

    public void Reset() => Resetting = true;
    public void Step(HydroSettings s, int frontInput, int rearInput, float dt)
    {
        if (!s.IsValid() || !float.IsFinite(dt) || dt <= 0) return;
        dt = Math.Min(dt, .05f); // No catch-up jump after pauses or a stalled frame.
        frontInput = Math.Clamp(frontInput, -1, 1);
        rearInput = Math.Clamp(rearInput, -1, 1);
        if (frontInput != 0 || rearInput != 0) Resetting = false;
        float step = s.Speed * dt;
        float half = s.Stroke * .5f;
        Front = Math.Clamp(Front, Math.Max(s.FrontMin, -half), Math.Min(s.FrontMax, half));
        Rear = Math.Clamp(Rear, Math.Max(s.RearMin, -half), Math.Min(s.RearMax, half));
        if (Resetting)
        {
            Front = TowardsZero(Front, step);
            Rear = TowardsZero(Rear, step);
            if (Front == 0 && Rear == 0) Resetting = false;
        }
        else if (s.IndependentControls)
        {
            Front = Math.Clamp(Front + frontInput * step, Math.Max(s.FrontMin, -half), Math.Min(s.FrontMax, half));
            Rear = Math.Clamp(Rear + rearInput * step, Math.Max(s.RearMin, -half), Math.Min(s.RearMax, half));
        }
        else
        {
            float fMin = Math.Max(s.FrontMin, -half), fMax = Math.Min(s.FrontMax, half);
            float rMin = Math.Max(s.RearMin, -half), rMax = Math.Min(s.RearMax, half);
            float heave = Math.Clamp((Front + Rear) * .5f + frontInput * step, Math.Max(fMin, rMin), Math.Min(fMax, rMax));
            float pitch = (Rear - Front) * .5f + rearInput * step;
            // Page Up raises rear and lowers front by the SAME amount. Stop both
            // at the first limit, so saturation cannot turn tilt into a level lift.
            pitch = Math.Clamp(pitch, Math.Max(heave - fMax, rMin - heave), Math.Min(heave - fMin, rMax - heave));
            Front = heave - pitch; Rear = heave + pitch;
        }
    }

    private static float TowardsZero(float v, float step) => Math.Abs(v) <= step ? 0 : v - Math.Sign(v) * step;
    public float At(float frontFraction) => Rear + (Front - Rear) * Math.Clamp(frontFraction, 0f, 1f);
}

public readonly record struct SpringGeometry(float Length, float ReferenceAngle, float MinAngle, float MaxAngle)
{
    // Native stops may straddle a cosine turning point (e.g. a -60 degree
    // design arm with +/-35 degree travel). Limit hydraulic commands to the
    // monotonic side containing the neutral angle; don't change native stops
    // or move the neutral stance to make an ambiguous geometry look valid.
    public SpringGeometry ConstrainToReferenceBranch()
    {
        if (!float.IsFinite(ReferenceAngle) || !float.IsFinite(MinAngle) || !float.IsFinite(MaxAngle) ||
            ReferenceAngle == 0 || ReferenceAngle <= -MathF.PI || ReferenceAngle >= MathF.PI)
            return this;
        const float margin = .0101f;
        float lower = ReferenceAngle > 0 ? margin : -MathF.PI + margin;
        float upper = ReferenceAngle > 0 ? MathF.PI - margin : -margin;
        return this with { MinAngle = Math.Max(MinAngle, lower), MaxAngle = Math.Min(MaxAngle, upper) };
    }

    public bool Valid => float.IsFinite(Length) && Length > .01f &&
        float.IsFinite(ReferenceAngle) && float.IsFinite(MinAngle) && float.IsFinite(MaxAngle) &&
        MinAngle < MaxAngle && ReferenceAngle >= MinAngle && ReferenceAngle <= MaxAngle &&
        ((MinAngle > .01f && MaxAngle < MathF.PI - .01f) ||
         (MaxAngle < -.01f && MinAngle > -MathF.PI + .01f));

    public (float Min, float Max) HeightRange(float reserve)
    {
        if (!Valid) return (0, 0);
        float d = Length * MathF.Cos(ReferenceAngle);
        float a = Length * MathF.Cos(MinAngle), b = Length * MathF.Cos(MaxAngle);
        float lower = Math.Min(a, b), upper = Math.Max(a, b);
        // Retain the editor's neutral stand, including if it is close to a native stop.
        reserve = Math.Clamp(reserve, 0, Math.Max(0, Math.Min(d - lower, upper - d)));
        return (lower + reserve - d, upper - reserve - d);
    }

    public float AngleForHeight(float height, float reserve)
    {
        if (!Valid) return ReferenceAngle;
        var range = HeightRange(reserve);
        height = Math.Clamp(height, range.Min, range.Max);
        // Native GetPosition: x = origin.x + L*sin(a), y = origin.y - L*cos(a).
        float cosine = Math.Clamp(MathF.Cos(ReferenceAngle) + height / Length, -1, 1);
        return Math.Clamp(Math.Sign(ReferenceAngle) * MathF.Acos(cosine), MinAngle, MaxAngle);
    }
}

public static class BeltSupport
{
    // Native samples run along belt-local X; either side may reverse that axis.
    public static float FrontFraction(float x, float minX, float maxX, float firstFraction, float lastFraction)
    {
        float t = Math.Clamp((x - minX) / (maxX - minX), 0, 1);
        return Math.Clamp(firstFraction + (lastFraction - firstFraction) * t, 0, 1);
    }
    public static float RestHeight(float baseline, float desiredHullRise, float min, float max)
        => baseline - Math.Clamp(desiredHullRise, min, max);
}

public static class HydroSpring
{
    // Bounded progressive angular-spring approximation; does not claim full gas/oil simulation.
    // Preserve the neutral preload when changing stiffness, instead of raising the tank for free.
    public static (float RestAngle, float StiffnessRatio) Fit(float nativeRest, SpringGeometry geometry,
        float height, float actualAngle, HydroSettings settings)
    {
        float target = geometry.AngleForHeight(height, settings.Reserve);
        float compression = Math.Max(0, geometry.Length * (MathF.Cos(target) - MathF.Cos(actualAngle)));
        float fraction = Math.Clamp(compression / Math.Max(.02f, settings.Stroke * .5f), 0, 1);
        float ratio = Math.Clamp(settings.Stiffness * (1 + settings.Progression * fraction * fraction), .25f, 4);
        float rest = target + (nativeRest - geometry.ReferenceAngle) / ratio;
        return (rest, ratio);
    }
}
