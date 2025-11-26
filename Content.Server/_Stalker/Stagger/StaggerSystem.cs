using Content.Server.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Robust.Server.GameObjects;

namespace Content.Server._Stalker.Stagger;

public sealed class StaggerSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public const float UpdateDelay = 0.7f;
    private float _updateTime = 0;

    public override void Initialize()
    {
        SubscribeLocalEvent<StaggerComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeedModifiers);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTime += frameTime;
        if (_updateTime < UpdateDelay)
            return;

        _updateTime -= UpdateDelay;


        var query = EntityQueryEnumerator<StaggerComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var stagger, out var mobState, out var xform))
        {
            if (stagger.NetUserId is null)
            {
                if (!_mind.TryGetMind(uid, out _, out var mind))
                    continue;

                stagger.NetUserId = _mind.GetSession(mind)?.UserId;
                continue;
            }

            if (mobState.CurrentState != MobState.Alive)
                continue;

            var closestDistance = float.MaxValue;
            var near = _entityLookup.GetEntitiesInRange<StaggerComponent>(xform.Coordinates, stagger.SlownessDistanceMax);
            var finded = false;
            foreach (var entity in near)
            {
                if (entity.Comp.NetUserId != stagger.NetUserId)
                    continue;

                if (!_mobState.IsDead(entity))
                    continue;

                var dist = (_transform.GetWorldPosition(xform) - _transform.GetWorldPosition(entity)).Length();
                if (dist >= closestDistance)
                    continue;

                closestDistance = dist;
                finded = true;
            }

            if (!finded)
            {
                stagger.MovementSpeedModifier = 1f;
                _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
                continue;
            }

            stagger.MovementSpeedModifier = Math.Min(stagger.SlownessDistanceMax, closestDistance + stagger.SlownessDistanceMin) / stagger.SlownessDistanceMax;
            _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
        }
    }

    private void OnRefreshMovementSpeedModifiers(Entity<StaggerComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.MovementSpeedModifier, ent.Comp.MovementSpeedModifier);
    }
}
