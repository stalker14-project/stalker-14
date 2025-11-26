using Content.Server._Stalker.Weight;
using Content.Shared._Stalker.Exoskeleton;
using Content.Shared._Stalker.Weight;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Server.Containers;

namespace Content.Server._Stalker.Exoskeleton;

public sealed class STPoweredExoskeletonSystem : STSharedPoweredExoskeletonSystem
{
    [Dependency] private readonly STWeightSystem _weight = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly SharedPowerCellSystem _powerCellSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STPoweredExoskeletonComponent, PowerCellSlotEmptyEvent>(OnDischarge);
        SubscribeLocalEvent<STPoweredExoskeletonComponent, STTogglePoweredExoskeletonEvent>(OnToggle);
    }

    public bool TrySetEnabled(
        EntityUid uid,
        bool enabled,
        EntityUid? ownerUid = null,
        STPoweredExoskeletonComponent? exoskeleton = null,
        STWeightComponent? ownerWeight = null)
    {
        if (!Resolve(uid, ref exoskeleton))
            return false;

        if (!TryComp<PowerCellSlotComponent>(uid, out var powerCellSlot) ||
            !TryComp<PowerCellDrawComponent>(uid, out var powerCellDraw))
            return false;

        if (enabled && !_powerCellSystem.HasDrawCharge(uid, powerCellDraw, powerCellSlot))
            return false;

        if (!ownerUid.HasValue)
        {
            GetExoskeletonOwnerUid(uid, out ownerUid, exoskeleton);

            if (!ownerUid.HasValue)
                return false;
        }

        if (!Resolve(ownerUid.Value, ref ownerWeight))
            return false;

        SetEnabled(uid, ownerUid.Value, enabled, exoskeleton, ownerWeight, powerCellDraw);

        return true;
    }

    public void GetExoskeletonOwnerUid(EntityUid uid,
        out EntityUid? exoskeletonOwner,
        STPoweredExoskeletonComponent? exoskeleton = null)
    {
        if (!Resolve(uid, ref exoskeleton) || !_container.TryGetContainingContainer(uid, out var inventory))
        {
            exoskeletonOwner = null;
            return;
        }

        exoskeletonOwner = inventory.Owner;
    }

    public void SetEnabled(
        EntityUid uid,
        EntityUid ownerUid,
        bool enabled,
        STPoweredExoskeletonComponent exoskeleton,
        STWeightComponent ownerWeight,
        PowerCellDrawComponent powerCellDraw)
    {
        float overloadToSet, maximumToSet;

        if (enabled == exoskeleton.Enabled)
            return;

        overloadToSet = ownerWeight.Overload;
        maximumToSet  = ownerWeight.Maximum;

        overloadToSet+= enabled ? exoskeleton.OverloadChange : -exoskeleton.OverloadChange;
        maximumToSet += enabled ? exoskeleton.MaximumChange  : -exoskeleton.MaximumChange;

        _weight.SetWeightLimits((ownerUid, ownerWeight), overloadToSet, maximumToSet);
        _powerCellSystem.SetDrawEnabled((uid, powerCellDraw), enabled);
        exoskeleton.Enabled = enabled;
    }

    private void OnToggle(EntityUid uid, STPoweredExoskeletonComponent exoskeleton, STTogglePoweredExoskeletonEvent args)
    {
        TrySetEnabled(uid, !exoskeleton.Enabled, args.Performer, exoskeleton);
    }

    private void OnDischarge(EntityUid uid, STPoweredExoskeletonComponent exoskeleton, ref PowerCellSlotEmptyEvent args)
    {
        TrySetEnabled(uid, false, null, exoskeleton);
    }
}
