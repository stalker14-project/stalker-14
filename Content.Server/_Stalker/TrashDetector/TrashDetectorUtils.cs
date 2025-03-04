using Robust.Shared.Log;
using Content.Server._Stalker.AdvancedSpawner;

namespace Content.Server.TrashDetector;

public static class TrashDetectorUtils
{
    public static int GetWeightModifier(string category, int commonWeight, int rareWeight, int legendaryWeight, int negativeWeight)
    {
        int modifier = category switch
        {
            "Common" => commonWeight,
            "Rare" => rareWeight,
            "Legendary" => legendaryWeight,
            "Negative" => negativeWeight,
            _ => 0
        };

        Logger.Info($"[TrashDetectorUtils] GetWeightModifier: {category} = {modifier}");
        return modifier;
    }
}
