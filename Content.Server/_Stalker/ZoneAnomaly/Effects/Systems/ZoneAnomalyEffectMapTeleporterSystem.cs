using System.Numerics;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectMapTeleporterSystem : SharedZoneAnomalyEffectMapTeleporterSystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
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
        // Creating a map, a common thing
        if (!_mapLoader.TryLoadMap(effect.Comp.MapPath, out var map, out _, DeserializationOptions.Default with {InitializeMaps = true}))
            return null;

        // Save the created map so as not to shit on them
        var mapId = map.Value.Comp.MapId;
        var mapUid = _mapSystem.GetMap(mapId);
        effect.Comp.MapId = mapId;
        effect.Comp.MapEntity = mapUid;

        return mapUid;
    }
}
