using Content.Server.Stealth;
using Content.Shared._Stalker.ZoneAnomaly;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared._Stalker.ZoneAnomaly.Effects.Components;
using Content.Shared.Stealth.Components;

namespace Content.Server._Stalker.ZoneAnomaly.Effects.Systems;

public sealed partial class ZoneAnomalyEffectStealthSystem : EntitySystem
{
    [Dependency] private readonly StealthSystem _stealth = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZoneAnomalyEffectStealthComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZoneAnomalyEffectStealthComponent, ZoneAnomalyChangedState>(OnChangeState);
    }

    private void OnStartup(Entity<ZoneAnomalyEffectStealthComponent> effect, ref ComponentStartup args)
    {
        if (!HasComp<StealthComponent>(effect.Owner))
            return;
        _stealth.SetVisibility(effect, effect.Comp.Idle);
    }

    private void OnChangeState(Entity<ZoneAnomalyEffectStealthComponent> effect, ref ZoneAnomalyChangedState args)
    {
        switch (args.Current)
        {
            case ZoneAnomalyState.Idle:
                _stealth.SetVisibility(effect, effect.Comp.Idle);
                break;

            case ZoneAnomalyState.Activated:
                _stealth.SetVisibility(effect, effect.Comp.Activated);
                break;
        }
    }
}
