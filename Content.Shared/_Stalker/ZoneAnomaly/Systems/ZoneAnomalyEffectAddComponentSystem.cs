using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;

namespace Content.Shared._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectAddComponentSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly ISerializationManager _serializationManager = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZoneAnomalyEffectAddComponentComponent, ZoneAnomalyEntityAddEvent>(OnAdd);
        SubscribeLocalEvent<ZoneAnomalyEffectAddComponentComponent, ZoneAnomalyEntityRemoveEvent>(OnRemove);
    }

    private void OnAdd(Entity<ZoneAnomalyEffectAddComponentComponent> effect, ref ZoneAnomalyEntityAddEvent args)
    {
        EntityManager.AddComponents(args.Entity, effect.Comp.Components, true);
    }

    private void OnRemove(Entity<ZoneAnomalyEffectAddComponentComponent> effect, ref ZoneAnomalyEntityRemoveEvent args)
    {
        RemoveComponents(args.Entity, effect.Comp.Components);
    }

    private void RemoveComponents(EntityUid uid, ComponentRegistry components)
    {
        foreach (var (name, data) in components)
        {
            var component = (Component)_componentFactory.GetComponent(name);
            component.Owner = uid;

            var temp = (object)component;
            _serializationManager.CopyTo(data.Component, ref temp);
            RemComp(uid, temp!.GetType());
        }
    }
}
