using Content.Server.Stunnable;
using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Server.NPC.HTN;
using Content.Server.Popups;
using Robust.Server.Audio;
using System.Numerics;
using Content.Shared.Camera;
using Content.Server._Stalker.Dizzy;

namespace Content.Server._Stalker.NPCs;

public sealed class NPCBloodsuckerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly StunSystem _stunSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _sharedCameraRecoil = default!;
    [Dependency] private DizzySystem _dizzy = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NPCBloodsuckerComponent, HTNComponent>();
        // getting target by blackbouard Target
        while (query.MoveNext(out var uid, out var comp, out var htn))
        {
            if (!htn.Blackboard.TryGetValue<EntityUid>(comp.TargetKey, out var target, EntityManager))
                continue;

            TryBloodsuck((uid, comp), target);
        }
    }

    // main method, starting all bloodsuck iterations
    public void TryBloodsuck(Entity<NPCBloodsuckerComponent?> user, EntityUid target)
    {
        if (!Resolve(user, ref user.Comp, false) || user.Comp == null || user.Comp.NextTimeUpdate > _timing.CurTime)
            return;

        user.Comp.NextTimeUpdate = _timing.CurTime + TimeSpan.FromSeconds(user.Comp.UpdateCooldown); // cooldown just only for ticks shit, since we dant want to iterate 33 times per second

        if (!IsTargetInRange(user, user.Comp, target))
            return;

        if (user.Comp.IsSucking) // continue stages if they are started already
        {
            ProcessStages(user, target);
        }
        else if (_timing.CurTime > user.Comp.EndTime) // start stages if its not started already
        {
            StartSucking(user, target);
        }
    }

    private void StartSucking(Entity<NPCBloodsuckerComponent?> user, EntityUid target)
    {
        if (user.Comp == null) return;

        // methods to start stages and shit + cooldown
        user.Comp.IsSucking = true;
        user.Comp.CurrentStep = 0;
        user.Comp.NextStepTime = _timing.CurTime + TimeSpan.FromSeconds(1);
        user.Comp.StartTime = _timing.CurTime;
        user.Comp.EndTime = user.Comp.StartTime + TimeSpan.FromSeconds(
            user.Comp.ReloadTime + _random.NextFloat(-user.Comp.RandomiseReloadTime, user.Comp.RandomiseReloadTime)
        );

        if (TryComp<MobStateComponent>(target, out var mobState) && _mobState.IsAlive(target, mobState))
        {
            _stunSystem.TrySlowdown(target, TimeSpan.FromSeconds(1), false, 0f, 0f); // we want target to stay still all the time // +0.5 since there is a possibility that mob will attack not enouch times on these delay due to tickrate and serverlag
            _stunSystem.TrySlowdown(user.Owner, TimeSpan.FromSeconds(1), false, 0f, 0f); // we want user to stay still all the time
            _stunSystem.TryStun(user.Owner, TimeSpan.FromSeconds(1), false); // we dont want bloodsucker to attack while suckin

            _damage.TryChangeDamage(target, user.Comp.DamageOnSuck, true, origin: user.Owner); // damage target
            _damage.TryChangeDamage(user.Owner, user.Comp.HealOnSuck, true, origin: target); // heal user


            _audio.PlayPvs(user.Comp.BloodsuckSound, user); // play sound on suck
            _popup.PopupEntity(Loc.GetString("action-bloodsucker-sucks-blood"), user, Shared.Popups.PopupType.LargeCaution); // popup a message on suck
            _stunSystem.TryKnockdown(target, TimeSpan.FromSeconds(0.3), false); // knockdown each time on suck (i guess its better be here, since i dont like how easy it is to kill him while he is staying still)

            // camera recoil on suck
            var kick = new Vector2(_random.NextFloat(), _random.NextFloat()) * user.Comp.ShakeStrength;
            _sharedCameraRecoil.KickCamera(target, kick);
        }
    }

    // staging, iterating each second (3 seconds of stun = 3 stages, 3 times more damage, etc)
    private void ProcessStages(Entity<NPCBloodsuckerComponent?> user, EntityUid target)
    {
        if (user.Comp == null || user.Comp.NextStepTime > _timing.CurTime)
            return;

        int totalSteps = (int)user.Comp.StunTime;
        if (user.Comp.CurrentStep >= totalSteps)
        {
            user.Comp.IsSucking = false;
            return;
        }

        if (TryComp<MobStateComponent>(target, out var mobState) && _mobState.IsAlive(target, mobState))
        {

            _stunSystem.TrySlowdown(target, TimeSpan.FromSeconds(1), false, 0f, 0f); // we want target to stay still all the time // +0.5 since there is a possibility that mob will attack not enouch times on these delay due to tickrate and serverlag
            _stunSystem.TrySlowdown(user.Owner, TimeSpan.FromSeconds(1), false, 0f, 0f); // we want user to stay still all the time
            _stunSystem.TryStun(user.Owner, TimeSpan.FromSeconds(1), false); // we dont want bloodsucker to attack while suckin

            _damage.TryChangeDamage(target, user.Comp.DamageOnSuck, true, origin: user.Owner); // damage target
            _damage.TryChangeDamage(user.Owner, user.Comp.HealOnSuck, true, origin: target); // heal user


            _audio.PlayPvs(user.Comp.BloodsuckSound, user); // play sound on suck
            _popup.PopupEntity(Loc.GetString("action-bloodsucker-sucks-blood"), user, Shared.Popups.PopupType.LargeCaution); // popup a message on suck
            _stunSystem.TryKnockdown(target, TimeSpan.FromSeconds(0.3), false); // knockdown each time on suck (i guess its better be here, since i dont like how easy it is to kill him while he is staying still)

            // camera recoil on suck
            var kick = new Vector2(_random.NextFloat(), _random.NextFloat()) * user.Comp.ShakeStrength;
            _sharedCameraRecoil.KickCamera(target, kick);
        }

        user.Comp.CurrentStep++;
        user.Comp.NextStepTime = _timing.CurTime + TimeSpan.FromSeconds(1);
    }

    //simple method just to check in range stuff
    private bool IsTargetInRange(EntityUid uid, NPCBloodsuckerComponent component, EntityUid target)
    {
        if (!HasComp<HumanoidAppearanceComponent>(target))
            return false;

        if (!TryComp<MobStateComponent>(target, out var mobState) || !_mobState.IsAlive(target, mobState))
            return false;

        var distance = (_xform.GetWorldPosition(uid) - _xform.GetWorldPosition(target)).Length();
        return distance <= component.AttackRadius;
    }
}
