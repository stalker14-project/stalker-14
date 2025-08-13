using Robust.Shared.Audio;

namespace Content.Server._Stalker.Anomaly.Effects.Components;

[RegisterComponent]
public sealed partial class STAnomalyEffectSoundComponent : Component
{
    [DataField]
    public Dictionary<string, STAnomalyEffectSoundOptions> Options = new();
}

[Serializable, DataDefinition]
public partial struct STAnomalyEffectSoundOptions
{
    [DataField]
    public SoundSpecifier? Sound;
}
