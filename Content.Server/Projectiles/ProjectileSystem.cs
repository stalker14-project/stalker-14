using Content.Server.Administration.Logs;
using Content.Server.Effects;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Armor;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Database;
using Content.Shared.Inventory;
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
            || component.DamagedEntity || component is { Weapon: null, OnlyCollideWhenShot: true })
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

        var ev = new ProjectileHitEvent(component.Damage, target, component.Shooter);
        RaiseLocalEvent(uid, ref ev);

        var otherName = ToPrettyString(target);
        var direction = args.OurBody.LinearVelocity.Normalized();
        var modifiedDamage = _damageableSystem.TryChangeDamage(target, ev.Damage, component.IgnoreResistances || ignoreResitance, origin: component.Shooter, ignoreResistors: ignore); // Stalker-Changes-End
        var deleted = Deleted(target);

        if (modifiedDamage is not null && EntityManager.EntityExists(component.Shooter))
        {
            if (modifiedDamage.AnyPositive() && !deleted)
            {
                _color.RaiseEffect(Color.Red, new List<EntityUid> { target }, Filter.Pvs(target, entityManager: EntityManager));
            }

            _adminLogger.Add(LogType.BulletHit,
                HasComp<ActorComponent>(target) ? LogImpact.Extreme : LogImpact.High,
                $"Projectile {ToPrettyString(uid):projectile} shot by {ToPrettyString(component.Shooter!.Value):user} hit {otherName:target} and dealt {modifiedDamage.GetTotal():damage} damage");
        }

        if (!deleted)
        {
            _guns.PlayImpactSound(target, modifiedDamage, component.SoundHit, component.ForceSound);
            _sharedCameraRecoil.KickCamera(target, direction);
        }

        component.DamagedEntity = true;

        if (component.DeleteOnCollide)
            QueueDel(uid);

        if (component.ImpactEffect != null && TryComp(uid, out TransformComponent? xform))
        {
            RaiseNetworkEvent(new ImpactEffectEvent(component.ImpactEffect, GetNetCoordinates(xform.Coordinates)), Filter.Pvs(xform.Coordinates, entityMan: EntityManager));
        }
    }
}
