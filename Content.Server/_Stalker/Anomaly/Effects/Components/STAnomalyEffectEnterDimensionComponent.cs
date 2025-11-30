using Content.Shared._Stalker.Dimension;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Anomaly.Effects.Components;

[RegisterComponent]
public sealed partial class STAnomalyEffectEnterDimensionComponent : Component
{
    [DataField]
    public Dictionary<string, STAnomalyEnterDimensionEffectOptions> Options = new();
}

[Serializable, DataDefinition]
public partial struct STAnomalyEnterDimensionEffectOptions
{
    [DataField]
    public ProtoId<STDimensionPrototype> Dimension;

    [DataField]
    public float Range;

    [DataField]
    public EntityWhitelist? Whitelist;
}
