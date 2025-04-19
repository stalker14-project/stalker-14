using Content.Shared._Stalker.Weapon.Projectile;
using Content.Shared.Examine;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable.Components;

namespace Content.Shared._Stalker.Weapon;

public sealed class STWeaponSystem : EntitySystem
{
    private const string AccuracyExamineColour = "yellow";

    [Dependency] private readonly STProjectileSystem _projectile = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STWeaponDamageFalloffComponent, AmmoShotEvent>(OnWeaponDamageFalloffShot);
        SubscribeLocalEvent<STWeaponDamageFalloffComponent, GunRefreshModifiersEvent>(OnWeaponDamageFalloffRefreshModifiers);

        SubscribeLocalEvent<STWeaponAccuracyComponent, ExaminedEvent>(OnWeaponAccuracyExamined);
        SubscribeLocalEvent<STWeaponAccuracyComponent, GunRefreshModifiersEvent>(OnWeaponAccuracyRefreshModifiers);
        SubscribeLocalEvent<STWeaponAccuracyComponent, AmmoShotEvent>(OnWeaponAccuracyShot);
    }

    private void OnWeaponDamageFalloffShot(Entity<STWeaponDamageFalloffComponent> weapon, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp(projectile, out STProjectileDamageFalloffComponent? falloffComponent))
                continue;

            _projectile.SetProjectileFalloffWeaponModifier((projectile, falloffComponent), weapon.Comp.ModifiedFalloffMultiplier);
        }
    }

    private void OnWeaponDamageFalloffRefreshModifiers(Entity<STWeaponDamageFalloffComponent> weapon, ref GunRefreshModifiersEvent args)
    {
        var ev = new STGetDamageFalloffEvent(weapon.Comp.FalloffMultiplier);
        RaiseLocalEvent(weapon.Owner, ref ev);

        weapon.Comp.ModifiedFalloffMultiplier = MathF.Max(ev.FalloffMultiplier, 0);
        Dirty(weapon);
    }

    private void OnWeaponAccuracyExamined(Entity<STWeaponAccuracyComponent> weapon, ref ExaminedEvent args)
    {
        if (!HasComp<GunComponent>(weapon.Owner))
            return;

        using (args.PushGroup(nameof(STWeaponAccuracyComponent)))
        {
            args.PushMarkup(Loc.GetString("st-examine-text-weapon-accuracy", ("colour", AccuracyExamineColour), ("accuracy", weapon.Comp.ModifiedAccuracyMultiplier)));
        }
    }

    private void OnWeaponAccuracyRefreshModifiers(Entity<STWeaponAccuracyComponent> weapon, ref GunRefreshModifiersEvent args)
    {
        var baseMultiplier = weapon.Comp.AccuracyMultiplierUnwielded;
        if (TryComp(weapon.Owner, out WieldableComponent? wieldableComponent) && wieldableComponent.Wielded)
            baseMultiplier = weapon.Comp.AccuracyMultiplier;

        var ev = new STGetWeaponAccuracyEvent(baseMultiplier);
        RaiseLocalEvent(weapon.Owner, ref ev);

        weapon.Comp.ModifiedAccuracyMultiplier = MathF.Max(0.1f, ev.AccuracyMultiplier);
        Dirty(weapon);
    }

    private void OnWeaponAccuracyShot(Entity<STWeaponAccuracyComponent> weapon, ref AmmoShotEvent args)
    {
        var netId = GetNetEntity(weapon.Owner).Id;
        for (var t = 0; t < args.FiredProjectiles.Count; ++t)
        {
            if (!TryComp(args.FiredProjectiles[t], out STProjectileAccuracyComponent? accuracyComponent))
                continue;

            accuracyComponent.Accuracy *= weapon.Comp.ModifiedAccuracyMultiplier;
            accuracyComponent.GunSeed = ((long) t << 32) | (uint) netId;

            Dirty<STProjectileAccuracyComponent>((args.FiredProjectiles[t], accuracyComponent));
        }
    }
}
