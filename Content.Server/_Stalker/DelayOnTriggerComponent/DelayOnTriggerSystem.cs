using Content.Server.Explosion.EntitySystems;
using Content.Shared.Timing;

namespace Content.Server._Stalker.DelayOnTriggerComponent;

public sealed class DelayOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly UseDelaySystem _delay = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DelayOnTriggerComponent, TriggerEvent>(OnTriggered);
    }

    private void OnTriggered(EntityUid uid, DelayOnTriggerComponent component, TriggerEvent args)
    {
        EnsureComp<UseDelayComponent>(uid, out var delay);
        _delay.SetLength((args.Triggered, delay), TimeSpan.FromSeconds(component.Delay));
    }
}
