using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.Weapons.Ranged;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STGunSystem))]
public sealed partial class STWeaponDamageFalloffComponent : Component
{
    /// <summary>
    /// This is the baase multiplier applied the all fired projectiles' falloff.
    /// Conversion from 13: damage_falloff_mult
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 FalloffMultiplier = 1;

    [DataField, AutoNetworkedField]
    public FixedPoint2 ModifiedFalloffMultiplier = 1;
}
