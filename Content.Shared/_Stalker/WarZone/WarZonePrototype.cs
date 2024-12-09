using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared._Stalker.WarZone.Requirenments;

namespace Content.Shared._Stalker.WarZone;

[Prototype("stWarZone"), Serializable, NetSerializable]
public sealed class STWarZonePrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Reward points for holding the spot per <see cref="RewardPeriod/>
    /// </summary>
    [DataField, ViewVariables]
    public float RewardPointsPerPeriod { get; set; } = default!;

    /// <summary>
    /// When the zone gives reward points to the owner
    /// </summary>
    [DataField, ViewVariables]
    public TimeSpan RewardPeriod { get; set; } = default!;

    /// <summary>
    /// Requirenments to hold the zone
    /// </summary>
    [DataField]
    public HashSet<BaseWarZoneRequirenment>? Requirements;
}
