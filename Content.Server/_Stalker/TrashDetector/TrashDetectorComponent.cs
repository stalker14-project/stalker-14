using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using System;

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
        /// Спавнер, который будет использоваться для выбора предметов. Он всегда "RandomTrashDetectorSpawner".
        /// </summary>
        public const string LootSpawner = "RandomTrashDetectorSpawner";

        /// <summary>
        /// Дополнительный шанс к спавну в указанной категории.
        /// </summary>
        [DataField("additionalSpawnWeight")]
        public int AdditionalSpawnWeight { get; set; } = 0;

        /// <summary>
        /// Дополнительный предмет, который добавляется в общий список спавна.
        /// </summary>
        [DataField("extraPrototype")]
        public string? ExtraPrototype { get; set; }

        /// <summary>
        /// Категория, в которую будет добавлен ExtraPrototype (Common, Rare, Legendary, Negative).
        /// </summary>
        [DataField("extraPrototypeCategory")]
        public string ExtraPrototypeCategory { get; set; } = "Common";
    }
}
