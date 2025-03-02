using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Content.Server._Stalker.AdvancedSpawner;
using Content.Server.TrashDetector.Components;
using Robust.Shared.Prototypes;
using Content.Shared.Popups;
using System.Numerics;
using Robust.Shared.Log;

namespace Content.Server._Stalker.AdvancedSpawner
{
    public sealed class AdvancedRandomSpawnerSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
        private ISawmill _sawmill = default!;

        public override void Initialize()
        {
            base.Initialize();
            _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("AdvancedSpawner");
            SubscribeLocalEvent<AdvancedRandomSpawnerComponent, MapInitEvent>(OnMapInit);
        }

        private void OnMapInit(EntityUid uid, AdvancedRandomSpawnerComponent comp, MapInitEvent args)
        {
            _sawmill.Info($"OnMapInit вызван для сущности {uid}");
            var config = new AdvancedRandomSpawnerConfig(comp);

            var detectorData = EntityManager.GetComponentOrNull<TempDetectorDataComponent>(uid);
            if (detectorData != null)
            {
                config.ApplyModifiers(detectorData.Detector);
                EntityManager.RemoveComponent<TempDetectorDataComponent>(uid);
            }

            SpawnEntitiesUsingSpawner(uid, config);
        }

        public List<string> SpawnEntitiesUsingSpawner(EntityUid uid, AdvancedRandomSpawnerConfig config)
        {
            var spawnCoords = Transform(uid).Coordinates;

            var spawner = new Spawner(
                _random,
                EntityManager,
                config.CategoryWeights,
                config.CommonPrototypes,
                config.RarePrototypes,
                config.LegendaryPrototypes,
                config.NegativePrototypes,
                config.MaxSpawnCount
            );

            var spawnedItems = spawner.SpawnEntities(spawnCoords, config.Offset);

            foreach (var item in spawnedItems)
            {
                _popupSystem.PopupEntity($"Заспавнен предмет: {item}", uid);
            }

            if (config.DeleteSpawnerAfterSpawn)
            {
                QueueDel(uid);
            }

            return spawnedItems;
        }
    }
}
