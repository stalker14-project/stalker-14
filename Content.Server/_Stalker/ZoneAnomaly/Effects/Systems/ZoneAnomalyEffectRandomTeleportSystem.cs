using System.Linq;
using Content.Shared._Stalker.ZoneAnomaly;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Systems;
using Robust.Shared.Random;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectRandomTeleportSystem : SharedZoneAnomalyEffectRandomTeleportSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ZoneAnomalySystem _anomaly = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZoneAnomalyEffectRandomTeleportComponent, ZoneAnomalyActivateEvent>(OnActivate);
    }

    private void OnActivate(Entity<ZoneAnomalyEffectRandomTeleportComponent> effect, ref ZoneAnomalyActivateEvent args)
    {
        var points = EntityQuery<ZoneAnomalyEffectRandomTeleportComponent>().ToList();
        if (points.Contains(effect))
            points.Remove(effect);

        foreach (var trigger in args.Triggers)
        {
            var transform = Transform(effect);
            if (points.Count == 0)
            {
                TeleportEntity(trigger, transform.Coordinates);
                return;
            }

            var point = _random.Pick(points);
            var destination = Transform(point.Owner).Coordinates;

            if (TryComp<ZoneAnomalyComponent>(point.Owner, out var comp))
                _anomaly.TryRecharge((point.Owner, comp));

            TeleportEntity(trigger, destination);
        }
    }
}
