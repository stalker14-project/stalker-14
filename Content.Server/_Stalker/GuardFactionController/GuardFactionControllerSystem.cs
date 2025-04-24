using Content.Server._Stalker.AllowTaking;
using Content.Server._Stalker.ApproachEmitter;
using Content.Server.NPC.HTN;
using Content.Shared.Actions;
using Content.Shared.Mobs;
using Content.Shared.NPC.Systems;
using Content.Shared._Stalker.GuardFactionController;

namespace Content.Server._Stalker.GuardFactionController;

public sealed partial class GuardFactionControllerSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedActionsSystem _action = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NpcFactionSystem _npc = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GuardFactionControllerComponent, ComponentStartup>(OnStart);
        SubscribeLocalEvent<GuardFactionResistantComponent, ComponentStartup>(MakePeacefull);
        SubscribeLocalEvent<GuardFactionControllerComponent, GuardFactionControllerOffActionEvent>(ToggleOff);
        SubscribeLocalEvent<GuardFactionControllerComponent, GuardFactionControllerOnActionEvent>(ToggleOn);
        SubscribeLocalEvent<GuardFactionControllerComponent, MobStateChangedEvent>(OnStateChange);
    }


    public void OnStart(EntityUid uid, GuardFactionControllerComponent component, ComponentStartup args)
    {
        // Add actions
        _action.AddAction(uid, "GuardFactionControllerOffAction", uid);
        _action.AddAction(uid, "GuardFactionControllerOnAction", uid);
    }

    public void MakePeacefull(EntityUid uid, GuardFactionResistantComponent component, ComponentStartup args)
    {
        _npc.RemoveFaction(uid, component.Faction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<GuardFactionResistantComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            component.TimeBeforeRemove -= TimeSpan.FromSeconds(frameTime);
            if (component.TimeBeforeRemove <= TimeSpan.Zero)
            {
                RemComp<GuardFactionResistantComponent>(uid);
                _npc.AddFaction(uid, component.Faction);
            }
        }
    }

    public void ToggleOff(EntityUid uid, GuardFactionControllerComponent component, GuardFactionControllerOffActionEvent args)
    {
        var position = Transform(uid).Coordinates;
        var humans = _lookup.GetEntitiesInRange<ApproachEmitterComponent>(position, 20f); // Selecting all alive objects which are not Sins

        foreach (var human in humans)
        {
            EnsureComp<GuardFactionResistantComponent>(human);
        }
    }

    public void ToggleOn(EntityUid uid, GuardFactionControllerComponent component, GuardFactionControllerOnActionEvent args)
    {
        ZeroRemovalTime();
    }
    public void OnStateChange(EntityUid uid, GuardFactionControllerComponent component, MobStateChangedEvent args)
    {
        ZeroRemovalTime();
    }

    public void ZeroRemovalTime()
    {
        var query = EntityQueryEnumerator<GuardFactionResistantComponent>();
        while (query.MoveNext(out var resistantguy, out var resistant))
        {
            resistant.TimeBeforeRemove = TimeSpan.Zero;
        }
    }

}
