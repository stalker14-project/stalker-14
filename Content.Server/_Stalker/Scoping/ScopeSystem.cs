/// Portions of this file are derived from the RMC-14 project, specifically from
/// https://github.com/RMC-14/RMC-14/tree/481a21c95148f5a7bff6ed1609324c836663ca30/Content.Shared/_RMC14/Scoping.
/// These files have been modified for use in this project.
/// The original code is licensed under the MIT License:

using Content.Shared._Stalker.Scoping;
using Robust.Shared.Player;
using Robust.Server.GameObjects;

namespace Content.Server._Stalker.Scoping;

public sealed class ScopeSystem : SharedScopeSystem
{
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;

    protected override Direction? StartScoping(Entity<ScopeComponent> scope, EntityUid user)
    {
        if (base.StartScoping(scope, user) is not { } direction)
            return null;

        scope.Comp.User = user;

        if (TryComp(user, out ActorComponent? actor))
        {
            var coords = Transform(user).Coordinates;
            var offset = GetScopeOffset(scope, direction);
            scope.Comp.RelayEntity = SpawnAtPosition(null, coords.Offset(offset));
            _viewSubscriber.AddViewSubscriber(scope.Comp.RelayEntity.Value, actor.PlayerSession);
        }

        return direction;
    }

    protected override bool Unscope(Entity<ScopeComponent> scope)
    {
        var user = scope.Comp.User;
        if (!base.Unscope(scope))
            return false;

        DeleteRelay(scope, user);
        return true;
    }

    protected override void DeleteRelay(Entity<ScopeComponent> scope, EntityUid? user)
    {
        if (scope.Comp.RelayEntity is not { } relay)
            return;

        scope.Comp.RelayEntity = null;

        if (TryComp(user, out ActorComponent? actor))
            _viewSubscriber.RemoveViewSubscriber(relay, actor.PlayerSession);

        if (!TerminatingOrDeleted(relay))
            QueueDel(relay);
    }
}
