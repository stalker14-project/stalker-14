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
        // Determine current UTC hour
        var currentHour = DateTime.UtcNow.Hour;
        bool withinWindow;

        // Validate configured hours
        if (StartHourUtc < 0 || StartHourUtc > 23 || EndHourUtc < 0 || EndHourUtc > 23)
        {
            withinWindow = false;
        }
        else if (StartHourUtc <= EndHourUtc)
        {
            // Simple range (e.g., 8 to 16)
            withinWindow = currentHour >= StartHourUtc && currentHour < EndHourUtc;
        }
        else
        {
            // Overnight range (e.g., 22 to 4)
            withinWindow = currentHour >= StartHourUtc || currentHour < EndHourUtc;
        }

        // Block if outside allowed window
        if (!withinWindow)
        {
            // Always provide feedback if callback is available; fallback uses default EntityUid
            if (feedbackCallback != null)
            {
                var args = new (string, object)[] { ("startHour", StartHourUtc), ("endHour", EndHourUtc) };
                var target = attackerEntity ?? default;
                feedbackCallback(target, "st-warzone-timewindow-fail", args);
            }
            return CaptureBlockReason.TimeWindow;
        }
        return CaptureBlockReason.None;
    }
}
