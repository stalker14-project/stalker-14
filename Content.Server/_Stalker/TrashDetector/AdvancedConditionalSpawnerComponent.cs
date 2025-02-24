using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server.Spawners.Components
{
    [RegisterComponent, EntityCategory("Spawner")]
    public sealed partial class AdvancedRandomSpawnerComponent : Component
    {
        /// <summary>
        /// Список обычных прототипов, которые могут заспавниться.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public List<EntProtoId> CommonPrototypes { get; set; } = new();

        /// <summary>
        /// Список редких прототипов, которые могут заспавниться с шансом RareChance.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public List<EntProtoId> RarePrototypes { get; set; } = new();

        /// <summary>
        /// Список легендарных прототипов, которые могут заспавниться с шансом LegendaryChance.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public List<EntProtoId> LegendaryPrototypes { get; set; } = new();

        /// <summary>
        /// Шанс появления редкого объекта вместо обычного.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public float RareChance { get; set; } = 0.1f;

        /// <summary>
        /// Шанс появления легендарного объекта вместо редкого или обычного.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public float LegendaryChance { get; set; } = 0.02f;

        /// <summary>
        /// Разброс координат при спавне.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public float Offset { get; set; } = 0.2f;

        /// <summary>
        /// Минимальное количество обычных предметов, которые могут заспавниться.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public int MinCommonCount { get; set; } = 1;

        /// <summary>
        /// Максимальное количество обычных предметов, которые могут заспавниться.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public int MaxCommonCount { get; set; } = 2;

        /// <summary>
        /// Удалять спавнер после спавна или нет.
        /// </summary>
        [DataField]
        public bool DeleteSpawnerAfterSpawn { get; set; } = true;

        /// <summary>
        /// Метод для выбора случайных прототипов с учетом вероятностей.
        /// </summary>
        public List<EntProtoId> GetRandomPrototypes(IRobustRandom random)
        {
            var result = new List<EntProtoId>();

            // Проверяем шанс легендарного предмета
            if (LegendaryPrototypes.Count > 0 && random.Prob(LegendaryChance))
            {
                result.Add(random.Pick(LegendaryPrototypes));
                return result; // Если выпало легендарное, больше не спавним
            }

            // Проверяем шанс редкого предмета
            if (RarePrototypes.Count > 0 && random.Prob(RareChance))
            {
                result.Add(random.Pick(RarePrototypes));
                return result; // Если выпал редкий, больше не спавним
            }

            // Спавн обычных предметов (от MinCommonCount до MaxCommonCount)
            if (CommonPrototypes.Count > 0)
            {
                int spawnCount = random.Next(MinCommonCount, MaxCommonCount + 1);
                result.AddRange(CommonPrototypes.OrderBy(_ => random.Next()).Take(spawnCount));
            }

            return result;
        }
    }
}

