namespace Content.Server._Stalker_EN.ElectronicsSearchable;

[RegisterComponent]
public sealed partial class ElectronicsSearchableComponent : Component
{
    [DataField]
    public float TimeBeforeNextSearch = 0f;
}
