using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Server.Flash;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed class ZoneAnomalyEffectFlashSystem : EntitySystem
{
    [Dependency] private readonly FlashSystem _flashSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZoneAnomalyEffectFlashComponent, ZoneAnomalyActivateEvent>(OnActivate);
    }

    private void OnActivate(Entity<ZoneAnomalyEffectFlashComponent> effect, ref ZoneAnomalyActivateEvent args)
    {
        if (!TryComp<ZoneAnomalyComponent>(effect, out var anomaly))
            return;

        foreach (var trigger in anomaly.InAnomaly)
        {
            Flash(effect, trigger);
        }
    }

    private void Flash(Entity<ZoneAnomalyEffectFlashComponent> effect, EntityUid target)
    {
        if (!TryComp<ZoneAnomalyEffectFlashComponent>(effect, out var comp))
            return;
        _flashSystem.FlashArea(effect.Owner, target, comp.Range, comp.Duration * 1000f);
    }
}
