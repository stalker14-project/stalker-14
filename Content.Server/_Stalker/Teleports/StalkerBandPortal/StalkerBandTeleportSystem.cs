using System.Linq;
using System.Numerics;
using Content.Server._Stalker.StalkerDB;
using Content.Server._Stalker.Storage;
using Content.Shared._Stalker.StalkerRepository;
using Content.Shared._Stalker.Teleport;
using Content.Shared.Access.Systems;
using Content.Shared.Teleportation.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Stalker.Teleports.StalkerBandPortal;

public sealed class StalkerBandTeleportSystem : SharedTeleportSystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly StalkerDbSystem _stalkerDbSystem = default!;
    [Dependency] private readonly StalkerStorageSystem _stalkerStorageSystem = default!;
    [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
    [Dependency] private readonly StalkerPortalSystem _stalkerPortals = default!;
    private static readonly ResPath ArenaMapPath = new("/Maps/_ST/PersonalStalkerArena/StalkerMap.yml");
    private Dictionary<string, EntityUid> ArenaMap { get; } = new();
    private Dictionary<string, EntityUid?> ArenaGrid { get; } = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StalkerBandTeleportComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<StalkerBandTeleportComponent, EndCollideEvent>(OnEndCollide);
        SubscribeLocalEvent<StalkerPortalPersonalComponent, EndCollideEvent>(OnEndCollidePersonal);
    }

    private void OnEndCollidePersonal(Entity<StalkerPortalPersonalComponent> entity, ref EndCollideEvent args)
    {
        if (TryComp<PortalTimeoutComponent>(args.OtherEntity, out var timeout) &&
            timeout.EnteredPortal != args.OurEntity)
            RemCompDeferred<PortalTimeoutComponent>(args.OtherEntity);
    }
    private void OnCollide(Entity<StalkerBandTeleportComponent> entity, ref StartCollideEvent args)
    {
        var subject = args.OtherEntity;
        var portalEnt = args.OurEntity;

        // timeout entity
        if (HasComp<PortalTimeoutComponent>(subject))
            return;

        if (!TryComp<ActorComponent>(subject, out _))
            return;

        if (!_accessReaderSystem.IsAllowed(subject, portalEnt))
            return;

        var timeout = EnsureComp<PortalTimeoutComponent>(subject);
        timeout.EnteredPortal = portalEnt;
        Dirty(subject, timeout);

        var (mapUid, gridUid) = StalkerAssertArenaLoaded(entity.Comp, entity);
        TeleportEntity(subject, new EntityCoordinates(gridUid ?? mapUid, Vector2.One));
    }

    private void OnEndCollide(Entity<StalkerBandTeleportComponent> entity, ref EndCollideEvent args)
    {
        if (TryComp<PortalTimeoutComponent>(args.OtherEntity, out var timeout) &&
            timeout.EnteredPortal != args.OurEntity)
            RemCompDeferred<PortalTimeoutComponent>(args.OtherEntity);
    }
    private (EntityUid Map, EntityUid? Grid) StalkerAssertArenaLoaded(StalkerBandTeleportComponent component, EntityUid? returnTeleportEntityUid)
    {

        if (_stalkerPortals.InStalkerTeleportDataList(component.PortalName))
        {
            var stalkerTeleportData = _stalkerPortals.GetFromStalkerTeleportDataList(component.PortalName);

            _stalkerPortals.SetReturnPortal(stalkerTeleportData.GridId,component.PortalName,returnTeleportEntityUid);

            return (stalkerTeleportData.MapId,stalkerTeleportData.GridId);
        }

        _map.TryLoadMap(
            ArenaMapPath,
            out var map,
            out var grids,
            DeserializationOptions.Default with { InitializeMaps = true });

        if (map != null)
        {
            ArenaMap[component.PortalName] = map.Value.Owner;
            _metaDataSystem.SetEntityName(ArenaMap[component.PortalName], $"STALKER_MAP-{component.PortalName}");
        }

        EntityUid? firstGrid = null;

        if (grids != null && grids.Count != 0)
        {
            firstGrid = grids.First();
            _metaDataSystem.SetEntityName(firstGrid.Value, $"STALKER_GRID-{component.PortalName}");
            ArenaGrid[component.PortalName] = firstGrid;
        }
        else
            ArenaGrid[component.PortalName] = null;

        if (firstGrid != null && TryComp(firstGrid.Value, out TransformComponent? xform))
        {
            // TODO: Obsolete
            var enumerator = xform.ChildEnumerator;
            while(enumerator.MoveNext(out var entity))
            {
                if (!TryComp(entity, out StalkerRepositoryComponent? stalkerRepositoryComponent))
                    continue;

                stalkerRepositoryComponent.StorageOwner = component.PortalName;
                stalkerRepositoryComponent.LoadedDbJson = _stalkerDbSystem.GetInventoryJson(component.PortalName);
                stalkerRepositoryComponent.MaxWeight = component.RepositoryWeight;
                _stalkerStorageSystem.LoadStalkerItemsByEntityUid(entity);
            }
        }

        _stalkerPortals.StalkerArenaDataList.Add(new StalkerPortalSystem.StalkerArenaData(component.PortalName,ArenaMap[component.PortalName],ArenaGrid[component.PortalName]));

        _stalkerPortals.SetReturnPortal(ArenaGrid[component.PortalName],component.PortalName,returnTeleportEntityUid);

        return (ArenaMap[component.PortalName], ArenaGrid[component.PortalName]);
    }
}
