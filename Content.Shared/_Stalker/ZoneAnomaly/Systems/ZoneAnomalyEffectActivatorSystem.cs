using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Shared.Whitelist;

namespace Content.Shared._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectActivatorSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ZoneAnomalySystem _anomalySystem = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZoneAnomalyEffectActivatorComponent, ZoneAnomalyActivateEvent>(OnActivate);
    }

    private void OnActivate(Entity<ZoneAnomalyEffectActivatorComponent> effect, ref ZoneAnomalyActivateEvent args)
    {
        var entities = _lookup.GetEntitiesInRange(Transform(effect).Coordinates, effect.Comp.Distance);
        foreach (var entity in entities)
        {
            if(!_whitelist.IsWhitelistPass(effect.Comp.Whitelist, entity))
                continue;

            if (!TryComp<ZoneAnomalyComponent>(entity, out var anomaly))
                return;

            _anomalySystem.TryActivate((entity, anomaly));
        }
    }
}
