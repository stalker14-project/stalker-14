namespace Content.Server.TrashSearchable;

[RegisterComponent]
public sealed partial class TrashSearchableComponent : Component
{
    [DataField]
    public float TimeBeforeNextSearch = 0f;
}
