namespace Content.Shared.Radiation.Events;

/// <summary>
///     Raised on entity when it was irradiated
///     by some radiation source.
/// </summary>
public sealed class OnIrradiatedEvent : EntityEventArgs
{
    public readonly float FrameTime;

    public readonly Dictionary<string, float> DamageTypes; // stalker-changes

    public OnIrradiatedEvent(float frameTime, Dictionary<string, float> damageTypes) // stalker-changes
    {
        FrameTime = frameTime;
        DamageTypes = damageTypes; // stalker-changes
    }
}
