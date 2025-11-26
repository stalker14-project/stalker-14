using System.Numerics;
using System.Runtime.CompilerServices;
using Content.Server._Stalker.ApproachTrigger;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.SpawnOnApproach;

public sealed class SpawnOnApproachSystem : EntitySystem
{
    [Robust.Shared.IoC.Dependency] private readonly IRobustRandom _random = default!;
    [Robust.Shared.IoC.Dependency] private readonly IGameTiming _timing = default!;
    [Robust.Shared.IoC.Dependency] private readonly TurfSystem _turf = default!;
    [Robust.Shared.IoC.Dependency] private readonly EntityLookupSystem _lookupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnOnApproachComponent, TriggerEvent>(OnTrigger);
        SubscribeLocalEvent<SpawnOnApproachComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<SpawnOnApproachComponent> entity, ref ComponentInit args)
    {
        if(_timing.CurTime < entity.Comp.MinStartAction)
            return;
        // Check components with instant spawn
        if (!entity.Comp.InstantSpawn)
            return;

        SpawnWithOffset(entity);
    }
    private void OnTrigger(Entity<SpawnOnApproachComponent> entity, ref TriggerEvent args)
    {
        if (!entity.Comp.Enabled)
            return;

        if(_timing.CurTime < entity.Comp.MinStartAction)
            return;

        SpawnWithOffset(entity);
    }

    private void SpawnWithOffset(Entity<SpawnOnApproachComponent> entity)
    {
        var comp = entity.Comp;
        if (!_random.Prob(Math.Clamp(comp.Chance, 0f, 1f)))
        {
            if (comp.ShouldTimeoutOnRoll)
            {
                comp.CoolDownTime = _timing.CurTime + TimeSpan.FromSeconds(comp.Cooldown);
                comp.Enabled = false;
            }
            return;
        }

        var xform = Transform(entity);

        var amount = _random.Next(comp.MinAmount, comp.MaxAmount);
        for (var i = 0; i < amount; i++)
        {
            var initialCoords = xform.Coordinates;
            var offsetCoords = RandomizeCoords(comp, initialCoords);

            if (CheckBlocked(offsetCoords))
                offsetCoords = !comp.SpawnInside ? RandomizeUntilCorrect(comp, initialCoords) : initialCoords;

            // Randomizing entity
            var proto = _random.Pick(comp.EntProtoIds);
            Spawn(proto, offsetCoords);
        }
        if (TryComp<ApproachTriggerComponent>(entity, out var approach))
            approach.Enabled = false;

        comp.CoolDownTime = _timing.CurTime + TimeSpan.FromSeconds(comp.Cooldown);
        comp.Enabled = false;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private EntityCoordinates RandomizeCoords(SpawnOnApproachComponent comp, EntityCoordinates initial)
    {
        var offset = _random.NextFloat(comp.MinOffset, comp.MaxOffset);
        var xOffset = _random.NextFloat(-offset, offset);
        var yOffset = _random.NextFloat(-offset, offset);
        return initial.Offset(new Vector2(xOffset, yOffset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private EntityCoordinates RandomizeUntilCorrect(SpawnOnApproachComponent comp, EntityCoordinates initial)
    {
        var offset = new EntityCoordinates();
        while (CheckBlocked(offset) && CheckEntities(offset, comp))
        {
            offset = RandomizeCoords(comp, initial);
        }

        return offset;
    }

    private bool CheckEntities(EntityCoordinates coords, SpawnOnApproachComponent comp)
    {
        var tile = coords.GetTileRef();
        if (tile == null)
            return false;

        foreach (var entity in _lookupSystem.GetLocalEntitiesIntersecting(tile.Value, 0f))
        {
            var meta = MetaData(entity);
            if (meta.EntityPrototype == null)
                continue;

            return comp.RestrictedProtos.Contains(meta.EntityPrototype.ID);
        }
        return false;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CheckBlocked(EntityCoordinates coords)
    {
        var tile = coords.GetTileRef();

        return tile != null && _turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SpawnOnApproachComponent>();
        while (query.MoveNext(out var uid, out var spawner))
        {
            if (spawner.CoolDownTime > _timing.CurTime)
                continue;

            if (TryComp<ApproachTriggerComponent>(uid, out var approach))
                approach.Enabled = true;

            spawner.Enabled = true;
        }
    }
}
