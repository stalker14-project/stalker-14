using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.WeaponModule;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class STWeaponModuleContainerComponent : Component
{
    [DataField, ViewVariables, AutoNetworkedField]
    public STWeaponModuleEffect HashedEffect;
    [DataField, ViewVariables, AutoNetworkedField]
    public STWeaponModuleScopeEffect? HashedScopeEffect;
}
