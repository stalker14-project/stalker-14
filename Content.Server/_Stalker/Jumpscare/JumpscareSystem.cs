using Content.Server.Stunnable;
using Content.Shared._Stalker.Jumpscare;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using YamlDotNet.Core.Tokens;

namespace Content.Server._Stalker.Jumpscare;

public sealed class JumpscareSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly StunSystem _stunSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<JumpscareComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextTimeUpdate > _timing.CurTime)
                continue;
            comp.NextTimeUpdate = _timing.CurTime + TimeSpan.FromSeconds(comp.UpdateCooldown);

            if (comp.MovingToJumpTarget)
            {
                MoveTowardsTarget(uid, comp, frameTime);
                continue;
            }

            if (CheckHumanTarget(uid, comp) is not { } humanTarget)
                continue;

            if (_timing.CurTime > comp.EndTime)
            {
                comp.StartTime = _timing.CurTime;
                comp.EndTime = comp.StartTime + TimeSpan.FromSeconds(comp.ReloadTime + _random.NextFloat(-comp.RandomiseReloadTime, comp.RandomiseReloadTime));

                if (TryComp<MobStateComponent>(uid, out var entityMobState) && _mobState.IsAlive(uid, entityMobState))
                {
                    _stunSystem.TrySlowdown(uid, TimeSpan.FromSeconds(0.4f), false, 0f, 0f);
                    comp.PreparingStartTime = _timing.CurTime;
                    comp.PreparingEndTime = comp.PreparingStartTime + comp.PreparingReloadTime;
                    comp.OnCoolDown = false;
                }
            }

            if (_timing.CurTime > comp.PreparingEndTime && !comp.OnCoolDown)
            {
                var startPos = _xform.GetWorldPosition(uid);
                var targetPos = _xform.GetWorldPosition(humanTarget);
                var direction = (targetPos - startPos).Normalized();
                comp.JumpTarget = targetPos;
                comp.CurrentStep = 0;   
                comp.NextStepTime = _timing.CurTime;
                comp.MovingToJumpTarget = true;
                comp.OnCoolDown = true;
            }
        }
    }

    private void MoveTowardsTarget(EntityUid uid, JumpscareComponent comp, float frameTime)
    {
        if (_timing.CurTime < comp.NextStepTime)
            return;

        var currentPos = _xform.GetWorldPosition(uid);
        var targetPos = comp.JumpTarget;
        var direction = (targetPos - currentPos).Normalized();
        var distanceRemaining = (targetPos - currentPos).Length();
        
        if (distanceRemaining < 0.1f || comp.CurrentStep >= comp.TotalSteps)
        {
            comp.MovingToJumpTarget = false;
            return;
        }

        var stepDistance = comp.JumpDistance / comp.TotalSteps;
        if (stepDistance > distanceRemaining)
            stepDistance = distanceRemaining;
        
        _throwing.TryThrow(uid, direction * stepDistance, comp.JumpPower, user: uid, pushbackRatio: 0);
        _stunSystem.TrySlowdown(uid, comp.SlowdownTime, false, 0.5f, 0.5f);

        comp.CurrentStep++;
        comp.NextStepTime = _timing.CurTime + TimeSpan.FromSeconds(comp.StepInterval);
    }

    private EntityUid? CheckHumanTarget(EntityUid uid, JumpscareComponent component)
    {
        var closestDistance = float.MaxValue;
        EntityUid? target = null;

        var entities = new HashSet<Entity<MobStateComponent>>();
        var xform = Transform(uid);
        var mapCoords = _xform.ToMapCoordinates(xform.Coordinates);
        _lookup.GetEntitiesInRange(mapCoords, component.AttackRadius, entities, LookupFlags.Dynamic);

        foreach (var entity in entities)
        {
            if (!HasComp<HumanoidAppearanceComponent>(entity))
                continue;

            if (!_mobState.IsAlive(entity, entity.Comp))
                continue;

            var dist = (_xform.GetWorldPosition(uid) - _xform.GetWorldPosition(entity)).Length();
            if (dist >= closestDistance)
                continue;

            target = entity;
            closestDistance = dist;
        }

        return target;
    }
}
