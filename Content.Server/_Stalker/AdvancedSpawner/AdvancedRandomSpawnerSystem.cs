using Content.Shared.Popups;
using Robust.Shared.Random;

namespace Content.Server._Stalker.AdvancedSpawner;

public sealed class AdvancedRandomSpawnerSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AdvancedRandomSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, AdvancedRandomSpawnerComponent component, MapInitEvent args)
    {
        Log.Debug($"[AdvancedSpawner] Initializing entity {uid}");

        var config = new AdvancedRandomSpawnerConfig(component);

        if (EntityManager.TryGetComponent<TempDetectorDataComponent>(uid, out var detectorData))
        {
            Log.Debug($"[AdvancedSpawner] Applying modifiers from TempDetectorDataComponent on {uid}");


            config.ApplyModifiers(detectorData.Detector.WeightModifiers, detectorData.Detector.ExtraPrototypes);

            EntityManager.RemoveComponent<TempDetectorDataComponent>(uid);
        }

        SpawnEntitiesUsingSpawner(uid, config);
    }

    public List<string> SpawnEntitiesUsingSpawner(EntityUid uid, AdvancedRandomSpawnerConfig config)
    {
        var spawnCoords = Transform(uid).Coordinates;

        var spawner = Spawner.CreateInstance(_random, EntityManager, config.Categories, config.MaxSpawnCount);
        var spawnedItems = spawner.SpawnEntities(spawnCoords, config.Offset, config);

        Log.Debug($"[AdvancedSpawner] Spawned {spawnedItems.Count} entities at {spawnCoords}");

        foreach (var item in spawnedItems)
        {
            _popupSystem.PopupEntity($"Spawned item: {item}", uid);
        }

        if (config.DeleteAfterSpawn)
        {
            Log.Debug($"[AdvancedSpawner] Deleting entity {uid} after spawn");
            EntityManager.QueueDeleteEntity(uid);
        }

        return spawnedItems;
    }
}
