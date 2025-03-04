using System.Collections.Generic;

namespace Content.Server._Stalker.AdvancedSpawner
{

    public enum SpawnCategoryType
    {
        Common,
        Rare,
        Legendary,
        Negative
    }

    public class SpawnCategory
    {
        public SpawnCategoryType CategoryType { get; }
        public int Weight { get; set; }
        public List<SpawnEntry> Prototypes { get; }

        public SpawnCategory(SpawnCategoryType categoryType, int weight, List<SpawnEntry> prototypes)
        {
            CategoryType = categoryType;
            Weight = weight;
            Prototypes = prototypes;
        }


        public string Name => CategoryType.ToString();
    }
}
