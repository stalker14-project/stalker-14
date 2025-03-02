using System;
using System.Collections.Generic;
using Content.Server.TrashDetector.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;

namespace Content.Server._Stalker.AdvancedSpawner
{
    public class AdvancedRandomSpawnerConfig
    {
        public Dictionary<string, int> CategoryWeights { get; set; }
        public List<SpawnEntry> CommonPrototypes { get; set; }
        public List<SpawnEntry> RarePrototypes { get; set; }
        public List<SpawnEntry> LegendaryPrototypes { get; set; }
        public List<SpawnEntry> NegativePrototypes { get; set; }
        public float Offset { get; set; }
        public bool DeleteSpawnerAfterSpawn { get; set; }
        public int MaxSpawnCount { get; set; }

        public AdvancedRandomSpawnerConfig(AdvancedRandomSpawnerComponent comp)
        {
            CategoryWeights = new Dictionary<string, int>(comp.CategoryWeights);
            CommonPrototypes = new List<SpawnEntry>(comp.CommonPrototypes);
            RarePrototypes = new List<SpawnEntry>(comp.RarePrototypes);
            LegendaryPrototypes = new List<SpawnEntry>(comp.LegendaryPrototypes);
            NegativePrototypes = new List<SpawnEntry>(comp.NegativePrototypes);
            Offset = comp.Offset;
            DeleteSpawnerAfterSpawn = comp.DeleteSpawnerAfterSpawn;
            MaxSpawnCount = comp.MaxSpawnCount;
        }

        public void ApplyModifiers(TrashDetectorComponent detector)
        {
            Logger.Info($"[ApplyModifiers] Применение модификаций от детектора {detector.Owner}");

            foreach (var category in new[] { "Common", "Rare", "Legendary", "Negative" })
            {
                if (CategoryWeights.ContainsKey(category))
                {
                    int oldWeight = CategoryWeights[category];
                    int modifier = detector.GetWeightModifier(category);
                    CategoryWeights[category] = Math.Max(1, oldWeight + modifier);
                    Logger.Info($"[ApplyModifiers] {category}: {oldWeight} -> {CategoryWeights[category]} (модификатор: {modifier})");
                }
            }

            // Логирование добавленных прототипов
            Logger.Info($"[ApplyModifiers] Добавление новых прототипов:");

            Logger.Info($"Common: {detector.ExtraCommonPrototypes.Count} новых элементов");
            CommonPrototypes.AddRange(detector.ExtraCommonPrototypes);

            Logger.Info($"Rare: {detector.ExtraRarePrototypes.Count} новых элементов");
            RarePrototypes.AddRange(detector.ExtraRarePrototypes);

            Logger.Info($"Legendary: {detector.ExtraLegendaryPrototypes.Count} новых элементов");
            LegendaryPrototypes.AddRange(detector.ExtraLegendaryPrototypes);

            Logger.Info($"Negative: {detector.ExtraNegativePrototypes.Count} новых элементов");
            NegativePrototypes.AddRange(detector.ExtraNegativePrototypes);
        }
    }
}

