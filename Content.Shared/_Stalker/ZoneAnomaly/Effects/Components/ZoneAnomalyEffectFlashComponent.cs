using Content.Shared.Whitelist;

namespace Content.Shared._Stalker.ZoneAnomaly.Effects.Components;

[RegisterComponent]
public sealed partial class ZoneAnomalyEffectFlashComponent : Component
{
    [DataField]
    public EntityWhitelist Whitelist = new();

    [DataField]
    public float Range = 3f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(8);
}
