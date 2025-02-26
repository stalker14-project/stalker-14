using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Spawners.Components
{
    [RegisterComponent, EntityCategory("Spawner")]
    public sealed partial class AdvancedRandomSpawnerComponent : Component
    {
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public EntProtoId[] CommonPrototypes { get; set; } = new EntProtoId[0];

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public EntProtoId[] RarePrototypes { get; set; } = new EntProtoId[0];

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public EntProtoId[] LegendaryPrototypes { get; set; } = new EntProtoId[0];

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public EntProtoId[] NegativePrototypes { get; set; } = new EntProtoId[0];

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public float CommonChance { get; set; } = 0.8f; // 80% шанс на успешное выпадение обычных предметов

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public float RareChance { get; set; } = 0.1f;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public float LegendaryChance { get; set; } = 0.02f;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public float NegativeEventChance { get; set; } = 0.05f;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public float Offset { get; set; } = 0.2f;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public int MinCommonCount { get; set; } = 2; // Минимум 2 обычных предмета

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public int MaxCommonCount { get; set; } = 3; // Максимум 3, но третий с 50% шансом

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public int MinRareCount { get; set; } = 0;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public int MaxRareCount { get; set; } = 2;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public int MinLegendaryCount { get; set; } = 0;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public int MaxLegendaryCount { get; set; } = 1;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public int MinNegativeCount { get; set; } = 1;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public int MaxNegativeCount { get; set; } = 2;

        [DataField]
        public bool DeleteSpawnerAfterSpawn { get; set; } = true;

        /// <summary>
        /// Выбирает случайные прототипы для спавна.
        /// </summary>
        public EntProtoId[] GetRandomPrototypes(IRobustRandom random)
        {
            var result = new EntProtoId[0];
            int count;

            // Проверяем шанс выпадения обычных предметов
            if (!random.Prob(CommonChance))
            {
                // Если шанс не сработал → спавним плохие события
                return GetNegativePrototypes(random);
            }

            // Добавляем легендарные предметы
            if (LegendaryPrototypes.Length > 0 && random.Prob(LegendaryChance))
            {
                count = random.Next(MinLegendaryCount, MaxLegendaryCount + 1);
                result = AddRandomElements(result, LegendaryPrototypes, count, random);
            }

            // Добавляем редкие предметы
            if (RarePrototypes.Length > 0 && random.Prob(RareChance))
            {
                count = random.Next(MinRareCount, MaxRareCount + 1);
                result = AddRandomElements(result, RarePrototypes, count, random);
            }

            // Спавн обычных предметов с уменьшающимся шансом
            count = MinCommonCount; // Гарантированно падает минимум N предметов
            int extraItems = MaxCommonCount - MinCommonCount; // Доп. предметы с шансами
            float dropChance = 0.5f; // Начальный шанс выпадения доп. предметов

            for (int i = 0; i < extraItems; i++)
            {
                if (random.Prob(dropChance))
                {
                    count++;
                    dropChance /= 2; // Уменьшаем шанс для следующего предмета
                }
                else
                {
                    break; // Если шанс не прошел - прекращаем добавление предметов
                }
            }

            result = AddRandomElements(result, CommonPrototypes, count, random);

            return result;
        }

        /// <summary>
        /// Выбирает случайные "плохие события" для спавна.
        /// </summary>
        public EntProtoId[] GetNegativePrototypes(IRobustRandom random)
        {
            var result = new EntProtoId[0];

            if (NegativePrototypes.Length > 0 && random.Prob(NegativeEventChance))
            {
                int count = random.Next(MinNegativeCount, MaxNegativeCount + 1);
                result = AddRandomElements(result, NegativePrototypes, count, random);
            }

            return result;
        }

        /// <summary>
        /// Добавляет случайные элементы из массива, заменяя `List<T>`.
        /// </summary>
        private EntProtoId[] AddRandomElements(EntProtoId[] targetArray, EntProtoId[] sourceArray, int count, IRobustRandom random)
        {
            if (sourceArray.Length == 0)
                return targetArray;

            var tempArray = (EntProtoId[])sourceArray.Clone(); // Делаем копию
            var newArray = new EntProtoId[targetArray.Length + count];

            // Копируем старые элементы
            for (int i = 0; i < targetArray.Length; i++)
            {
                newArray[i] = targetArray[i];
            }

            for (int i = 0; i < count && tempArray.Length > 0; i++)
            {
                int index = random.Next(0, tempArray.Length);
                newArray[targetArray.Length + i] = tempArray[index];

                // Убираем использованный элемент
                for (int j = index; j < tempArray.Length - 1; j++)
                {
                    tempArray[j] = tempArray[j + 1];
                }
                Array.Resize(ref tempArray, tempArray.Length - 1);
            }

            return newArray;
        }
    }
}
