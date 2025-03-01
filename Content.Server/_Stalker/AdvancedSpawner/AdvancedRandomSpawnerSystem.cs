using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.IoC;
using Content.Shared.Popups;
using Content.Server._Stalker.AdvancedSpawner;
using Content.Server.TrashDetector.Components;

public class SpawnCategory
{
    public string Id { get; set; }
    public int Weight { get; set; }
    public List<SpawnEntry> Prototypes { get; set; }

    public SpawnCategory(string id, int weight, List<SpawnEntry> prototypes)
    {
        Id = id;
        Weight = weight;
        Prototypes = prototypes;
    }
}

namespace Content.Server._Stalker.AdvancedSpawner
{
    public sealed class AdvancedRandomSpawnerSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<AdvancedRandomSpawnerComponent, MapInitEvent>(OnMapInit);
        }

        private void OnMapInit(EntityUid uid, AdvancedRandomSpawnerComponent comp, MapInitEvent args)
        {
            Logger.Info($"OnMapInit вызван для сущности {uid}");
            var config = new AdvancedRandomSpawnerConfig(comp);

            var detectorData = EntityManager.GetComponentOrNull<TempDetectorDataComponent>(uid);
            if (detectorData != null)
            {
                config.ApplyModifiers(detectorData.Detector);
                EntityManager.RemoveComponent<TempDetectorDataComponent>(uid);
            }

            // При инициализации можно проигнорировать возвращаемое значение.
            SpawnEntitiesFromModifiedConfig(uid, config);
        }

        // Метод возвращает список заспавненных категорий (имён категорий).
        public List<string> SpawnEntitiesFromModifiedConfig(EntityUid uid, AdvancedRandomSpawnerConfig config)
        {
            var availableCategories = GetAvailableCategories(config);
            var chosenCategory = PickWeighted(availableCategories, c => c.Weight);

            if (chosenCategory == null || chosenCategory.Prototypes.Count == 0)
            {
                Logger.Warning($"Не удалось выбрать категорию для сущности {uid}");
                return new List<string>();
            }

            Logger.Info($"Выбрана категория: {chosenCategory.Id}");

            // Получаем базовые координаты спавна.
            var spawnCoords = Transform(uid).MapPosition;
            // Вызываем метод, в который передаем и координаты, и величину смещения.
            TrySpawnEntities(uid, chosenCategory, spawnCoords, config.Offset);

            _popupSystem.PopupEntity($"Заспавнены предметы из категории: {chosenCategory.Id}", uid);

            if (config.DeleteSpawnerAfterSpawn)
                QueueDel(uid);

            // Возвращаем список с единственным элементом – именем выбранной категории.
            return new List<string> { chosenCategory.Id };
        }

        // Изменённый метод TrySpawnEntities теперь принимает базовые координаты и offset.
        private void TrySpawnEntities(EntityUid uid, SpawnCategory chosenCategory, MapCoordinates spawnCoords, float offset)
        {
            int itemCount = GetItemCount(chosenCategory.Id);
            Logger.Info($"Спавн {itemCount} предметов из категории {chosenCategory.Id}");

            // Для каждого спавна выбираем прототип с учётом веса.
            var chosenItem = PickWeighted(chosenCategory.Prototypes, e => e.Weight);
            if (chosenItem == null)
                return;

            for (int i = 0; i < itemCount; i++)
            {
                // Для каждого экземпляра рассчитываем случайное смещение.
                for (int j = 0; j < chosenItem.Count; j++)
                {
                    var angle = _random.NextFloat() * MathF.PI * 2;
                    var radius = _random.NextFloat() * offset;
                    var offsetX = MathF.Cos(angle) * radius;
                    var offsetY = MathF.Sin(angle) * radius;
                    var newCoords = new MapCoordinates(spawnCoords.Position.X + offsetX, spawnCoords.Position.Y + offsetY, spawnCoords.MapId);
                    // Обновляем координаты сущности, используя новое смещение.
                    var entityCoords = Transform(uid).Coordinates.WithPosition(newCoords.Position);
                    EntityManager.SpawnEntity(chosenItem.PrototypeId, entityCoords);
                }
            }
        }

        private List<SpawnCategory> GetAvailableCategories(AdvancedRandomSpawnerConfig config)
        {
            return new List<SpawnCategory>
            {
                new("Common", config.CategoryWeights.GetValueOrDefault("Common", 50), config.CommonPrototypes),
                new("Rare", config.CategoryWeights.GetValueOrDefault("Rare", 30), config.RarePrototypes),
                new("Legendary", config.CategoryWeights.GetValueOrDefault("Legendary", 10), config.LegendaryPrototypes),
                new("Negative", config.CategoryWeights.GetValueOrDefault("Negative", 10), config.NegativePrototypes)
            }.Where(c => c.Prototypes.Count > 0).ToList();
        }

        private int GetItemCount(string category)
        {
            double mean = 3 / 2.5;
            double stdDev = 3 / 6.0;

            if (category == "Legendary")
            {
                mean = 1.2;
                stdDev = 0.4;
            }
            else if (category == "Rare")
            {
                mean = 1.8;
                stdDev = 0.6;
            }

            int itemCount;
            do
            {
                itemCount = (int)Math.Round(_random.NextGaussian(mean, stdDev));
            }
            while (itemCount < 1 || itemCount > 3);

            return itemCount;
        }

        private T? PickWeighted<T>(List<T> items, Func<T, int> weightSelector) where T : class
        {
            if (items.Count == 0)
                return default;

            int totalWeight = items.Sum(weightSelector);
            int roll = _random.Next(0, totalWeight);
            int currentWeight = 0;

            foreach (var item in items)
            {
                currentWeight += weightSelector(item);
                if (roll < currentWeight)
                    return item;
            }
            return items.Last();
        }
    }
}
