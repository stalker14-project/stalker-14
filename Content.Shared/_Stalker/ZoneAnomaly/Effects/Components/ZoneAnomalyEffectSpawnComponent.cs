using Content.Shared.Storage;

namespace Content.Shared._Stalker.ZoneAnomaly.Effects.Components;

[RegisterComponent]
public sealed partial class ZoneAnomalyEffectSpawnComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<EntitySpawnEntry> Entry = new();

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Offset = 2f;
}
