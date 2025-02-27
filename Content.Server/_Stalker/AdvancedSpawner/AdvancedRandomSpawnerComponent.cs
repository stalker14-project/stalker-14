using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Server.Spawners.Components
{
    [RegisterComponent]
    public partial class AdvancedRandomSpawnerComponent : Component
    {
        // Общие параметры для всех категорий
        [DataField("minCount")]
        public int MinCount { get; private set; } = 1; // Минимальное количество по умолчанию

        [DataField("maxCount")]
        public int MaxCount { get; private set; } = 3; // Максимальное количество по умолчанию

        // Списки прототипов для категорий
        [DataField("commonPrototypes")]
        public List<string> CommonPrototypes { get; private set; } = new();

        [DataField("rarePrototypes")]
        public List<string> RarePrototypes { get; private set; } = new();

        [DataField("legendaryPrototypes")]
        public List<string> LegendaryPrototypes { get; private set; } = new();

        [DataField("negativePrototypes")]
        public List<string> NegativePrototypes { get; private set; } = new();

        // Шансы для выбора прототипов внутри категории
        [DataField("commonChance")]
        public float CommonChance { get; private set; } = 0.8f;

        [DataField("rareChance")]
        public float RareChance { get; private set; } = 0.05f;

        [DataField("legendaryChance")]
        public float LegendaryChance { get; private set; } = 0.02f;

        [DataField("negativeEventChance")]
        public float NegativeEventChance { get; private set; } = 0.1f;

        // Индивидуальные диапазоны количества для категорий (необязательные)
        [DataField("minCommonCount")]
        public int? MinCommonCount { get; private set; } = null;

        [DataField("maxCommonCount")]
        public int? MaxCommonCount { get; private set; } = null;

        [DataField("minRareCount")]
        public int? MinRareCount { get; private set; } = null;

        [DataField("maxRareCount")]
        public int? MaxRareCount { get; private set; } = null;

        [DataField("minLegendaryCount")]
        public int? MinLegendaryCount { get; private set; } = null;

        [DataField("maxLegendaryCount")]
        public int? MaxLegendaryCount { get; private set; } = null;

        [DataField("minNegativeCount")]
        public int? MinNegativeCount { get; private set; } = null;

        [DataField("maxNegativeCount")]
        public int? MaxNegativeCount { get; private set; } = null;

        // Другие параметры
        [DataField("deleteSpawnerAfterSpawn")]
        public bool DeleteSpawnerAfterSpawn { get; private set; } = false;

        [DataField("offset")]
        public float Offset { get; private set; } = 0f;

        [DataField("categoryChances")]
        public Dictionary<string, float> CategoryChances { get; private set; } = new()
        {
            { "common", 0.7f },
            { "rare", 0.2f },
            { "legendary", 0.05f },
            { "negative", 0.05f }
        };

        // Методы
        public List<string> GetFinalizedPrototypes(IRobustRandom random)
        {
            var result = new List<string>();
            var category = PickCategory(random);

            switch (category)
            {
                case "common":
                    result.AddRange(GetItemsWithScalingChance(random, CommonPrototypes, CommonChance,
                        MinCommonCount ?? MinCount, MaxCommonCount ?? MaxCount));
                    break;
                case "rare":
                    result.AddRange(GetItemsWithScalingChance(random, RarePrototypes, RareChance,
                        MinRareCount ?? MinCount, MaxRareCount ?? MaxCount));
                    break;
                case "legendary":
                    result.AddRange(GetItemsWithScalingChance(random, LegendaryPrototypes, LegendaryChance,
                        MinLegendaryCount ?? MinCount, MaxLegendaryCount ?? MaxCount));
                    break;
                case "negative":
                    result.AddRange(GetItemsWithScalingChance(random, NegativePrototypes, NegativeEventChance,
                        MinNegativeCount ?? MinCount, MaxNegativeCount ?? MaxCount));
                    break;
            }

            return result;
        }

        private string PickCategory(IRobustRandom random)
        {
            float roll = random.NextFloat(0, 1);
            float cumulative = 0f;

            foreach (var (category, chance) in CategoryChances)
            {
                cumulative += chance;
                if (roll < cumulative)
                    return category;
            }

            return "common"; // Запасной вариант на случай ошибки
        }

        private List<string> GetItemsWithScalingChance(IRobustRandom random, List<string> prototypes, float baseChance, int minCount, int maxCount)
        {
            var result = new List<string>();
            if (prototypes.Count == 0 || minCount > maxCount) return result;
            var availableItems = new List<string>(prototypes);

            // Шаг 1: Гарантируем добавление minCount предметов, если достаточно прототипов
            for (int i = 0; i < minCount && availableItems.Count > 0; i++)
            {
                var item = random.PickAndTake(availableItems);
                result.Add(item);
            }

            // Шаг 2: Пытаемся добавить дополнительные предметы до maxCount с убывающим шансом
            float chance = baseChance;
            while (result.Count < maxCount && availableItems.Count > 0)
            {
                if (chance >= 1.0f || random.Prob(chance))
                {
                    var item = random.PickAndTake(availableItems);
                    result.Add(item);
                    chance /= 2f; // Уменьшаем шанс только после успешного добавления
                }
                else
                {
                    break; // Прерываем, если шанс не сработал
                }
            }

            return result;
        }
    }
}
