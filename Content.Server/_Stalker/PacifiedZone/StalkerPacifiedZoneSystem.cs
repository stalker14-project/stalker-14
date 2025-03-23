using Content.Shared.CombatMode.Pacification;
using Robust.Shared.Physics.Events;
using Content.Shared.Access.Systems;
using Content.Shared.Buckle.Components;

namespace Content.Server._Stalker.PacifiedZone;

public sealed class StalkerPacifiedZoneSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StalkerPacifiedZoneComponent, StartCollideEvent>(HandleCollision);
    }

    private void HandleCollision(EntityUid zoneUid, StalkerPacifiedZoneComponent zoneComponent, ref StartCollideEvent args)
    {
        var entity = args.OtherEntity;
        if (entity == EntityUid.Invalid)
            return;

        if (!TryComp(entity, out StrapComponent? strap))
        {
            RemComp<StrapComponent>(entity);
            return;
        }

        if (zoneComponent.Reader && _accessReader.IsAllowed(entity, args.OurEntity))
            return;

        if (zoneComponent.Pacified)
            EnsureComp<PacifiedComponent>(entity);
        else
            RemComp<PacifiedComponent>(entity);
    }
}
