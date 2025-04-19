using Content.Shared._Stalker.Weapon.Module.Effects;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.Weapon.Module;

/// <summary>
/// Indicates that this entity is a weapon module.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STWeaponModuleComponent : Component
{
    [DataField, ViewVariables, AutoNetworkedField]
    public STWeaponModuleEffect Effect;

    [DataField, ViewVariables, AutoNetworkedField]
    public STWeaponModuleScopeEffect? ScopeEffect;
}
