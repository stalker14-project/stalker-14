using System.Numerics;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectMapTeleporterSystem : SharedZoneAnomalyEffectMapTeleporterSystem
{
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;

    public override void Initialize()
    {

        SubscribeLocalEvent<ZoneAnomalyEffectMapTeleporterComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZoneAnomalyEffectMapTeleporterComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ZoneAnomalyEffectMapTeleporterComponent, ZoneAnomalyActivateEvent>(OnActivate);
    }

    private void OnStartup(Entity<ZoneAnomalyEffectMapTeleporterComponent> effect, ref ComponentStartup args)
    {
        if (GetFtlTargetMap(effect) is not { } mapEntity)
            return;

        effect.Comp.MapEntity = mapEntity;
    }

    private void OnRemove(Entity<ZoneAnomalyEffectMapTeleporterComponent> effect, ref ComponentRemove args)
    {
        if (effect.Comp.MapEntity is not { } entity)
            return;

        QueueDel(entity);
    }

    private void OnActivate(Entity<ZoneAnomalyEffectMapTeleporterComponent> effect, ref ZoneAnomalyActivateEvent args)
    {
        if (!TryComp<ZoneAnomalyComponent>(effect, out var anomaly))
            return;

        if (effect.Comp.MapEntity is null || effect.Comp.MapId is not { } mapId)
            return;

        foreach (var target in anomaly.InAnomaly)
        {
            TeleportEntity(target, new MapCoordinates(Vector2.Zero, mapId));
        }
    }

    private EntityUid? GetFtlTargetMap(Entity<ZoneAnomalyEffectMapTeleporterComponent> effect)
    {
        // Can be used for gaming events
        if (effect.Comp.MapEntity is var map && Exists(map))
            return map;

        // Creating a map, a common thing
        var mapId = _map.CreateMap();
        var mapUid = _map.GetMapEntityId(mapId);

        _map.AddUninitializedMap(mapId);

        if (!_mapLoader.TryLoad(mapId, effect.Comp.MapPath.CanonPath, out _))
            return null;

        // Save the created map so as not to shit on them
        effect.Comp.MapId = mapId;
        effect.Comp.MapEntity = mapUid;

        _map.DoMapInitialize(mapId);
        return mapUid;
    }
}
