using Content.Shared._Stalker.Invisibility;
using Content.Shared.Actions;

namespace Content.Shared._Stalker.Psyonics.Abilities.Stealth;

public sealed class STPsyonicsStealthSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STPsyonicsStealthComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<STPsyonicsStealthComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<STPsyonicsStealthComponent, STPsyonicsStealthActionEvent>(OnAction);
    }

    private void OnStartup(Entity<STPsyonicsStealthComponent> entity, ref ComponentStartup args)
    {
        entity.Comp.Action = _actions.AddAction(entity, entity.Comp.ActionId);
        Dirty(entity);
    }

    private void OnShutdown(Entity<STPsyonicsStealthComponent> entity, ref ComponentShutdown args)
    {
        if (entity.Comp.Action is null)
            return;

        _actions.RemoveAction(entity, entity.Comp.Action.Value);

        entity.Comp.Action = null;
        Dirty(entity);
    }

    private void OnAction(Entity<STPsyonicsStealthComponent> entity, ref STPsyonicsStealthActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (EnsureComp<STInvisibleComponent>(entity, out var invisibleComponent))
        {
            RemCompDeferred<STInvisibleComponent>(entity);
            return;
        }

        invisibleComponent.Opacity = entity.Comp.Opacity;
        Dirty(entity, invisibleComponent);
    }
}
