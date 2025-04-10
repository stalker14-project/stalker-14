using System;
using System.Collections.Generic;
using Content.Shared._Stalker.WarZone.Requirenments;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.WarZone;

[Prototype("stWarZone"), Serializable, NetSerializable]
public sealed partial class STWarZonePrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    [DataField, ViewVariables]
    public float RewardPointsPerPeriod { get; set; } = default!;

    [DataField, ViewVariables]
    public int RewardPeriod { get; set; } = default!;

    [DataField]
    public HashSet<BaseWarZoneRequirenment>? Requirements;

    [DataField]
    public int CaptureCooldownHours { get; set; } = 12;

    [DataField]
    public bool ShouldAwardWhenDefenderPresent { get; set; } = false;
}