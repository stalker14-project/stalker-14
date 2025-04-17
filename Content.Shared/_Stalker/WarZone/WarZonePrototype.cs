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
    public float CaptureCooldownHours { get; set; } = 12;

    [DataField]
    public bool ShouldAwardWhenDefenderPresent { get; set; } = false;
    
    /// <summary>
    /// Time required to capture this zone in seconds
    /// </summary>
    [DataField("captureTime"), ViewVariables]
    public float CaptureTime { get; set; } = 1800f; // Default 30 minutes
}
