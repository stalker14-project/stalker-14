using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Temperature.Systems;
using Content.Shared._Stalker.ZoneAnomaly;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Shared.Damage;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectDamageSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZoneAnomalyEffectDamageComponent, ZoneAnomalyActivateEvent>(OnActivate);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZoneAnomalyComponent, ZoneAnomalyEffectDamageComponent>();
        while (query.MoveNext(out var uid, out var anomaly, out var effect))
        {
            if (!effect.DamageUpdate)
                continue;

            if (anomaly.State != ZoneAnomalyState.Activated)
                continue;

            if (effect.DamageUpdateTime > _timing.CurTime)
                continue;

            if (effect.Damage is not { } damage)
                continue;

            foreach (var target in anomaly.InAnomaly)
            {
                Damage((uid, effect), target);
            }

            effect.DamageUpdateTime = _timing.CurTime + effect.DamageUpdateDelay;
        }
    }

    private void OnActivate(Entity<ZoneAnomalyEffectDamageComponent> effect, ref ZoneAnomalyActivateEvent args)
    {
        if (!TryComp<ZoneAnomalyComponent>(effect, out var anomaly))
            return;

        effect.Comp.DamageUpdateTime = TimeSpan.Zero;

       foreach (var trigger in anomaly.InAnomaly)
        {
            Damage(effect, trigger);
        }
    }

    private void Damage(Entity<ZoneAnomalyEffectDamageComponent> effect, EntityUid target)
    {
        if (!TryComp<FlammableComponent>(target, out _))
            return;
        if (effect.Comp.FireStacks > 0)
        {
            _flammable.AdjustFireStacks(target, effect.Comp.FireStacks);
            _flammable.Ignite(target, effect);
        }

        if (effect.Comp.Damage is not { } damage)
            return;

        _damageable.TryChangeDamage(target, damage, effect.Comp.IgnoreResistances, effect.Comp.InterruptsDoAfters);
    }
}
