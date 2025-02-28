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

            int itemCount = DetermineItemCount(comp);

            for (int i = 0; i < itemCount; i++)
            {
                var chosenCategory = PickRandomCategory(categories);
                if (chosenCategory == null)
                    return;

                var chosenEntry = PickRandomEntry(chosenCategory.Prototypes);
                if (chosenEntry == null)
                    return;

                var spawnCoords = Transform(uid).Coordinates;
                EntityManager.SpawnEntity(chosenEntry.PrototypeId, spawnCoords);
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

            int roll = _random.Next(0, totalWeight);
            foreach (var category in categories)
            {
                if (roll < category.Weight)
                    return category;
                roll -= category.Weight;
            }

            return categories[0];
        }

        private SpawnEntry? PickRandomEntry(List<SpawnEntry> entries)
        {
            if (entries.Count == 0) return null;

            int totalWeight = 0;
            foreach (var entry in entries)
                totalWeight += entry.Weight;

            int roll = _random.Next(0, totalWeight);
            foreach (var entry in entries)
            {
                if (roll < entry.Weight)
                    return entry;
                roll -= entry.Weight;
            }

            return entries[0];
        }
    }

    public sealed class SpawnCategory
    {
        public string Id { get; set; } = string.Empty;
        public int Weight { get; set; } = 1;
        public List<SpawnEntry> Prototypes { get; set; } = new();
    }
}

