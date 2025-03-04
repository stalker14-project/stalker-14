using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.Projectiles;

[RegisterComponent, NetworkedComponent]
[Access(typeof(STProjectileSystem))]
public sealed partial class DeleteOnCollideComponent : Component;
