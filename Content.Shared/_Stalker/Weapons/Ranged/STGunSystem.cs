using Content.Shared._Stalker.Projectiles;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable.Components;

namespace Content.Shared._Stalker.Weapons.Ranged;

public sealed class STGunSystem : EntitySystem
{
    [Dependency] private readonly STProjectileSystem _stProjectileSystem = default!;

    private const string AccuracyExamineColour = "yellow";

    public override void Initialize()
    {
        SubscribeLocalEvent<STWeaponDamageFalloffComponent, AmmoShotEvent>(OnWeaponDamageFalloffShot);
        SubscribeLocalEvent<STWeaponDamageFalloffComponent, GunRefreshModifiersEvent>(OnWeaponDamageFalloffRefreshModifiers);

        SubscribeLocalEvent<STWeaponAccuracyComponent, ExaminedEvent>(OnWeaponAccuracyExamined);
        SubscribeLocalEvent<STWeaponAccuracyComponent, GunRefreshModifiersEvent>(OnWeaponAccuracyRefreshModifiers);
        SubscribeLocalEvent<STWeaponAccuracyComponent, AmmoShotEvent>(OnWeaponAccuracyShot);
    }

    private void OnWeaponDamageFalloffRefreshModifiers(Entity<STWeaponDamageFalloffComponent> weapon, ref GunRefreshModifiersEvent args)
    {
        var ev = new GetDamageFalloffEvent(weapon.Comp.FalloffMultiplier);
        RaiseLocalEvent(weapon.Owner, ref ev);

        weapon.Comp.ModifiedFalloffMultiplier = FixedPoint2.Max(ev.FalloffMultiplier, 0);

        Dirty(weapon);
    }

    private void OnWeaponDamageFalloffShot(Entity<STWeaponDamageFalloffComponent> weapon, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp(projectile, out STProjectileDamageFalloffComponent? falloffComponent))
                continue;

            _stProjectileSystem.SetProjectileFalloffWeaponMult((projectile, falloffComponent), weapon.Comp.ModifiedFalloffMultiplier);
        }
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
        var baseMult = weapon.Comp.AccuracyMultiplierUnwielded;

        if (TryComp(weapon.Owner, out WieldableComponent? wieldableComponent) && wieldableComponent.Wielded)
            baseMult = weapon.Comp.AccuracyMultiplier;

        var ev = new GetWeaponAccuracyEvent(baseMult);
        RaiseLocalEvent(weapon.Owner, ref ev);

        weapon.Comp.ModifiedAccuracyMultiplier = Math.Max(0.1, (double) ev.AccuracyMultiplier);

        Dirty(weapon);
    }

    private void OnWeaponAccuracyShot(Entity<STWeaponAccuracyComponent> weapon, ref AmmoShotEvent args)
    {
        var netId = GetNetEntity(weapon.Owner).Id;
        FixedPoint2 orderAccuracy = 0;
        FixedPoint2 orderAccuracyPerTile = 0;

        for (int t = 0; t < args.FiredProjectiles.Count; ++t)
        {
            if (!TryComp(args.FiredProjectiles[t], out STProjectileAccuracyComponent? accuracyComponent))
                continue;

            accuracyComponent.Accuracy *= weapon.Comp.ModifiedAccuracyMultiplier;
            accuracyComponent.Accuracy += orderAccuracy;

            if (orderAccuracyPerTile != 0)
                accuracyComponent.Thresholds.Add(new AccuracyFalloffThreshold(0f, -orderAccuracyPerTile, false));

            accuracyComponent.GunSeed = (long) t << 32 | netId;
            Dirty<STProjectileAccuracyComponent>((args.FiredProjectiles[t], accuracyComponent));
        }
    }
}
