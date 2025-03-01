using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.IoC;

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
            TrySpawnEntities(uid, comp);

            if (comp.DeleteSpawnerAfterSpawn)
                QueueDel(uid);
        }

        public void TrySpawnEntities(EntityUid uid, AdvancedRandomSpawnerComponent comp)
        {
            var categories = new List<SpawnCategory>();

            if (comp.CommonPrototypes.Count > 0)
                categories.Add(new SpawnCategory { Id = "Common", Weight = comp.CategoryWeights.GetValueOrDefault("Common", 0), Prototypes = comp.CommonPrototypes });

            if (comp.RarePrototypes.Count > 0)
                categories.Add(new SpawnCategory { Id = "Rare", Weight = comp.CategoryWeights.GetValueOrDefault("Rare", 0), Prototypes = comp.RarePrototypes });

            if (comp.LegendaryPrototypes.Count > 0)
                categories.Add(new SpawnCategory { Id = "Legendary", Weight = comp.CategoryWeights.GetValueOrDefault("Legendary", 0), Prototypes = comp.LegendaryPrototypes });

            if (comp.NegativePrototypes.Count > 0)
                categories.Add(new SpawnCategory { Id = "Negative", Weight = comp.CategoryWeights.GetValueOrDefault("Negative", 0), Prototypes = comp.NegativePrototypes });

            if (categories.Count == 0)
                return; // Нет доступных категорий для спавна

            int itemCount = DetermineItemCount(comp);
            var spawnCoords = Transform(uid).MapPosition; // Получаем глобальные координаты

            for (int i = 0; i < itemCount; i++)
            {
                var chosenCategory = PickRandomCategory(categories);
                if (chosenCategory == null || chosenCategory.Prototypes.Count == 0)
                    continue;

                var entry = PickWeighted(chosenCategory.Prototypes, e => e.Weight);
                if (entry == null || !_random.Prob(1.0f)) // 100% шанс спавна
                    continue;

                for (int j = 0; j < entry.Count; j++) // Учитываем количество предметов
                {
                    // Генерируем случайное смещение
                    var angle = _random.NextFloat() * MathF.PI * 2;
                    var radius = _random.NextFloat() * comp.Offset;
                    var offsetX = MathF.Cos(angle) * radius;
                    var offsetY = MathF.Sin(angle) * radius;

                    // Создаем новые MapCoordinates
                    var newCoords = new MapCoordinates(
                        spawnCoords.Position.X + offsetX,
                        spawnCoords.Position.Y + offsetY,
                        spawnCoords.MapId
                    );

                    // Преобразуем MapCoordinates в EntityCoordinates
                    var entityCoords = Transform(uid).Coordinates.WithPosition(newCoords.Position);

                    EntityManager.SpawnEntity(entry.PrototypeId, entityCoords);
                }
            }
        }

        private int DetermineItemCount(AdvancedRandomSpawnerComponent comp)
        {
            int itemCount = 1; // Первый предмет всегда спавнится.

            while (itemCount < comp.MaxSpawnCount && _random.Prob(0.5f)) // 50% шанс на каждый следующий предмет
            {
                itemCount++;
            }

            return itemCount;
        }

        private SpawnCategory? PickRandomCategory(List<SpawnCategory> categories)
        {
            if (categories.Count == 0) return null;

            int totalWeight = 0;
            foreach (var category in categories)
                totalWeight += category.Weight;

            if (totalWeight == 0)
                return null;

            int roll = _random.Next(0, totalWeight);
            foreach (var category in categories)
            {
                if (roll < category.Weight)
                    return category;
                roll -= category.Weight;
            }

            return categories[0]; // Если что-то пошло не так, выбираем первую категорию
        }

        private SpawnEntry? PickWeighted(List<SpawnEntry> entries, Func<SpawnEntry, int> weightSelector)
        {
            if (entries.Count == 0) return null;

            int totalWeight = 0;
            foreach (var entry in entries)
                totalWeight += weightSelector(entry);

            if (totalWeight == 0)
                return null;

            int roll = _random.Next(0, totalWeight);
            foreach (var entry in entries)
            {
                if (roll < weightSelector(entry))
                    return entry;
                roll -= weightSelector(entry);
            }

            return entries[0]; // Если что-то пошло не так, выбираем первый элемент
        }
    }

    public sealed class SpawnCategory
    {
        public string Id { get; set; } = string.Empty;
        public int Weight { get; set; } = 1;
        public List<SpawnEntry> Prototypes { get; set; } = new();
    }
}
