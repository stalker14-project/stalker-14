namespace Content.Server._Stalker.Anomaly.Effects.Components;

[RegisterComponent]
public sealed partial class STAnomalyEffectThrowComponent : Component
{
    [DataField]
    public Dictionary<string, STAnomalyEffectThrowOptions> Options = new();
}

[Serializable, DataDefinition]
public partial struct STAnomalyEffectThrowOptions
{
    [DataField]
    public float Range = 1f;

    [DataField]
    public float Force = 5f;

    [DataField]
    public float Distance = 5f;

    [DataField]
    public STAnomalyEffectThrowType Type = STAnomalyEffectThrowType.RandomDirection;
}

[Serializable]
public enum STAnomalyEffectThrowType
{
    RandomDirection,
    ToAnomaly,
    FromAnomaly,
}
