using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.Weapon.Projectile;

[DataRecord, Serializable, NetSerializable]
public readonly struct DamageFalloffThreshold
{
    /// <summary>
    /// The range at which falloff starts to take effect.
    /// </summary>
    public readonly float Range;

    /// <summary>
    /// This is the number by which the projectile's damage is decreased for each tile travelled beyond its effective range.
    /// </summary>
    public readonly float Falloff;

    /// <summary>
    /// This makes this falloff value ignore the firing weapon's falloff multiplier. Used primarily to simulate having a capped maximum range. Should generally be false.
    /// </summary>
    public readonly bool IgnoreModifiers;

    public DamageFalloffThreshold(float range, float falloff, bool ignoreModifiers)
    {
        Range = range;
        Falloff = falloff;
        IgnoreModifiers = ignoreModifiers;
    }
}
