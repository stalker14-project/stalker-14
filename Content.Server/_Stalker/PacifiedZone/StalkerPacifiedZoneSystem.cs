using Content.Server.NPC.HTN;
using Content.Shared.CombatMode.Pacification;
using Robust.Shared.Physics.Events;
using Content.Shared.Access.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;

namespace Content.Server._Stalker.PacifiedZone;

public sealed class StalkerPacifiedZoneSystem : EntitySystem
{

    [Dependency] private readonly NpcFactionSystem _npc = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StalkerPacifiedZoneComponent, StartCollideEvent>(OnCollideStalkerPacifiedZone);
    }

    private void OnCollideStalkerPacifiedZone(EntityUid uid, StalkerPacifiedZoneComponent component, ref StartCollideEvent args)
    {
        var target = args.OtherEntity;
        var self = args.OurEntity;

        if (target == EntityUid.Invalid
            || self ==  EntityUid.Invalid
            || component.Reader
            && TryComp(target, out NpcFactionMemberComponent? targetMember)
            && TryComp(self, out NpcFactionMemberComponent? selfMember)
            && _npc.IsMember(target, component.Faction))
            return;

        if (TryComp(target, out StrapComponent? strap))
        {
            RemComp<StrapComponent>(target);
            return;
        }

        if (TryComp(target, out HTNComponent? htn))
        {
            QueueDel(target);
            return;
        }

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
