using System;
using System.Collections.Generic;
using Content.Server._Stalker.AdvancedSpawner;

public class AdvancedRandomSpawnerConfig
{
    public readonly Dictionary<string, int> CategoryWeights;
    public readonly List<SpawnCategory> Categories;
    public readonly Dictionary<string, List<SpawnEntry>> Prototypes;
    public readonly float Offset;
    public readonly bool DeleteAfterSpawn;
    public readonly int MaxSpawnCount;

    public AdvancedRandomSpawnerConfig(AdvancedRandomSpawnerComponent comp)
    {
        CategoryWeights = new Dictionary<string, int>(comp.CategoryWeights);
        Categories = new List<SpawnCategory>();
        Prototypes = new Dictionary<string, List<SpawnEntry>>();

        foreach (var category in comp.CategoryWeights.Keys)
        {
            Prototypes[category] = new List<SpawnEntry>(comp.PrototypeLists.GetValueOrDefault(category, new List<SpawnEntry>()));
            Categories.Add(new SpawnCategory(category, CategoryWeights[category], Prototypes[category]));
        }

        Offset = comp.Offset;
        DeleteAfterSpawn = comp.DeleteAfterSpawn;
        MaxSpawnCount = comp.MaxSpawnCount;
    }

    public void ApplyModifiers(Dictionary<string, int> weightModifiers, Dictionary<string, List<SpawnEntry>> extraPrototypes)
    {
        foreach (var (category, modifier) in weightModifiers)
        {
            if (CategoryWeights.ContainsKey(category))
                CategoryWeights[category] = Math.Max(1, CategoryWeights[category] + modifier);
        }

        foreach (var category in CategoryWeights.Keys)
        {
            if (!Prototypes.ContainsKey(category))
                Prototypes[category] = new List<SpawnEntry>();
        }

        foreach (var (category, extraEntries) in extraPrototypes)
        {
            if (!Prototypes.ContainsKey(category))
                Prototypes[category] = new List<SpawnEntry>();

            foreach (var entry in extraEntries)
            {
                if (!Prototypes[category].Exists(e => e.PrototypeId == entry.PrototypeId))
                    Prototypes[category].Add(entry);
            }
        }
    }
}
