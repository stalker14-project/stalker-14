using Content.Shared.Storage;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.ZoneAnomaly.Effects.Components;

[RegisterComponent, EntityCategory("Spawner", "StSkipSpawnTest")]
public sealed partial class ZoneAnomalyEffectSpawnComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<EntitySpawnEntry> Entry = new();

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float Offset = 2f;
}
