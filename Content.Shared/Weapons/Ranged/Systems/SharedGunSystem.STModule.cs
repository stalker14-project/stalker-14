using Content.Shared._Stalker.WeaponModule;
using Robust.Shared.Containers;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    protected const string MuzzleSlot = "gun_module_muzzle";
    protected const string ScopeSlot = "gun_module_scope";
    protected const string UnderbarrelSlot = "gun_module_underbarrel";
    protected const string SelectiveFireSlot = "gun_module_selective_fire";

    protected virtual void InitializeModule()
    {
        SubscribeLocalEvent<STWeaponModuleContainerComponent, EntInsertedIntoContainerMessage>(OnModuleSlotChange);
        SubscribeLocalEvent<STWeaponModuleContainerComponent, EntRemovedFromContainerMessage>(OnModuleSlotChange);
    }

    protected virtual void OnModuleSlotChange(EntityUid uid, STWeaponModuleContainerComponent component, ContainerModifiedMessage args)
    {
        if (args.Container.ID == MuzzleSlot)
            OnMuzzleSlotChange(uid, component, args);

        if (args.Container.ID == ScopeSlot)
            OnScopeSlotChange(uid, component, args);

        if (args.Container.ID == UnderbarrelSlot)
            OnUnderbarrelSlotChange(uid, component, args);

        if (args.Container.ID == SelectiveFireSlot)
            OnSelectiveFireSlotChange(uid, component, args);
    }

    protected virtual void OnMuzzleSlotChange(EntityUid uid, STWeaponModuleContainerComponent component, ContainerModifiedMessage args)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        var muzzleEnt = GetMuzzleEntity(uid);
        Appearance.SetData(uid, AmmoVisuals.MuzzleEquiped, muzzleEnt != null, appearance);
    }

    protected virtual void OnScopeSlotChange(EntityUid uid, STWeaponModuleContainerComponent component, ContainerModifiedMessage args)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        var scopeEnt = GetScopeEntity(uid);
        Appearance.SetData(uid, AmmoVisuals.ScopeEquiped, scopeEnt != null, appearance);
    }

    protected virtual void OnUnderbarrelSlotChange(EntityUid uid, STWeaponModuleContainerComponent component, ContainerModifiedMessage args)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        var underbarrelEnt = GetUnderbarrelEntity(uid);
        Appearance.SetData(uid, AmmoVisuals.UnderbarrelEquiped, underbarrelEnt != null, appearance);
    }

    protected virtual void OnSelectiveFireSlotChange(EntityUid uid, STWeaponModuleContainerComponent component, ContainerModifiedMessage args)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        var selectiveFireEnt = GetSelectiveFireEntity(uid);
        Appearance.SetData(uid, AmmoVisuals.SelectiveFireEquiped, selectiveFireEnt != null, appearance);
    }

    protected EntityUid? GetScopeEntity(EntityUid uid)
    {
        if (!Containers.TryGetContainer(uid, ScopeSlot, out var container) ||
            container is not ContainerSlot slot)
        {
            return null;
        }

        return slot.ContainedEntity;
    }
    protected EntityUid? GetUnderbarrelEntity(EntityUid uid)
    {
        if (!Containers.TryGetContainer(uid, UnderbarrelSlot, out var container) ||
            container is not ContainerSlot slot)
        {
            return null;
        }

        return slot.ContainedEntity;
    }

    protected EntityUid? GetMuzzleEntity(EntityUid uid)
    {
        if (!Containers.TryGetContainer(uid, MuzzleSlot, out var container) ||
            container is not ContainerSlot slot)
        {
            return null;
        }

        return slot.ContainedEntity;
    }

    protected EntityUid? GetSelectiveFireEntity(EntityUid uid)
    {
        if (!Containers.TryGetContainer(uid, SelectiveFireSlot, out var container) ||
            container is not ContainerSlot slot)
        {
            return null;
        }

        return slot.ContainedEntity;
    }
}
