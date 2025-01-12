using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.Weapons.Ranged;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STGunSystem))]
public sealed partial class STWeaponAccuracyComponent : Component
{
    /// <summary>
    /// This is the base multiplier applied to all fired projectiles' accuracy scores when the weapon is wielded.
    /// Conversion from 13: accuracy_mult
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 AccuracyMultiplier = 1;

    /// <summary>
    /// This is the base multiplier applied to all fired projectiles' accuracy scores when the weapon is not wielded.
    /// Conversion from 13: accuracy_mult_unwielded
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 AccuracyMultiplierUnwielded = 1;

    [DataField, AutoNetworkedField]
    public FixedPoint2 ModifiedAccuracyMultiplier = 1;
}
