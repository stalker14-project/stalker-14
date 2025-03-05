using System.Collections.Generic;

namespace Content.Server._Stalker.AdvancedSpawner
{
    public class SpawnCategory
    {
        public string Name { get; }
        public int Weight { get; set; }
        public List<SpawnEntry> Prototypes { get; }

        public SpawnCategory(string name, int weight, List<SpawnEntry> prototypes)
        {
            Name = name;
            Weight = weight;
            Prototypes = prototypes;
        }
    }
}
