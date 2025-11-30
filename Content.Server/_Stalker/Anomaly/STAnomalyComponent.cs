namespace Content.Server._Stalker.Anomaly;

[RegisterComponent]
public sealed partial class STAnomalyComponent : Component
{
    [DataField, ViewVariables]
    public string State;

    [DataField, ViewVariables]
    public Dictionary<string, HashSet<STAnomalyStateTransition>> States = new();
}

[Serializable, DataDefinition]
public partial struct STAnomalyStateTransition
{
    [DataField, ViewVariables]
    public string State;

    [DataField, ViewVariables]
    public string Group;
}
