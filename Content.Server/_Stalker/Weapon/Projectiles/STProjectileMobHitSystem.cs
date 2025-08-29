using Content.Shared.Mobs.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server._Stalker.Weapon.Projectiles;

public sealed class STProjectileMobHitSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STProjectileMobHitComponent, PreventCollideEvent>(OnHit);
    }

    private void OnHit(Entity<STProjectileMobHitComponent> entity, ref PreventCollideEvent args)
    {
        if (HasComp<MobStateComponent>(args.OtherEntity))
            return;

        args.Cancelled = true;
    }
}
