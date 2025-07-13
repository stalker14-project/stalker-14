using Content.Shared.Whitelist;

namespace Content.Server._Stalker.Anomaly.Effects.Components;

[RegisterComponent]
public sealed partial class STAnomalyEffectTunnelComponent : Component
{
    [DataField]
    public Dictionary<string, STAnomalyEffectTunnelOptions> Options = new();
}

[Serializable, DataDefinition]
public partial struct STAnomalyEffectTunnelOptions
{
    [DataField]
    public List<string> Maps = new();

    [DataField]
    public float Range;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public STAnomalyEffectTunnelType Type = STAnomalyEffectTunnelType.Enter;
}

[Serializable]
public enum STAnomalyEffectTunnelType
{
    Enter,
    Exit,
}
