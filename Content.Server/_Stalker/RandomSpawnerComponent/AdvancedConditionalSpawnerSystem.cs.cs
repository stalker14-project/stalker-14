using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.AdvancedSpawners.Components;
using Content.Shared.GameTicking.Components;
using JetBrains.Annotations;
using Robust.Shared.Random;
using Robust.Shared.Map;

namespace Content.Server.AdvancedSpawners.Systems;

/// <summary>
/// Система, управляющая работой AdvancedConditionalSpawnerComponent.
/// </summary>
[UsedImplicitly]
public sealed class AdvancedConditionalSpawnerSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly GameTicker _ticker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRuleStartedEvent>(OnRuleStarted);
        SubscribeLocalEvent<AdvancedConditionalSpawnerComponent, MapInitEvent>(OnCondSpawnMapInit);
    }

    private void OnCondSpawnMapInit(EntityUid uid, AdvancedConditionalSpawnerComponent component, MapInitEvent args)
    {
        TrySpawn(uid, component);
    }

    private void OnRuleStarted(ref GameRuleStartedEvent args)
    {
        var query = EntityQueryEnumerator<AdvancedConditionalSpawnerComponent>();
        while (query.MoveNext(out var uid, out var spawner))
        {
            if (spawner.GameRules.Contains(args.RuleId))
                Spawn(uid, spawner);
        }
    }

    private void TrySpawn(EntityUid uid, AdvancedConditionalSpawnerComponent component)
    {
        if (component.GameRules.Count == 0)
        {
            Spawn(uid, component);
            return;
        }

        foreach (var rule in component.GameRules)
        {
            if (_ticker.IsGameRuleActive(rule))
            {
                Spawn(uid, component);
                return;
            }
        }
    }

    private void Spawn(EntityUid uid, AdvancedConditionalSpawnerComponent component)
    {
        if (component.Chance < 1.0f && !_robustRandom.Prob(component.Chance))
            return;

        if (component.Prototypes.Count == 0)
        {
            Log.Warning($"[AdvancedSpawner] Пустой список прототипов! Объект: {ToPrettyString(uid)}");
            return;
        }

        if (!Deleted(uid))
            EntityManager.SpawnEntity(_robustRandom.Pick(component.Prototypes), Transform(uid).Coordinates);
    }
}


