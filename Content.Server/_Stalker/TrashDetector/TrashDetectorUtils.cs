namespace Content.Server._Stalker.TrashDetector;

public static class TrashDetectorUtils
{
    [Obsolete("Obsolete")]
    public static int GetWeightModifier(string category, Dictionary<string, int> weightModifiers)
    {
        var modifier = weightModifiers.GetValueOrDefault(category, 0);
        Logger.Info($"[TrashDetectorUtils] GetWeightModifier: {category} = {modifier}");
        return modifier;
    }
}
