using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Anomaly.Effects.Components;

[RegisterComponent]
public sealed partial class STAnomalyEffectSpawnComponent : Component
{
    [DataField]
    public Dictionary<string, Option> Options = new();

    [Serializable, DataDefinition]
    public partial struct Option
    {
        [DataField]
        public EntProtoId Id;

        [DataField]
        public ComponentRegistry Components;

        [DataField]
        public float Range;

        [DataField]
        public Vector2 Offset;
    }
}
