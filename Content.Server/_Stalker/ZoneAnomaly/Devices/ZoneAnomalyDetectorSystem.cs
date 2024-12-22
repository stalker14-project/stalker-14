using Content.Server.Popups;
using Content.Shared._Stalker.ZoneAnomaly;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.ZoneAnomaly.Devices;

public sealed class ZoneAnomalyDetectorSystem : SharedZoneAnomalyDetectorSystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly ZoneAnomalySystem _zoneAnomaly = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZoneAnomalyDetectorComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ZoneAnomalyDetectorActivatorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZoneAnomalyDetectorComponent>();
        while (query.MoveNext(out var uid, out var detector))
        {
            if (!detector.Enabled)
                continue;

            if (_timing.CurTime < detector.NextBeepTime)
                continue;

            UpdateBeep((uid, detector));
        }
    }


    private void OnUseInHand(Entity<ZoneAnomalyDetectorComponent> detector, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryToggle(detector, args.User);
    }

    private bool TryToggle(Entity<ZoneAnomalyDetectorComponent> detector, EntityUid? user = null)
    {
        return detector.Comp.Enabled
            ? TryDisable(detector)
            : TryEnable(detector);
    }

    private bool TryEnable(Entity<ZoneAnomalyDetectorComponent> detector)
    {
        if (detector.Comp.Enabled)
            return false;

        detector.Comp.Enabled = true;
        detector.Comp.NextBeepTime = _timing.CurTime;

        _appearance.SetData(detector, ZoneAnomalyDetectorVisuals.Enabled, true);

        UpdateBeep(detector, false);
        return true;
    }

    private bool TryDisable(Entity<ZoneAnomalyDetectorComponent> detector)
    {
        if (!detector.Comp.Enabled)
            return false;

        detector.Comp.Enabled = false;

        _appearance.SetData(detector, ZoneAnomalyDetectorVisuals.Enabled, false);

        UpdateBeep(detector);
        return true;
    }

    private void UpdateBeep(Entity<ZoneAnomalyDetectorComponent> detector, bool playBeep = true)
    {
        if (!detector.Comp.Enabled)
        {
            detector.Comp.NextBeepTime += detector.Comp.MaxBeepInterval;
            return;
        }

        var xformQuery = GetEntityQuery<TransformComponent>();
        var xform = xformQuery.GetComponent(detector);

        float? closestDistance = null;
        foreach (var ent in _entityLookup.GetEntitiesInRange<ZoneAnomalyComponent>(_transform.GetMapCoordinates(xform), detector.Comp.Distance))
        {
            if (!ent.Comp.Detected || ent.Comp.DetectedLevel > detector.Comp.Level)
                continue;

            var dist = (_transform.GetWorldPosition(xform, xformQuery) - _transform.GetWorldPosition(ent, xformQuery)).Length();
            if (dist >= (closestDistance ?? float.MaxValue))
                continue;

            closestDistance = dist;
        }

        if (closestDistance is not { } distance)
            return;

        if (playBeep)
            _audio.PlayPvs(detector.Comp.BeepSound, detector);

        var scalingFactor = distance / detector.Comp.Distance;
        var interval = (detector.Comp.MaxBeepInterval - detector.Comp.MinBeepInterval) * scalingFactor + detector.Comp.MinBeepInterval;

        detector.Comp.NextBeepTime += interval;
        if (detector.Comp.NextBeepTime < _timing.CurTime)
            detector.Comp.NextBeepTime = _timing.CurTime + interval;
    }

    private void ActivateAnomalies(Entity<ZoneAnomalyDetectorActivatorComponent> activator, EntityUid user)
    {
        if (activator.Comp.NexActivationTime > _timing.CurTime)
        {
            _popup.PopupEntity("Локатор ще не готовий!", user, user, PopupType.Medium);
            return;
        }

        var count = 0;
        foreach (var ent in _entityLookup.GetEntitiesInRange<ZoneAnomalyComponent>(_transform.GetMapCoordinates(Transform(activator)), activator.Comp.Distance))
        {
            if (ent.Comp.DetectedLevel > activator.Comp.Level)
                continue;

            _zoneAnomaly.TryActivate((ent, ent.Comp));
            count++;

            if (count > activator.Comp.MaxCount && activator.Comp.MaxCount != 0)
                break;
        }

        _popup.PopupEntity("Прилад електризує оточення", user, user, PopupType.Medium);
        activator.Comp.NexActivationTime = _timing.CurTime + activator.Comp.ActivationDelay;
    }

    private void OnGetVerbs(Entity<ZoneAnomalyDetectorActivatorComponent> activator, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;
        AlternativeVerb verb = new()
        {
            Text = "Тумблер B",
            Act = () =>
            {
                ActivateAnomalies(activator, user);
            },
            Message = "Активувати аномалії",
        };
        args.Verbs.Add(verb);
    }
}
