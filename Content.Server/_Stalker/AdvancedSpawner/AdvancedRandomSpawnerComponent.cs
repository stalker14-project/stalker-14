using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server._Stalker.AdvancedSpawner
{
    [RegisterComponent]
    public sealed partial class AdvancedRandomSpawnerComponent : Component
    {
        // Использование SpawnCategoryType вместо string
        [DataField] public Dictionary<SpawnCategoryType, int> CategoryWeights = new();
        [DataField] public List<SpawnEntry> CommonPrototypes = new();
        [DataField] public List<SpawnEntry> RarePrototypes = new();
        [DataField] public List<SpawnEntry> LegendaryPrototypes = new();
        [DataField] public List<SpawnEntry> NegativePrototypes = new();
        [DataField] public float Offset = 0.2f;
        [DataField] public bool DeleteAfterSpawn = true;
        [DataField] public int MaxSpawnCount = 3;

        public AdvancedRandomSpawnerConfig ToConfig()
        {
            return new AdvancedRandomSpawnerConfig(this);
        }
    }
}
