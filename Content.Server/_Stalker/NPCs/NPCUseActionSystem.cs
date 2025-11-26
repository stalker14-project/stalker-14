using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Actions;
using Content.Shared.Coordinates;
using Robust.Shared.Timing;

namespace Content.Server._Stalker.NPCs;

public sealed class NPCUseActionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NPCUseActionComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<NPCUseActionComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.ActionEnt = _actions.AddAction(ent, ent.Comp.ActionId);
    }

    public bool TryUseAction(Entity<NPCUseActionComponent?> user, EntityUid target)
    {
        if (!Resolve(user, ref user.Comp, false))
            return false;

        if (TryComp<EntityWorldTargetActionComponent>(user.Comp.ActionEnt, out var actionEntityWorldTarget))
        {
            if (!_actions.ValidAction(actionEntityWorldTarget))
                return false;

            if (actionEntityWorldTarget.Event != null)
            {
                actionEntityWorldTarget.Event.Coords = Transform(target).Coordinates;
            }

            _actions.PerformAction(user,
                null,
                user.Comp.ActionEnt.Value,
                actionEntityWorldTarget,
                actionEntityWorldTarget.BaseEvent,
                _timing.CurTime,
                false);
            return true;
        }
        else if (TryComp<WorldTargetActionComponent>(user.Comp.ActionEnt, out var actionWorldTarget))
        {
            if (!_actions.ValidAction(actionWorldTarget))
                return false;

            if (actionWorldTarget.Event != null)
            {
                actionWorldTarget.Event.Target = Transform(target).Coordinates;
            }
            _actions.PerformAction(user,
                null,
                user.Comp.ActionEnt.Value,
                actionWorldTarget,
                actionWorldTarget.BaseEvent,
                _timing.CurTime,
                false);
            return true;
        }
        else if (TryComp<EntityTargetActionComponent>(user.Comp.ActionEnt, out var actionTarget))
        {
            if (!_actions.ValidAction(actionTarget))
                return false;
            if (actionTarget.Event is null)
                return false;
            actionTarget.Event.Target = target;
            _actions.SetCooldown(user.Comp.ActionEnt.Value, actionTarget.UseDelay ?? TimeSpan.FromSeconds(1));
            _actions.PerformAction(user,
                null,
                user.Comp.ActionEnt.Value,
                actionTarget,
                actionTarget.BaseEvent,
                _timing.CurTime,
                false);
            return true;
        }
        else if (TryComp<InstantActionComponent>(user.Comp.ActionEnt, out var instantAction))
        {
            if (!_actions.ValidAction(instantAction))
                return false;

            _actions.SetCooldown(user.Comp.ActionEnt.Value, instantAction.UseDelay ?? TimeSpan.FromSeconds(1));
            _actions.PerformAction(user,
                null,
                user.Comp.ActionEnt.Value,
                instantAction,
                instantAction.BaseEvent,
                _timing.CurTime,
                false);
            return true;
        }
        return false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Tries to use the attack on the current target.
        var query = EntityQueryEnumerator<NPCUseActionComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out var comp, out var htn))
        {
            if (!htn.Blackboard.TryGetValue<EntityUid>(comp.TargetKey, out var target, EntityManager))
                continue;

            TryUseAction((uid, comp), target);
        }
    }
}
