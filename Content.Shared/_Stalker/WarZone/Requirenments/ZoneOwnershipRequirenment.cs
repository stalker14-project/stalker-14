using System;
using System.Collections.Generic;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.WarZone.Requirenments;

[Serializable, NetSerializable]
public sealed partial class ZoneOwnershipRequirenment : BaseWarZoneRequirenment
{
    [DataField("requiredZones")]
    public List<ProtoId<STWarZonePrototype>> RequiredZones = new();

    public override CaptureBlockReason Check(
        string? attackerBandProtoId,
        string? attackerFactionProtoId,
        Dictionary<ProtoId<STWarZonePrototype>, (string? BandProtoId, string? FactionProtoId)> ownerships,
        Dictionary<ProtoId<STWarZonePrototype>, DateTime?> lastCaptureTimes,
        Dictionary<ProtoId<STWarZonePrototype>, STWarZonePrototype> zonePrototypes,
        ProtoId<STWarZonePrototype> currentZoneId,
        float frameTime)
    {
        foreach (var zoneId in RequiredZones)
        {
            if (!ownerships.TryGetValue(zoneId, out var owner))
                return CaptureBlockReason.Ownership;

            var owns = false;
            if (attackerBandProtoId != null && owner.BandProtoId == attackerBandProtoId)
                owns = true;
            if (attackerFactionProtoId != null && owner.FactionProtoId == attackerFactionProtoId)
                owns = true;

            if (!owns)
                return CaptureBlockReason.Ownership;
        }

        // Check capture cooldown for this zone
        if (lastCaptureTimes.TryGetValue(currentZoneId, out var lastCaptureTime) &&
            lastCaptureTime != null &&
            zonePrototypes.TryGetValue(currentZoneId, out var proto))
        {
            var cooldown = TimeSpan.FromHours(proto.CaptureCooldownHours);
            if (DateTime.UtcNow - lastCaptureTime < cooldown)
            {
                return CaptureBlockReason.Cooldown;
            }
        }

        return CaptureBlockReason.None;
    }
}