namespace Content.Server._Stalker.AdvancedSpawner;

public class SpawnCategory(string name, int weight, List<SpawnEntry> prototypes)
{
    public string Name { get; } = name;
    public int Weight { get; set; } = weight;
    public List<SpawnEntry> Prototypes { get; } = prototypes;
}
