using Content.Server._Stalker.Anomaly.Effects.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._Stalker.Anomaly.Triggers.Events;

namespace Content.Server._Stalker.Anomaly.Effects.Systems;

public sealed class STAnomalyEffectExplosionSystem : EntitySystem
{
    [Dependency] private readonly ExplosionSystem _explosion = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAnomalyEffectExplosionComponent, STAnomalyTriggerEvent>(OnTriggered);
    }

    private void OnTriggered(Entity<STAnomalyEffectExplosionComponent> effect, ref STAnomalyTriggerEvent args)
    {
        foreach (var group in args.Groups)
        {
            if (!effect.Comp.Options.TryGetValue(group, out var option))
                continue;

            _explosion.QueueExplosion(effect, option.Id, option.Intensity, option.Slope, option.MaxIntensity, canCreateVacuum: false, addLog: false);
        }
    }
}
