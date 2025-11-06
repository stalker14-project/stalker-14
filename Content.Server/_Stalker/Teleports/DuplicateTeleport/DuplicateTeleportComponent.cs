using Content.Shared._Stalker.StalkerRepository;
using Robust.Shared.Utility;

namespace Content.Server._Stalker.Teleports.DuplicateTeleport;

[RegisterComponent]
public sealed partial class DuplicateTeleportComponent : Component
{
    [DataField("prefix", required: true)]
    public string DuplicateString;

    [DataField("maxWeight")]
    public float MaxWeight;

    [DataField("mapPath")]
    public ResPath ArenaMapPath = new("/Maps/_ST/PersonalStalkerArena/StalkerMap.yml");
}
