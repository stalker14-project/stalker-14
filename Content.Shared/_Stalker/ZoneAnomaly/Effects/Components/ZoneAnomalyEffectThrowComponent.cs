using Content.Shared.Whitelist;

namespace Content.Shared._Stalker.ZoneAnomaly.Effects.Components;

[RegisterComponent]
public sealed partial class ZoneAnomalyEffectThrowComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityWhitelist? Whitelist;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Distance = 10f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Force  = 10f;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MinDistance;
}
