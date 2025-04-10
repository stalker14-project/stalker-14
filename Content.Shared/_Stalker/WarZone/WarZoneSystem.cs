using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared._Stalker.WarZone;
[Virtual]
public partial class SharedWarZoneSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
}
