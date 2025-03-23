using Content.Shared.CombatMode.Pacification;
using Robust.Shared.Physics.Events;
using Content.Shared.Access.Systems;
using Content.Shared.Buckle.Components;

namespace Content.Server._Stalker.PacifiedZone;

public sealed class StalkerPacifiedZoneSystem : EntitySystem
{

    [Dependency] private readonly AccessReaderSystem _access = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StalkerPacifiedZoneComponent, StartCollideEvent>(OnCollideStalkerPacifiedZone);
    }

    private void OnCollideStalkerPacifiedZone(EntityUid uid, StalkerPacifiedZoneComponent component, ref StartCollideEvent args)
    {
        var target = args.OtherEntity;

        if (target == EntityUid.Invalid)
            return;

        if (!TryComp(target, out StrapComponent? strap))
        {
            RemComp<StrapComponent>(target);
            return;
        }

        if (component.Reader && _access.IsAllowed(args.OtherEntity, args.OurEntity))
            return;

        if (component.Pacified)
        {
            EnsureComp<PacifiedComponent>(target);
        }
        else
        {
            RemComp<PacifiedComponent>(target);
        }

    }

}
