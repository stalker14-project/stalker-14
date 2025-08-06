using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Bands;

/// <summary>
/// Component to apply band status icon for band members.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BandsComponent : Component
{
    [DataField("bandIcon"), ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public string BandStatusIcon = "stalker";

    [DataField("band"), ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public string BandName = "Stalker";

    [DataField]
    public string? AltBand;

    [DataField]
    public bool CanChange;

    [DataField("actionChange"), ViewVariables(VVAccess.ReadOnly)]
    public string ActionChange = "ActionChangeBand";

    [DataField] public EntityUid? ActionChangeEntity;

    [DataField("bandProto"), ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<STBandPrototype> BandProto;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int BandRankId = 1;
}
