using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._Stalker.ZoneAnomaly;
using Content.Server.Projectiles;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectInversionSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly ProjectileSystem _projectile = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly ZoneAnomalySystem _anomaly = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZoneAnomalyEffectInversionComponent, ZoneAnomalyActivateEvent>(OnActivate);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZoneAnomalyComponent, ZoneAnomalyEffectInversionComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var anomaly, out var inversion, out var transform))
        {
            GravPulse(_transform.GetMapCoordinates(uid, transform), anomaly.InAnomaly, inversion.Radial, inversion.Tangential);
        }
    }

    private void OnActivate(Entity<ZoneAnomalyEffectInversionComponent> effect, ref ZoneAnomalyActivateEvent args)
    {
        if (!TryComp<ZoneAnomalyComponent>(effect, out var anomaly))
            return;

        var epicenter = _transform.GetWorldPosition(effect);

        var triggers = _lookup.GetEntitiesInRange<MobStateComponent>(_transform.GetMapCoordinates(effect), effect.Comp.Distance);
        foreach (var trigger in triggers)
        {
            if (trigger.Comp.CurrentState != MobState.Alive)
                continue;

            if (_whitelist.IsWhitelistPass(effect.Comp.Whitelist, trigger))
                continue;

            triggers.Remove(trigger);
        }

        if (triggers.Count == 0)
        {
            foreach (var entity in anomaly.InAnomaly)
            {
                _throwing.TryThrow(entity, epicenter, effect.Comp.Speed);
                _anomaly.TryRemoveEntity((effect, anomaly), entity);
            }

            return;
        }

        var target = _random.Pick(triggers);

        foreach (var entity in anomaly.InAnomaly)
        {
            _physics.SetLinearVelocity(entity, Vector2.Zero);

            if (TryComp<ProjectileComponent>(entity, out var projectile))
            {
                _projectile.SetShooter(entity, projectile, effect);
                _gun.ShootProjectile(entity, _transform.GetWorldPosition(target) - _transform.GetWorldPosition(entity), Vector2.Zero, effect, null, effect.Comp.Speed);
            }
            else
            {
                _throwing.TryThrow(entity, _transform.GetWorldPosition(target) - _transform.GetWorldPosition(entity), effect.Comp.Speed);
            }

            _anomaly.TryRemoveEntity((effect, anomaly), entity);
        }
    }

    private void GravPulse(MapCoordinates mapPos, HashSet<EntityUid> targets, float radial, float tangential)
    {
        var epicenter = mapPos.Position;
        var bodyQuery = GetEntityQuery<PhysicsComponent>();
        var xformQuery = GetEntityQuery<TransformComponent>();

        foreach (var entity in targets)
        {
            if (!bodyQuery.TryGetComponent(entity, out var physics) || physics.BodyType == BodyType.Static)
                continue;

            var displacement = epicenter - _transform.GetWorldPosition(entity, xformQuery);
            var distance2 = displacement.LengthSquared();

            if (distance2 <= 0)
                continue;

            var scaling = (1f / distance2) * physics.Mass;

            var radialImpulse = Vector2.Normalize(displacement) * radial;

            var tangentialImpulse = new Vector2(-displacement.Y, displacement.X) * tangential;

            var totalImpulse = (radialImpulse + tangentialImpulse) * scaling;

            _physics.ApplyLinearImpulse(entity, totalImpulse, body: physics);
        }
    }

}
