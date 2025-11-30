using Content.Shared._Stalker.Psyonics;
using Content.Shared._Stalker.Psyonics.Actions;
using Content.Shared._Stalker.Psyonics.Actions.Shield;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.Psyonics.Actions.Shield;
public sealed class PsyonicsShieldSystem : BasePsyonicsActionSystem<PsyonicsActionShieldComponent, PsyonicsActionShieldEvent>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PsyonicsSystem _psy = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PsyonicsActionShieldComponent, DamageModifyEvent>(OnDamageModified);
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PsyonicsActionShieldComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsActive)
                continue;
            var newPeriodStartsAt = comp.LastDecay + TimeSpan.FromSeconds(comp.PeriodSeconds);
            if (_timing.CurTime > newPeriodStartsAt)
            {
                comp.LastDecay = _timing.CurTime;

                if (TryComp<PsyonicsComponent>(uid, out var psyComponent))
                {
                    _psy.RemovePsy((uid, psyComponent), comp.PricePerPeriod);
                }
            }
        }
    }
    private void OnDamageModified(EntityUid uid, PsyonicsActionShieldComponent component, DamageModifyEvent args)
    {
        if (args.IgnoreResistors.Contains(uid))
            return;

        if (!component.IsActive)
            return;
        var initialHealth = component.Health;
        var remainingDamage = new DamageSpecifier();
        var totalDamage = FixedPoint2.Zero;

        foreach (var (damageType, damageValue) in args.Damage.DamageDict)
        {
            if (component.IgnoredDamageTypes.Contains(damageType))
                continue;

            if (damageValue < 0)
            {
                // Healing, ignore this type of "damage"
                continue;
            }

            totalDamage += damageValue;

            if (component.Health > 0)
            {
                var absorbedDamage = FixedPoint2.Min(component.Health, damageValue);
                component.Health -= absorbedDamage;

                if (damageValue > absorbedDamage)
                {
                    remainingDamage.DamageDict[damageType] = damageValue - absorbedDamage;
                }
            }
            else
            {
                remainingDamage.DamageDict[damageType] = damageValue;
            }
        }

        if (component.Health <= 0)
        {
            args.Damage = remainingDamage;
        }
        else
        {
            args.Damage = new DamageSpecifier();
        }

        component.Health = FixedPoint2.Max(component.Health, 0);

        // Deactivate the shield if it's depleted
        if (component.Health == 0 && totalDamage > 0)
        {
            component.IsActive = false;
        }

        if (component.Health != initialHealth)
            UpdateAppearance(uid, component);
    }

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    protected override void OnAction(Entity<PsyonicsActionShieldComponent> entity, ref PsyonicsActionShieldEvent args)
    {
        base.OnAction(entity, ref args);

        if (!TryComp(entity.Owner, out AppearanceComponent? appearance))
            return;

        if (entity.Comp.IsActive)
            entity.Comp.IsActive = false;
        else
        {
            entity.Comp.Health = entity.Comp.MaxHealth;
            entity.Comp.IsActive = true;
        }

        UpdateAppearance(entity.Owner, entity.Comp);
        args.Handled = true;
    }

    public void UpdateAppearance(EntityUid uid, PsyonicsActionShieldComponent? shield = null)
    {
        if (!TryComp(uid, out AppearanceComponent? appearance))
            return;
        if (!Resolve(uid, ref shield, ref appearance))
            return;

        _appearance.SetData(uid, ShieldVisuals.HasShield, shield.IsActive, appearance);
        _appearance.SetData(uid, ShieldVisuals.ShieldHealth, shield.Health / shield.MaxHealth, appearance);
    }
}
