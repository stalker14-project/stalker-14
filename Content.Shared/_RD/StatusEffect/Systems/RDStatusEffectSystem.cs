/*
 * Project: raincidation
 * File: RDStatusEffectSystem.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Content.Shared._RD.StatusEffect.Components;
using Content.Shared._RD.StatusEffect.Events;
using Content.Shared.Alert;
using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RD.StatusEffect.Systems;

public sealed partial class RDStatusEffectSystem : RDEntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly INetManager _net = default!;

    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;

    private EntityQuery<RDStatusEffectContainerComponent> _containerQuery;
    private EntityQuery<RDStatusEffectComponent> _effectQuery;

    public override void Initialize()
    {
        base.Initialize();

        _containerQuery = GetEntityQuery<RDStatusEffectContainerComponent>();
        _effectQuery = GetEntityQuery<RDStatusEffectComponent>();

        SubscribeLocalEvent<RDStatusEffectContainerComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<RDStatusEffectComponent, RDStatusEffectAppliedEvent>(OnStatusEffectApplied);
        SubscribeLocalEvent<RDStatusEffectComponent, RDStatusEffectRemovedEvent>(OnStatusEffectRemoved);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RDStatusEffectComponent>();
        while (query.MoveNext(out var entity, out var effect))
        {
            if (effect.EndEffectTime is null)
                continue;

            if (_timing.CurTime < effect.EndEffectTime)
                continue;

            if (effect.AppliedTo is null)
                continue;

            var meta = MetaData(entity);
            if (meta.EntityPrototype is null)
                continue;

            TryRemoveStatusEffect(effect.AppliedTo.Value, meta.EntityPrototype);
        }
    }

    private void EditStatusEffectTime(EntityUid effect, TimeSpan delta)
    {
        if (!_effectQuery.TryComp(effect, out var effectComp))
            return;

        if (effectComp.AppliedTo is null)
            return;

        if (effectComp.Alert is not null)
        {
            effectComp.EndEffectTime = (effectComp.EndEffectTime ?? TimeSpan.Zero) + delta;
            _alerts.ShowAlert(
                effectComp.AppliedTo.Value,
                effectComp.Alert.Value,
                cooldown: effectComp.EndEffectTime is null ? null : (_timing.CurTime, effectComp.EndEffectTime.Value));
        }
    }

    private void SetStatusEffectTime(EntityUid effect, TimeSpan duration)
    {
        if (!_effectQuery.TryComp(effect, out var effectComp))
            return;

        if (effectComp.AppliedTo is null)
            return;

        if (effectComp.Alert is not null)
        {
            effectComp.EndEffectTime = _timing.CurTime + duration;
            _alerts.ShowAlert(
                effectComp.AppliedTo.Value,
                effectComp.Alert.Value,
                cooldown: effectComp.EndEffectTime is null ? null : (_timing.CurTime, effectComp.EndEffectTime.Value));
        }
    }

    private void OnShutdown(Entity<RDStatusEffectContainerComponent> entity, ref ComponentShutdown _)
    {
        entity.Comp.ActiveStatusEffects.Clear();
    }

    private void OnStatusEffectApplied(Entity<RDStatusEffectComponent> entity, ref RDStatusEffectAppliedEvent args)
    {
        if (entity.Comp.AppliedTo is null)
            return;

        if (entity.Comp.Alert is not null)
        {
            _alerts.ShowAlert(
                entity.Comp.AppliedTo.Value,
                entity.Comp.Alert.Value,
                cooldown: entity.Comp.EndEffectTime is null ? null : (_timing.CurTime, entity.Comp.EndEffectTime.Value));
        }

        if (_net.IsServer)
            AddComps(args.Target, entity.Comp.Components);
    }

    private void OnStatusEffectRemoved(Entity<RDStatusEffectComponent> entity, ref RDStatusEffectRemovedEvent args)
    {
        if (entity.Comp.AppliedTo is null)
            return;

        if (entity.Comp.Alert is not null)
            _alerts.ClearAlert(entity.Comp.AppliedTo.Value, entity.Comp.Alert.Value);

        if (_net.IsServer)
            RemComps(args.Target, entity.Comp.Components);
    }
}
