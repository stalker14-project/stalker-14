using Content.Server._Stalker.Anomaly.Effects.Components;
using Content.Server._Stalker.Map;
using Content.Server._Stalker.Utils;
using Content.Shared._Stalker.Anomaly.Triggers.Events;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Stalker.Anomaly.Effects.Systems;

public sealed class STAnomalyEffectTunnelSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly STMapKeySystem _mapKey = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAnomalyEffectTunnelComponent, STAnomalyTriggerEvent>(OnTriggered);
    }

    private void OnTriggered(Entity<STAnomalyEffectTunnelComponent> entity, ref STAnomalyTriggerEvent args)
    {
        foreach (var group in args.Groups)
        {
            if (!entity.Comp.Options.TryGetValue(group, out var options))
                continue;

            if (options.Type == STAnomalyEffectTunnelType.Exit)
                continue;

            var maps = new List<MapId>();
            foreach (var map in options.Maps)
            {
                if (!_mapKey.TryGet(map, out var mapEntity))
                    continue;

                maps.Add(mapEntity.Value.Comp.MapId);
            }

            var destinations = GetDestinations(maps, group);
            if (destinations.Count == 0)
                continue;

            var entities = _entityLookup.GetEntitiesInRange<TransformComponent>(Transform(entity).Coordinates, options.Range);
            foreach (var targetEntity in entities)
            {
                if (targetEntity.Comp.Anchored)
                    continue;

                if (!STUtilsMap.InWorld((targetEntity, targetEntity), EntityManager))
                    continue;

                if (_whitelistSystem.IsWhitelistFail(options.Whitelist, targetEntity))
                    continue;

                var destination = _random.Pick(destinations);
                _transform.SetCoordinates(targetEntity, Transform(destination).Coordinates);
            }
        }
    }

    private List<EntityUid> GetDestinations(List<MapId> maps, string group)
    {
        var result = new List<EntityUid>();
        var query = EntityQueryEnumerator<STAnomalyEffectTunnelComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var effectComponent, out var transformComponent))
        {
            if (!maps.Contains(transformComponent.MapID))
                continue;

            if (!effectComponent.Options.TryGetValue(group, out var options))
                continue;

            if (options.Type != STAnomalyEffectTunnelType.Exit)
                continue;

            result.Add(uid);
        }

        return result;
    }
}
