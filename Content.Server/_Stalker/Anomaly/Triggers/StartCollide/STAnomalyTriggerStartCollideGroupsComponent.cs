using Content.Shared.Whitelist;

namespace Content.Server._Stalker.Anomaly.Triggers.StartCollide;

[RegisterComponent]
public sealed partial class STAnomalyTriggerStartCollideGroupsComponent : Component
{
    [DataField]
    public Dictionary<EntityWhitelist, string> Groups = new();
}
