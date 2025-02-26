using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Server.Spawners.Components
{
    [RegisterComponent]
    public sealed partial class AdvancedRandomSpawnerComponent : Component
    {
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("commonPrototypes")]
        public List<string> CommonPrototypes { get; private set; } = new();

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("rarePrototypes")]
        public List<string> RarePrototypes { get; private set; } = new();

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("legendaryPrototypes")]
        public List<string> LegendaryPrototypes { get; private set; } = new();

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("negativePrototypes")]
        public List<string> NegativePrototypes { get; private set; } = new();

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("commonChance")]
        public float CommonChance { get; private set; } = 0.8f;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("rareChance")]
        public float RareChance { get; private set; } = 0.1f;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("legendaryChance")]
        public float LegendaryChance { get; private set; } = 0.02f;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("negativeEventChance")]
        public float NegativeEventChance { get; private set; } = 0.05f;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("offset")]
        public float Offset { get; private set; } = 0.2f;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("minCommonCount")]
        public int MinCommonCount { get; private set; } = 2;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("maxCommonCount")]
        public int MaxCommonCount { get; private set; } = 3;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("minRareCount")]
        public int MinRareCount { get; private set; } = 0;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("maxRareCount")]
        public int MaxRareCount { get; private set; } = 2;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("minLegendaryCount")]
        public int MinLegendaryCount { get; private set; } = 0;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("maxLegendaryCount")]
        public int MaxLegendaryCount { get; private set; } = 1;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("minNegativeCount")]
        public int MinNegativeCount { get; private set; } = 1;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("maxNegativeCount")]
        public int MaxNegativeCount { get; private set; } = 2;

        [DataField("deleteSpawnerAfterSpawn")]
        public bool DeleteSpawnerAfterSpawn { get; private set; } = true;

        public List<string> GetRandomPrototypes(IRobustRandom random)
        {
            var result = new List<string>();

            if (!random.Prob(CommonChance))
            {
                return GetNegativePrototypes(random);
            }

            // Добавляем Legendary с уменьшающимся шансом
            if (LegendaryPrototypes.Count > 0 && random.Prob(LegendaryChance))
            {
                int count = GetItemCount(random, MinLegendaryCount, MaxLegendaryCount);
                result.AddRange(PickUnique(random, LegendaryPrototypes, count));
            }

            // Добавляем Rare с уменьшающимся шансом
            if (RarePrototypes.Count > 0 && random.Prob(RareChance))
            {
                int count = GetItemCount(random, MinRareCount, MaxRareCount);
                result.AddRange(PickUnique(random, RarePrototypes, count));
            }

            // Добавляем Common с уменьшающимся шансом
            int commonCount = GetItemCount(random, MinCommonCount, MaxCommonCount);
            result.AddRange(PickUnique(random, CommonPrototypes, commonCount));

            return result;
        }

        public List<string> GetNegativePrototypes(IRobustRandom random)
        {
            var result = new List<string>();

            if (NegativePrototypes.Count > 0 && random.Prob(NegativeEventChance))
            {
                int count = GetItemCount(random, MinNegativeCount, MaxNegativeCount);
                result.AddRange(PickUnique(random, NegativePrototypes, count));
            }

            return result;
        }

        /// <summary>
        /// Вычисляет количество предметов с уменьшающимся шансом.
        /// </summary>
        private int GetItemCount(IRobustRandom random, int min, int max)
        {
            int count = min;
            int extraItems = max - min;
            float dropChance = 0.5f;

            for (int i = 0; i < extraItems; i++)
            {
                if (random.Prob(dropChance))
                {
                    count++;
                    dropChance /= 2;
                }
                else
                {
                    break;
                }
            }

            return count;
        }

        private List<T> PickUnique<T>(IRobustRandom random, List<T> list, int count)
        {
            var tempList = new List<T>(list);
            var result = new List<T>();

            for (int i = 0; i < count && tempList.Count > 0; i++)
            {
                var picked = random.PickAndTake(tempList);
                result.Add(picked);
            }

            return result;
        }
    }
}

