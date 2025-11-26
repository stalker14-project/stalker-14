using Content.Shared._Stalker.StalkerRepository;

namespace Content.Server._Stalker.Teleports.DuplicateTeleport;

[RegisterComponent]
public sealed partial class DuplicateTeleportComponent : Component
{
    [DataField("prefix", required: true)]
    public string DuplicateString;

    [DataField("maxWeight")]
    public float MaxWeight;

    [DataField("mapPath")]
    public string ArenaMapPath = "/Maps/_ST/PersonalStalkerArena/StalkerMap.yml";
}
