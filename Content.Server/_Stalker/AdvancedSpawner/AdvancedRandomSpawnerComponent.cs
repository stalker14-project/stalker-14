using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server._Stalker.AdvancedSpawner
{
    [RegisterComponent]
    public sealed partial class AdvancedRandomSpawnerComponent : Component
    {
        [DataField("categoryWeights")]
        public Dictionary<string, int> CategoryWeights { get; set; } = new();

        [DataField("commonPrototypes")]
        public List<SpawnEntry> CommonPrototypes { get; set; } = new();

        [DataField("rarePrototypes")]
        public List<SpawnEntry> RarePrototypes { get; set; } = new();

        [DataField("legendaryPrototypes")]
        public List<SpawnEntry> LegendaryPrototypes { get; set; } = new();

        [DataField("negativePrototypes")]
        public List<SpawnEntry> NegativePrototypes { get; set; } = new();

        [DataField("offset")]
        public float Offset { get; set; } = 0.2f;

        [DataField("deleteSpawnerAfterSpawn")]
        public bool DeleteSpawnerAfterSpawn { get; set; } = true;

        [DataField("maxSpawnCount")]
        public int MaxSpawnCount { get; set; } = 3;
    }

    [DataDefinition]
    public sealed partial class SpawnEntry
    {
        [DataField("id")]
        public string PrototypeId { get; set; } = string.Empty;

        [DataField("weight")]
        public int Weight { get; set; } = 1;

        [DataField("count", required: false)]
        public int Count { get; set; } = 1; // Если count не указан, будет 1
    }
}
