using System;
using System.Collections.Generic;
using Content.Server.TrashDetector.Components;
using Robust.Shared.GameObjects;

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
            foreach (var category in new[] { "Common", "Rare", "Legendary", "Negative" })
            {
                if (CategoryWeights.ContainsKey(category))
                    CategoryWeights[category] = Math.Max(1, CategoryWeights[category] + detector.GetWeightModifier(category));
            }

            CommonPrototypes.AddRange(detector.ExtraCommonPrototypes);
            RarePrototypes.AddRange(detector.ExtraRarePrototypes);
            LegendaryPrototypes.AddRange(detector.ExtraLegendaryPrototypes);
            NegativePrototypes.AddRange(detector.ExtraNegativePrototypes);
        }
    }
}
