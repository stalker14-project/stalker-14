using Content.Shared._Stalker.Random;
using Content.Shared._Stalker.Weapon.Evasion;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared._Stalker.Weapon.Projectile;

public sealed class STProjectileSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly STEvasionSystem _evasion = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STProjectileDamageFalloffComponent, MapInitEvent>(OnFalloffProjectileMapInit);
        SubscribeLocalEvent<STProjectileDamageFalloffComponent, ProjectileHitEvent>(OnFalloffProjectileHit);

        SubscribeLocalEvent<STProjectileAccuracyComponent, MapInitEvent>(OnProjectileAccuracyMapInit);
        SubscribeLocalEvent<STProjectileAccuracyComponent, PreventCollideEvent>(OnProjectileAccuracyPreventCollide);
    }

    public void SetProjectileFalloffWeaponModifier(Entity<STProjectileDamageFalloffComponent> projectile, float modifier)
    {
        projectile.Comp.WeaponModifier = modifier;
        Dirty(projectile);
    }

    private void OnFalloffProjectileMapInit(Entity<STProjectileDamageFalloffComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.StartCoordinates = _transform.GetMoverCoordinates(ent.Owner);
        Dirty(ent);
    }

    private void OnFalloffProjectileHit(Entity<STProjectileDamageFalloffComponent> ent, ref ProjectileHitEvent args)
    {
        if (ent.Comp.StartCoordinates is null || ent.Comp.MinRemainingDamageModifier < 0)
            return;

        var distance = (_transform.GetMoverCoordinates(args.Target).Position - ent.Comp.StartCoordinates.Value.Position).Length();
        var minDamage = args.Damage.GetTotal() * ent.Comp.MinRemainingDamageModifier;

        foreach (var threshold in ent.Comp.Thresholds)
        {
            var range = distance - threshold.Range;
            if (range <= 0)
                continue;

            var totalDamage = args.Damage.GetTotal();
            if (totalDamage <= minDamage)
                break;

            var extraModifier = threshold.IgnoreModifiers ? 1 : ent.Comp.WeaponModifier;
            var minModifier = FixedPoint2.Min(minDamage / totalDamage, 1);
            args.Damage *= FixedPoint2.Clamp((totalDamage - range * threshold.Falloff * extraModifier) / totalDamage, minModifier, 1);
        }
    }

    private void OnProjectileAccuracyMapInit(Entity<STProjectileAccuracyComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.StartCoordinates = _transform.GetMoverCoordinates(ent.Owner);
        ent.Comp.Tick = _timing.CurTick.Value;
        Dirty(ent);
    }

    private void OnProjectileAccuracyPreventCollide(Entity<STProjectileAccuracyComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled || ent.Comp.ForceHit || ent.Comp.StartCoordinates is null)
            return;

        if (!HasComp<STEvasionComponent>(args.OtherEntity))
            return;

        var accuracy = ent.Comp.Accuracy;
        var targetCoordinates = _transform.GetMoverCoordinates(args.OtherEntity);
        var distance = (targetCoordinates.Position - ent.Comp.StartCoordinates.Value.Position).Length();

        foreach (var threshold in ent.Comp.Thresholds)
        {
            accuracy += CalculateFalloff(distance - threshold.Range, threshold.Falloff, threshold.AccuracyGrowth);
        }

        accuracy -= _evasion.GetEvasion(args.OtherEntity);
        accuracy = accuracy > ent.Comp.MinAccuracy ? accuracy : ent.Comp.MinAccuracy;

        var random = new STXoshiro128P(ent.Comp.GunSeed, ((long) ent.Comp.Tick << 32) | (uint) GetNetEntity(args.OtherEntity).Id).NextFloat(0f, 1f);
        if (accuracy >= random)
            return;

        args.Cancelled = true;
    }

    private static float CalculateFalloff(float range, float falloff, bool accuracyGrowth)
    {
        if (accuracyGrowth)
            return range >= 0 ? 0 : falloff * range;

        return range <= 0 ? 0 : -falloff * range;
    }
}
