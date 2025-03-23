using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Shared.Stacks;
using Content.Shared.Hands.EntitySystems;
using Content.Shared._Stalker.Shop.Prototypes;

namespace Content.Server._Stalker.Shop;

public sealed class STCurrencySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;


    public bool TryDeductCurrencies(EntityUid uid, IReadOnlyDictionary<ProtoId<CurrencyPrototype>, FixedPoint2> costs)
    {
        foreach (var (currencyProto, amount) in costs)
        {
            if (!TryDeductCurrency(uid, currencyProto, amount.Int()))
                return false;
        }
        return true;
    }
    public bool TryDeductCurrency(EntityUid uid, ProtoId<STCurrencyPrototype> currencyProto, int amount)
    {
        var totalFound = 0;
        var toRemove = new List<EntityUid>();

        foreach (var entity in GetContainersRecursive(uid))
        {
            if (!TryComp<MetaDataComponent>(entity, out var meta)
                || meta.EntityPrototype?.ID != _proto.Index(currencyProto).EntityId)
                continue;

            if (TryComp<StackComponent>(entity, out var stack))
            {
                var available = stack.Count;
                if (totalFound + available >= amount)
                {
                    var needed = amount - totalFound;
                    _stack.SetCount(entity, stack.Count - needed);
                    return true;
                }

                toRemove.Add(entity);
                totalFound += available;
            }
            else
            {
                toRemove.Add(entity);
                totalFound++;
            }
        }

        if (totalFound < amount)
            return false;

        foreach (var entity in toRemove)
            Del(entity);

        return true;
    }

    public void AddCurrency(EntityUid uid, string currencyProto, int amount)
    {
        var coordinates = Transform(uid).Coordinates;
        var currencyEntity = Spawn(currencyProto, coordinates);

        if (TryComp<StackComponent>(currencyEntity, out var stack))
            _stack.SetCount(currencyEntity, amount, stack);

        _hands.TryPickupAnyHand(uid, currencyEntity);

        if (!Deleted(currencyEntity))
            Transform(currencyEntity).Coordinates = coordinates;
    }

    private IEnumerable<EntityUid> GetContainersRecursive(EntityUid uid)
    {
        var containers = new List<EntityUid>();
        if (!TryComp<ContainerManagerComponent>(uid, out var containerManager))
            return containers;

        foreach (var container in containerManager.Containers.Values)
        {
            foreach (var entity in container.ContainedEntities)
            {
                containers.Add(entity);
                containers.AddRange(GetContainersRecursive(entity));
            }
        }
        return containers;
    }

    private string? GetCurrencyProto(EntityUid uid)
    {
        return TryComp<MetaDataComponent>(uid, out var meta)
            ? meta.EntityPrototype?.ID
            : null;
    }
}
