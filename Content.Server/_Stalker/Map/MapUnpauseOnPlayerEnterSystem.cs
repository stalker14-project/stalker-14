using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.GameObjects;

namespace Content.Server._Stalker.Map;

public sealed class MapUnpauseOnPlayerEnterSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapMan = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EntParentChangedMessage>(OnEntParentChanged);
    }

    private void OnEntParentChanged(ref EntParentChangedMessage args)
    {
        // Only care about player-attached entities
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity == args.Entity)
            {
                var xform = EntityManager.GetComponent<TransformComponent>(args.Entity);
                var mapId = xform.MapID;
                if (_mapMan.IsMapPaused(mapId))
                {
                    _mapMan.SetMapPaused(mapId, false);
                }
                break;
            }
        }
    }
}

