using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.Weapon.Projectile;

[DataRecord, Serializable, NetSerializable]
public readonly struct AccuracyFalloffThreshold
{
    /// <summary>
    /// The range at which accuracy falloff starts to take effect.
    /// </summary>
    public readonly float Range;

    /// <summary>
    /// This is the number by which the projectile's accuracy is decreased for each tile travelled beyond its effective range.
    /// </summary>
    public readonly float Falloff;

    /// <summary>
    /// Setting this to true makes it so AccurateRange is treated as the minimum accurate range.
    /// Falloff is applied by how much the shot falls short of that distance, instead of by how much it exceeds it.
    /// </summary>
    public readonly bool AccuracyGrowth;

    public AccuracyFalloffThreshold(float range, float falloff, bool accuracyGrowth)
    {
        Range = range;
        Falloff = falloff;
        AccuracyGrowth = accuracyGrowth;
    }
}
