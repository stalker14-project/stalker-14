using System;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Stalker.WarZone.Requirenments;

[Serializable, NetSerializable]
public sealed partial class TimeWindowRequirenment : BaseWarZoneRequirenment
{
    [DataField("startHourUtc", required: true)]
    public int StartHourUtc { get; private set; }

    [DataField("endHourUtc", required: true)]
    public int EndHourUtc { get; private set; }

    public override CaptureBlockReason Check(
        string? attackerBandProtoId,
        string? attackerFactionProtoId,
        Dictionary<ProtoId<STWarZonePrototype>, (string? BandProtoId, string? FactionProtoId)> ownerships,
        Dictionary<ProtoId<STWarZonePrototype>, DateTime?> lastCaptureTimes,
        Dictionary<ProtoId<STWarZonePrototype>, STWarZonePrototype> zonePrototypes,
        ProtoId<STWarZonePrototype> currentZoneId,
        float frameTime,
        EntityUid? attackerEntity,
        Action<EntityUid, string, (string, object)[]?>? feedbackCallback)
    {
        var currentUtcTime = DateTime.UtcNow;
        var currentHour = currentUtcTime.Hour;

        if (StartHourUtc < 0 || StartHourUtc > 23 || EndHourUtc < 0 || EndHourUtc > 23)
        {
            return CaptureBlockReason.TimeWindow; // Or a specific Error reason
        }

        // Handle time ranges that span across midnight
        if (StartHourUtc <= EndHourUtc)
        {
            // Standard range (e.g., 8 to 16)
            if (currentHour >= StartHourUtc && currentHour < EndHourUtc)
            {
                return CaptureBlockReason.None;
            }
        }
        else
        {
            // Range spans midnight (e.g., 22 to 4)
            if (currentHour >= StartHourUtc || currentHour < EndHourUtc)
            {
                return CaptureBlockReason.None;
            }
        }

        if (attackerEntity.HasValue && feedbackCallback != null)
        {
            // Provide feedback using the callback
            var args = new (string, object)[] { ("startHour", StartHourUtc), ("endHour", EndHourUtc) };
            feedbackCallback(attackerEntity.Value, "st-warzone-timewindow-fail", args);
        }
        return CaptureBlockReason.TimeWindow;
    }
}
