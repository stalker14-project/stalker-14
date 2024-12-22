using System.Linq;
using Content.Server._Stalker.IncomingDamage;
using Content.Server._Stalker.Teleports.NewMapTeleports;
using Content.Server.Popups;
using Content.Shared._Stalker.Teleport;
using Content.Shared.Access.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.Popups;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.Teleports.GroupTeleport;

public sealed partial class GroupTeleportSystem : SharedTeleportSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly LinkedEntitySystem _link = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
    [Dependency] private readonly SharedGodmodeSystem _godmode = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    // probably should replace this with nested hashset
    private readonly Dictionary<string, HashSet<EntityUid>> _cachedPortals = new();
    private ISawmill _sawmill = default!;
    private TimeSpan _updateTime = TimeSpan.Zero;

    public override void Initialize()
    {
        SubscribeLocalEvent<GroupTeleportComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<GroupTeleportComponent, EndCollideEvent>(OnEndCollide);
        SubscribeLocalEvent<MapsLoadedEvent>(OnMapsLoaded);
        _sawmill = Logger.GetSawmill("groupPortals");
        InitializeCommands();
    }

    private void OnMapsLoaded(ref MapsLoadedEvent args)
    {
        var query = EntityQueryEnumerator<GroupTeleportComponent>();
        while (query.MoveNext(out var uid, out var group))
        {
            if (!_cachedPortals.ContainsKey(group.Group))
                _cachedPortals.Add(group.Group, new HashSet<EntityUid>());
            _cachedPortals[group.Group].Add(uid);
        }
        _sawmill.Debug($"Successfully cached {_cachedPortals.Count} portals");
    }

    private void OnStartCollide(Entity<GroupTeleportComponent> entity, ref StartCollideEvent args)
    {
        var subject = args.OtherEntity;
        if (!entity.Comp.AllowAll)
        {
            if (!_accessReaderSystem.IsAllowed(args.OtherEntity, args.OurEntity))
                return;
        }
        if (TryComp<PortalTimeoutComponent>(subject, out var timeoutComponent) && entity.Comp.CooldownEnabled)
        {
            if (timeoutComponent.Cooldown != null)
            {
                if (timeoutComponent.Cooldown > _timing.CurTime)
                    return;
            }
        }
        else if (HasComp<PortalTimeoutComponent>(subject))
        {
            return;
        }

        // If there are no linked entity - no teleport
        if (!TryComp<LinkedEntityComponent>(entity, out var link))
            return;

        if (link.LinkedEntities.Count <= 0)
        {
            _popup.PopupEntity("Здається прохід тут з'явиться пізніше...", entity, subject, PopupType.Medium);
            return;
        }

        var randomTarget = _random.Pick(link.LinkedEntities);
        if (HasComp<GroupTeleportComponent>(randomTarget))
        {
            var timeout = EnsureComp<PortalTimeoutComponent>(subject);

            // setup decreased
            var decreased = EnsureComp<DecreasedDamageComponent>(subject);
            decreased.TimeToDelete = _timing.CurTime + TimeSpan.FromSeconds(entity.Comp.DecreasedTime);
            decreased.Modifiers = entity.Comp.ModifierSet;

            if (entity.Comp.CooldownEnabled)
                timeout.Cooldown = _timing.CurTime + TimeSpan.FromSeconds(entity.Comp.CooldownTime);

            timeout.EnteredPortal = entity;
            Dirty(subject, timeout);
        }
        var xform = Transform(randomTarget);
        TeleportEntity(subject, xform.Coordinates);
    }

    private void OnEndCollide(Entity<GroupTeleportComponent> entity, ref EndCollideEvent args)
    {
        var subject = args.OtherEntity;

        if (!TryComp<PortalTimeoutComponent>(subject, out var timeout) || timeout.EnteredPortal == entity)
            return;

        if (timeout.Cooldown != null && timeout.Cooldown <= _timing.CurTime)
        {
            RemCompDeferred<PortalTimeoutComponent>(subject);
            _godmode.DisableGodmode(subject);
        }
        else if(timeout.Cooldown == null)
            RemCompDeferred<PortalTimeoutComponent>(subject);
    }

    private bool ReLinkGroup(HashSet<EntityUid> portals)
    {
        // get any portal from our group to get targets
        var targetGroupEnt = portals.First();
        if (!TryComp<GroupTeleportComponent>(targetGroupEnt, out var groupComp))
        {
            _sawmill.Warning($"Спроба перелінкувати портал без GroupTeleportComponent {ToPrettyString(targetGroupEnt)}");
            return false;
        }

        var targetGroup = groupComp.TargetGroup;
        if (!_cachedPortals.TryGetValue(targetGroup, out var targetPortals))
        {
            _sawmill.Warning($"Спробував перелінкувати портали на {targetGroup}, який не кешується!");
            return false;
        }

        // iterating through our portals and randomizing links for them
        foreach (var portal in portals)
        {
            // TODO: Should add two iterates here,
            // first one will assign only "link-free" portals to each-other,
            // if there are only assigned portals on second iteration, we will assign two or more portals to them then
            // it'll help to avoid situation with 20 portals linked to 1
            var randomPortal = _random.Pick(targetPortals);

            // cleanup old links for our portals,
            // this one also makes sure our portals won't be linked together again(i mean, the same portals)
            CleanOldLinks(portal);
            CleanOldLinks(randomPortal);

            // linking itself
            _link.TryLink(portal, randomPortal);
        }

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_updateTime > _timing.CurTime)
            return;
        _updateTime = _timing.CurTime + TimeSpan.FromSeconds(2);

        foreach (var kvp in _cachedPortals)
        {
            var portals = kvp.Value;
            var randomPortal = _random.Pick(portals);
            if (!TryComp<GroupTeleportComponent>(randomPortal, out var comp))
                continue;
            if (comp.ReLink != null && comp.ReLink > _timing.CurTime)
                continue;
            comp.ReLink = _timing.CurTime + TimeSpan.FromSeconds(comp.ReLinkTime);

            if (!ReLinkGroup(portals))
                _sawmill.Warning($"Не вдається перев'язати портали {kvp.Key}");
        }
    }

    private void CleanOldLinks(EntityUid entity)
    {
        if (!TryComp<LinkedEntityComponent>(entity, out var links))
            return;

        foreach (var link in links.LinkedEntities)
        {
            _link.TryUnlink(entity, link);
        }
    }
}
