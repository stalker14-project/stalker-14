using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.ZoneAnomaly.Effects;

[RegisterComponent]
public sealed partial class ZoneAnomalyWeightModifierComponent : Component
{
    [DataField]
    public float Multiply = 1f;

    [DataField]
    public float Additional;
}
