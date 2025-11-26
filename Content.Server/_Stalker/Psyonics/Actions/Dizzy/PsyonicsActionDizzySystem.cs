using Content.Server._Stalker.Dizzy;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._Stalker.Psyonics.Actions;
using Content.Shared._Stalker.Psyonics.Actions.Dizzy;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Drunk;
using Content.Shared.StatusEffect;

namespace Content.Server._Stalker.Psyonics.Actions.Dizzy;

public sealed class PsyonicsActionDizzySystem : BasePsyonicsActionSystem<PsyonicsActionDizzyComponent, PsyonicsActionDizzyEvent>
{
    [Dependency] private DizzySystem _dizzy = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    protected override void OnAction(Entity<PsyonicsActionDizzyComponent> entity, ref PsyonicsActionDizzyEvent args)
    {
        base.OnAction(entity, ref args);

        _dizzy.TryApplyDizziness(args.Target, (float)entity.Comp.Duration.TotalSeconds);

        if (entity.Comp.Damage is null)
            return;
        if (!TryComp<DamageableComponent>(args.Target, out var damageable))
            return;

        _damageable.TryChangeDamage(args.Target, entity.Comp.Damage);
    }
}
