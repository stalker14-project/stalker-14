using Content.Shared.Whitelist;

namespace Content.Shared._Stalker.ZoneAnomaly.Effects.Components;

[RegisterComponent]
public sealed partial class ZoneAnomalyEffectFlashComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityWhitelist Whitelist = new();

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Range = 3f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Duration = 8f;
}
