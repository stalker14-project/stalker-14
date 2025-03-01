using System;
using System.Collections.Generic;
using Content.Server.TrashDetector.Components; // Для доступа к компоненту детектора
using Robust.Shared.GameObjects;

namespace Content.Server._Stalker.AdvancedSpawner
{
    /// <summary>
    /// Класс, представляющий итоговую конфигурацию спавнера.
    /// Создаётся на основе данных компонента AdvancedRandomSpawnerComponent.
    /// </summary>
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

        /// <summary>
        /// Конструктор, клонирующий исходную конфигурацию из компонента.
        /// </summary>
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

        /// <summary>
        /// Применяет модификаторы из детектора к данной конфигурации.
        /// Если в компоненте TrashDetector заданы extra-прототипы и модификаторы веса, они будут добавлены.
        /// Данная версия дополнительно предотвращает получение отрицательных весов.
        /// </summary>
        public void ApplyModifiers(TrashDetectorComponent detector)
        {
            // Обновляем веса с учетом модификаторов.
            // Если ключ отсутствует, базовое значение считаем равным 0.
            CategoryWeights["Common"] = Math.Max((CategoryWeights.ContainsKey("Common") ? CategoryWeights["Common"] : 0) + detector.CommonWeightMod, 0);
            CategoryWeights["Rare"] = Math.Max((CategoryWeights.ContainsKey("Rare") ? CategoryWeights["Rare"] : 0) + detector.RareWeightMod, 0);
            CategoryWeights["Legendary"] = Math.Max((CategoryWeights.ContainsKey("Legendary") ? CategoryWeights["Legendary"] : 0) + detector.LegendaryWeightMod, 0);
            CategoryWeights["Negative"] = Math.Max((CategoryWeights.ContainsKey("Negative") ? CategoryWeights["Negative"] : 0) + detector.NegativeWeightMod, 0);

            // Добавляем extra-прототипы, если они заданы в компоненте детектора.
            CommonPrototypes.AddRange(detector.ExtraCommonPrototypes);
            RarePrototypes.AddRange(detector.ExtraRarePrototypes);
            LegendaryPrototypes.AddRange(detector.ExtraLegendaryPrototypes);
            NegativePrototypes.AddRange(detector.ExtraNegativePrototypes);
        }
    }
}
