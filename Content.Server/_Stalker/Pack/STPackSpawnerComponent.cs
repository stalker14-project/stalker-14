using Content.Shared._Stalker.Pack;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Pack;

[RegisterComponent, EntityCategory("Spawner")]
public sealed partial class STPackSpawnerComponent : Component
{
    [DataField]
    public ProtoId<STPackPrototype> ProtoId;
}
