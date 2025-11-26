using System.Linq;
using Content.Shared._Stalker.Teleport;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Random;

namespace Content.Server._Stalker.Teleports.MapPortal;

/// <summary>
/// We use this to create a new map with a portal and link them
/// </summary>
public sealed class MapPortalSystem : SharedTeleportSystem
{
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly LinkedEntitySystem _linkedEntitySystem = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    private ISawmill _sawmill = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MapPortalComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<MapPortalComponent, EndCollideEvent>(OnEndCollide);
        _sawmill = _logManager.GetSawmill("MapPortalSystem");
    }

    private void OnStartCollide(EntityUid uid, MapPortalComponent component, ref StartCollideEvent args)
    {

        var subject = args.OtherEntity;
        if (!component.Loading)
            LoadMap(uid, component);

        // Destroy any pulls
        if (TryComp<PullableComponent>(subject, out var pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(subject, pullable);

        if (TryComp<PullerComponent>(subject, out var pulling)
            && pulling.Pulling != null && TryComp<PullableComponent>(pulling.Pulling.Value, out var subjectPulling))
            _pulling.TryStopPull(pulling.Pulling.Value, subjectPulling);

        // If there is a timeout on a person we just return out of a function not to teleport that entity back.
        if (HasComp<PortalTimeoutComponent>(subject))
            return;

        // Check for links and teleport entity to another linked entity
        if (TryComp<LinkedEntityComponent>(uid, out var link))
        {
            if (!link.LinkedEntities.Any())
                return;

            var target = _random.Pick(link.LinkedEntities);

            if (HasComp<MapPortalComponent>(target))
            {
                var timeout = EnsureComp<PortalTimeoutComponent>(subject);
                timeout.EnteredPortal = uid;
                Dirty(subject, timeout);
            }

            var xform = Transform(target);
            TeleportEntity(subject, xform.Coordinates, false);
        }
    }
    // Remove timeout from entity
    private void OnEndCollide(EntityUid uid, MapPortalComponent component, ref EndCollideEvent args)
    {
        var subject = args.OtherEntity;

        if (TryComp<PortalTimeoutComponent>(subject, out var timeout) && timeout.EnteredPortal != uid)
        {
            RemCompDeferred<PortalTimeoutComponent>(subject);
        }
    }

    // Just to load a new map if there are no one
    private void LoadMap(EntityUid uid, MapPortalComponent component)
    {
        // Check for links, if there is one we just return out of a function not to create one more map
        if (HasComp<LinkedEntityComponent>(uid))
            return;
        component.Loading = true;

        if (component.MapPath == null)
            return;
        if (_mapSystem.MapExists(component.MapId))
            return;

        var map = _mapSystem.CreateMap(out var mapId, true);
        component.MapId = mapId;
        //// Loads map from a specified path and initializes it.
        if (!_mapLoader.TryLoad(mapId, component.MapPath, out var grids))
            _sawmill.Error($"Map with id {mapId} from {component.MapPath} load failed.");

        //if (!_mapSystem.IsMapInitialized(mapId))
        //    _mapManager.DoMapInitialize(mapId);


        Dirty(uid, component);
        if (grids == null)
            return;

        // Get another entity on created map to make a link
        if (!TryComp<TransformComponent>(grids.FirstOrDefault(), out var transform))
            return;

        var enumerator = transform.ChildEnumerator;
        while (enumerator.MoveNext(out var ent))
        {
            if (!TryComp<MapPortalComponent>(ent, out var portal))
                continue;
            UpdateLinks();
        }

    }

    private void UpdateLinks()
    {
        var maps = _mapManager.GetAllMapIds();

        foreach (var map in maps)
        {
            var mapUid = _mapManager.GetMapEntityId(map);

            // Iterate through portals on the current map
            var enumerator = Transform(mapUid).ChildEnumerator;
            while (enumerator.MoveNext(out var entity))
            {
                if (!TryComp<MapPortalComponent>(entity, out var portal))
                    continue;

                // Link the current portal with portals on other maps
                LinkPortalsAcrossMaps(entity, portal, maps);
            }
        }
    }

    private void LinkPortalsAcrossMaps(EntityUid uid, MapPortalComponent portal, IEnumerable<MapId> maps)
    {
        // Iterate through other maps
        foreach (var anotherMap in maps)
        {

            var mapUid = _mapManager.GetMapEntityId(anotherMap);

            // Iterate through portals on the other map
            var enumerator = Transform(mapUid).ChildEnumerator;
            while (enumerator.MoveNext(out var anotherPortalEntity))
            {
                if (!TryComp<MapPortalComponent>(anotherPortalEntity, out var anotherPortal))
                    continue;

                // Link portals with the same name on different maps
                if (portal.PortalName == anotherPortal.PortalName && anotherPortalEntity != uid)
                {
                    _linkedEntitySystem.TryLink(uid, anotherPortalEntity);
                }
            }
        }
    }
}
