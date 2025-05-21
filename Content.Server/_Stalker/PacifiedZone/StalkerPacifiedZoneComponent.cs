namespace Content.Server._Stalker.PacifiedZone;

[RegisterComponent]
public sealed partial class StalkerPacifiedZoneComponent : Component
{
    /// <summary>
    /// bool that controls should be pacified component added or removed from ent.
    /// </summary>
    [DataField]
    public bool Pacified;

    /// <summary>
    /// should system read NPCFactionMember comp
    /// </summary>
    [DataField]
    public bool Reader = false;

    /// <summary>
    /// the target faction which will be ignored as friendly
    /// </summary>
    [DataField]
    public string Faction;
}
