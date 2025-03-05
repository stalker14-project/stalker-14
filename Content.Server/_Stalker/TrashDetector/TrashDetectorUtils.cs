using Robust.Shared.Log;
using System.Collections.Generic;

namespace Content.Server.TrashDetector;

public static class TrashDetectorUtils
{
    public static int GetWeightModifier(string category, Dictionary<string, int> weightModifiers)
    {
        int modifier = weightModifiers.GetValueOrDefault(category, 0);

        Logger.Info($"[TrashDetectorUtils] GetWeightModifier: {category} = {modifier}");
        return modifier;
    }
}
