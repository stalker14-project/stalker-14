using Content.Server.Administration.Logs;
using Content.Server.Destructible;
using Content.Server.Effects;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Armor;
using Content.Shared.Camera;
using Content.Shared.Inventory;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Projectiles;

public sealed class ProjectileSystem : SharedProjectileSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly ColorFlashEffectSystem _color = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly DestructibleSystem _destructibleSystem = default!;
    [Dependency] private readonly GunSystem _guns = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _sharedCameraRecoil = default!;
    [Dependency] private readonly InventorySystem _inventory = default!; // Stalker-Changes
    [Dependency] private readonly IPrototypeManager _prototype = default!; // Stalker-Changes

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(EntityUid uid, ProjectileComponent component, ref StartCollideEvent args)
    {
        // This is so entities that shouldn't get a collision are ignored.
        if (args.OurFixtureId != ProjectileFixture || !args.OtherFixture.Hard
            || component.ProjectileSpent || component is { Weapon: null, OnlyCollideWhenShot: true })
            return;

        // stalker-changes-start
        var ignoreResitance = false;
        List<EntityUid> ignore = new();
        string[] slots = {
            "outerClothing",
            "head",
            "cloak",
            "eyes",
            "ears",
            "mask",
            "jumpsuit",
            "neck",
            "back",
            "belt",
            "gloves",
            "shoes",
            "id",
            "legs",
            "torso"
        };

        foreach (var slot in slots)
        {
            if (_inventory.TryGetSlotEntity(args.OtherEntity, slot, out var entity) && TryComp<ArmorComponent>(entity, out var armorComp) && armorComp.ArmorClass.HasValue)
                if (component.ProjectileClass >= armorComp.ArmorClass.Value)
                    ignore.Add(entity.Value);
        }

        if (TryComp<DamageableComponent>(args.OtherEntity, out var damageable) && damageable.DamageModifierSetId != null)
            if (_prototype.TryIndex(damageable.DamageModifierSetId, out var damageModifierSetPrototype))
                ignoreResitance = component.ProjectileClass >= damageModifierSetPrototype.Class;
        // stalker-changes-end

        var target = args.OtherEntity;
        // it's here so this check is only done once before possible hit
        var attemptEv = new ProjectileReflectAttemptEvent(uid, component, false);
        RaiseLocalEvent(target, ref attemptEv);
        if (attemptEv.Cancelled)
        {
            SetShooter(uid, component, target);
            return;
        }

        var ev = new ProjectileHitEvent(component.Damage * _damageableSystem.UniversalProjectileDamageModifier, target, component.Shooter);
        RaiseLocalEvent(uid, ref ev);

        var otherName = ToPrettyString(target);
        var damageRequired = _destructibleSystem.DestroyedAt(target);
        if (TryComp<DamageableComponent>(target, out var damageableComponent))
        {
            damageRequired -= damageableComponent.TotalDamage;
            damageRequired = FixedPoint2.Max(damageRequired, FixedPoint2.Zero);
        }
        var deleted = Deleted(target);

        if (_damageableSystem.TryChangeDamage((target, damageableComponent), ev.Damage, out var damage, component.IgnoreResistances || ignoreResitance, origin: component.Shooter, ignoreResistors: ignore) && Exists(component.Shooter)) // Stalker-Changes-IgnoreResistors
        {
            if (!deleted)
            {
                _color.RaiseEffect(Color.Red, new List<EntityUid> { target }, Filter.Pvs(target, entityManager: EntityManager));
            }

            _adminLogger.Add(LogType.BulletHit,
                LogImpact.Medium,
                $"Projectile {ToPrettyString(uid):projectile} shot by {ToPrettyString(component.Shooter!.Value):user} hit {otherName:target} and dealt {damage:damage} damage");

            // If penetration is to be considered, we need to do some checks to see if the projectile should stop.
            if (component.PenetrationThreshold != 0)
            {
                // If a damage type is required, stop the bullet if the hit entity doesn't have that type.
                if (component.PenetrationDamageTypeRequirement != null)
                {
                    var stopPenetration = false;
                    foreach (var requiredDamageType in component.PenetrationDamageTypeRequirement)
                    {
                        if (!damage.DamageDict.Keys.Contains(requiredDamageType))
                        {
                            stopPenetration = true;
                            break;
                        }
                    }
                    if (stopPenetration)
                        component.ProjectileSpent = true;
                }

                // If the object won't be destroyed, it "tanks" the penetration hit.
                if (damage.GetTotal() < damageRequired)
                {
                    component.ProjectileSpent = true;
                }

                if (!component.ProjectileSpent)
                {
                    component.PenetrationAmount += damageRequired;
                    // The projectile has dealt enough damage to be spent.
                    if (component.PenetrationAmount >= component.PenetrationThreshold)
                    {
                        component.ProjectileSpent = true;
                    }
                }
            }
            else
            {
                component.ProjectileSpent = true;
            }
        }

        if (!deleted)
        {
            _guns.PlayImpactSound(target, damage, component.SoundHit, component.ForceSound);

            if (!args.OurBody.LinearVelocity.IsLengthZero())
                _sharedCameraRecoil.KickCamera(target, args.OurBody.LinearVelocity.Normalized());
        }

        if (component.DeleteOnCollide && component.ProjectileSpent)
            QueueDel(uid);

        if (component.ImpactEffect != null && TryComp(uid, out TransformComponent? xform))
        {
            RaiseNetworkEvent(new ImpactEffectEvent(component.ImpactEffect, GetNetCoordinates(xform.Coordinates)), Filter.Pvs(xform.Coordinates, entityMan: EntityManager));
        }
    }
}
