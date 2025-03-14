namespace Content.Server._Stalker.TrashDetector;

[RegisterComponent]
public sealed partial class TrashSearchableComponent : Component
{
    [DataField]
    public float CooldownAfterSearch = 600f;

    [DataField] internal float TimeBeforeNextSearch;

    [DataField("AllowedDetectors")]
    public List<string> AllowedDetectors { get; set; } = new();

    [DataField]
    public string LootSpawner { get; set; } = "RandomTrashDetectorSpawner";

    public void SetTimeBeforeNextSearch(float time)
    {
        TimeBeforeNextSearch = time;
    }
}
