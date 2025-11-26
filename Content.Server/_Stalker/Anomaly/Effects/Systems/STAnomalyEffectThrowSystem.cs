using System.Numerics;
using Content.Server._Stalker.Anomaly.Effects.Components;
using Content.Shared._Stalker.Anomaly.Triggers.Events;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server._Stalker.Anomaly.Effects.Systems;

public sealed class STAnomalyEffectThrowSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAnomalyEffectThrowComponent, STAnomalyTriggerEvent>(OnTriggered);
    }

    private void OnTriggered(Entity<STAnomalyEffectThrowComponent> effect, ref STAnomalyTriggerEvent args)
    {
        foreach (var group in args.Groups)
        {
            if (!effect.Comp.Options.TryGetValue(group, out var options))
                continue;

            var entities =
                _entityLookup.GetEntitiesInRange<PhysicsComponent>(Transform(effect).Coordinates, options.Range);

            foreach (var entity in entities)
            {
                switch (options.Type)
                {
                    case STAnomalyEffectThrowType.RandomDirection:
                        ThrowRandomDirection(effect, options, entity);
                        break;

                    case STAnomalyEffectThrowType.FromAnomaly:
                        ThrowFromAnomaly(effect, options, entity);
                        break;

                    case STAnomalyEffectThrowType.ToAnomaly:
                        ThrowToAnomaly(effect, options, entity);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }

    private void ThrowRandomDirection(Entity<STAnomalyEffectThrowComponent> effect, STAnomalyEffectThrowOptions options, EntityUid target)
    {
        var direction = _random.NextAngle(360);
        ThrowDirection(options, target, direction.ToVec());
    }

    private void ThrowFromAnomaly(Entity<STAnomalyEffectThrowComponent> effect, STAnomalyEffectThrowOptions options, EntityUid target)
    {
        var direction = (_transform.GetWorldPosition(target) - _transform.GetWorldPosition(effect)).Normalized();
        ThrowDirection(options, target, direction);
    }

    private void ThrowToAnomaly(Entity<STAnomalyEffectThrowComponent> effect, STAnomalyEffectThrowOptions options, EntityUid target)
    {
        var direction = (_transform.GetWorldPosition(effect) - _transform.GetWorldPosition(target)).Normalized();
        ThrowDirection(options, target, direction);
    }

    private void ThrowDirection(STAnomalyEffectThrowOptions options, EntityUid target, Vector2 direction)
    {
        _throwing.TryThrow(target, direction * options.Distance, options.Force, null, 0);
    }
}
