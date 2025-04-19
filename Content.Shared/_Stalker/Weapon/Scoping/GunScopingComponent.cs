using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.Weapon.Scoping;

/// <remarks>
/// Portions of this file are derived from the RMC-14 project, specifically from
/// https://github.com/RMC-14/RMC-14/tree/481a21c95148f5a7bff6ed1609324c836663ca30/Content.Shared/_RMC14/Scoping.
/// These files have been modified for use in this project.
/// The original code is licensed under the MIT License:
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STSharedScopeSystem))]
public sealed partial class GunScopingComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Scope;
}
