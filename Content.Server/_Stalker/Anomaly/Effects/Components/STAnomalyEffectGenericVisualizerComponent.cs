namespace Content.Server._Stalker.Anomaly.Effects.Components;

[RegisterComponent]
public sealed partial class STAnomalyEffectGenericVisualizerComponent : Component
{
    [DataField]
    public Dictionary<string, STAnomalyGenericVisualizerEffectOptions> Options = new();
}

[Serializable, DataDefinition]
public partial struct STAnomalyGenericVisualizerEffectOptions
{
    [DataField]
    public string State;
}

[Serializable]
public enum STAnomalyGenericVisualizerEffectVisuals
{
    Layer,
}
