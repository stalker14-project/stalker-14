using Content.Shared._Stalker.Weapon.Module.Effects;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.Weapon.Module;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(STSharedWeaponModuleSystem))]
public sealed partial class STWeaponModuleContainerComponent : Component
{
    [DataField, ViewVariables, AutoNetworkedField]
    public STWeaponModuleEffect CachedEffect;

    [DataField, ViewVariables, AutoNetworkedField]
    public STWeaponModuleScopeEffect? CachedScopeEffect;

    [DataField, ViewVariables, AutoNetworkedField]
    public bool IntegratedScopeEffect;
}
