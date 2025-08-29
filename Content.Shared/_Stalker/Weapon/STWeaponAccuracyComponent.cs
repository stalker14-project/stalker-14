using Content.Shared._Stalker.Weapon.Projectile;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.Weapon;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STWeaponSystem), typeof(STProjectileSystem))]
public sealed partial class STWeaponAccuracyComponent : Component
{
    /// <summary>
    /// This is the base multiplier applied to all fired projectiles' accuracy scores when the weapon is wielded.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AccuracyMultiplier = 1;

    /// <summary>
    /// This is the base multiplier applied to all fired projectiles' accuracy scores when the weapon is not wielded.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float AccuracyMultiplierUnwielded = 1;

    [DataField, AutoNetworkedField]
    public float ModifiedAccuracyMultiplier = 1;
}
