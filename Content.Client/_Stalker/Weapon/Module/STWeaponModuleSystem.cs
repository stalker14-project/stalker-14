using Content.Client.Weapons.Ranged.Components;
using Content.Shared._Stalker.Weapon.Module;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;

namespace Content.Client._Stalker.Weapon.Module;

public sealed class STWeaponModuleSystem : STSharedWeaponModuleSystem
{
    private EntityQuery<ContainerManagerComponent> _containerMangerQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<STWeaponModuleComponent> _weaponModuleQuery;

    public override void Initialize()
    {
        base.Initialize();

        _containerMangerQuery = GetEntityQuery<ContainerManagerComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _weaponModuleQuery = GetEntityQuery<STWeaponModuleComponent>();

        SubscribeLocalEvent<STWeaponModuleContainerComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<STWeaponModuleContainerComponent, EntRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnInserted(Entity<STWeaponModuleContainerComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateVisuals(ent, args.Entity, visible: true);
    }

    private void OnRemoved(Entity<STWeaponModuleContainerComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateVisuals(ent, args.Entity, visible: false);
    }

    private void UpdateVisuals(Entity<STWeaponModuleContainerComponent> ent, Entity<STWeaponModuleComponent?> moduleEnt, bool visible = true)
    {
        if (!_spriteQuery.TryComp(ent, out var spriteComponent))
            return;

        UpdateVisuals(spriteComponent, moduleEnt, visible: visible);
    }

    private void UpdateVisuals(SpriteComponent spriteComponent, Entity<STWeaponModuleComponent?> ent, bool visible = true)
    {
        if (spriteComponent.BaseRSI is null)
            return;

        if (!_weaponModuleQuery.Resolve(ent, ref ent.Comp, logMissing: false))
            return;

        var prototype = MetaData(ent).EntityPrototype;
        if (prototype is null)
            return;

        var state = $"{prototype.ID}-{ent.Comp.StatePostfix}";
        if (!spriteComponent.BaseRSI.TryGetState(state, out _))
            state = $"base-{ent.Comp.StatePostfix}";

        if (!spriteComponent.LayerMapTryGet(ent.Comp.Layer, out var layer))
            return;

        spriteComponent.LayerSetState(layer, state);
        spriteComponent.LayerSetVisible(layer, visible);
    }
}
