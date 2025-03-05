using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Maths;
using System.Numerics;
using Content.Server._Stalker.AdvancedSpawner;

public class Spawner
{
    private readonly IRobustRandom _random;
    private readonly IEntityManager _entityManager;
    private int _spawnCount = 0;

    private readonly List<SpawnCategory> _categories;
    private readonly int _maxSpawnCount;

    public Spawner(
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

            double chance = GetChanceForCategory(category, _spawnCount, config);
            if (_random.NextDouble() >= chance)
                break;
        }

        return spawnedItems;
    }

    private void SpawnPrototype(SpawnCategory category, EntityCoordinates spawnCoords, float offset, List<string> spawnedItems)
    {
        var prototype = SelectPrototypeWithWeights(category);
        int spawnAmount = prototype.Count;
        for (int i = 0; i < spawnAmount && _spawnCount < _maxSpawnCount; i++)
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
        int totalWeight = _categories.Sum(c => c.Weight + config.CategoryWeights.GetValueOrDefault(c.Name, 0));
        int roll = _random.Next(totalWeight);
        int currentSum = 0;

        foreach (var category in _categories)
        {
            int categoryWeight = category.Weight + config.CategoryWeights.GetValueOrDefault(category.Name, 0);
            currentSum += categoryWeight;
            if (roll < currentSum)
                return category;
        }

        return _categories.First();
    }

    private SpawnEntry SelectPrototypeWithWeights(SpawnCategory category)
    {
        var entries = category.Prototypes;
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

    private double GetChanceForCategory(SpawnCategory category, int spawnNumber, AdvancedRandomSpawnerConfig config)
    {
        int modifier = config.CategoryWeights.GetValueOrDefault(category.Name, 0);
        int categoryWeight = Math.Max(1, category.Weight + modifier);
        int totalWeight = _categories.Sum(c => c.Weight + config.CategoryWeights.GetValueOrDefault(c.Name, 0));

        if (totalWeight == 0)
            return 0.0;

        double baseChance = (double)categoryWeight / totalWeight;

        if (spawnNumber >= config.MaxSpawnCount)
            return 0.0;

        double coefficient = Math.Pow(1.0 - (double)spawnNumber / config.MaxSpawnCount, 2);
        double chance = baseChance * coefficient;

        return chance;
    }
}
