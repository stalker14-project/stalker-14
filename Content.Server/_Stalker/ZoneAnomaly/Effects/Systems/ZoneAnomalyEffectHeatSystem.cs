using Content.Server.Temperature.Components;
using Content.Server.Temperature.Systems;
using Content.Shared._Stalker.ZoneAnomaly;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectHeatSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZoneAnomalyEffectHeatComponent, ZoneAnomalyActivateEvent>(OnActivate);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZoneAnomalyComponent, ZoneAnomalyEffectHeatComponent>();
        while (query.MoveNext(out var uid, out var anomaly, out var effect))
        {
            if (!effect.Update)
                continue;

            if (anomaly.State != ZoneAnomalyState.Activated)
                continue;

            if (effect.UpdateTime > _timing.CurTime)
                continue;

            foreach (var target in anomaly.InAnomaly)
            {
                Heat((uid, effect), target);
            }

            effect.UpdateTime = _timing.CurTime + effect.UpdateDelay;
        }
    }

    private void OnActivate(Entity<ZoneAnomalyEffectHeatComponent> effect, ref ZoneAnomalyActivateEvent args)
    {
        if (!TryComp<ZoneAnomalyComponent>(effect, out var anomaly))
            return;

        effect.Comp.UpdateTime = TimeSpan.Zero;
        foreach (var target in anomaly.InAnomaly)
        {
            Heat(effect, target);
        }
    }

    private void Heat(Entity<ZoneAnomalyEffectHeatComponent> effect, EntityUid target)
    {
        if (TryComp<TemperatureComponent>(target, out _))
            _temperature.ChangeHeat(target, 12500 * effect.Comp.Heat, effect.Comp.IgnoreResistance);
    }
}
