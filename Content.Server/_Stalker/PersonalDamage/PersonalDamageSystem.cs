using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Effects;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.PersonalDamage;

public sealed class PersonalDamageSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;

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

                var dmgRes = _damageableSystem.TryChangeDamage(parent, component.Damage, component.IgnoreResistances, component.InterruptsDoAfters);

                // Apply stamina without its own visual; we'll choose the dominant color below to avoid double flashes.
                _stamina.TakeStaminaDamage(parent, component.StaminaDamage, visual: false);

                // Decide which color to show. If we dealt HP damage, compare magnitudes.
                if (dmgRes != null && dmgRes.GetTotal() > 0)
                {
                    var hp = dmgRes.GetTotal();
                    var stam = component.StaminaDamage;
                    var filter = Filter.Pvs(parent, entityManager: EntityManager);
                    if (stam > hp)
                    {
                        _color.RaiseEffect(Color.Aqua, new List<EntityUid> { parent }, filter);
                    }
                    else
                    {
                        _color.RaiseEffect(Color.Red, new List<EntityUid> { parent }, filter);
                    }
                }
                else
                {
                    // Only stamina damage
                    if (component.StaminaDamage > 0f)
                        _color.RaiseEffect(Color.Aqua, new List<EntityUid> { parent }, Filter.Pvs(parent, entityManager: EntityManager));
                }
                parent = Transform(parent).ParentUid;
            }

            component.NextDamage = _timing.CurTime + TimeSpan.FromSeconds(component.Interval);
        }
    }
}
