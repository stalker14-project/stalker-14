using Content.Server._Stalker.AdvancedSpawner; // Используем правильный неймспейс
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using System;
using System.Collections.Generic;

namespace Content.Server.TrashDetector.Components
{
    [RegisterComponent]
    public partial class TrashDetectorComponent : Component
    {
        /// <summary>
        /// Время на поиск (сколько секунд длится DoAfter).
        /// </summary>
        [DataField("searchTime")]
        public float SearchTime { get; set; } = 5f;

        /// <summary>
        /// Базовый спавнер, который будет использоваться.
        /// </summary>
        public const string LootSpawner = "RandomTrashDetectorSpawner";

        /// <summary>
        /// Модификаторы веса категорий (вероятность выпадения категории).
        /// </summary>
        [DataField("commonWeightMod")]
        public int CommonWeightMod { get; set; } = 0;

        [DataField("rareWeightMod")]
        public int RareWeightMod { get; set; } = 0;

        [DataField("legendaryWeightMod")]
        public int LegendaryWeightMod { get; set; } = 0;

        [DataField("negativeWeightMod")]
        public int NegativeWeightMod { get; set; } = 0;

        /// <summary>
        /// Дополнительные предметы, которые добавляются в категории с их весами и количеством.
        /// </summary>
        [DataField("extraCommonPrototypes")]
        public List<SpawnEntry> ExtraCommonPrototypes { get; set; } = new();

        [DataField("extraRarePrototypes")]
        public List<SpawnEntry> ExtraRarePrototypes { get; set; } = new();

        [DataField("extraLegendaryPrototypes")]
        public List<SpawnEntry> ExtraLegendaryPrototypes { get; set; } = new();

        [DataField("extraNegativePrototypes")]
        public List<SpawnEntry> ExtraNegativePrototypes { get; set; } = new();
    }
}

