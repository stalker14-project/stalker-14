using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server._Stalker.AdvancedSpawner
{
    [DataDefinition]
    public sealed partial class SpawnEntry
    {
        [DataField("id")] public string PrototypeId = string.Empty;
        [DataField] public int Weight = 1;
        [DataField] public int Count = 1;

        public SpawnEntry() { }

        public SpawnEntry(string prototypeId, int weight, int count)
        {
            PrototypeId = prototypeId;
            Weight = weight;
            Count = count;
        }
    }
}
