using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Stalker.Projectiles;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STProjectileSystem))]
public sealed partial class ProjectileMaxRangeComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityCoordinates? Origin;

    [DataField(required: true), AutoNetworkedField]
    public float Max;

    [DataField, AutoNetworkedField]
    public bool Delete = true;
}
