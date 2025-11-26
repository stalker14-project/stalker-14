using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Dimension;

[RegisterComponent, EntityCategory("StSkipSpawnTest")]
public sealed partial class STDimensionComponent : Component
{
    [DataField, ViewVariables]
    public ProtoId<STDimensionPrototype> Id;
}
