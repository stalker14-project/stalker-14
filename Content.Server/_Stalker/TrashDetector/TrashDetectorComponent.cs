namespace Content.Server.TrashDetector.Components;

[RegisterComponent]
public sealed partial class TrashDetectorComponent : Component
{
    /// <summary>
    /// Время на поиск (сколько секунд длится DoAfter).
    /// </summary>
    [DataField]
    public float SearchTime = 5f;

    /// <summary>
    /// Вероятности выпадения лута.
    /// </summary>
    [DataField] public float CommonProbability = 0.5f;
    [DataField] public float RareProbability = 0.1f;
    [DataField] public float LegendaryProbability = 0.02f;
    [DataField] public float NegativeProbability = 0.05f;

    /// <summary>
    /// Спавнер, который будет использоваться для выбора предметов.
    /// </summary>
    [DataField]
    public string LootSpawner = "RandomTrashDetectorSpawner";
}
