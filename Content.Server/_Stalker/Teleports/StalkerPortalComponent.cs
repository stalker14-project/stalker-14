namespace Content.Server._Stalker;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class StalkerPortalComponent : Component
{
    //Ім'я телепорту сталкерів, наприклад "Бандити", "Долг" тощо.
    [DataField("PortalName")]
    public string PortalName = string.Empty;

    [DataField]
    public bool AllowAll;
}
