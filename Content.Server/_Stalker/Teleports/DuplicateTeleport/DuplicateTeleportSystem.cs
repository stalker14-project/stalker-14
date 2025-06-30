using System.Numerics;
using Content.Server._Stalker.Sponsors;
using Content.Server._Stalker.StalkerDB;
using Content.Server._Stalker.StalkerRepository;
using Content.Server._Stalker.Storage;
using Content.Server._Stalker.Teleports.StalkerBandPortal;
using Content.Shared._Stalker.StalkerRepository;
using Content.Shared._Stalker.Teleport;
using Content.Shared.Access.Systems;
using Content.Shared.Teleportation.Components;
using Microsoft.Extensions.Logging;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using SponsorSystem = Content.Server._Stalker.Sponsors.System.SponsorSystem;

namespace Content.Server._Stalker.Teleports.DuplicateTeleport;

public sealed class DuplicateTeleportSystem : SharedTeleportSystem
{
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly StalkerDbSystem _stalkerDbSystem = default!;
    [Dependency] private readonly StalkerStorageSystem _stalkerStorageSystem = default!;
    [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
    [Dependency] private readonly StalkerPortalSystem _stalkerPortals = default!;
    [Dependency] private readonly StalkerRepositorySystem _repositorySystem = default!;
    [Dependency] private readonly SponsorSystem _sponsorSystem = default!;
    
    private const string MoneyId = "Roubles";
    private ISawmill _sawmill = default!;
    private Dictionary<string, EntityUid> ArenaMap { get; } = new();
    private Dictionary<string, EntityUid?> ArenaGrid { get; } = new();
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DuplicateTeleportComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<DuplicateTeleportComponent, EndCollideEvent>(OnEndCollide);
        _sawmill = Logger.GetSawmill("duplicateRepositories");
    }
    private void OnCollide(Entity<DuplicateTeleportComponent> entity, ref StartCollideEvent args)
    {
        var subject = args.OtherEntity;
        var portalEnt = args.OurEntity;

        // timeout entity
        if (HasComp<PortalTimeoutComponent>(subject))
            return;

        if (!TryComp<ActorComponent>(subject, out var actor))
            return;

        if (!_accessReaderSystem.IsAllowed(subject, portalEnt))
            return;

        var timeout = EnsureComp<PortalTimeoutComponent>(subject);
        timeout.EnteredPortal = portalEnt;
        Dirty(subject, timeout);

        var (mapUid, gridUid) = StalkerAssertArenaLoaded(actor.PlayerSession.Name, actor.PlayerSession.UserId, entity.Comp, entity);
        TeleportEntity(subject, new EntityCoordinates(gridUid ?? mapUid, Vector2.One));
    }

    private void OnEndCollide(Entity<DuplicateTeleportComponent> entity, ref EndCollideEvent args)
    {
        if (TryComp<PortalTimeoutComponent>(args.OtherEntity, out var timeout) &&
            timeout.EnteredPortal != args.OurEntity)
            RemCompDeferred<PortalTimeoutComponent>(args.OtherEntity);
    }

    private (EntityUid Map, EntityUid? Grid) StalkerAssertArenaLoaded(string adminCkey, NetUserId userId, DuplicateTeleportComponent component, EntityUid? returnTeleportEntityUid)
    {
        var concatenated = component.DuplicateString + adminCkey;

        if (_stalkerPortals.InStalkerTeleportDataList(concatenated))
        {
            var stalkerTeleportData = _stalkerPortals.GetFromStalkerTeleportDataList(concatenated);

            _stalkerPortals.SetReturnPortal(stalkerTeleportData.GridId, concatenated, returnTeleportEntityUid);

            return (stalkerTeleportData.MapId, stalkerTeleportData.GridId);
        }

        _mapSystem.CreateMap(out var mapId, true);
        ArenaMap[concatenated] = _mapManager.GetMapEntityId(mapId);
        _metaDataSystem.SetEntityName(ArenaMap[concatenated], $"STALKER_MAP-{concatenated}");
        var map = Comp<MapComponent>(ArenaMap[concatenated]);
        var isLoaded = _map.TryLoad(map.MapId, component.ArenaMapPath, out var grids);
        _mapSystem.SetPaused(map.MapId, false);
        if (grids is null || !isLoaded)
        {
            _sawmill.Error($"Couldn't load a map {component.ArenaMapPath} for {concatenated}");
            return (ArenaMap[concatenated], null);
        }

        if (grids.Count != 0)
        {
            _metaDataSystem.SetEntityName(grids[0], $"STALKER_GRID-{concatenated}");
            ArenaGrid[concatenated] = grids[0];
        }
        else
            ArenaGrid[concatenated] = null;

        if (TryComp(grids[0], out TransformComponent? xform))
        {
            // TODO: Obsolete
            var enumerator = xform.ChildEnumerator;
            while (enumerator.MoveNext(out var entity))
            {
                if (!TryComp(entity, out StalkerRepositoryComponent? stalkerRepositoryComponent))
                    continue;

                stalkerRepositoryComponent.StorageOwner = concatenated;
                stalkerRepositoryComponent.LoadedDbJson = _stalkerDbSystem.GetInventoryJson(concatenated);
                _stalkerStorageSystem.LoadStalkerItemsByEntityUid(entity);
                var ev = new RepositoryAdminSetEvent(GetNetEntity(entity), adminCkey);
                RaiseLocalEvent(entity, ev);

                // Repository weight
                // Set specific for this repository maximum weight
                if (component.MaxWeight != 0)
                {
                    stalkerRepositoryComponent.MaxWeight = component.MaxWeight;
                }
                
                // Sponsors
                stalkerRepositoryComponent.MaxWeight =
                    _sponsorSystem.GetRepositoryWeight(userId, stalkerRepositoryComponent.MaxWeight);

                // remove first stack of money that appears on new records added
                var deleted = _repositorySystem.RemoveItems((entity, stalkerRepositoryComponent), MoneyId, concatenated);
                if (deleted != 1)
                    _sawmill.Error($"Expected 1 deleted items, but got {deleted}");
            }
        }

        _stalkerPortals.StalkerArenaDataList.Add(new StalkerPortalSystem.StalkerArenaData(concatenated,ArenaMap[concatenated],ArenaGrid[concatenated]));

        _stalkerPortals.SetReturnPortal(ArenaGrid[concatenated],concatenated,returnTeleportEntityUid);

        return (ArenaMap[concatenated], ArenaGrid[concatenated]);
    }
}
