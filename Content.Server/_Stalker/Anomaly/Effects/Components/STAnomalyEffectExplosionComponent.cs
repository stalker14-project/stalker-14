using Content.Shared.Explosion;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Anomaly.Effects.Components;

[RegisterComponent]
public sealed partial class STAnomalyEffectExplosionComponent : Component
{
    [DataField]
    public Dictionary<string, Option> Options = new();

    [Serializable, DataDefinition]
    public partial struct Option
    {
        [DataField]
        public ProtoId<ExplosionPrototype> Id = "Son";

        [DataField]
        public float Intensity = 500f;

        [DataField]
        public float Slope = 4f;

        [DataField]
        public float MaxIntensity = 1000f;
    }
}
