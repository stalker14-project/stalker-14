using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server._Stalker.AdvancedSpawner
{
    [RegisterComponent]
    public sealed partial class AdvancedRandomSpawnerComponent : Component
    {
        [DataField] public Dictionary<string, int> CategoryWeights = new();
        [DataField] public Dictionary<string, List<SpawnEntry>> PrototypeLists = new();

        [DataField] public float Offset = 0.2f;
        [DataField] public bool DeleteAfterSpawn = true;
        [DataField] public int MaxSpawnCount = 3;

        public AdvancedRandomSpawnerConfig ToConfig()
        {
            return new AdvancedRandomSpawnerConfig(this);
        }
    }
}
