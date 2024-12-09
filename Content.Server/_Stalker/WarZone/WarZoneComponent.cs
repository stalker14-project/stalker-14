using Content.Shared._Stalker.WarZone;
using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.WarZone;

[RegisterComponent]
public sealed partial class WarZoneComponent : Component
{
    [DataField]
    public string PortalName = string.Empty;

    [DataField("proto")]
    public ProtoId<STWarZonePrototype> ZoneProto;
}
