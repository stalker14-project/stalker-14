using System;
using System.Collections.Generic;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.WarZone.Requirenments;

[ImplicitDataDefinitionForInheritors]
[Serializable, NetSerializable]
public abstract partial class BaseWarZoneRequirenment
{
    /// <summary>
    /// Checks if this requirement allows capture to proceed or progress.
    /// </summary>
    /// <param name="attackerBandProtoId">The prototype ID of the attacking band, if any.</param>
    /// <param name="attackerFactionProtoId">The prototype ID of the attacking faction, if any.</param>
    /// <param name="ownerships">Current ownership status of relevant zones.</param>
    /// <param name="lastCaptureTimes">Last capture times for relevant zones.</param>
    /// <param name="zonePrototypes">Prototypes of relevant zones.</param>
    /// <param name="currentZoneId">The ID of the zone being checked.</param>
    /// <param name="frameTime">The time elapsed since the last frame.</param>
    /// <param name="attackerEntity">The specific entity attempting the capture, if uniquely identifiable.</param>
    /// <param name="feedbackCallback">A callback to provide feedback messages to the attacker entity.</param>
    /// <returns>A reason why capture might be blocked, or None if allowed.</returns>
    public abstract CaptureBlockReason Check(
        string? attackerBandProtoId,
        string? attackerFactionProtoId,
        Dictionary<ProtoId<STWarZonePrototype>, (string? BandProtoId, string? FactionProtoId)> ownerships,
        Dictionary<ProtoId<STWarZonePrototype>, DateTime?> lastCaptureTimes,
        Dictionary<ProtoId<STWarZonePrototype>, STWarZonePrototype> zonePrototypes,
        ProtoId<STWarZonePrototype> currentZoneId,
        float frameTime,
        EntityUid? attackerEntity,
        Action<EntityUid, string, (string, object)[]?>? feedbackCallback); // Added attackerEntity and feedbackCallback
}