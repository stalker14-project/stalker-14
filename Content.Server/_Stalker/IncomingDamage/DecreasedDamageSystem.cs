using Content.Shared.Damage;
using Robust.Shared.Timing;
using EntityUid = Robust.Shared.GameObjects.EntityUid;

namespace Content.Server._Stalker.IncomingDamage;

public sealed class DecreasedDamageSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    private TimeSpan _nextTimeUpdate = TimeSpan.Zero;
    private readonly TimeSpan _updateTime = TimeSpan.FromSeconds(1);
    public override void Initialize()
    {
        SubscribeLocalEvent<DecreasedDamageComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(Entity<DecreasedDamageComponent> entity, ref DamageModifyEvent args)
    {
        var modifiers = entity.Comp.Modifiers;

        if (modifiers == null)
            return;

        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifiers);
    }

    public override void Update(float frameTime)
    {
        if (_nextTimeUpdate > _timing.CurTime)
            return;
        _nextTimeUpdate = _timing.CurTime + _updateTime;

        // enumerate all decreased damage and add to deleting hashset
        var query = EntityQueryEnumerator<DecreasedDamageComponent>();
        var toDelete = new HashSet<EntityUid>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.TimeToDelete > _timing.CurTime)
                continue;
            toDelete.Add(uid);
        }

        // delete all exhausted components
        foreach (var entityUid in toDelete)
        {
            RemCompDeferred<DecreasedDamageComponent>(entityUid);
        }
    }
}
