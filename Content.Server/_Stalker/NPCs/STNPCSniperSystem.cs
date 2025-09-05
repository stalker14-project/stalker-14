using System.Collections.Frozen;
using Content.Server.Chat.Systems;
using Content.Server.Interaction;
using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.GameTicking;
using Content.Shared.Interaction.Events;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Physics;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Whistle;
using Content.Shared.Whitelist;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.CombatMode.Pacification;

namespace Content.Server._Stalker.NPCs;

public sealed partial class STNPCSniperSystem : EntitySystem
{
    private static readonly ProtoId<NpcFactionPrototype> PlayerFaction = "Stalker";

    [Dependency] private readonly IConsoleHost _consoleHost = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;


    private FrozenDictionary<MapCoordinates, Entity<STNPCSniperComponent>> _hashedCoords = new Dictionary<MapCoordinates, Entity<STNPCSniperComponent>>().ToFrozenDictionary();

    public override void Initialize()
    {
        base.Initialize();

        InitializeCommands();

        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);

        SubscribeLocalEvent<BeforeThrowEvent>(OnBeforeThrow);
        SubscribeLocalEvent<AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<ShotAttemptedEvent>(OnShootAttempt);
    }

    private void OnRoundStarted(RoundStartedEvent @event)
    {
        RegenerateMap();
    }

    private void OnBeforeThrow(ref BeforeThrowEvent ev)
    {
        if (!TryAttack(ev.PlayerUid))
            return;

        ev.Cancelled = true;
    }

    private void OnAttackAttempt(AttackAttemptEvent ev)
    {
        if (!TryAttack(ev.Uid, ev.Target))
            return;

        ev.Cancel();
    }

    private void OnShootAttempt(ref ShotAttemptedEvent ev)
    {
        if (!TryAttack(ev.User))
            return;

        ev.Cancel();
    }

    private bool TryAttack(EntityUid attackerUid, EntityUid? targetUid)
    {
        // fallback
        if (targetUid is null)
            return false;

        // faction equal
        if (!_npcFaction.IsMember((targetUid.Value, null), PlayerFaction))
            return false;

        return TryAttack(attackerUid);
    }

    private bool TryAttack(EntityUid attackerUid)
    {
        var transform = Transform(attackerUid);
        var coords = new MapCoordinates(_transform.GetWorldPosition(transform).Rounded(), transform.MapID);

        // regenerate sniper coordinate
        if (!_hashedCoords.TryGetValue(coords, out var entity))
            return false;

        if (_whitelistSystem.IsWhitelistPass(entity.Comp.AttackerWhitelist, attackerUid))
            return false;

        // pacified entities should not be attacked in any case, prevents right click sniper bug abuse (read: arena bug reports)
        if (_entityManager.HasComponent<PacifiedComponent>(attackerUid))
            return false;

        if (entity.Comp.SoundGunshot is not null)
            _audio.PlayPvs(entity.Comp.SoundGunshot, Transform(entity).Coordinates);

        if (entity.Comp.Damage is not null)
            _damageable.TryChangeDamage(attackerUid, entity.Comp.Damage, ignoreResistances: true);

        if (entity.Comp.MessageShoot.Count > 0)
            _chat.TrySendInGameICMessage(entity, Loc.GetString(_random.Pick(entity.Comp.MessageShoot).Id), InGameICChatType.Speak, false);

        return true;
    }

    private void RegenerateMap()
    {
        Log.Info("Regenerating snipers map...");
        var coords = new Dictionary<MapCoordinates, Entity<STNPCSniperComponent>>();

        var query = EntityQueryEnumerator<STNPCSniperComponent, TransformComponent>();
        while (query.MoveNext(out var entityUid, out var sniperComponent, out var transformComponent))
        {
            var position = _transform.GetWorldPosition(transformComponent);

            var coordinates = new Vector2i((int) position.X, (int) position.Y);
            var size = sniperComponent.Range;
            var box2 = new Box2i(coordinates.X - size, coordinates.Y - size, coordinates.X + size + 1, coordinates.Y + size);

            for (var x = box2.Left; x < box2.Right; x++)
            {
                for (var y = box2.Bottom; y < box2.Top; y++)
                {
                    var mapCoords = new MapCoordinates(x, y, transformComponent.MapID);
                    if (!_interaction.InRangeUnobstructed(entityUid, mapCoords, 0f, CollisionGroup.InteractImpassable, uid => uid == entityUid))
                        continue;

                    coords.TryAdd(mapCoords, (entityUid, sniperComponent));
                }
            }
        }

        _hashedCoords = coords.ToFrozenDictionary();
        Log.Info($"Sniper map regenerated: {_hashedCoords}");
    }
}
