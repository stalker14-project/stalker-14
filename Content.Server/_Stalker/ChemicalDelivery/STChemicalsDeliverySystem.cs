using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Audio.Systems;
using Content.Shared.Coordinates;

namespace Content.Server._Stalker.ChemicalDelivery;

public sealed class STChemicalsDeliverySystem : EntitySystem
{
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STChemicalsDeliveryComponent, StepTriggeredOffEvent>(OnChemicalDelivery);
        SubscribeLocalEvent<STChemicalsDeliveryComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
    }

    private void OnStepTriggerAttempt(EntityUid uid, STChemicalsDeliveryComponent component, ref StepTriggerAttemptEvent args)
    {
        if (!HasComp<BloodstreamComponent>(args.Tripper))
            return;

        args.Continue = true;
    }

    private void OnChemicalDelivery(EntityUid uid, STChemicalsDeliveryComponent component, ref StepTriggeredOffEvent args)
    {
        var solution1 = new Solution(component.Reagent, component.Amount);
        if (!TryComp<BloodstreamComponent>(args.Tripper, out var bloodstream))
            return;

        _blood.TryAddToChemicals(args.Tripper, solution1, bloodstream);

        if (component.DeliverSound != null)
        {
            _audioSystem.PlayPvs(component.DeliverSound, args.Tripper);
        }

        if (component.Entry.HasValue)
        {
            Spawn(component.Entry.Value.PrototypeId, args.Source.ToCoordinates());
        }
    }

}
