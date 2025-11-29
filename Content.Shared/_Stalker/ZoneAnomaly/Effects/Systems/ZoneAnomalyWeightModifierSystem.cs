using Content.Shared._Stalker.Weight;

namespace Content.Shared._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyWeightModifierSystem : EntitySystem
{
    [Dependency] private readonly STWeightSystem _weight = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZoneAnomalyWeightModifierComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZoneAnomalyWeightModifierComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ZoneAnomalyWeightModifierComponent, GetWeightModifiersEvent>(OnGetWeightModifiers);
    }

    private void OnStartup(Entity<ZoneAnomalyWeightModifierComponent> effect, ref ComponentStartup args)
    {
        _weight.TryUpdateWeight(effect);
    }

    private void OnRemove(Entity<ZoneAnomalyWeightModifierComponent> effect, ref ComponentRemove args)
    {
        _weight.TryUpdateWeight(effect);
    }

    private void OnGetWeightModifiers(Entity<ZoneAnomalyWeightModifierComponent> effect, ref GetWeightModifiersEvent args)
    {
        args.Inside += effect.Comp.Additional;
        args.Inside = (args.Inside + args.Self) * effect.Comp.Multiply - args.Self;
    }
}
