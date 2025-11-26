using Content.Shared.Stunnable;
using Content.Shared.Throwing;

namespace Content.Shared._Stalker.Psyonics.Actions.Springboard;

public sealed class PsyonicsActionSpringboardSystem : BasePsyonicsActionSystem<PsyonicsActionSpringboardComponent, PsyonicsActionSpringboardEvent>
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    protected override void OnAction(Entity<PsyonicsActionSpringboardComponent> entity, ref PsyonicsActionSpringboardEvent args)
    {
        base.OnAction(entity, ref args);

        var targets = _entityLookup.GetEntitiesInRange(args.Target, entity.Comp.Radius);
        foreach (var target in targets)
        {
            var position = (Transform(target).Coordinates.Position - args.Target.Position).Normalized() * 1f;
            _throwing.TryThrow(target, position, entity.Comp.Strength);

            if (entity.Comp.Stun)
            {
                _stun.TryStun(target, entity.Comp.StunDuration, true);
            }
        }

        args.Handled = true;
    }
}
