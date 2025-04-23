using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Stalker.Weapon.Projectile;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STWeaponSystem), typeof(STProjectileSystem))]
public sealed partial class STProjectileAccuracyComponent : Component
{
    /// <summary>
    /// This lists all the thresholds and their falloff values.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<AccuracyFalloffThreshold> Thresholds = new()
    {
        new AccuracyFalloffThreshold(5f, 0.05f, false),
    };

    [DataField, AutoNetworkedField]
    public float TargetOccluded = 0.15f;

    /// <summary>
    /// Minimum hit chance.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MinAccuracy = 0.15f;

    /// <summary>
    /// The accuracy of the projectile before taking into account any modifiers reliant on the target.
    /// This value is multiplied by the firing weapon's accuracy multiplier upon the projectile being shot.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Accuracy = 0.9f;

    /// <summary>
    /// If set to true, this makes the projectile automatically hit regardless of accuracy or any other modifiers.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ForceHit;

    [DataField, AutoNetworkedField]
    public long GunSeed;

    [DataField, AutoNetworkedField]
    public uint Tick;

    /// <summary>
    /// These are the coordinates from which the projectile was shot. Used to determine the distance travelled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityCoordinates? StartCoordinates;
}
