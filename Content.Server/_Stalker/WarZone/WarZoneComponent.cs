using System;
using System.Collections.Generic;
using Content.Shared._Stalker.WarZone;
using Robust.Shared.Prototypes;
using Robust.Shared.ViewVariables;

namespace Content.Server._Stalker.WarZone;

[RegisterComponent]
public sealed partial class WarZoneComponent : Component
{
    [DataField]
    public string PortalName = string.Empty;

    [DataField("proto")]
    public ProtoId<STWarZonePrototype> ZoneProto;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string? DefendingBandProtoId = null;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string? DefendingFactionProtoId = null;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string? CurrentAttackerBandProtoId = null;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string? CurrentAttackerFactionProtoId = null;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan? CooldownEndTime = null;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool InitialLoadComplete = false;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public HashSet<string> PresentBandProtoIds = new();

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public HashSet<string> PresentFactionProtoIds = new();
    [ViewVariables(VVAccess.ReadOnly)]
    public float CaptureProgress = 0f;
}
