using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.PersonalDamage;

public sealed class PersonalDamageSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<PersonalDamageComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.NextDamage > _timing.CurTime)
                continue;

            var parent = uid;
            while (!HasComp<MapComponent>(parent))
            {
                if (TerminatingOrDeleted(parent))
                    break;

                if (HasComp<PersonalDamageBlockComponent>(parent))
                    break;

                _damageableSystem.TryChangeDamage(parent, component.Damage, component.IgnoreResistances, component.InterruptsDoAfters);
                _stamina.TakeStaminaDamage(parent, component.StaminaDamage);
                parent = Transform(parent).ParentUid;
            }

            component.NextDamage = _timing.CurTime + TimeSpan.FromSeconds(component.Interval);
        }
    }
}
