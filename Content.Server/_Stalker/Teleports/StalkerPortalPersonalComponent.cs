namespace Content.Server._Stalker;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class StalkerPortalPersonalComponent : Component
{
    //Портал из которого игрок пришел в сталкер арену
    [DataField("ReturnPortal")]
    public string ReturnPortal = string.Empty;

    //Ентити айди портала из которого игрок пришел в сталкер арену, необоходимо для возвращения обратно
    [DataField("ReturnPortalEntity")]
    public EntityUid ReturnPortalEntity;
}
