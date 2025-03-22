namespace Content.Server._Stalker.PacifiedZone;

[RegisterComponent]
public sealed partial class StalkerPacifiedZoneComponent : Component
{
    [DataField("Pacified")]
    public bool Pacified;

    [DataField]
    public bool Reader = false;
}
