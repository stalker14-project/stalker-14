namespace Content.Shared.Radiation.Events;

/// <summary>
///     Raised on entity when it was irradiated
///     by some radiation source.
/// </summary>
public readonly record struct OnIrradiatedEvent(float FrameTime, float RadsPerSecond, EntityUid? Origin)
{
    public readonly float FrameTime = FrameTime;

    public readonly float RadsPerSecond = RadsPerSecond;

    public readonly EntityUid? Origin = Origin;
    public readonly Dictionary<string, float> DamageTypes = new()// stalker-changes start
    {
        { "radiation", 1 }
    }; // stalker-changes end

    public float TotalRads => RadsPerSecond * FrameTime;
}
