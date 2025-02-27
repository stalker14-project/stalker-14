using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using System;

namespace Content.Server.TrashDetector.Components
{
    [RegisterComponent]
    public sealed partial class TrashDetectorComponent : Component
    {
        /// <summary>
        /// Время на поиск (сколько секунд длится DoAfter).
        /// </summary>
        [DataField("searchTime")]
        public float SearchTime { get; set; } = 5f;

        private float _commonProbability = 0.5f;
        private float _rareProbability = 0.1f;
        private float _legendaryProbability = 0.02f;
        private float _negativeProbability = 0.05f;

        [DataField("commonProbability")]
        public float CommonProbability
        {
            get => _commonProbability;
            set
            {
                _commonProbability = Math.Clamp(value, 0f, 1f);
                NormalizeProbabilities();
            }
        }

        [DataField("rareProbability")]
        public float RareProbability
        {
            get => _rareProbability;
            set
            {
                _rareProbability = Math.Clamp(value, 0f, 1f);
                NormalizeProbabilities();
            }
        }

        [DataField("legendaryProbability")]
        public float LegendaryProbability
        {
            get => _legendaryProbability;
            set
            {
                _legendaryProbability = Math.Clamp(value, 0f, 1f);
                NormalizeProbabilities();
            }
        }

        [DataField("negativeProbability")]
        public float NegativeProbability
        {
            get => _negativeProbability;
            set
            {
                _negativeProbability = Math.Clamp(value, 0f, 1f);
                NormalizeProbabilities();
            }
        }

        /// <summary>
        /// Спавнер, который будет использоваться для выбора предметов.
        /// </summary>
        [DataField("lootSpawner")]
        public string LootSpawner { get; set; } = "RandomTrashDetectorSpawner";

        /// <summary>
        /// Автоматически нормализует вероятности, чтобы сумма не превышала 100%.
        /// </summary>
        private void NormalizeProbabilities()
        {
            float total = _commonProbability + _rareProbability + _legendaryProbability + _negativeProbability;
            if (total > 1.0f)
            {
                _commonProbability /= total;
                _rareProbability /= total;
                _legendaryProbability /= total;
                _negativeProbability /= total;
            }
        }
    }
}
