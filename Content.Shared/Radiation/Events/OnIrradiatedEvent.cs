namespace Content.Shared.Radiation.Events;

/// <summary>
///     Raised on entity when it was irradiated
///     by some radiation source.
/// </summary>
public readonly record struct OnIrradiatedEvent(float FrameTime, float RadsPerSecond, EntityUid Origin)
{
    public readonly float FrameTime = FrameTime;

    public readonly float RadsPerSecond = RadsPerSecond;

    public readonly EntityUid Origin = Origin;
    public readonly Dictionary<string, float> DamageTypes; // stalker-changes

    public float TotalRads => RadsPerSecond * FrameTime;
    public OnIrradiatedEvent(float frameTime, Dictionary<string, float> damageTypes) // stalker-changes
    {
        FrameTime = frameTime;
        DamageTypes = damageTypes; // stalker-changes
    }
}
