using Content.Server._Stalker.Anomaly.Effects.Components;
using Content.Shared._Stalker.Anomaly.Triggers.Events;
using Robust.Shared.Random;

namespace Content.Server._Stalker.Anomaly.Effects.Systems;

public sealed class STAnomalySpawnEffectSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAnomalySpawnEffectComponent, STAnomalyTriggerEvent>(OnTriggered);
    }

    private void OnTriggered(Entity<STAnomalySpawnEffectComponent> effect, ref STAnomalyTriggerEvent args)
    {
        foreach (var group in args.Groups)
        {
            if (!effect.Comp.Options.TryGetValue(group, out var option))
                continue;

            var position = Transform(effect).Coordinates;
            if (option.Range != 0)
                position = position.Offset(_random.NextVector2(option.Range));

            var entity = Spawn(option.Id, position);
            EntityManager.AddComponents(entity, option.Components);
        }
    }
}
