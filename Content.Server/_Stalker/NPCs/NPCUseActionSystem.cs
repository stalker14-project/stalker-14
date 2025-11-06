using Content.Server.NPC.HTN;
using Content.Shared.Actions;

namespace Content.Server._Stalker.NPCs;

public sealed class NPCUseActionSystem : EntitySystem
{
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

        if (_actions.GetAction(user.Comp.ActionEnt) is not { } actionEntityWorldTarget)
            return false;

        if (!_actions.ValidAction(actionEntityWorldTarget))
            return false;

        _actions.SetEventTarget(actionEntityWorldTarget, target);

        // NPC is serverside, no prediction :(
        _actions.PerformAction(user.Owner, actionEntityWorldTarget, predicted: false);

        return true;
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
