using System;
using System.Collections.Generic;
using Content.Server.TrashDetector.Components;
using Content.Server.TrashDetector;
using Content.Server._Stalker.AdvancedSpawner; // Убедиться, что здесь нужные using

public class AdvancedRandomSpawnerConfig
{
    public readonly Dictionary<SpawnCategoryType, int> CategoryWeights;
    public readonly List<SpawnCategory> Categories;
    public readonly Dictionary<SpawnCategoryType, List<SpawnEntry>> Prototypes;
    public readonly float Offset;
    public readonly bool DeleteAfterSpawn;
    public readonly int MaxSpawnCount;

    public int CommonWeightMod { get; private set; }
    public int RareWeightMod { get; private set; }
    public int LegendaryWeightMod { get; private set; }
    public int NegativeWeightMod { get; private set; }

    public AdvancedRandomSpawnerConfig(AdvancedRandomSpawnerComponent comp)
    {
        CategoryWeights = new Dictionary<SpawnCategoryType, int>();
        Categories = new List<SpawnCategory>();
        Prototypes = new Dictionary<SpawnCategoryType, List<SpawnEntry>>();

        foreach (SpawnCategoryType category in Enum.GetValues(typeof(SpawnCategoryType)))
        {
            var weight = comp.CategoryWeights.GetValueOrDefault(category, 1);
            CategoryWeights[category] = weight;

            if (!Prototypes.ContainsKey(category))
                Prototypes[category] = new List<SpawnEntry>(GetPrototypeList(category, comp));

            Categories.Add(new SpawnCategory(category, weight, Prototypes[category]));
        }

        Offset = comp.Offset;
        DeleteAfterSpawn = comp.DeleteAfterSpawn;
        MaxSpawnCount = comp.MaxSpawnCount;
    }

    private static List<SpawnEntry> GetPrototypeList(SpawnCategoryType category, AdvancedRandomSpawnerComponent comp) => category switch
    {
        SpawnCategoryType.Common => comp.CommonPrototypes,
        SpawnCategoryType.Rare => comp.RarePrototypes,
        SpawnCategoryType.Legendary => comp.LegendaryPrototypes,
        SpawnCategoryType.Negative => comp.NegativePrototypes,
        _ => new List<SpawnEntry>()
    };

    public void ApplyModifiers(TrashDetectorComponent detector)
    {

        CommonWeightMod = detector.CommonWeightMod;
        RareWeightMod = detector.RareWeightMod;
        LegendaryWeightMod = detector.LegendaryWeightMod;
        NegativeWeightMod = detector.NegativeWeightMod;

        foreach (var category in CategoryWeights.Keys)
        {
            CategoryWeights[category] = Math.Max(1, CategoryWeights[category] +
                TrashDetectorUtils.GetWeightModifier(category.ToString(),
                    CommonWeightMod, RareWeightMod, LegendaryWeightMod, NegativeWeightMod));
        }


        if (!Prototypes.ContainsKey(SpawnCategoryType.Common))
            Prototypes[SpawnCategoryType.Common] = new List<SpawnEntry>();
        if (!Prototypes.ContainsKey(SpawnCategoryType.Rare))
            Prototypes[SpawnCategoryType.Rare] = new List<SpawnEntry>();
        if (!Prototypes.ContainsKey(SpawnCategoryType.Legendary))
            Prototypes[SpawnCategoryType.Legendary] = new List<SpawnEntry>();
        if (!Prototypes.ContainsKey(SpawnCategoryType.Negative))
            Prototypes[SpawnCategoryType.Negative] = new List<SpawnEntry>();

        Prototypes[SpawnCategoryType.Common].AddRange(detector.ExtraCommonPrototypes);
        Prototypes[SpawnCategoryType.Rare].AddRange(detector.ExtraRarePrototypes);
        Prototypes[SpawnCategoryType.Legendary].AddRange(detector.ExtraLegendaryPrototypes);
        Prototypes[SpawnCategoryType.Negative].AddRange(detector.ExtraNegativePrototypes);
    }
}
