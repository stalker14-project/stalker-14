namespace Content.Shared.Radiation.Events;

/// <summary>
///     Raised on entity when it was irradiated
///     by some radiation source.
/// </summary>
public readonly record struct OnIrradiatedEvent(float FrameTime, Dictionary<string, float> DamageTypes, EntityUid? Origin) // stalker-changes
{
    public readonly float FrameTime = FrameTime;

    public readonly Dictionary<string, float> DamageTypes = DamageTypes; // stalker-changes

    public readonly EntityUid? Origin = Origin;
}
