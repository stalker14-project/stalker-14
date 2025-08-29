using Content.Server._Stalker.Anomaly.TargetGroups.Weight;
using Content.Shared._Stalker.Anomaly.Triggers.Events;
using Content.Shared._Stalker.Weight;
using Content.Shared.Whitelist;

namespace Content.Server._Stalker.Anomaly.Triggers.StartCollide;

public sealed class STAnomalyTriggerStartCollideGroupsSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STAnomalyTriggerStartCollideGroupsComponent, STAnomalyTriggerStartCollideGetAdditionalGroupsEvent>(GetAdditionalGroups);
    }

    private void GetAdditionalGroups(Entity<STAnomalyTriggerStartCollideGroupsComponent> trigger, ref STAnomalyTriggerStartCollideGetAdditionalGroupsEvent args)
    {
        foreach (var (whitelist, group) in trigger.Comp.Groups)
        {
            if (!_whitelist.IsBlacklistPass(whitelist, args.Target))
                continue;

            args.Add(group);
        }
    }
}
