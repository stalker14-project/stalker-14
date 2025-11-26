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

    /// <summary>
    /// Tracks the specific entities currently inside the zone's trigger area.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public HashSet<EntityUid> PresentEntities = new();

    /// <summary>
    /// Tracks the count of entities per band currently inside the zone.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<string, int> PresentBandCounts = new();

    /// <summary>
    /// Tracks the count of entities per faction currently inside the zone.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<string, int> PresentFactionCounts = new();

    /// <summary>
    /// Current capture progress in seconds
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float CaptureProgressTime = 0f;
    
    /// <summary>
    /// Normalized capture progress (0.0 to 1.0)
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float CaptureProgress = 0f;

    /// <summary>
    /// Last announced progress step (each 10% increment: 0–10)
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public int LastAnnouncedProgressStep = 0;

    /// <summary>
    /// The next game time when the capture logic should be updated for this zone.
    /// </summary>
    [DataField]
    public TimeSpan NextCheckTime = TimeSpan.Zero;
}
