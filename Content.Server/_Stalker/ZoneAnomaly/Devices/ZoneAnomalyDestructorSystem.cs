using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Server._Stalker.ZoneAnomaly.Devices;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared._Stalker.ZoneAnomaly;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server._Stalker.ZoneAnomaly.Devices;

public sealed class ZoneAnomalyDestructorSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZoneAnomalyDestructorComponent, GetVerbsEvent<InteractionVerb>>(AddVerb);
        SubscribeLocalEvent<ZoneAnomalyDestructorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ZoneAnomalyDestructorComponent, InteractionDoAfterEvent>(OnDoAfter);
    }

    public void AddVerb(EntityUid uid, ZoneAnomalyDestructorComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var argsDoAfter = new DoAfterArgs(EntityManager, args.User, component.Delay, new InteractionDoAfterEvent(), uid, uid)
        {
            NeedHand = true,
            BreakOnMove = true,
            CancelDuplicate = true
        };

        args.Verbs.Add(new InteractionVerb
        {
            Text = "Activate",
            Act = () => _doAfter.TryStartDoAfter(argsDoAfter),
        });
    }

    public void OnAfterInteract(EntityUid uid, ZoneAnomalyDestructorComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { Valid: true })
            return;

        var argsDoAfter = new DoAfterArgs(EntityManager, args.User, component.Delay, new InteractionDoAfterEvent(), uid, uid)
        {
            NeedHand = true,
            BreakOnMove = true,
            CancelDuplicate = true
        };

        _doAfter.TryStartDoAfter(argsDoAfter);
        args.Handled = true;
    }

    public void OnDoAfter(EntityUid uid, ZoneAnomalyDestructorComponent component, InteractionDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        var user = args.Args.User;
        if (!Exists(user))
            return;

        //_popup.PopupEntity("It works", uid);

        //starting delete logic
        var coords = _transform.GetMapCoordinates(Transform(uid));
        var deletedAny = false;

        // Destroy ALL anomalies in range — no filtering by type
        foreach (var ent in _lookup.GetEntitiesInRange(coords, 1.3f))
        {
            if (!HasComp<ZoneAnomalyComponent>(ent))
                continue;

            if (!TryComp<MetaDataComponent>(ent, out var meta) || meta.EntityPrototype == null)
                continue;

            var proto = meta.EntityPrototype;

            if (proto.Parents == null || Array.IndexOf(proto.Parents, component.TargetPrototype) == -1)
                continue;

            QueueDel(ent);
            deletedAny = true;
        }

        if (deletedAny)
            _popup.PopupEntity("Anomaly neutralized.", uid);
        else
            _popup.PopupEntity("No anomalies nearby.", uid);

        //end of delete logic

        args.Handled = true;
    }
}
