using Content.Shared.Damage;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Server._Stalker.Teleports.NewMapTeleports;
// TODO: Rename
[RegisterComponent]
public sealed partial class NewMapTeleportComponent : Component
{
    [DataField("portalName")]
    public string PortalName = "";

    [DataField]
    public bool AllowAll = true;

    [DataField]
    public bool CooldownEnabled;

    [DataField]
    public DamageModifierSet? ModifierSet;

    [DataField]
    public float DecreasedTime;

    [DataField]
    public float CooldownTime;

    [DataField]
    public bool IsCollisionDisabled;
}
