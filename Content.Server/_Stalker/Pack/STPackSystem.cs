using Content.Server.Administration.Commands;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._Stalker.Pack;
using Content.Shared.Mobs;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Stalker.Pack;

public sealed class STPackSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly HTNSystem _htn = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<STPackSpawnerComponent, ComponentInit>(OnSpawnerInit);

        SubscribeLocalEvent<STPackHeadComponent, MobStateChangedEvent>(OnHeadStateChanged);
        SubscribeLocalEvent<STPackHeadComponent, DeleteComponent>(OnHeadDeleted);
    }

    private void OnHeadStateChanged(Entity<STPackHeadComponent> entity, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        SetRandomHead(entity);
    }

    private void OnHeadDeleted(Entity<STPackHeadComponent> entity, ref DeleteComponent args)
    {
        SetRandomHead(entity);
    }

    private void OnSpawnerInit(Entity<STPackSpawnerComponent> entity, ref ComponentInit args)
    {
        CreatePack(entity.Comp.ProtoId, _transform.GetMapCoordinates(entity));
    }

    public void CreatePack(ProtoId<STPackPrototype> prototypeId, MapCoordinates coordinates)
    {
        if (!_prototype.TryIndex(prototypeId, out var prototype))
        {
            Log.Error($"Failed create pack, prototype {prototypeId} not exists");
            return;
        }

        // Creating head
        var headUid = Spawn(prototype.Head, coordinates);
        AddComp<STPackHeadComponent>(headUid);

        // Creating members
        var memberCount = _random.Next(prototype.MinMemberCount, prototype.MaxMemberCount);
        for (var i = 0; i < memberCount; i++)
        {
            var memberPrototype = _random.Pick(prototype.Members);

            var memberUid = Spawn(memberPrototype, coordinates);
            var memberComponent = EnsureComp<STPackMemberComponent>(memberUid);
            memberComponent.Head = headUid;

            SetBlackboard(memberUid, memberComponent.BlackboardHeadKey, headUid);
        }
    }

    private void SetRandomHead(EntityUid previousHead)
    {
        EntityUid? newHead = null;
        var query = EntityQueryEnumerator<STPackMemberComponent>();

        while (query.MoveNext(out var uid, out var memberComponent))
        {
            if (memberComponent.Head != previousHead)
                continue;

            if (newHead is null)
            {
                newHead ??= uid;
                continue;
            }

            memberComponent.Head = newHead.Value;
            SetBlackboard(uid, memberComponent.BlackboardHeadKey, newHead.Value);
        }

        if (newHead is null)
            return;

        if (HasComp<STPackMemberComponent>(newHead.Value))
            RemComp<STPackMemberComponent>(newHead.Value);

        if (!HasComp<STPackHeadComponent>(newHead.Value))
            AddComp<STPackHeadComponent>(newHead.Value);
    }

    private void SetBlackboard(EntityUid member, string blackboard, EntityUid head)
    {
        if (!TryComp<HTNComponent>(member, out var htn))
            return;

        if (htn.Plan is not null)
            _htn.ShutdownPlan(htn);

        _npc.SetBlackboard(member, blackboard, new EntityCoordinates(head, Vector2.Zero));
        _htn.Replan(htn);
    }
}
