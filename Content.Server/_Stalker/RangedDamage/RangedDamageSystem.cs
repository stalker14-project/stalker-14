using Content.Server.Explosion.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Interaction.Events;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Timing;
using Robust.Shared.Timing;
using System.Linq;
using Content.Shared.Damage.Systems;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;

namespace Content.Server._Stalker.RangedDamage;
/// <summary>
/// Logic of <see cref="RangedDamageComponent"/>
/// </summary>
public sealed class RangedDamageSystem : EntitySystem
{
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly EntityLookupSystem _entLook = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RangedDamageComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<RangedDamageComponent, StepTriggerAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<RangedDamageComponent, StepTriggeredOffEvent>(OnStep);
        SubscribeLocalEvent<RangedDamageComponent, TriggerEvent>(OnTriggered);
        SubscribeLocalEvent<RangedDamageComponent, UseInHandEvent>(OnUse);
    }

    private void OnInit(EntityUid uid, RangedDamageComponent component, ComponentInit args)
    {
        if (component.ActivateOnSpawn)
            TryActivateTimerTrigger(uid);
    }

    private void OnAttempt(EntityUid uid, RangedDamageComponent component, ref StepTriggerAttemptEvent args)
    {
        if (TryComp<UseDelayComponent>(uid, out var useDelayComponent))
        {
            if (useDelayComponent.Delays.FirstOrDefault().Value.EndTime > _timing.CurTime)
                args.Continue = false;
        }

        args.Continue = true;
    }

    private void OnStep(EntityUid uid, RangedDamageComponent component, ref StepTriggeredOffEvent args)
    {
        if (TryComp<UseDelayComponent>(uid, out var useDelayComponent))
        {
            if (useDelayComponent.Delays.FirstOrDefault().Value.EndTime > _timing.CurTime)
                return;
        }

        TryActivateTimerTrigger(uid);
    }

    private void OnUse(EntityUid uid, RangedDamageComponent component, UseInHandEvent args)
    {
        TryActivateTimerTrigger(uid);
    }

    private void OnTriggered(EntityUid uid, RangedDamageComponent component, TriggerEvent args)
    {
        if (component.Damage == null)
            return;

        var entities = _entLook.GetEntitiesInRange(uid, component.Range);

        foreach (var entity in entities)
        {
            _damageable.TryChangeDamage(entity, component.Damage, component.IgnoreResistances,
                component.InterruptDoAfters);
        }

        if (component.DeleteSelfOnTrigger)
            RemComp<RangedDamageComponent>(uid);
    }

    private bool TryActivateTimerTrigger(EntityUid uid)
    {
        if (!TryComp<TimerTriggerComponent>(uid, out var trigger))
            return false;

        _trigger.ActivateTimerTrigger((uid, trigger));
        return true;
    }
}
