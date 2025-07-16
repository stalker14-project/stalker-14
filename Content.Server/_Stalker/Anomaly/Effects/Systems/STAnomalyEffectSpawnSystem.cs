using Content.Server._Stalker.Anomaly.Effects.Components;
using Content.Shared._Stalker.Anomaly.Triggers.Events;
using Content.Shared.Directions;
using Robust.Shared.Random;

namespace Content.Server._Stalker.Anomaly.Effects.Systems;

public sealed class STAnomalyEffectSpawnSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAnomalyEffectSpawnComponent, STAnomalyTriggerEvent>(OnTriggered);
    }

    private void OnTriggered(Entity<STAnomalyEffectSpawnComponent> effect, ref STAnomalyTriggerEvent args)
    {
        foreach (var group in args.Groups)
        {
            if (!effect.Comp.Options.TryGetValue(group, out var option))
                continue;

            var position = Transform(effect).Coordinates;
            if (option.Range != 0)
                position = position.Offset(option.Offset).Offset(_random.NextVector2(option.Range));

            var entity = Spawn(option.Id, position);
            EntityManager.AddComponents(entity, option.Components);
        }
    }
}
