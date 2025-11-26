using Content.Server._Stalker.ApproachTrigger;
using Content.Server.Explosion.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.ApproachEmitter;

public sealed class ApproachEmitterSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private TimeSpan _lastUpdateTime;
    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _lastUpdateTime.Add(TimeSpan.FromSeconds(2f))) // Decrease it to increase check frequency | Уменьши значение чтобы увеличить частоту проверки.
            return;
        _lastUpdateTime = _timing.CurTime;
        var query = EntityQueryEnumerator<ApproachEmitterComponent>();
        while (query.MoveNext(out var uid, out var approach))
        {
            var entities = CheckEntitiesAround<ApproachTriggerComponent>((uid, approach));
            foreach (var entity in entities)
            {
                _trigger.Trigger(entity);
            }
        }
    }

    private HashSet<Entity<T>> CheckEntitiesAround<T>(Entity<ApproachEmitterComponent> entity) where T : Component
    {
        var mapCoords = _xform.ToMapCoordinates(Transform(entity).Coordinates);
        if (entity.Comp.UseMinRange)
        {
            var maxRange = new HashSet<Entity<T>>();
            _lookup.GetEntitiesInRange(mapCoords, entity.Comp.Range, maxRange, LookupFlags.Static);
            var minRange = new HashSet<Entity<T>>();
            _lookup.GetEntitiesInRange(mapCoords, entity.Comp.MinRange, minRange, LookupFlags.Static);
            return GetApproach(maxRange, minRange);
        }
        else
        {
            var maxRange = new HashSet<Entity<T>>();
            _lookup.GetEntitiesInRange(mapCoords, entity.Comp.Range, maxRange, LookupFlags.Static);
            return maxRange;
        }
    }

    private HashSet<Entity<T>> GetApproach<T>(HashSet<Entity<T>> maxRange, HashSet<Entity<T>> minRange) where T : IComponent
    {
        foreach (var entity in minRange)
        {
            if (maxRange.Contains(entity))
                maxRange.Remove(entity);
        }

        return maxRange;
    }
}
