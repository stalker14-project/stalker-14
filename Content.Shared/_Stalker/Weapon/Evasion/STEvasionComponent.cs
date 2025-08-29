using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.Weapon.Evasion;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STEvasionSystem))]
public sealed partial class STEvasionComponent : Component
{
    public const float DefaultEvasion = 0;

    /// <summary>
    /// Base evasion value.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Evasion;

    /// <summary>
    /// Evasion value after applicable modifiers. This is subtracted from the hit chance of most incoming projectiles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ModifiedEvasion;
}
