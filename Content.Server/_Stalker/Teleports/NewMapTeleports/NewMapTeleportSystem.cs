using System.Linq;
using System.Threading.Tasks;
using Content.Server._Stalker.IncomingDamage;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared._Stalker.Teleport;
using Content.Shared.Access.Systems;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.Teleports.NewMapTeleports;
// TODO: Rename this system
public sealed class NewMapTeleportSystem : SharedTeleportSystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly LinkedEntitySystem _link = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedGodmodeSystem _godmode = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly LinkedEntitySystem _linkedEntitySystem = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("NewMapTeleport");

        SubscribeLocalEvent<NewMapTeleportComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<NewMapTeleportComponent, EndCollideEvent>(OnEndCollide);
        //SubscribeLocalEvent<RoundStartedEvent>(OnRoundStart);
        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad);
    }
    private void OnPostGameMapLoad(PostGameMapLoad args)
    {
#if !DEBUG
        var prototypes = _protoMan.EnumeratePrototypes<MapLoaderPrototype>();
        foreach (var prototype in prototypes)
        {
            foreach (var path in prototype.MapPaths.Values)
            {
                LoadMap(path);
            }
        }
#endif

        var ev = new MapsLoadedEvent();
        RaiseLocalEvent(ref ev);
        UpdateLinks();
    }

    private void UpdateLinks()
    {
        var maps = _mapManager.GetAllMapIds();

        foreach (var map in maps)
        {
            var mapUid = _mapManager.GetMapEntityId(map);

            // Iterate through entities on grids
            var enumerator = Transform(mapUid).ChildEnumerator;
            while (enumerator.MoveNext(out var uid))
            {
                if (!TryComp<NewMapTeleportComponent>(uid, out var portal))
                    continue;

                // Link the current portal with portals on other maps
                LinkPortalsAcrossMaps(uid, portal, maps);
            }
        }
    }
    private void LinkPortalsAcrossMaps(EntityUid uid, NewMapTeleportComponent portal, IEnumerable<MapId> maps)
    {

        foreach (var anotherMap in maps)
        {
            var mapUid = _mapManager.GetMapEntityId(anotherMap);

            var enumerator = Transform(mapUid).ChildEnumerator;
            while (enumerator.MoveNext(out var anotherPortalEntity))
            {
                if (!TryComp<NewMapTeleportComponent>(anotherPortalEntity, out var anotherPortal))
                    continue;

                // Link portals with the same name on different maps
                if (portal.PortalName == anotherPortal.PortalName && anotherPortalEntity != uid) {
                    _link.TryLink(uid, anotherPortalEntity);
                }
            }

        }
    }

    private void LoadMap(string path)
    {
        var map = _mapSystem.CreateMap(out var mapId);
        if (_mapLoader.TryLoad(mapId, path, out _) && !_mapSystem.IsInitialized(mapId))
        {
            _mapSystem.InitializeMap(mapId);
        }
        if (_mapSystem.IsPaused(mapId))
        {
            _mapSystem.SetPaused(mapId, false);
        }
        if (!_mapSystem.IsInitialized(mapId))
            _sawmill.Error($"Map with id {mapId} from {path} load failed.");
    }
    private void OnStartCollide(EntityUid uid, NewMapTeleportComponent component, ref StartCollideEvent args)
    {
        if (!component.AllowAll)
        {
            if (!_accessReaderSystem.IsAllowed(args.OtherEntity, args.OurEntity))
                return;
        }
        if (component.IsCollisionDisabled)
            return;
        var subject = args.OtherEntity;

        // If there is a timeout on a person we just return out of a function not to teleport that entity back.
        if (TryComp<PortalTimeoutComponent>(subject, out var timeoutComponent) && component.CooldownEnabled && timeoutComponent.Cooldown != null)
        {
            if (timeoutComponent.Cooldown > _timing.CurTime)
                return;
        }
        else if (HasComp<PortalTimeoutComponent>(subject))
            return;


        // If there are no linked entity - link one
        if (!TryComp<LinkedEntityComponent>(uid, out var link))
        {
            var ents = _entMan.GetEntities();
            foreach (var ent in ents)
            {
                if (!TryComp<NewMapTeleportComponent>(ent, out var local))
                    continue;

                if (local.PortalName != component.PortalName || uid == ent)
                    continue;

                _linkedEntitySystem.TryLink(uid, ent, true);
            }
        }

        if (link == null)
        {
            _sawmill.Error($"{component.PortalName} link is null");
            return;
        }

        if (!link.LinkedEntities.Any())
        {
            _sawmill.Error($"{component.PortalName} doesn't have linked entities");
            return;
        }

        var target = link.LinkedEntities.FirstOrDefault();

        if (target == default)
        {
            _sawmill.Error($"{component.PortalName} target is empty");
            return;
        }

        if (HasComp<NewMapTeleportComponent>(target))
        {
            var timeout = EnsureComp<PortalTimeoutComponent>(subject);

            // setup decreased
            var decreased = EnsureComp<DecreasedDamageComponent>(subject);
            decreased.TimeToDelete = _timing.CurTime + TimeSpan.FromSeconds(component.DecreasedTime);
            decreased.Modifiers = component.ModifierSet;

            if (component.CooldownEnabled)
                timeout.Cooldown = _timing.CurTime + TimeSpan.FromSeconds(component.CooldownTime);

            timeout.EnteredPortal = uid;
            Dirty(subject, timeout);
        }

        var xform = Transform(target);
        TeleportEntity(subject, xform.Coordinates, true);
    }

    private void OnEndCollide(EntityUid uid, NewMapTeleportComponent component, ref EndCollideEvent args)
    {
        var subject = args.OtherEntity;

        if (!TryComp<PortalTimeoutComponent>(subject, out var timeout) || timeout.EnteredPortal == uid)
            return;

        if (timeout.Cooldown != null && timeout.Cooldown <= _timing.CurTime)
        {
            RemCompDeferred<PortalTimeoutComponent>(subject);
            _godmode.DisableGodmode(subject);
        }
        else if(timeout.Cooldown == null)
            RemCompDeferred<PortalTimeoutComponent>(subject);
    }
}

[ByRefEvent]
public record struct MapsLoadedEvent;
