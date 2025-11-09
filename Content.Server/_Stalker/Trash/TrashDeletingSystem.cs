using Content.Server.Chat.Managers;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.Trash;

public sealed class TrashDeletingSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    /// <summary>
    /// next time to clean up trash
    /// </summary>
    private TimeSpan _nextTimeUpdate = TimeSpan.Zero;

    /// <summary>
    /// time in minutes between trash cleanups
    /// </summary>
    private readonly int _updateTime = 15;

    /// <summary>
    /// if a warning has been issued for the next cleanup
    /// </summary>
    private bool _warningIssued;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TrashComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<TrashComponent, EntParentChangedMessage>(OnChangedParent);
        _nextTimeUpdate = _timing.CurTime + TimeSpan.FromMinutes(_updateTime);
    }

    private void OnInit(Entity<TrashComponent> entity, ref ComponentInit args)
    {
        if (entity.Comp.IgnoreConditions)
            SetTime(entity);
    }
    private void OnChangedParent(Entity<TrashComponent> ent, ref EntParentChangedMessage args)
    {
        if (_mapMan.IsMap(args.Transform.ParentUid) || _mapMan.IsGrid(args.Transform.ParentUid))
            SetTime(ent);
        else if (!ent.Comp.IgnoreConditions)
            ResetTime(ent);
    }

    private void SetTime(Entity<TrashComponent> ent)
    {
        var comp = ent.Comp;

        if (comp.DeletingTime != null)
            return;

        comp.DeletingTime = _timing.CurTime + TimeSpan.FromSeconds(comp.TimeToDelete);
    }

    private void ResetTime(Entity<TrashComponent> ent)
    {
        var comp = ent.Comp;
        comp.DeletingTime = null;
    }

    public void SetNextCleanupTime(int seconds)
    {
        if (seconds < 0)
            throw new ArgumentException("time must be at least 1 second");

        _nextTimeUpdate = _timing.CurTime + TimeSpan.FromSeconds(seconds);
        _warningIssued = false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_warningIssued && _timing.CurTime >= _nextTimeUpdate - TimeSpan.FromSeconds(30))
        {
            var timeBeforeCleanup = Math.Round((_nextTimeUpdate - _timing.CurTime).TotalSeconds);
            _chat.DispatchServerAnnouncement($"Cleaning of garbage and empty caches will occur in {timeBeforeCleanup} seconds, objects on the floor may disappear!");
            _warningIssued = true;
        }

        if (_timing.CurTime <= _nextTimeUpdate)
            return;

        _chat.DispatchServerAnnouncement("There was a cleaning of garbage and empty caches, some items on the floor are gone!");
        RaiseLocalEvent(new RequestClearArenaGridsEvent());

        var trashEnts = EntityQueryEnumerator<TrashComponent>();
        while (trashEnts.MoveNext(out var uid, out var comp))
        {
            if (comp.DeletingTime == null)
                continue;
            var parentUid = Transform(uid).ParentUid;

            if (!_mapMan.IsMap(parentUid) &&
                !_mapMan.IsGrid(parentUid) &&
                !comp.IgnoreConditions)
                ResetTime((uid, comp));

            if (comp.DeletingTime <= _timing.CurTime)
                QueueDel(uid);
        }

        _warningIssued = false;
        _nextTimeUpdate = _timing.CurTime + TimeSpan.FromMinutes(_updateTime);

        // --- Pause maps with no players ---
        foreach (var map in _mapMan.GetAllMapIds())
        {
            if (map == MapId.Nullspace || map == new MapId(1)) // Never pause map 1
                continue;

            // Skip maps that explicitly disable pausing
            var mapEntity = _mapMan.GetMapEntityIdOrThrow(map);
            if (mapEntity != EntityUid.Invalid && EntityManager.HasComponent<NoPausingComponent>(mapEntity))
                continue;

            bool hasPlayer = false;
            foreach (var session in _playerManager.Sessions)
            {
                if (session.AttachedEntity is not { Valid: true } ent)
                    continue;
                var xform = Transform(ent);
                if (xform.MapID == map)
                {
                    hasPlayer = true;
                    break;
                }
            }
            if (!hasPlayer && !_mapMan.IsMapPaused(map))
            {
                _mapMan.SetMapPaused(map, true);
            }
        }
    }


}
