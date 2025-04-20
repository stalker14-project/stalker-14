using Content.Shared.Actions;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Stalker.Bands;
/// <summary>
/// Handles assigning and removing <see cref="BandsComponent"/> to remove/assign action on entity
/// </summary>
public sealed class SharedBandsSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BandsComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<BandsComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<BandsComponent, ToggleBandsEvent>(OnToggle);
        SubscribeLocalEvent<BandsComponent, ChangeBandEvent>(OnChange);
    }

    private void OnInit(EntityUid uid, BandsComponent component, ComponentInit args)
    {
        EnsureComp<StatusIconComponent>(uid);

        var proto = _proto.Index<JobIconPrototype>(component.BandStatusIcon);
        //if (proto.HideOnStealth)
        //    return;
        if (component is { AltBand: not null, CanChange: true })
            _actions.AddAction(uid, ref component.ActionChangeEntity, component.ActionChange, uid);

        _actions.AddAction(uid, ref component.ActionEntity, component.Action, uid);
    }

    private void OnChange(Entity<BandsComponent> entity, ref ChangeBandEvent args)
    {
        var comp = entity.Comp;
        if (comp.AltBand == null || !comp.CanChange)
            return;

        (comp.BandStatusIcon, comp.AltBand) = (comp.AltBand, comp.BandStatusIcon);
        Dirty(entity);
        args.Handled = true;
    }

    private void OnRemove(EntityUid uid, BandsComponent component, ComponentRemove args)
    {
        RemComp<StatusIconComponent>(uid);

        var proto = _proto.Index<JobIconPrototype>(component.BandStatusIcon);
        //if (proto.HideOnStealth)
        //    return;

        _actions.RemoveAction(uid, component.ActionEntity);
        if (component.ActionChangeEntity != null)
            _actions.RemoveAction(uid, component.ActionChangeEntity);
    }
    private void OnToggle(EntityUid uid, BandsComponent component, ToggleBandsEvent args)
    {
        if (!_mobState.IsAlive(uid))
            return;

        var proto = _proto.Index<JobIconPrototype>(component.BandStatusIcon);
        //if (proto.HideOnStealth)
        //    return;

        component.Enabled = !component.Enabled;
        Dirty(uid, component);

        args.Handled = true;
    }
}
