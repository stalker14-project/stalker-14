using Content.Shared.Camera;
using Content.Shared.Mobs;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Stunnable;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Shared._Stalker.Weapon.Scoping;

/// <remarks>
/// Portions of this file are derived from the RMC-14 project, specifically from
/// https://github.com/RMC-14/RMC-14/tree/481a21c95148f5a7bff6ed1609324c836663ca30/Content.Shared/_RMC14/Scoping.
/// These files have been modified for use in this project.
/// The original code is licensed under the MIT License:
/// </remarks>
public partial class STSharedScopeSystem
{
    private void InitializeUser()
    {
        SubscribeLocalEvent<ScopingComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ScopingComponent, MoveInputEvent>(OnMoveInput);
        SubscribeLocalEvent<ScopingComponent, PullStartedMessage>(OnPullStarted);
        SubscribeLocalEvent<ScopingComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<ScopingComponent, ContainerGettingInsertedAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<ScopingComponent, EntityTerminatingEvent>(OnEntityTerminating);
        SubscribeLocalEvent<ScopingComponent, GetEyeOffsetEvent>(OnGetEyeOffset);
        SubscribeLocalEvent<ScopingComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<ScopingComponent, KnockedDownEvent>(OnKnockedDown);
        SubscribeLocalEvent<ScopingComponent, StunnedEvent>(OnStunned);
        SubscribeLocalEvent<ScopingComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnRemove(Entity<ScopingComponent> user, ref ComponentRemove args)
    {
        if (!TerminatingOrDeleted(user))
            UpdateOffset(user);
    }

    private void OnMoveInput(Entity<ScopingComponent> ent, ref MoveInputEvent args)
    {
        if (!ent.Comp.AllowMovement)
            UserStopScoping(ent);
    }

    private void OnPullStarted(Entity<ScopingComponent> ent, ref PullStartedMessage args)
    {
        if (args.PulledUid != ent.Owner)
            return;

        UserStopScoping(ent);
    }

    private void OnParentChanged(Entity<ScopingComponent> ent, ref EntParentChangedMessage args)
    {
        UserStopScoping(ent);
    }

    private void OnInsertAttempt(Entity<ScopingComponent> ent, ref ContainerGettingInsertedAttemptEvent args)
    {
        UserStopScoping(ent);
    }

    private void OnEntityTerminating(Entity<ScopingComponent> ent, ref EntityTerminatingEvent args)
    {
        UserStopScoping(ent);
    }

    private void OnGetEyeOffset(Entity<ScopingComponent> ent, ref GetEyeOffsetEvent args)
    {
        args.Offset += ent.Comp.EyeOffset;
    }

    private void OnPlayerDetached(Entity<ScopingComponent> ent, ref PlayerDetachedEvent args)
    {
        UserStopScoping(ent);
    }

    private void OnKnockedDown(Entity<ScopingComponent> ent, ref KnockedDownEvent args)
    {
        UserStopScoping(ent);
    }

    private void OnStunned(Entity<ScopingComponent> ent, ref StunnedEvent args)
    {
        UserStopScoping(ent);
    }

    private void OnMobStateChanged(Entity<ScopingComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        UserStopScoping(ent);
    }

    private void UserStopScoping(Entity<ScopingComponent> ent)
    {
        var scope = ent.Comp.Scope;
        RemCompDeferred<ScopingComponent>(ent);

        if (TryComp(scope, out Weapon.Scoping.ScopeComponent? scopeComponent) && scopeComponent.User == ent)
            Unscope((scope.Value, scopeComponent));
    }
}
