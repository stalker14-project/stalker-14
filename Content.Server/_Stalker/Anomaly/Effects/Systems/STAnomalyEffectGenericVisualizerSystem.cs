using Content.Server._Stalker.Anomaly.Effects.Components;
using Content.Shared._Stalker.Anomaly.Triggers.Events;
using Robust.Server.GameObjects;

namespace Content.Server._Stalker.Anomaly.Effects.Systems;

public sealed class STAnomalyEffectGenericVisualizerSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAnomalyEffectGenericVisualizerComponent, STAnomalyTriggerEvent>(OnTriggered);
    }

    private void OnTriggered(Entity<STAnomalyEffectGenericVisualizerComponent> effect, ref STAnomalyTriggerEvent args)
    {
        foreach (var group in args.Groups)
        {
            if (!effect.Comp.Options.TryGetValue(group, out var options))
                continue;

            _appearance.SetData(effect, STAnomalyGenericVisualizerEffectVisuals.Layer, options.State);
        }
    }
}
