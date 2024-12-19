using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Projectiles;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STProjectileSystem))]
public sealed partial class ModifyTargetOnHitComponent : Component
{
    [DataField]
    public ComponentRegistry? Add;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? Whitelist;
}
