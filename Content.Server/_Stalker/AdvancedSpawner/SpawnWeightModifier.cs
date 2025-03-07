namespace Content.Server._Stalker.AdvancedSpawner;

public static class SpawnWeightModifier
{
    public static void ApplyModifiers(Dictionary<string, int> categoryWeights,
        Dictionary<string, List<SpawnEntry>> prototypes,
        Dictionary<string, int> weightModifiers,
        Dictionary<string, List<SpawnEntry>> extraPrototypes)
    {

        foreach (var (category, modifier) in weightModifiers)
        {
            if (categoryWeights.ContainsKey(category))
                categoryWeights[category] = Math.Max(1, categoryWeights[category] + modifier);
        }


        foreach (var category in categoryWeights.Keys)
        {
            if (!prototypes.ContainsKey(category))
                prototypes[category] = new List<SpawnEntry>();
        }


        foreach (var (category, extraEntries) in extraPrototypes)
        {
            if (!prototypes.ContainsKey(category))
                prototypes[category] = new List<SpawnEntry>();

            prototypes[category].AddRange(extraEntries);
        }
    }
}
