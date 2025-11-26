using Content.Shared._Stalker.ZoneAnomaly;
using Content.Shared._Stalker.ZoneAnomaly.Components;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.ZoneAnomaly;

public sealed class ZoneAnomalySystem : SharedZoneAnomalySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZoneAnomalyComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<ZoneAnomalyComponent, EndCollideEvent>(OnEndCollide);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZoneAnomalyComponent>();
        while (query.MoveNext(out var uid, out var anomaly))
        {
            switch (anomaly.State)
            {
                case ZoneAnomalyState.Idle:
                    break;

                case ZoneAnomalyState.Activated:
                    if (_timing.CurTime < anomaly.ActivationTime)
                        break;

                    Recharge((uid, anomaly));
                    break;

                case ZoneAnomalyState.Charging:
                    if (_timing.CurTime < anomaly.ChargingTime)
                        break;

                    CalmDown((uid, anomaly));
                    break;

                case ZoneAnomalyState.Preparing:
                    if (_timing.CurTime < anomaly.PreparingTime)
                        break;

                    Activate((uid, anomaly));
                    break;
            }
        }
    }

    private void OnStartCollide(Entity<ZoneAnomalyComponent> anomaly, ref StartCollideEvent args)
    {
        if(_whitelistSystem.IsBlacklistPass(anomaly.Comp.CollisionBlacklist, args.OtherEntity))
            return;

        TryAddEntity(anomaly, args.OtherEntity);
    }

    private void OnEndCollide(Entity<ZoneAnomalyComponent> anomaly, ref EndCollideEvent args)
    {
        TryRemoveEntity(anomaly, args.OtherEntity);
    }

    public bool TryActivate(Entity<ZoneAnomalyComponent> anomaly, EntityUid? trigger = null)
    {
        var list = new HashSet<EntityUid>();
        if (trigger is { } item)
            list.Add(item);

        return TryActivate(anomaly, list);
    }

    public bool TryActivate(Entity<ZoneAnomalyComponent> anomaly, HashSet<EntityUid> triggers)
    {
        if (anomaly.Comp.State != ZoneAnomalyState.Idle)
            return false;

        anomaly.Comp.Triggers.UnionWith(triggers);

        if (anomaly.Comp.PreparingDelay.TotalSeconds == 0)
        {
            Activate(anomaly);
            return true;
        }

        anomaly.Comp.PreparingTime = _timing.CurTime + anomaly.Comp.PreparingDelay;
        SetState(anomaly, ZoneAnomalyState.Preparing);
        return true;
    }

    public void Activate(Entity<ZoneAnomalyComponent> anomaly)
    {
        SetState(anomaly, ZoneAnomalyState.Activated);

        var ev = new ZoneAnomalyActivateEvent(anomaly, anomaly.Comp.Triggers);
        RaiseLocalEvent(anomaly, ref ev);

        anomaly.Comp.Triggers.Clear();
        anomaly.Comp.ActivationTime = _timing.CurTime + anomaly.Comp.ActivationDelay;
    }

    public bool TryRecharge(Entity<ZoneAnomalyComponent> anomaly, TimeSpan? delay = null)
    {
        Recharge(anomaly, delay);
        return true;
    }

    private void Recharge(Entity<ZoneAnomalyComponent> anomaly, TimeSpan? delay = null)
    {
        SetState(anomaly, ZoneAnomalyState.Charging);

        anomaly.Comp.ChargingTime = _timing.CurTime + (delay ?? anomaly.Comp.ChargingDelay);
    }

    private void CalmDown(Entity<ZoneAnomalyComponent> anomaly)
    {
        SetState(anomaly, ZoneAnomalyState.Idle);
    }

    private void SetState(Entity<ZoneAnomalyComponent> anomaly, ZoneAnomalyState state)
    {
        var previous = anomaly.Comp.State;
        anomaly.Comp.State = state;

        var ev = new ZoneAnomalyChangedState(anomaly, state, previous);
        RaiseLocalEvent(anomaly, ref ev);

        _appearance.SetData(anomaly, ZoneAnomalyVisuals.Layer, state);
    }

    private void TryAddEntity(Entity<ZoneAnomalyComponent> anomaly, EntityUid uid)
    {
        if (anomaly.Comp.InAnomaly.Contains(uid))
            return;

        anomaly.Comp.InAnomaly.Add(uid);

        var ev = new ZoneAnomalyEntityAddEvent(anomaly, uid);
        RaiseLocalEvent(anomaly, ref ev);
    }

    public void TryRemoveEntity(Entity<ZoneAnomalyComponent> anomaly, EntityUid uid)
    {
        if (!anomaly.Comp.InAnomaly.Contains(uid))
            return;

        anomaly.Comp.InAnomaly.Remove(uid);

        var ev = new ZoneAnomalyEntityRemoveEvent(anomaly, uid);
        RaiseLocalEvent(anomaly, ref ev);
    }
}
