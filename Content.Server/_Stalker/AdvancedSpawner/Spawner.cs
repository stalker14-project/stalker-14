using System.Linq;
using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Stalker.AdvancedSpawner;

public class Spawner
{
    private readonly IRobustRandom _random;
    private readonly IEntityManager _entityManager;
    private int _spawnCount;

    private readonly List<SpawnCategory> _categories;
    private readonly int _maxSpawnCount;

    private Spawner(
        IRobustRandom random,
        IEntityManager entityManager,
        List<SpawnCategory> categories,
        int maxSpawnCount)
    {
        _random = random;
        _entityManager = entityManager;
        _categories = categories;
        _maxSpawnCount = maxSpawnCount;
    }

    public static Spawner CreateInstance(IRobustRandom random, IEntityManager entityManager, List<SpawnCategory> categories, int maxSpawnCount)
    {
        return new Spawner(random, entityManager, categories, maxSpawnCount);
    }

    public List<string> SpawnEntities(EntityCoordinates spawnCoords, float offset, AdvancedRandomSpawnerConfig config)
    {
        List<string> spawnedItems = new();
        _spawnCount = 0;

        while (_spawnCount < _maxSpawnCount)
        {
            var category = SelectCategoryWithWeights(config);

            if (!config.Prototypes.ContainsKey(category.Name) || config.Prototypes[category.Name].Count == 0)
                continue;

            SpawnPrototype(category, spawnCoords, offset, spawnedItems);
            _spawnCount++;

            if (_spawnCount >= _maxSpawnCount)
                break;

            var chance = GetChanceForCategory(category, _spawnCount, config);
            if (_random.NextDouble() >= chance)
                break;
        }

        return spawnedItems;
    }

    private void SpawnPrototype(SpawnCategory category, EntityCoordinates spawnCoords, float offset, List<string> spawnedItems)
    {
        var prototype = SelectPrototypeWithWeights(category);
        var spawnAmount = prototype.Count;
        for (var i = 0; i < spawnAmount && _spawnCount < _maxSpawnCount; i++)
        {
            SpawnEntity(prototype, spawnCoords, offset);
            spawnedItems.Add(prototype.PrototypeId);
            _spawnCount++;
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

    private SpawnCategory SelectCategoryWithWeights(AdvancedRandomSpawnerConfig config)
    {
        var totalWeight = _categories.Sum(c => c.Weight + config.CategoryWeights.GetValueOrDefault(c.Name, 0));
        var roll = _random.Next(totalWeight);
        var currentSum = 0;

        foreach (var category in _categories)
        {
            var categoryWeight = category.Weight + config.CategoryWeights.GetValueOrDefault(category.Name, 0);
            currentSum += categoryWeight;
            if (roll < currentSum)
                return category;
        }

        return _categories.First();
    }

    private SpawnEntry SelectPrototypeWithWeights(SpawnCategory category)
    {
        var entries = category.Prototypes;
        var totalWeight = entries.Sum(p => p.Weight);
        var roll = _random.Next(totalWeight);
        var currentSum = 0;

        foreach (var prototype in entries)
        {
            currentSum += prototype.Weight;
            if (roll < currentSum)
                return prototype;
        }

        return entries.First();
    }

    private double GetChanceForCategory(SpawnCategory category, int spawnNumber, AdvancedRandomSpawnerConfig config)
    {
        var modifier = config.CategoryWeights.GetValueOrDefault(category.Name, 0);
        var categoryWeight = Math.Max(1, category.Weight + modifier);
        var totalWeight = _categories.Sum(c => c.Weight + config.CategoryWeights.GetValueOrDefault(c.Name, 0));

        if (totalWeight == 0)
            return 0.0;

        var baseChance = (double)categoryWeight / totalWeight;

        if (spawnNumber >= config.MaxSpawnCount)
            return 0.0;

        var coefficient = Math.Pow(1.0 - (double)spawnNumber / config.MaxSpawnCount, 2);
        var chance = baseChance * coefficient;

        return chance;
    }
}
