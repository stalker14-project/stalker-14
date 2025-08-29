using Content.Server._Stalker.Anomaly.Effects.Components;
using Content.Shared._Stalker.Anomaly.Triggers.Events;
using Robust.Server.Audio;

namespace Content.Server._Stalker.Anomaly.Effects.Systems;

public sealed class STAnomalyEffectSoundSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAnomalyEffectSoundComponent, STAnomalyTriggerEvent>(OnTriggered);
    }

    private void OnTriggered(Entity<STAnomalyEffectSoundComponent> effect, ref STAnomalyTriggerEvent args)
    {
        foreach (var group in args.Groups)
        {
            if (!effect.Comp.Options.TryGetValue(group, out var options))
                continue;

            _audio.PlayPredicted(options.Sound, Transform(effect).Coordinates, effect);
        }
    }
}
