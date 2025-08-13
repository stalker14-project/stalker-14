using Content.Shared.Damage;

namespace Content.Server._Stalker.Anomaly.Effects.Components;

[RegisterComponent]
public sealed partial class STAnomalyEffectDamageComponent : Component
{
    [DataField]
    public Dictionary<string, STAnomalyDamageEffectOptions> Options = new();
}

[Serializable, DataDefinition]
public partial struct STAnomalyDamageEffectOptions
{
    [DataField]
    public DamageSpecifier Damage;

    [DataField]
    public float Range = 1f;
}
