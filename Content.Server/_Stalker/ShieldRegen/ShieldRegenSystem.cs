using Content.Shared.Blocking;
using Content.Shared.Damage;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.ShieldRegen;

public sealed class ShieldRegenSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ShieldRegenComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime > comp.EndTime)
            {
                comp.StartTime = _timing.CurTime;
                comp.EndTime = comp.StartTime + comp.ReloadTime;

                if (TryComp<DamageableComponent>(uid, out var damageableComponent) &&
                    TryComp<BlockingComponent>(uid, out var block))
                {
                    block.PassiveBlockFraction = damageableComponent.TotalDamage > comp.Health ? 0f : 0.9f;
                }
            }
            if (_timing.CurTime <= comp.RegenEndTime)
                continue;

            comp.RegenStartTime = _timing.CurTime;
            comp.RegenEndTime = comp.RegenStartTime + comp.RegenReloadTime;

            _damageable.TryChangeDamage(uid, -comp.HealDamage, true, false);
        }
    }
}
