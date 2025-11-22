using Content.Shared._Stalker.Weapon.Module.Effects;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.Weapon.Module;

[RegisterComponent, NetworkedComponent]
[Access(typeof(STSharedWeaponModuleSystem))]
public sealed partial class STWeaponModuleContainerComponent : Component
{
    [ViewVariables]
    public STWeaponModuleEffect CachedEffect;

    [ViewVariables]
    public STWeaponModuleScopeEffect? CachedScopeEffect;

    [ViewVariables]
    public bool IntegratedScopeEffect;
}
