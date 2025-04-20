using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

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

    [DataField("action"), ViewVariables(VVAccess.ReadOnly)]
    public string Action = "ActionToggleBands";

    [DataField] public EntityUid? ActionEntity;

    [AutoNetworkedField]
    public bool Enabled = true;
}
