using Content.Server.Chat.Managers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.Trash;

public sealed class TrashDeletingSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IChatManager _chat = default!;

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
            _chat.DispatchServerAnnouncement($"Очистка мусора и пустых схронов произойдет через {timeBeforeCleanup} секунд, предметы на полу могут пропасть!");
            _warningIssued = true;
        }

        if (_timing.CurTime <= _nextTimeUpdate)
            return;

        _chat.DispatchServerAnnouncement("Произошла очистка мусора и пустых схронов, некоторые предметы на полу пропали!");
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
    }


}
