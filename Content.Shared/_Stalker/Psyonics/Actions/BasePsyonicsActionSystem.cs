using Content.Shared.Actions;
using Content.Shared.Popups;

namespace Content.Shared._Stalker.Psyonics.Actions;

public abstract class BasePsyonicsActionSystem<TActionComponent, TActionEvent> : EntitySystem where TActionComponent : BasePsyonicsActionComponent where TActionEvent : BaseActionEvent
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PsyonicsSystem _psyonics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TActionComponent, MapInitEvent>(ActionMapInit);
        SubscribeLocalEvent<TActionComponent, TActionEvent>(ActionStarter);
    }

    // Stalker-TODO: I don't like it duplicating dizzy action for Controller. Need a proper fix
    private void ActionMapInit(Entity<TActionComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ent.Comp.ActionId);

        OnMapInit(ent, ref args);
    }

    private void ActionStarter(Entity<TActionComponent> ent, ref TActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<PsyonicsComponent>(ent, out var comp))
            return;

        if (comp is not { } psyComp)
            return;

        var psyonics = (ent, psyComp);

        if (!_psyonics.HasPsy(psyonics, ent.Comp.Cost))
        {
            _popup.PopupEntity(Loc.GetString("psy-not-enough"), ent, ent);
            args.Handled = false;
            return;
        }

        OnAction(ent, ref args);

        if (args.Handled && ent.Comp.Consumable)
        {
            _psyonics.RemovePsy(psyonics, ent.Comp.Cost);
        }
    }

    protected virtual void OnMapInit(Entity<TActionComponent> entity, ref MapInitEvent args)
    {

    }

    protected virtual void OnAction(Entity<TActionComponent> entity, ref TActionEvent args)
    {

    }
}
