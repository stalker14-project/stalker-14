using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.IoC;
using Content.Server._Stalker.AdvancedSpawner;
using Content.Server.TrashDetector.Components;
using Robust.Shared.Prototypes;
using Content.Shared.Popups;
using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Server._Stalker.AdvancedSpawner
{
    // Класс для описания категории спавна
    public sealed class SpawnCategory
    {
        public string Id { get; }
        public int Weight { get; }
        public List<SpawnEntry> Prototypes { get; }

        public SpawnCategory(string id, int weight, List<SpawnEntry> prototypes)
        {
            Id = id;
            Weight = weight;
            Prototypes = prototypes;
        }
    }

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

            SpawnEntitiesFromModifiedConfig(uid, config);
        }

        public List<string> SpawnEntitiesFromModifiedConfig(EntityUid uid, AdvancedRandomSpawnerConfig config)
        {
            var availableCategories = GetAvailableCategories(config);
            if (availableCategories.Count == 0)
            {
                Logger.Warning($"Нет доступных категорий для спавна сущности {uid}");
                return new List<string>();
            }

            var finalCategory = SelectFinalCategory(availableCategories);
            if (finalCategory == null || finalCategory.Prototypes.Count == 0)
            {
                Logger.Warning($"Не удалось выбрать категорию для сущности {uid}");
                return new List<string>();
            }

            Logger.Info($"Выбрана категория: {finalCategory.Id}");

            var finalPrototype = SelectFinalPrototype(finalCategory.Prototypes);
            if (finalPrototype == null)
            {
                Logger.Warning($"Не удалось выбрать прототип из категории {finalCategory.Id}");
                return new List<string>();
            }

            Logger.Info($"Выбран прототип: {finalPrototype.PrototypeId}");

            var spawnCoords = Transform(uid).Coordinates; // Используем EntityCoordinates
            TrySpawnEntities(uid, finalCategory.Id, finalPrototype, spawnCoords, config.Offset, config.MaxSpawnCount);

            _popupSystem.PopupEntity($"Заспавнен предмет: {finalPrototype.PrototypeId}", uid);

            if (config.DeleteSpawnerAfterSpawn)
                QueueDel(uid);

            return new List<string> { finalCategory.Id };
        }

        private SpawnCategory? SelectFinalCategory(List<SpawnCategory> categories)
        {
            if (categories.Count == 0)
            {
                Logger.Warning("Список категорий пуст");
                return null;
            }

            int totalWeight = categories.Sum(c => c.Weight);
            if (totalWeight <= 0)
            {
                Logger.Warning("Сумма весов категорий равна 0 или отрицательна");
                return null;
            }

            // Этап 1: 13 бросков
            var firstStageWinners = new List<SpawnCategory>();
            for (int i = 0; i < 13; i++)
            {
                int roll = _random.Next(totalWeight);
                int currentSum = 0;
                foreach (var category in categories)
                {
                    currentSum += category.Weight;
                    if (roll < currentSum)
                    {
                        firstStageWinners.Add(category);
                        break;
                    }
                }
            }

            if (firstStageWinners.Count == 0)
            {
                Logger.Warning("Не удалось выбрать ни одной категории на первом этапе");
                return null;
            }

            // Этап 2: 6 бросков из 13
            var secondStageWinners = new List<SpawnCategory>();
            for (int i = 0; i < 6; i++)
            {
                int roll = _random.Next(firstStageWinners.Count);
                secondStageWinners.Add(firstStageWinners[roll]);
            }

            if (secondStageWinners.Count == 0)
            {
                Logger.Warning("Не удалось выбрать ни одной категории на втором этапе");
                return null;
            }

            // Этап 3: 1 бросок из 6
            int finalRoll = _random.Next(secondStageWinners.Count);
            return secondStageWinners[finalRoll];
        }

        private SpawnEntry? SelectFinalPrototype(List<SpawnEntry> prototypes)
        {
            if (prototypes.Count == 0)
            {
                Logger.Warning("Список прототипов пуст");
                return null;
            }

            int totalWeight = prototypes.Sum(p => p.Weight);
            if (totalWeight <= 0)
            {
                Logger.Warning("Сумма весов прототипов равна 0 или отрицательна");
                return null;
            }

            // Этап 1: 13 бросков
            var firstStageWinners = new List<SpawnEntry>();
            for (int i = 0; i < 13; i++)
            {
                int roll = _random.Next(totalWeight);
                int currentSum = 0;
                foreach (var prototype in prototypes)
                {
                    currentSum += prototype.Weight;
                    if (roll < currentSum)
                    {
                        firstStageWinners.Add(prototype);
                        break;
                    }
                }
            }

            if (firstStageWinners.Count == 0)
            {
                Logger.Warning("Не удалось выбрать ни одного прототипа на первом этапе");
                return null;
            }

            // Этап 2: 6 бросков из 13
            var secondStageWinners = new List<SpawnEntry>();
            for (int i = 0; i < 6; i++)
            {
                int roll = _random.Next(firstStageWinners.Count);
                secondStageWinners.Add(firstStageWinners[roll]);
            }

            if (secondStageWinners.Count == 0)
            {
                Logger.Warning("Не удалось выбрать ни одного прототипа на втором этапе");
                return null;
            }

            // Этап 3: 1 бросок из 6
            int finalRoll = _random.Next(secondStageWinners.Count);
            return secondStageWinners[finalRoll];
        }

        private void TrySpawnEntities(EntityUid uid, string category, SpawnEntry chosenPrototype, EntityCoordinates spawnCoords, float offset, int maxSpawnCount)
        {
            // Определяем количество предметов для спавна
            int itemCount = GetItemCount(category, maxSpawnCount);

            Logger.Info($"Спавн {itemCount} экземпляров прототипа {chosenPrototype.PrototypeId}");

            for (int i = 0; i < itemCount; i++)
            {
                var angle = _random.NextFloat() * MathF.PI * 2;
                var radius = _random.NextFloat() * offset;
                var offsetX = MathF.Cos(angle) * radius;
                var offsetY = MathF.Sin(angle) * radius;
                var newPosition = spawnCoords.Position + new Vector2(offsetX, offsetY);
                var newCoords = spawnCoords.WithPosition(newPosition);
                EntityManager.SpawnEntity(chosenPrototype.PrototypeId, newCoords);
            }
        }

        private List<SpawnCategory> GetAvailableCategories(AdvancedRandomSpawnerConfig config)
        {
            return new List<SpawnCategory>
            {
                new SpawnCategory("Common", config.CategoryWeights.GetValueOrDefault("Common", 50), config.CommonPrototypes),
                new SpawnCategory("Rare", config.CategoryWeights.GetValueOrDefault("Rare", 30), config.RarePrototypes),
                new SpawnCategory("Legendary", config.CategoryWeights.GetValueOrDefault("Legendary", 10), config.LegendaryPrototypes),
                new SpawnCategory("Negative", config.CategoryWeights.GetValueOrDefault("Negative", 10), config.NegativePrototypes)
            }.Where(c => c.Prototypes.Count > 0).ToList();
        }

        // Новый метод GetItemCount с ограничением
        private int GetItemCount(string category, int maxItems)
        {
            int itemCount = 1; // Начинаем с 1 предмета
            int probability; // Шанс в процентах

            // Задаём шансы в зависимости от категории
            switch (category)
            {
                case "Legendary":
                    probability = 10; // 10% шанс на доп. предмет
                    break;
                case "Rare":
                    probability = 20; // 20% шанс на доп. предмет
                    break;
                default: // "Common" и "Negative"
                    probability = 50; // 50% шанс
                    break;
            }

            // Подбрасываем "кубик" для каждого дополнительного предмета
            while (itemCount < maxItems && _random.Next(100) < probability)
            {
                itemCount++;
            }

            return itemCount;
        }
    }
}
