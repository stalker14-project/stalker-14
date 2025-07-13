using Content.Server._Stalker.Anomaly.Triggers.Systems;
using Content.Shared._Stalker.Anomaly.Triggers.Events;
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Events;

namespace Content.Server._Stalker.Anomaly.Triggers.StartCollide;

public sealed class STAnomalyTriggerStartCollideSystem : EntitySystem
{
    [Dependency] private readonly STAnomalyTriggerSystem _anomalyTrigger = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAnomalyTriggerStartCollideComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(Entity<STAnomalyTriggerStartCollideComponent> trigger, ref StartCollideEvent args)
    {
        var targetUid = args.OtherEntity;
        if (trigger.Comp.Blacklist is not null && _whitelistSystem.IsBlacklistPass(trigger.Comp.Blacklist, targetUid))
            return;

        var groups = new HashSet<string> { trigger.Comp.MainTriggerGroup };

        var ev = new STAnomalyTriggerStartCollideGetAdditionalGroupsEvent(targetUid);
        RaiseLocalEvent(targetUid, ev);
        RaiseLocalEvent(trigger, ev); // YAPI~!

        groups.UnionWith(ev.Groups);

        _anomalyTrigger.Trigger(trigger, groups);
    }
}
