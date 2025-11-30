using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;

namespace Content.Server._Stalker.Lay;

public sealed partial class STLaySystem
{
    [Dependency] private readonly StandingStateSystem _standingState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;

    private void InitializeLaid()
    {
        SubscribeLocalEvent<STLaidComponent, ComponentInit>(OnLaidInit);
        SubscribeLocalEvent<STLaidComponent, ComponentShutdown>(OnLaidShutdown);
        SubscribeLocalEvent<STLaidComponent, StandAttemptEvent>(OnLaidStandAttempt);

        SubscribeLocalEvent<STLaidComponent, TileFrictionEvent>(OnLaidTileFriction);
        SubscribeLocalEvent<STLaidComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeedModifiers);
    }

    private void OnLaidInit(Entity<STLaidComponent> laid, ref ComponentInit args)
    {
        _standingState.Down(laid, dropHeldItems: false, fixtureAttempt: false);
        _movementSpeedModifier.RefreshMovementSpeedModifiers(laid);
    }

    private void OnLaidShutdown(Entity<STLaidComponent> laid, ref ComponentShutdown args)
    {
        laid.Comp.Standing = true;
        _standingState.Stand(laid);
        laid.Comp.Standing = true;
        _movementSpeedModifier.RefreshMovementSpeedModifiers(laid);
    }

    private void OnLaidStandAttempt(Entity<STLaidComponent> laid, ref StandAttemptEvent args)
    {
        var standing = laid.Comp.Standing;
        laid.Comp.Standing = false;

        if (args.Cancelled)
            return;

        if (standing)
            return;

        args.Cancel();
    }

    private void OnLaidTileFriction(Entity<STLaidComponent> laid, ref TileFrictionEvent args)
    {
        args.Modifier *= laid.Comp.TileFrictionModifier;
    }

    private void OnRefreshMovementSpeedModifiers(Entity<STLaidComponent> laid, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (laid.Comp.Standing)
            return;

        args.ModifySpeed(laid.Comp.MovementSpeedModifier, laid.Comp.MovementSpeedModifier);
    }
}
