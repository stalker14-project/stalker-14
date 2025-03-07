namespace Content.Server._Stalker.TrashDetector;

[RegisterComponent]
public sealed partial class TrashSearchableComponent : Component
{
    [DataField]
    public float CooldownAfterSearch = 600f;

    [DataField] internal float TimeBeforeNextSearch;

    public void SetTimeBeforeNextSearch(float time)
    {
        TimeBeforeNextSearch = time;
    }
}
