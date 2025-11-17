using System.Linq;
using Content.Shared._Stalker.Anomaly.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Stalker.Anomaly.Data;

[DataDefinition, Serializable]
public partial struct STAnomalyGenerationOptions
{
    public MapId MapId;

    [DataField]
    public int TotalCount = 400;

    [DataField]
    public HashSet<STAnomalyGeneratorAnomalyEntry> AnomalyEntries = [];
}


[DataDefinition, Serializable]
public partial struct STAnomalyGeneratorAnomalyEntry
{
    [DataField]
    public EntProtoId ProtoId;

    /// <summary>
    /// Idk how do it
    /// </summary>
    [DataField]
    public float Dangerous = 1f;

    [DataField]
    public float Weight = 1f;
}
