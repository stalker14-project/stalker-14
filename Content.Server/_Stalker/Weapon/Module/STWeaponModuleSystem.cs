using Content.Server._DZ.FarGunshot;
using Content.Server._Stalker.Weapon.Scoping;
using Content.Shared._Stalker.Weapon.Module;
using Content.Shared._Stalker.Weapon.Module.Effects;
using Content.Shared._Stalker.Weapon.Scoping;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;

namespace Content.Server._Stalker.Weapon.Module;

public sealed class STWeaponModuleSystem : STSharedWeaponModuleSystem
{
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly STScopeSystem _sharedScope = default!;

    private EntityQuery<ContainerManagerComponent> _containerMangerQuery;
    private EntityQuery<STWeaponModuleContainerComponent> _containerModuleQuery;

    public override void Initialize()
    {
        base.Initialize();

        _containerMangerQuery = GetEntityQuery<ContainerManagerComponent>();
        _containerModuleQuery = GetEntityQuery<STWeaponModuleContainerComponent>();

        SubscribeLocalEvent<STWeaponModuleComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<STWeaponModuleComponent, EntGotRemovedFromContainerMessage>(OnRemoved);

        SubscribeLocalEvent<STWeaponModuleContainerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<STWeaponModuleContainerComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
    }

    private void OnInserted(Entity<STWeaponModuleComponent> entity, ref EntGotInsertedIntoContainerMessage args)
    {
        UpdateContainerEffect(args.Container);
    }

    private void OnRemoved(Entity<STWeaponModuleComponent> entity, ref EntGotRemovedFromContainerMessage args)
    {
        UpdateContainerEffect(args.Container);
    }

    private void OnInit(Entity<STWeaponModuleContainerComponent> entity, ref ComponentInit args)
    {
        entity.Comp.CachedEffect = new STWeaponModuleEffect();
        entity.Comp.CachedScopeEffect = null;
        entity.Comp.IntegratedScopeEffect = HasComp<ScopeComponent>(entity);

        if (!_containerMangerQuery.TryGetComponent(entity, out var containerComponent))
            return;

        foreach (var (_, container) in containerComponent.Containers)
        {
            UpdateContainerEffect(entity, container);
        }
    }

    private void OnGunRefreshModifiers(Entity<STWeaponModuleContainerComponent> entity, ref GunRefreshModifiersEvent args)
    {
        var effect = entity.Comp.CachedEffect;

        args.FireRate *= effect.FireRateModifier;
        args.AngleDecay *= effect.AngleDecayModifier;
        args.AngleIncrease *= effect.AngleIncreaseModifier;
        args.MinAngle *= effect.MinAngleModifier;
        args.MaxAngle *= effect.MaxAngleModifier;
        args.ProjectileSpeed *= effect.ProjectileSpeedModifier;

        if (TryComp(entity.Owner, out FarGunshotComponent? farGunshotComponent)
            && farGunshotComponent.Sound is not null)
        {
            farGunshotComponent.SilencerDecrease = effect.FarshotSoundDecrease;

            var farAudioParams = farGunshotComponent.Sound.Params;

            farAudioParams.Volume += effect.SoundGunshotVolumeAddition;
            farGunshotComponent.Sound.Params = farAudioParams;
        }

        if (args.SoundGunshot is null)
            return;

        var audioParams = args.SoundGunshot?.Params ?? AudioParams.Default;

        // Hotfix how to handle super silent silencers happening because volume additions
        // pile up. We need to find something else, because a user in the future might have
        // not only one volume reducing module
        audioParams.Volume += effect.SoundGunshotVolumeAddition;
        args.SoundGunshot!.Params = audioParams;
    }

    private void UpdateContainerEffect(BaseContainer container)
    {
        UpdateContainerEffect(container.Owner, container);
    }

    private void UpdateContainerEffect(EntityUid entityUid, BaseContainer container)
    {
        if (!_containerModuleQuery.TryGetComponent(entityUid, out var containerComponent))
            return;

        UpdateContainerEffect((entityUid, containerComponent), container);
    }

    private void UpdateContainerEffect(Entity<STWeaponModuleContainerComponent> entity, BaseContainer container)
    {
        var effect = new STWeaponModuleEffect();
        STWeaponModuleScopeEffect? scopeEffect = null;

        foreach (var containedEntity in container.ContainedEntities)
        {
            if (!TryComp<STWeaponModuleComponent>(containedEntity, out var moduleComponent))
                continue;

            effect = STWeaponModuleEffect.Merge(effect, moduleComponent.Effect);

            if (moduleComponent.ScopeEffect is null)
                continue;

            scopeEffect ??= moduleComponent.ScopeEffect;
            scopeEffect = STWeaponModuleScopeEffect.Merge(scopeEffect.Value, moduleComponent.ScopeEffect.Value);
        }

        var modeDelta = effect.AdditionalAvailableModes ^ entity.Comp.CachedEffect.AdditionalAvailableModes;

        entity.Comp.CachedEffect = effect;
        Dirty(entity);

        if (!entity.Comp.IntegratedScopeEffect)
            _sharedScope.TrySet(entity.Owner, scopeEffect);

        if (!TryComp<GunComponent>(entity, out var gun))
            return;

        // Here bit mask works like switch and switch modes that changed
        _gun.SetAvailableModes(entity, gun.AvailableModes ^ modeDelta, gun);
        _gun.RefreshModifiers((entity, gun));
    }
}
