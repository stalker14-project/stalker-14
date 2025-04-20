using Content.Shared._Stalker.Weapon.Projectile;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.Weapon;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STWeaponSystem), typeof(STProjectileSystem))]
public sealed partial class STWeaponDamageFalloffComponent : Component
{
    /// <summary>
    /// This is the base multiplier applied the all fired projectiles' falloff.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FalloffMultiplier = 1;

    [DataField, AutoNetworkedField]
    public float ModifiedFalloffMultiplier = 1;
}
