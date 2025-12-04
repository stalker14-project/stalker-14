using Content.Server._Stalker.Dizzy;
using Content.Shared._Stalker.Psyonics.Actions;
using Content.Shared._Stalker.Psyonics.Actions.Dizzy;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.Psyonics.Actions.Dizzy;

public sealed class PsyonicsActionDizzySystem : BasePsyonicsActionSystem<PsyonicsActionDizzyComponent, PsyonicsActionDizzyEvent>
{
    [Dependency] private DizzySystem _dizzy = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    protected override void OnAction(Entity<PsyonicsActionDizzyComponent> entity, ref PsyonicsActionDizzyEvent args)
    {
        base.OnAction(entity, ref args);

        _dizzy.TryApplyDizziness(args.Target, (float)entity.Comp.Duration.TotalSeconds);

        if (entity.Comp.Damage is null)
            return;
        if (!TryComp<DamageableComponent>(args.Target, out var damageable))
            return;

        _damageable.TryChangeDamage(args.Target, entity.Comp.Damage);

        // Stalker-TODO: This solution is temporary. Remove this after proper fix in systems
        // Stalker-Temporary-Fix-Start
        if (!TryComp<ActionsComponent>(entity.Owner, out var actionsComp))
            return;

        foreach (var action in actionsComp.Actions)
        {
            if (!TryComp<ActionComponent>(action, out var actionComp))
                continue;

            if (!TryComp(action, out MetaDataComponent? metaData))
                return;

            var actionProto = metaData.EntityPrototype?.ID ?? string.Empty;

            if (actionProto != entity.Comp.ActionId || actionProto != entity.Comp.MutantActionId)
                continue;

            if (_actions.IsCooldownActive(actionComp, _timing.CurTime))
                continue;

            if (actionComp.UseDelay != null)
                _actions.SetCooldown(action, actionComp.UseDelay.Value);
        }
        // Stalker-Temporary-Fix-End
    }
}
