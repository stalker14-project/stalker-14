using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.Weapon;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STAccuracyComponent : Component
{
    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 0.1f;
}
