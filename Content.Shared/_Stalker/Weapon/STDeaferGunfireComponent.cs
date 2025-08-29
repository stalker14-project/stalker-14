using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Weapon;

[RegisterComponent, NetworkedComponent]
public sealed partial class STDeaferGunfireComponent : Component
{
    [DataField]
    public EntProtoId Effect = "STDeafEffect";

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(120);
}
