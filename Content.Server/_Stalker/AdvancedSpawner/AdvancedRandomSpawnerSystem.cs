using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.IoC;
using Content.Server.TrashDetector.Components; // Добавлено для доступа к TrashDetectorComponent

namespace Content.Server._Stalker.AdvancedSpawner
{
    public sealed class AdvancedRandomSpawnerSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<AdvancedRandomSpawnerComponent, MapInitEvent>(OnMapInit);
        }

        private void OnMapInit(EntityUid uid, AdvancedRandomSpawnerComponent comp, MapInitEvent args)
        {
            Logger.InfoS("advancedspawner", $"OnMapInit вызван для сущности {uid}");
            var config = new AdvancedRandomSpawnerConfig(comp);

            // Применяем модификаторы, если компонент детектора присутствует
            var detector = EntityManager.GetComponentOrNull<TrashDetectorComponent>(uid);
            if (detector != null)
            {
                config.ApplyModifiers(detector);
                Logger.InfoS("advancedspawner", $"Модификаторы применены для сущности {uid}");
            }
            else
            {
                Logger.InfoS("advancedspawner", $"Компонент TrashDetector не найден для сущности {uid}");
            }

            SpawnEntitiesFromModifiedConfig(uid, config);
        }

        public List<string> SpawnEntitiesFromModifiedConfig(EntityUid uid, AdvancedRandomSpawnerConfig config)
        {
            Logger.InfoS("advancedspawner", $"SpawnEntitiesFromModifiedConfig вызван для сущности {uid}");
            var spawnedCategories = TrySpawnEntities(uid, config);
            if (config.DeleteSpawnerAfterSpawn)
                QueueDel(uid);
            return spawnedCategories;
        }

        private List<string> TrySpawnEntities(EntityUid uid, AdvancedRandomSpawnerConfig config)
        {
            var categories = new List<SpawnCategory>();

            if (config.CommonPrototypes.Count > 0)
                categories.Add(new SpawnCategory { Id = "Common", Weight = config.CategoryWeights.GetValueOrDefault("Common", 50), Prototypes = config.CommonPrototypes });

            if (config.RarePrototypes.Count > 0)
                categories.Add(new SpawnCategory { Id = "Rare", Weight = config.CategoryWeights.GetValueOrDefault("Rare", 30), Prototypes = config.RarePrototypes });

            if (config.LegendaryPrototypes.Count > 0)
                categories.Add(new SpawnCategory { Id = "Legendary", Weight = config.CategoryWeights.GetValueOrDefault("Legendary", 10), Prototypes = config.LegendaryPrototypes });

            if (config.NegativePrototypes.Count > 0)
                categories.Add(new SpawnCategory { Id = "Negative", Weight = config.CategoryWeights.GetValueOrDefault("Negative", 10), Prototypes = config.NegativePrototypes });

            if (categories.Count == 0)
            {
                Logger.WarningS("advancedspawner", $"Нет доступных категорий для сущности {uid}");
                return new List<string>();
            }

            var spawnedCategories = new List<string>();
            var spawnCoords = Transform(uid).MapPosition;

            // Определяем, сколько предметов нужно заспавнить.
            var categoryItems = DetermineItemCount(config, categories);
            if (categoryItems.Count == 0)
            {
                Logger.WarningS("advancedspawner", $"Не выбран ни один предмет для спавна для сущности {uid}. Прерываем спавн.");
                return spawnedCategories;
            }

            foreach (var (category, items) in categoryItems)
            {
                spawnedCategories.Add(category.Id);
                foreach (var entry in items)
                {
                    if (string.IsNullOrWhiteSpace(entry.PrototypeId))
                    {
                        Logger.WarningS("advancedspawner", $"Пропускаем запись с пустым PrototypeId в категории {category.Id} для сущности {uid}");
                        continue;
                    }
                    for (int j = 0; j < entry.Count; j++)
                    {
                        var entityCoords = GetRandomSpawnCoords(spawnCoords, config.Offset);
                        EntityManager.SpawnEntity(entry.PrototypeId, entityCoords);
                        Logger.InfoS("advancedspawner", $"Заспаунен предмет {entry.PrototypeId} на {entityCoords}");
                    }
                }
            }
            return spawnedCategories;
        }

        private Dictionary<SpawnCategory, List<SpawnEntry>> DetermineItemCount(AdvancedRandomSpawnerConfig config, List<SpawnCategory> categories)
        {
            int itemCount = 1;
            while (itemCount < config.MaxSpawnCount && _random.Prob(0.5f))
            {
                itemCount++;
            }
            Logger.InfoS("advancedspawner", $"Определено количество предметов: {itemCount}");

            var categorySpawn = new Dictionary<SpawnCategory, List<SpawnEntry>>();

            int legendaryLimit = 1;
            int rareLimit = 2;
            var categoryCounts = new Dictionary<string, int>
            {
                { "Legendary", 0 },
                { "Rare", 0 }
            };

            int allocated = 0;
            while (allocated < itemCount)
            {
                var availableCategories = new List<SpawnCategory>();
                foreach (var cat in categories)
                {
                    if (cat.Id == "Legendary" && categoryCounts["Legendary"] >= legendaryLimit)
                        continue;
                    if (cat.Id == "Rare" && categoryCounts["Rare"] >= rareLimit)
                        continue;
                    availableCategories.Add(cat);
                }

                if (availableCategories.Count == 0)
                {
                    Logger.WarningS("advancedspawner", "Нет доступных категорий для выбора, лимиты исчерпаны.");
                    break;
                }

                var chosenCategory = PickWeighted(availableCategories, c => c.Weight);
                if (chosenCategory == null)
                    break;

                if (chosenCategory.Id == "Legendary")
                    categoryCounts["Legendary"]++;
                if (chosenCategory.Id == "Rare")
                    categoryCounts["Rare"]++;

                var chosenItem = PickWeighted(chosenCategory.Prototypes, e => e.Weight);
                if (chosenItem == null)
                    continue;

                if (!categorySpawn.ContainsKey(chosenCategory))
                    categorySpawn[chosenCategory] = new List<SpawnEntry>();
                categorySpawn[chosenCategory].Add(chosenItem);
                allocated++;
            }

            return categorySpawn;
        }

        private MapCoordinates GetRandomSpawnCoords(MapCoordinates baseCoords, float offset)
        {
            var angle = _random.NextFloat() * MathF.PI * 2;
            var radius = _random.NextFloat() * offset;
            var offsetX = MathF.Cos(angle) * radius;
            var offsetY = MathF.Sin(angle) * radius;
            return new MapCoordinates(baseCoords.Position.X + offsetX, baseCoords.Position.Y + offsetY, baseCoords.MapId);
        }

        private T? PickWeighted<T>(List<T> items, Func<T, int> weightSelector)
        {
            int totalWeight = 0;
            foreach (var item in items)
            {
                int weight = weightSelector(item);
                if (weight > 0)
                    totalWeight += weight;
            }
            if (totalWeight == 0)
                return default;
            int roll = _random.Next(0, totalWeight);
            foreach (var item in items)
            {
                int weight = weightSelector(item);
                if (weight > 0 && roll < weight)
                    return item;
                roll -= weight;
            }
            return items[^1];
        }
    }

    public sealed class SpawnCategory
    {
        public string Id { get; set; } = string.Empty;
        public int Weight { get; set; } = 1;
        public List<SpawnEntry> Prototypes { get; set; } = new();
    }
}
