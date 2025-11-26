using Robust.Shared.Map;

namespace Content.Server._Stalker.Teleports.MapPortal;

/// <summary>
/// Component to load map and link two portals on them.
/// </summary>
[RegisterComponent]
public sealed partial class MapPortalComponent : Component
{
    // Stores path to map yml file.
    [DataField("mapPath")]
    public string? MapPath = "/Maps/_StalkerMaps/PersonalStalkerArena/StalkerMap.yml";

    // Portal name to link two portals with the same name together
    [DataField("portalName")]
    public string? PortalName = string.Empty;

    // Stores id of the created map.
    // TODO: It shouldn't be string type, make it MapId class.
    [DataField("mapId")]
    public MapId MapId;

    public bool Loading;

}
