using Content.Shared.Movement.Systems;

namespace Content.Shared._Stalker.Weight;

public sealed partial class STWeightSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STWeightComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<STWeightComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeedModifiers);
    }

    private void OnParentChanged(Entity<STWeightComponent> weight, ref EntParentChangedMessage args)
    {
        UpdateWeight(weight);
        TryUpdateWeight(args.OldParent);
    }

    private void OnRefreshMovementSpeedModifiers(Entity<STWeightComponent> weight, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(weight.Comp.MovementSpeedModifier, weight.Comp.MovementSpeedModifier);
    }

    private void SetMovementSpeedModifiers(Entity<STWeightComponent> weight, float modifier)
    {
        weight.Comp.MovementSpeedModifier = modifier;

        _movementSpeedModifier.RefreshMovementSpeedModifiers(weight);
    }

    private void SetInsideWeight(Entity<STWeightComponent> weight, float inside)
    {
        var ev = new GetWeightModifiersEvent(inside, weight.Comp.Self);
        RaiseLocalEvent(weight, ev);

        weight.Comp.InsideWeight = ev.Inside;

        // Update movement speed
        var speedModifier = Math.Clamp(1 - (weight.Comp.Total - weight.Comp.Overload) / (weight.Comp.TotalMaximum - weight.Comp.TotalOverload), 0f, 1f);
        SetMovementSpeedModifiers(weight, speedModifier);
    }

    private void UpdateWeight(Entity<STWeightComponent> weight)
    {
        if (!TryComp<TransformComponent>(weight, out var transform))
            return;

        var newInside = 0f;
        var enumerator = transform.ChildEnumerator;
        while (enumerator.MoveNext(out var uid))
        {
            if (!TryGetWeight(uid, out var childWeight))
                continue;

            newInside += childWeight;
        }

        SetInsideWeight(weight, newInside);

        // Call update the weight of the parent component if there is one
        TryUpdateParent(weight);
    }

    public void SetWeightLimits(Entity<STWeightComponent> weight, float newOverload, float newMaximum)
    {
        weight.Comp.Maximum = newMaximum;
        weight.Comp.Overload = newOverload;

        UpdateWeight(weight);
    }

    public bool TrySetWeightLimits(EntityUid uid, float newOverload, float newMaximum, STWeightComponent? weight = null)
    {
        if (!Resolve(uid, ref weight))
            return false;

        if (newMaximum == weight.Maximum && newOverload == weight.Overload)
            return false;

        SetWeightLimits((uid, weight), newOverload, newMaximum);
        return true;
    }

    public bool TryUpdateWeight(EntityUid? uid)
    {
        if (uid is not { } target)
            return false;

        if (!TryComp<STWeightComponent>(uid, out var comp))
            return false;

        UpdateWeight((target, comp));
        return true;
    }

    private bool TryGetWeight(EntityUid uid, out float weight)
    {
        weight = 0f;
        if (!TryComp<STWeightComponent>(uid, out var comp))
            return false;

        weight = comp.Total;
        return true;
    }

    private bool TryUpdateParent(Entity<STWeightComponent> weight)
    {
        var parent = Transform(weight).ParentUid;
        if (!TryComp<STWeightComponent>(parent, out var comp))
            return false;

        UpdateWeight((parent, comp));
        return true;
    }
}
