using Content.Shared.Radio;
using Robust.Shared.GameStates;

namespace Content.Shared._Stalker.RadioStalker.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class RadioStalkerComponent : Component
{
    [DataField("requiresPower"), ViewVariables(VVAccess.ReadWrite)]
    public bool RequiresPower = false;
}
