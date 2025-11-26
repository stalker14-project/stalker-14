using Content.Server.Body.Components;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Shared.Actions;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.CartridgeLoader;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Implants.Components;
using Content.Shared.Item;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectRemoveItemSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZoneAnomalyEffectRemoveItemComponent, ZoneAnomalyActivateEvent>(OnActivate);

        _sawmill = Logger.GetSawmill("zoneAnomaly.effectRemoveItem");
    }

    private void OnActivate(Entity<ZoneAnomalyEffectRemoveItemComponent> effect, ref ZoneAnomalyActivateEvent args)
    {
        for (var i = 0; i < effect.Comp.Count; i++)
        {
            foreach (var trigger in args.Triggers)
            {
                var items = GetRecursiveContainerElements(trigger);
                items.Remove(trigger);

                if (items.Count == 0)
                    continue;

                var item = _random.Pick(items);

                if (TryComp<MetaDataComponent>(item, out var metaData))
                    _sawmill.Info(metaData.EntityName);

                Del(item);
            }
        }
    }

    private List<EntityUid> GetRecursiveContainerElements(EntityUid uid, ContainerManagerComponent? managerComponent = null)
    {
        var result = new List<EntityUid>();

        if (!Resolve(uid, ref managerComponent))
            return result;

        foreach (var container in managerComponent.Containers)
        {
            if (container.Key == "toggleable-clothing") // We don't need anything from this container
                continue;

            foreach (var element in container.Value.ContainedEntities)
            {
                if (HasComp<OrganComponent>(element) ||
                    HasComp<InstantActionComponent>(element) ||
                    HasComp<SubdermalImplantComponent>(element) ||
                    HasComp<BodyPartComponent>(element) ||
                    HasComp<CartridgeComponent>(element) ||
                    HasComp<BloodstreamComponent>(element) ||
                    !HasComp<ItemComponent>(element))
                    continue;

                if (TryComp<ContainerManagerComponent>(element, out var manager))
                    AddRange(GetRecursiveContainerElements(element, manager), ref result);

                result.Add(element);
            }
        }

        return result;
    }

    private void AddRange(List<EntityUid> toAdd, ref List<EntityUid> adding)
    {
        foreach (var el in toAdd)
        {
            adding.Add(el);
        }
    }
}
