using Content.Server.Damage.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Damage;
using Content.Server.Stunnable;

namespace Content.Server.Damage.Systems;

public sealed class StaminaDamageUserOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly StunSystem _stun = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<StaminaDamageUserOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(EntityUid uid, StaminaDamageUserOnTriggerComponent component, TriggerEvent args)
    {
        if (args.User is null || !args.Handled)
            return;
        _stun.TryParalyze(args.User.Value, TimeSpan.FromSeconds(component.Stun), true);

    }

}
