using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Maths;
using System.Numerics;

namespace Content.Server._Stalker.AdvancedSpawner
{
    public class Spawner
    {
        private readonly IRobustRandom _random;
        private readonly IEntityManager _entityManager;
        private int spawnCount = 0;

        private readonly Dictionary<string, int> categoryWeights; // Веса категорий
        private readonly Dictionary<string, List<SpawnEntry>> prototypes; // Прототипы внутри категорий
        private readonly int maxSpawnCount; // Максимальное количество спавнов

        public Spawner(
            IRobustRandom random,
            IEntityManager entityManager,
            Dictionary<string, int> categoryWeights,
            List<SpawnEntry> commonPrototypes,
            List<SpawnEntry> rarePrototypes,
            List<SpawnEntry> legendaryPrototypes,
            List<SpawnEntry> negativePrototypes,
            int maxSpawnCount)
        {
            _random = random;
            _entityManager = entityManager;
            this.categoryWeights = categoryWeights;
            this.maxSpawnCount = maxSpawnCount;
            this.prototypes = new Dictionary<string, List<SpawnEntry>>
            {
                { "Common", commonPrototypes },
                { "Rare", rarePrototypes },
                { "Legendary", legendaryPrototypes },
                { "Negative", negativePrototypes }
            };
        }

        public List<string> SpawnEntities(EntityCoordinates spawnCoords, float offset)
        {
            List<string> spawnedItems = new();
            spawnCount = 0;

            while (spawnCount < maxSpawnCount)
            {
                string category = SelectCategoryWithWeights();
                SpawnPrototype(category, spawnCoords, offset, spawnedItems);
                spawnCount++;

                if (spawnCount >= maxSpawnCount)
                    break;

                double chance = GetChanceForCategory(category, spawnCount);
                if (_random.NextDouble() >= chance)
                    break;
            }

            return spawnedItems;
        }

        private void SpawnPrototype(string category, EntityCoordinates spawnCoords, float offset, List<string> spawnedItems)
        {
            SpawnEntry prototype = SelectPrototypeWithWeights(category);
            int spawnAmount = prototype.Count;
            for (int i = 0; i < spawnAmount && spawnCount < maxSpawnCount; i++)
            {
                SpawnEntity(prototype, spawnCoords, offset);
                spawnedItems.Add(category);
                spawnCount++;
            }
        }

        private void SpawnEntity(SpawnEntry prototype, EntityCoordinates spawnCoords, float offset)
        {
            var angle = _random.NextFloat() * MathF.PI * 2;
            var radius = _random.NextFloat() * offset;
            var offsetX = MathF.Cos(angle) * radius;
            var offsetY = MathF.Sin(angle) * radius;
            var newPosition = spawnCoords.Position + new Vector2(offsetX, offsetY);
            var newCoords = spawnCoords.WithPosition(newPosition);

            _entityManager.SpawnEntity(prototype.PrototypeId, newCoords);
        }

        private string SelectCategoryWithWeights()
        {
            int totalWeight = categoryWeights.Values.Sum();
            int roll = _random.Next(totalWeight);
            int currentSum = 0;

            foreach (var (category, weight) in categoryWeights)
            {
                currentSum += weight;
                if (roll < currentSum)
                    return category;
            }

            return "Common";
        }

        private SpawnEntry SelectPrototypeWithWeights(string category)
        {
            var entries = prototypes[category];
            int totalWeight = entries.Sum(p => p.Weight);
            int roll = _random.Next(totalWeight);
            int currentSum = 0;

            foreach (var prototype in entries)
            {
                currentSum += prototype.Weight;
                if (roll < currentSum)
                    return prototype;
            }

            return entries.First();
        }

        private double GetChanceForCategory(string category, int spawnNumber)
        {
            if (!categoryWeights.TryGetValue(category, out int categoryWeight))
                return 0.0;

            int totalWeight = categoryWeights.Values.Sum();
            double baseChance = (double)categoryWeight / totalWeight;

            if (spawnNumber >= maxSpawnCount)
                return 0.0;

            double coefficient = 1.0 - (double)spawnNumber / maxSpawnCount;
            double chance = baseChance * coefficient;

            return Math.Max(0.0, chance);
        }
    }
}
