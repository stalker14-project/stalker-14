using Content.Server.Administration.Commands;
using Content.Server.Forensics;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Stalker.Teleport;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Stalker.MapPausing;

/// <summary>
/// This system's life reserved for pausing unused maps, like repositories and other shit we don't need to process
/// System could help us reduce amount of entities which game needs to handle like, NPC moving and smth like that
/// </summary>
public sealed class MapPauseSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapMan = default!;
    private ISawmill _sawmill = default!;
    private bool _enabled = false;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BeforeEntityTeleportedEvent>(OnPlayerTeleported);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<DnaComponent, RespawnedByCommandEvent>(OnRespawnNow);


        // had to call that method before SpawnPointSystem will try to find any valid spawn points
        SubscribeAllEvent<PlayerSpawningEvent>(OnSpawn,
            before: new []{typeof(SpawnPointSystem)});

        _sawmill = Logger.GetSawmill("mapPause");
    }

    private void OnSpawn(PlayerSpawningEvent args)
    {
        if (!_enabled)
            return;

        // get stations' grids
        if (args.Station is not {} station ||
            !TryComp<StationDataComponent>(station, out var stationData))
            return;

        // get random grid(probably planet itself)
        var grid = stationData.Grids.FirstOrNull();
        if (grid == null)
            return;

        // get grid's map and unfreeze if its needed
        var mapId = Transform(grid.Value).MapID;
        if (!_mapMan.IsMapPaused(mapId))
            return;

        _mapMan.SetMapPaused(mapId, false);
        _sawmill.Info($"UnPaused {mapId} map because another player spawned on it");
    }

    private void OnRespawnNow(Entity<DnaComponent> newEntity, ref RespawnedByCommandEvent args)
    {
        if (!_enabled)
            return;

        var mapId = Transform(newEntity).MapID;
        // make sure that map is unpaused
        CheckDestination(mapId);
        var oldEnt = GetEntity(args.OldEntity);
        if (Deleted(oldEnt))
            return;
        var oldMapId = Transform(oldEnt).MapID;
        CheckOrigin(oldMapId);
    }
    private void OnPlayerAttached(PlayerAttachedEvent args, EntitySessionEventArgs msg)
    {
        if (!_enabled)
            return;

        var mapId = Transform(args.Entity).MapID;

        if (!_mapMan.IsMapPaused(mapId))
            return;

        _mapMan.SetMapPaused(mapId, false);
        _sawmill.Info($"Unpaused map {mapId} because of player attached to entity there");
    }
    private void OnPlayerTeleported(ref BeforeEntityTeleportedEvent args)
    {
        if (!_enabled)
            return;

        // if its local teleport -> do nothing
        if (args.Origin == args.Destination)
            return;

        if (!TryComp<ActorComponent>(args.EntityUid, out var actor) || actor.PlayerSession.AttachedEntity == null)
            return;

        CheckDestination(args.Destination);
        CheckOrigin(args.Origin);
    }

    private void CheckDestination(MapId map)
    {
        if (!_enabled)
            return;

        if (!_mapMan.IsMapPaused(map))
            return;

        _mapMan.SetMapPaused(map, false);
        _sawmill.Info($"Unpausing {map} due to player teleported there");
    }
    private void CheckOrigin(MapId origin)
    {
        if (!_enabled)
            return;

        var playersOnOrigin = Filter.BroadcastMap(origin);
        if (playersOnOrigin.Count-1 <= 0)
        {
            _mapMan.SetMapPaused(origin, true);
            _sawmill.Info($"Paused map {origin} because of 0 players there");
            return;
        }

        if (!_mapMan.IsMapPaused(origin))
            return;

        // this one shouldn't eventually happen, i hope
        _mapMan.SetMapPaused(origin, false);
        _sawmill.Warning($"Map was paused but there was {playersOnOrigin.Count} on it, unpausing...");
    }
}
