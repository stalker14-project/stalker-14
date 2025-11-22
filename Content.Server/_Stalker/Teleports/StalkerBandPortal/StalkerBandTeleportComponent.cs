using Robust.Shared.Prototypes;

namespace Content.Server._Stalker.Teleports.StalkerBandPortal;

[RegisterComponent, EntityCategory("StSkipSpawnTest")]
public sealed partial class StalkerBandTeleportComponent : Component
{
    [DataField(required: true)]
    public string PortalName;

    [DataField]
    public int RepositoryWeight;
}
